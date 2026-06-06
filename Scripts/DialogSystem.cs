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
    
    [Export] private bool _centerBottom = true;
    [Export] private int _margin = 20;
    
    private Queue<DialogLine> _dialogQueue = new Queue<DialogLine>();
    private bool _isTyping = false;
    private bool _canProceed = false;
    private Tween _currentTween;
    private Timer _typeTimer;
    private bool _isTransitioning = false;
    private int _currentCharIndex = 0;
    private string _currentTypingText = "";
    
    private Player _player;
    private CameraManager _cameraManager;
    private Node2D _mainNode;
    private Vector2 _viewportSize;

    public override void _Ready()
    {
        _typeTimer = new Timer();
        _typeTimer.WaitTime = 0.03;
        _typeTimer.OneShot = true;
        AddChild(_typeTimer);
        
        _viewportSize = GetViewport().GetVisibleRect().Size;
        
        // Поиск Main через корень
        _mainNode = GetNodeOrNull<Node2D>("/root/Main");
        
        if (_mainNode != null)
        {
            _player = _mainNode.GetNodeOrNull<Player>("Player");
            _cameraManager = _mainNode.GetNodeOrNull<CameraManager>("CameraManager");
        }
        
        if (_player == null) GD.PrintErr("DialogSystem: Player not found in Main!");
        if (_cameraManager == null) GD.PrintErr("DialogSystem: CameraManager not found in Main!");
        
        if (_player != null) _player.SetCanMove(false);
        
        if (_dialogContainer != null)
        {
            _dialogContainer.Modulate = new Color(1, 1, 1, 0);
            _dialogContainer.Visible = false;
        }
        
        if (_fadePanel != null) _fadePanel.Modulate = new Color(0, 0, 0, 0);
        
        GD.Print($"DialogSystem ready. Searching for CameraManager in Main...");
        
        LoadDialogues();
        StartDialog();
    }
    
    private void LoadDialogues()
    {
        AddDialogue("Зевс", "Как же я устал от этой работы, хочется спокойно быть дома с детьми, а не убивать за деньги, ты меня понимаешь?");
        AddDialogue("Бармен", "Дружище, тебе нужно больше отдыхать, может выпьешь ещё?");
        AddDialogue("Система", "После того как Зевс выпивает ещё, к нему подсаживается работяга и начинает разговор");
        AddDialogue("Работяга", "Привет, я услышал, что ты работаешь киллером, сможешь помочь мне решить одно дельце, в долгу не останусь?");
        AddDialogue("Зевс", "Ну выкладывай");
        AddDialogue("Работяга", "Мою сестру за долги похитила корпорация и требует выкуп, ты не подумай, деньги есть, но я не хочу их отдавать, сможешь решить проблему?");
        AddDialogue("Зевс", "Да как нехуй");
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
        if (_currentTween != null && _currentTween.IsValid()) _currentTween.Kill();
        if (_clickIndicator == null) return;
        
        _currentTween = CreateTween();
        _currentTween.SetLoops(0);
        _currentTween.TweenProperty(_clickIndicator, "modulate:a", 0.3f, 0.3f);
        _currentTween.TweenProperty(_clickIndicator, "modulate:a", 1.0f, 0.3f);
    }
    
    private void StopClickBlink()
    {
        if (_currentTween != null && _currentTween.IsValid()) _currentTween.Kill();
        if (_clickIndicator != null) _clickIndicator.Modulate = new Color(1, 1, 1, 1);
    }
    
    private void ShowNextLine()
    {
        if (_dialogQueue.Count == 0)
        {
            EndDialogAndTransitionToLevel2();
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
        
        _typeTimer.Timeout += TypeNextChar;
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
            
            _typeTimer.Timeout -= TypeNextChar;
            
            if (_clickIndicator != null && _clickIndicator.Visible) StartClickBlink();
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
    
    private async void EndDialogAndTransitionToLevel2()
    {
        _isTransitioning = true;
        _canProceed = false;
        
        GD.Print("========================================");
        GD.Print("DialogSystem: All dialogues completed!");
        GD.Print("Starting transition to Level 2...");
        GD.Print("========================================");
        
        if (_clickIndicator != null) _clickIndicator.Visible = false;
        
        if (_dialogContainer != null)
        {
            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.TweenProperty(_dialogContainer, "modulate:a", 0, 0.3f);
            await ToSignal(_currentTween, Tween.SignalName.Finished);
            _dialogContainer.Visible = false;
        }
        
        // НЕ затемняем здесь! Доверяем CameraManager'у
        
        // Поиск CameraManager в Main
        Node2D mainNode = GetNodeOrNull<Node2D>("/root/Main");
        if (mainNode != null)
        {
            _cameraManager = mainNode.GetNodeOrNull<CameraManager>("CameraManager");
        }
        
        if (_cameraManager != null)
        {
            // CameraManager сам сделает затемнение, телепортацию и снятие затемнения
            await _cameraManager.AttachToPlayerOnLevel2();
            GD.Print("DialogSystem: CameraManager.AttachToPlayerOnLevel2() completed.");
        }
        else
        {
            GD.PrintErr("DialogSystem: CameraManager not found in Main!");
            if (_player != null) _player.SetCanMove(true);
        }
        
        _isTransitioning = false;
        
        GD.Print("========================================");
        GD.Print("DialogSystem: Transition completed!");
        GD.Print("========================================");
    }
    
    public override void _ExitTree()
    {
        base._ExitTree();
        if (_typeTimer != null)
        {
            _typeTimer.Timeout -= TypeNextChar;
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