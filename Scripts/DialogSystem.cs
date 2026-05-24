using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class DialogSystem : CanvasLayer
{
    [Export] private Control _dialogContainer;
    [Export] private Label _speakerLabel;
    [Export] private RichTextLabel _messageLabel;
    [Export] private ColorRect _fadePanel;
    [Export] private TextureRect _clickIndicator;
    [Export] private float _fadeDuration = 1.0f;
    [Export] private float _cameraMoveDuration = 1.5f;
    [Export] private float _pauseBetweenLevels = 0.5f;
    
    private Camera2D _gameCamera;
    private Node2D _mainNode;
    private List<Node2D> _levels = new List<Node2D>();
    
    private Queue<DialogLine> _dialogQueue = new Queue<DialogLine>();
    private bool _isTyping = false;
    private bool _canProceed = false;
    private Tween _currentTween;
    private Timer _typeTimer;
    private bool _isTransitioning = false;
    private int _currentCharIndex = 0;
    private string _currentTypingText = "";
    private int _currentLevelIndex = 0;
    
    public override void _Ready()
    {
        // Настройка таймера
        _typeTimer = new Timer();
        _typeTimer.WaitTime = 0.03;
        _typeTimer.OneShot = true;
        AddChild(_typeTimer);
        
        // Поиск Main сцены
        _mainNode = GetNodeOrNull<Node2D>("/root/Main");
        
        if (_mainNode == null)
        {
            GD.PrintErr("Main node not found!");
            return;
        }
        
        // Поиск камеры
        _gameCamera = _mainNode.GetNodeOrNull<Camera2D>("Camera2D");
        
        if (_gameCamera == null)
        {
            GD.PrintErr("Camera2D not found in Main!");
            return;
        }
        
        // Находим все уровни
        foreach (Node child in _mainNode.GetChildren())
        {
            if (child is Node2D node && child.Name.ToString().StartsWith("Level"))
            {
                _levels.Add(node);
            }
        }
        
        // Сортируем уровни
        _levels.Sort((a, b) =>
        {
            int numA = int.Parse(a.Name.ToString().Replace("Level", ""));
            int numB = int.Parse(b.Name.ToString().Replace("Level", ""));
            return numA.CompareTo(numB);
        });
        
        // Прячем диалоговое окно
        if (_dialogContainer != null)
        {
            _dialogContainer.Modulate = new Color(1, 1, 1, 0);
            _dialogContainer.Visible = false;
        }
        
        // Настройка затемнения
        if (_fadePanel != null)
        {
            _fadePanel.Modulate = new Color(0, 0, 0, 0);
        }
        
        // Загружаем диалоги
        LoadDialogues();
        
        // Запускаем диалог
        StartDialog();
    }
    
    private void LoadDialogues()
    {
        // ДИАЛОГИ ДЛЯ ПЕРВОГО УРОВНЯ
        AddDialogue("Зевс(гг, персонаж):", "-Как же я устал от этой работы , хочется спокойно быть дома с детьми а не убивать за деньги , ты меня понимаешь?");
        AddDialogue("бармен:", "-Дружище , тебе нужно больше отдыхать , может выпишешь ещё?");
        AddDialogue("Система", "После того как зевс выпивает ещё , к нему подсаживается работяга и начинает разговор.");
        AddDialogue("работяга:", "-Привет , я услышал , что ты работаешь киллером , сможешь помочь мне решить одно дельце , в долгу не останусь?");
        AddDialogue("Зевс(гг, персонаж):", "-Ну выкладывай.");
        AddDialogue("работяга:", "-Мою сестру за долги похитила корпорация и требует выкуп , ты не подумай , деньги есть , но я не хочу их отдавать , сможешь решить проблему?");
        AddDialogue("Зевс(гг, персонаж):", "-Как нехуй делать.");
    }
    
    private void AddDialogue(string speaker, string message)
    {
        _dialogQueue.Enqueue(new DialogLine(speaker, message));
    }
    
    private async void StartDialog()
    {
        if (_dialogContainer == null) return;
        
        _dialogContainer.Visible = true;
        _dialogContainer.Position = new Vector2(-_dialogContainer.Size.X, _dialogContainer.Position.Y);
        
        _currentTween = CreateTween();
        _currentTween.SetTrans(Tween.TransitionType.Back);
        _currentTween.SetEase(Tween.EaseType.Out);
        _currentTween.TweenProperty(_dialogContainer, "modulate:a", 1.0f, 0.5f);
        _currentTween.Parallel().TweenProperty(_dialogContainer, "position:x", 0, 0.5f);
        
        await ToSignal(_currentTween, Tween.SignalName.Finished);
        
        if (_clickIndicator != null)
        {
            _clickIndicator.Visible = true;
            StartClickBlink();
        }
        
        ShowNextLine();
    }
    
    private void StartClickBlink()
    {
        if (_currentTween != null && _currentTween.IsValid())
            _currentTween.Kill();
        
        if (_clickIndicator == null) return;
        
        _currentTween = CreateTween();
        _currentTween.SetLoops(0);
        _currentTween.TweenProperty(_clickIndicator, "modulate:a", 0.3f, 0.3f);
        _currentTween.TweenProperty(_clickIndicator, "modulate:a", 1.0f, 0.3f);
    }
    
    private void StopClickBlink()
    {
        if (_currentTween != null && _currentTween.IsValid())
            _currentTween.Kill();
        
        if (_clickIndicator != null)
            _clickIndicator.Modulate = new Color(1, 1, 1, 1);
    }
    
    private void ShowNextLine()
    {
        if (_dialogQueue.Count == 0)
        {
            EndDialogAndMoveThroughLevels();
            return;
        }
        
        var currentLine = _dialogQueue.Dequeue();
        _speakerLabel.Text = $"{currentLine.Speaker}:";
        _canProceed = false;
        _messageLabel.Text = "";
        TypeText(currentLine.Message);
    }
    
    private void TypeText(string text)
    {
        _isTyping = true;
        _currentTypingText = text;
        _currentCharIndex = 0;
        _messageLabel.Text = "";
        
        _typeTimer.Timeout += () => TypeNextChar();
        _typeTimer.Start();
    }
    
    private void TypeNextChar()
    {
        if (_currentCharIndex < _currentTypingText.Length)
        {
            _messageLabel.Text += _currentTypingText[_currentCharIndex];
            _currentCharIndex++;
            _typeTimer.Start();
        }
        else
        {
            _isTyping = false;
            _canProceed = true;
            _currentCharIndex = 0;
            
            _typeTimer.Timeout -= () => TypeNextChar();
            
            if (_clickIndicator != null && _clickIndicator.Visible)
                StartClickBlink();
        }
    }
    
    public override void _Input(InputEvent @event)
    {
        if (_isTransitioning) return;
        
        bool isClick = false;
        
        if (@event is InputEventMouseButton mouseEvent && 
            mouseEvent.ButtonIndex == MouseButton.Left && 
            mouseEvent.Pressed)
        {
            isClick = true;
        }
        
        if (@event is InputEventKey keyEvent && 
            (keyEvent.Keycode == Key.Enter || keyEvent.Keycode == Key.Space) &&
            keyEvent.Pressed)
        {
            isClick = true;
        }
        
        if (isClick && _canProceed && !_isTyping)
        {
            StopClickBlink();
            ShowNextLine();
        }
    }
    
    private async void EndDialogAndMoveThroughLevels()
    {
        _isTransitioning = true;
        _canProceed = false;
        
        if (_clickIndicator != null)
            _clickIndicator.Visible = false;
        
        // Прячем диалоговое окно
        if (_dialogContainer != null)
        {
            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.TweenProperty(_dialogContainer, "modulate:a", 0, 0.3f);
            await ToSignal(_currentTween, Tween.SignalName.Finished);
            _dialogContainer.Visible = false;
        }
        
        await MoveCameraThroughLevels();
    }
    
    private async Task MoveCameraThroughLevels()
    {
        for (int i = 0; i < _levels.Count - 1; i++)
        {
            Vector2 targetPosition = _levels[i + 1].GlobalPosition;
            
            // Затемнение
            if (_fadePanel != null)
            {
                _currentTween = CreateTween();
                _currentTween.SetTrans(Tween.TransitionType.Quad);
                _currentTween.SetEase(Tween.EaseType.InOut);
                _currentTween.TweenProperty(_fadePanel, "modulate:a", 1.0f, _fadeDuration);
                await ToSignal(_currentTween, Tween.SignalName.Finished);
            }
            
            // Движение камеры
            Vector2 startPosition = _gameCamera.GlobalPosition;
            float elapsed = 0f;
            
            while (elapsed < _cameraMoveDuration)
            {
                elapsed += (float)GetProcessDeltaTime();
                float t = elapsed / _cameraMoveDuration;
                _gameCamera.GlobalPosition = startPosition.Lerp(targetPosition, t);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            
            _gameCamera.GlobalPosition = targetPosition;
            
            // Пауза перед следующим уровнем
            await ToSignal(GetTree().CreateTimer(_pauseBetweenLevels), SceneTreeTimer.SignalName.Timeout);
        }
        
        await GameComplete();
    }
    
    private async Task GameComplete()
    {
        // Финальное затемнение
        if (_fadePanel != null)
        {
            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.SetEase(Tween.EaseType.InOut);
            _currentTween.TweenProperty(_fadePanel, "modulate:a", 1.0f, _fadeDuration);
            await ToSignal(_currentTween, Tween.SignalName.Finished);
        }
        
        // Показываем экран победы
        var winLabel = new Label();
        winLabel.Text = "ПОБЕДА!\nВы прошли все уровни!";
        winLabel.HorizontalAlignment = HorizontalAlignment.Center;
        winLabel.VerticalAlignment = VerticalAlignment.Center;
        winLabel.AddThemeFontSizeOverride("font_size", 48);
        _mainNode.AddChild(winLabel);
        
        GD.Print("Поздравляем! Игра пройдена!");
        
        await ToSignal(GetTree().CreateTimer(3.0f), SceneTreeTimer.SignalName.Timeout);
        GetTree().ReloadCurrentScene();
    }
    
    public override void _ExitTree()
    {
        base._ExitTree();
        if (_typeTimer != null)
        {
            _typeTimer.Timeout -= () => TypeNextChar();
            _typeTimer.QueueFree();
        }
    }
}

public class DialogLine
{
    public string Speaker { get; set; }
    public string Message { get; set; }
    
    public DialogLine(string speaker, string message)
    {
        Speaker = speaker;
        Message = message;
    }
}