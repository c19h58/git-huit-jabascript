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
    
    // Позиции для камеры
    [Export] private Vector2 _firstCameraPosition = new Vector2(1350, 559);
    [Export] private Vector2 _originalCameraZoom = new Vector2(1.0f, 1.0f);
    [Export] private Vector2 _reducedCameraZoom = new Vector2(0.5f, 0.5f); // уменьшенный зум в 2 раза
    
    // Ссылки на игрока и камеру
    private Camera2D _gameCamera;
    private CharacterBody2D _player;
    private Node2D _mainNode;
    private List<Node2D> _levels = new List<Node2D>();
    
    // Позиционирование диалога
    [Export] private bool _centerBottom = true;
    [Export] private bool _leftBottom = false;
    [Export] private bool _rightBottom = false;
    [Export] private int _margin = 20;
    
    private Queue<DialogLine> _dialogQueue = new Queue<DialogLine>();
    private bool _isTyping = false;
    private bool _canProceed = false;
    private Tween _currentTween;
    private Timer _typeTimer;
    private bool _isTransitioning = false;
    private int _currentCharIndex = 0;
    private string _currentTypingText = "";
    private bool _cameraReturned = false;
    private bool _cameraAttachedToPlayer = false;
    
    // Скрипт следования камеры (если есть)
    private object _cameraFollowScript;
    
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
        
        // Сохраняем скрипт следования камеры (если есть)
        if (_gameCamera.HasMethod("FollowPlayer"))
        {
            _cameraFollowScript = _gameCamera;
        }
        
        // Поиск игрока в Main сцене
        _player = _mainNode.GetNodeOrNull<CharacterBody2D>("Player");
        
        if (_player == null)
        {
            GD.PrintErr("Player not found in Main!");
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
        AddDialogue("Маг", "Приветствую, путник!");
        AddDialogue("Маг", "Добро пожаловать в мир приключений!");
        AddDialogue("Маг", "Твоё путешествие только начинается.");
        AddDialogue("Герой", "Я готов к испытаниям!");
        AddDialogue("Маг", "Отлично! Тогда вперёд!");
        AddDialogue("Маг", "Удачи тебе на этом пути!");
    }
    
    private void AddDialogue(string speaker, string message)
    {
        _dialogQueue.Enqueue(new DialogLine(speaker, message));
    }
    
    private void PositionDialogContainer()
    {
        if (_dialogContainer == null) return;
        
        var viewportSize = GetViewport().GetVisibleRect().Size;
        float dialogWidth = _dialogContainer.Size.X;
        float dialogHeight = _dialogContainer.Size.Y;
        
        if (_centerBottom)
        {
            _dialogContainer.Position = new Vector2(
                (viewportSize.X - dialogWidth) / 2,
                viewportSize.Y - dialogHeight - _margin
            );
        }
        else if (_leftBottom)
        {
            _dialogContainer.Position = new Vector2(_margin, viewportSize.Y - dialogHeight - _margin);
        }
        else if (_rightBottom)
        {
            _dialogContainer.Position = new Vector2(viewportSize.X - dialogWidth - _margin, viewportSize.Y - dialogHeight - _margin);
        }
        else
        {
            _dialogContainer.Position = new Vector2(
                (viewportSize.X - dialogWidth) / 2,
                viewportSize.Y - dialogHeight - _margin
            );
        }
    }
    
    private async void StartDialog()
    {
        if (_dialogContainer == null) return;
        
        PositionDialogContainer();
        _dialogContainer.Visible = true;
        
        _currentTween = CreateTween();
        _currentTween.SetTrans(Tween.TransitionType.Back);
        _currentTween.SetEase(Tween.EaseType.Out);
        _currentTween.TweenProperty(_dialogContainer, "modulate:a", 1.0f, 0.5f);
        
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
            EndDialogAndMoveCamera();
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
    
    private async void EndDialogAndMoveCamera()
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
        
        // 1. Затемняем экран
        if (_fadePanel != null)
        {
            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.SetEase(Tween.EaseType.InOut);
            _currentTween.TweenProperty(_fadePanel, "modulate:a", 1.0f, _fadeDuration);
            await ToSignal(_currentTween, Tween.SignalName.Finished);
        }
        
        // 2. Перемещаем камеру на позицию 1350, 559 и уменьшаем зум в 2 раза
        _gameCamera.GlobalPosition = _firstCameraPosition;
        _gameCamera.Zoom = _reducedCameraZoom;
        
        // 3. Убираем затемнение
        if (_fadePanel != null)
        {
            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.SetEase(Tween.EaseType.InOut);
            _currentTween.TweenProperty(_fadePanel, "modulate:a", 0f, _fadeDuration);
            await ToSignal(_currentTween, Tween.SignalName.Finished);
        }
        
        // 4. Плавно перемещаем камеру вдоль уровня 2
        await MoveCameraThroughLevel2();
        
        // 5. Возвращаем камеру к игроку и прикрепляем её
        await ReturnAndAttachCameraToPlayer();
        
        // 6. Камера теперь привязана к игроку, зум остаётся уменьшенным
        GD.Print("Camera is now attached to player with zoom: " + _gameCamera.Zoom);
    }
    
    private async Task MoveCameraThroughLevel2()
    {
        // Находим уровень 2
        Node2D level2 = null;
        foreach (var level in _levels)
        {
            if (level.Name.ToString() == "Level2")
            {
                level2 = level;
                break;
            }
        }
        
        if (level2 == null)
        {
            GD.PrintErr("Level2 not found!");
            return;
        }
        
        // Получаем границы уровня 2
        // Предполагаем, что уровень 2 имеет размер 1920x1080, центр в GlobalPosition
        float levelWidth = 1920;
        Vector2 startPosition = new Vector2(level2.GlobalPosition.X - levelWidth / 2, level2.GlobalPosition.Y);
        Vector2 endPosition = new Vector2(level2.GlobalPosition.X + levelWidth / 2, level2.GlobalPosition.Y);
        
        // Устанавливаем камеру на начало уровня 2
        _gameCamera.GlobalPosition = startPosition;
        
        // Плавно перемещаем камеру вдоль уровня 2
        float elapsed = 0f;
        float levelMoveDuration = 3.0f; // время прохода уровня 2
        
        while (elapsed < levelMoveDuration)
        {
            elapsed += (float)GetProcessDeltaTime();
            float t = elapsed / levelMoveDuration;
            _gameCamera.GlobalPosition = startPosition.Lerp(endPosition, t);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        
        _gameCamera.GlobalPosition = endPosition;
        
        // Небольшая пауза
        await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
    }
    
    private async Task ReturnAndAttachCameraToPlayer()
    {
        if (_player == null)
        {
            GD.PrintErr("Player not found, cannot return camera!");
            return;
        }
        
        // Резко возвращаем камеру к игроку (без анимации)
        _gameCamera.GlobalPosition = _player.GlobalPosition;
        
        // Зум остаётся уменьшенным (_reducedCameraZoom)
        _gameCamera.Zoom = _reducedCameraZoom;
        
        // Небольшая задержка для стабилизации
        await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
        
        // Прикрепляем камеру к игроку
        AttachCameraToPlayer();
        
        _cameraAttachedToPlayer = true;
    }
    
    private void AttachCameraToPlayer()
    {
        if (_player == null || _gameCamera == null) return;
        
        // Способ 1: Сделать камеру дочерней игрока
        var cameraParent = _gameCamera.GetParent();
        if (cameraParent != _player)
        {
            cameraParent.RemoveChild(_gameCamera);
            _player.AddChild(_gameCamera);
            _gameCamera.Position = Vector2.Zero; // Смещение относительно игрока
        }
        
        GD.Print("Camera attached to player at position: " + _player.GlobalPosition);
    }
    
    // Публичный метод для открепления камеры (если нужно)
    public void DetachCameraFromPlayer()
    {
        if (_gameCamera == null) return;
        
        // Возвращаем камеру обратно в Main
        var cameraParent = _gameCamera.GetParent();
        if (cameraParent == _player)
        {
            cameraParent.RemoveChild(_gameCamera);
            _mainNode.AddChild(_gameCamera);
        }
        
        _cameraAttachedToPlayer = false;
    }
    
    // Публичный метод для восстановления зума
    public void ResetCameraZoom()
    {
        if (_gameCamera != null)
        {
            _gameCamera.Zoom = _originalCameraZoom;
        }
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