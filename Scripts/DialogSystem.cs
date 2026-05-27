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
    
    // Настройки камеры после прикрепления
    [Export] private Vector2 _attachedCameraZoom = new Vector2(1.0f, 1.0f);
    
    // Границы уровней
    private Rect2 _level1Bounds = new Rect2(0, 0, 1152, 648);
    private Rect2 _level2Bounds = new Rect2(1200, 0, 2663, 648);
    private Rect2 _currentLevelBounds;
    
    // Ссылки на узлы
    private Camera2D _gameCamera;
    private Player _player;
    private Node2D _mainNode;
    
    // Позиционирование диалога
    [Export] private bool _centerBottom = true;
    [Export] private int _margin = 20;
    
    // Очередь диалогов
    private Queue<DialogLine> _dialogQueue = new Queue<DialogLine>();
    private bool _isTyping = false;
    private bool _canProceed = false;
    private Tween _currentTween;
    private Timer _typeTimer;
    private bool _isTransitioning = false;
    private int _currentCharIndex = 0;
    private string _currentTypingText = "";
    private bool _cameraAttached = false;
    
    // Размер окна просмотра
    private Vector2 _viewportSize;
    
    public override void _Ready()
    {
        // Настройка таймера
        _typeTimer = new Timer();
        _typeTimer.WaitTime = 0.03;
        _typeTimer.OneShot = true;
        AddChild(_typeTimer);
        
        // Получаем размер окна
        _viewportSize = GetViewport().GetVisibleRect().Size;
        
        // Поиск узлов в Main
        _mainNode = GetNodeOrNull<Node2D>("/root/Main");
        if (_mainNode == null)
        {
            GD.PrintErr("Main node not found!");
            return;
        }
        
        _gameCamera = _mainNode.GetNodeOrNull<Camera2D>("Camera2D");
        if (_gameCamera == null)
        {
            GD.PrintErr("Camera2D not found!");
            return;
        }
        
        _player = _mainNode.GetNodeOrNull<Player>("Player");
        if (_player == null)
        {
            GD.PrintErr("Player not found!");
            return;
        }
        
        // Настройка камеры
        _gameCamera.Zoom = new Vector2(1.0f, 1.0f);
        _gameCamera.PositionSmoothingEnabled = true;
        _gameCamera.PositionSmoothingSpeed = 5.0f;
        
        // Отключаем встроенные лимиты камеры
        _gameCamera.LimitLeft = int.MinValue / 2;
        _gameCamera.LimitRight = int.MaxValue / 2;
        _gameCamera.LimitTop = int.MinValue / 2;
        _gameCamera.LimitBottom = int.MaxValue / 2;
        
        // Устанавливаем начальные границы (уровень 1)
        _currentLevelBounds = _level1Bounds;
        
        // Ставим камеру на центр уровня 1
        Vector2 cameraCenter = new Vector2(
            _currentLevelBounds.Position.X + _currentLevelBounds.Size.X / 2,
            _currentLevelBounds.Position.Y + _currentLevelBounds.Size.Y / 2
        );
        _gameCamera.GlobalPosition = cameraCenter;
        
        // Отключаем движение игрока во время диалога
        _player.SetCanMove(false);
        
        // Прячем диалоговое окно
        if (_dialogContainer != null)
        {
            _dialogContainer.Modulate = new Color(1, 1, 1, 0);
            _dialogContainer.Visible = false;
        }
        
        // Затемнение
        if (_fadePanel != null)
        {
            _fadePanel.Modulate = new Color(0, 0, 0, 0);
        }
        
        GD.Print($"Viewport size: {_viewportSize}");
        GD.Print($"Camera start position: {_gameCamera.GlobalPosition}");
        
        LoadDialogues();
        StartDialog();
    }
    
    private Vector2 GetClampedCameraPosition(Vector2 targetPos, Vector2 cameraZoom)
    {
        // Вычисляем половину области видимости камеры с учётом зума
        float halfWidth = (_viewportSize.X * cameraZoom.X) / 2;
        float halfHeight = (_viewportSize.Y * cameraZoom.Y) / 2;
        
        // Вычисляем минимальные и максимальные границы для камеры
        // Камера не должна выходить за края уровня
        float minX = _currentLevelBounds.Position.X + halfWidth;
        float maxX = _currentLevelBounds.Position.X + _currentLevelBounds.Size.X - halfWidth;
        float minY = _currentLevelBounds.Position.Y + halfHeight;
        float maxY = _currentLevelBounds.Position.Y + _currentLevelBounds.Size.Y - halfHeight;
        
        // Если уровень меньше области видимости камеры по горизонтали
        if (minX > maxX)
        {
            float centerX = _currentLevelBounds.Position.X + _currentLevelBounds.Size.X / 2;
            minX = maxX = centerX;
        }
        
        // Если уровень меньше области видимости камеры по вертикали
        if (minY > maxY)
        {
            float centerY = _currentLevelBounds.Position.Y + _currentLevelBounds.Size.Y / 2;
            minY = maxY = centerY;
        }
        
        // Ограничиваем позицию камеры
        Vector2 clampedPos = targetPos;
        clampedPos.X = Mathf.Clamp(clampedPos.X, minX, maxX);
        clampedPos.Y = Mathf.Clamp(clampedPos.Y, minY, maxY);
        
        return clampedPos;
    }
    
    private void SetCameraPosition(Vector2 targetPos, Vector2 cameraZoom)
    {
        if (_gameCamera == null) return;
        
        Vector2 clampedPos = GetClampedCameraPosition(targetPos, cameraZoom);
        _gameCamera.GlobalPosition = clampedPos;
    }
    
    private void AttachCameraToPlayerOnLevel2()
    {
        if (_player == null || _gameCamera == null) return;
        
        // Меняем границы на уровень 2
        _currentLevelBounds = _level2Bounds;
        
        // Устанавливаем новый зум
        _gameCamera.Zoom = _attachedCameraZoom;
        
        // Устанавливаем позицию камеры на игрока с учётом границ
        Vector2 targetPos = _player.GetPlayerPosition();
        SetCameraPosition(targetPos, _attachedCameraZoom);
        
        // Включаем движение игрока
        _player.SetCanMove(true);
        
        _cameraAttached = true;
        
        // Отладочная информация
        float halfWidth = (_viewportSize.X * _attachedCameraZoom.X) / 2;
        float halfHeight = (_viewportSize.Y * _attachedCameraZoom.Y) / 2;
        
        GD.Print("=== Camera attached to player on Level2 ===");
        GD.Print($"Camera zoom: {_gameCamera.Zoom}");
        GD.Print($"Camera position: {_gameCamera.GlobalPosition}");
        GD.Print($"Player position: {_player.GetPlayerPosition()}");
        GD.Print($"Level2 bounds: {_currentLevelBounds}");
        GD.Print($"Half view size: ({halfWidth}, {halfHeight})");
        GD.Print($"Camera limits - X: [{_currentLevelBounds.Position.X + halfWidth}, {_currentLevelBounds.Position.X + _currentLevelBounds.Size.X - halfWidth}]");
        GD.Print($"Camera limits - Y: [{_currentLevelBounds.Position.Y + halfHeight}, {_currentLevelBounds.Position.Y + _currentLevelBounds.Size.Y - halfHeight}]");
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
            EndDialogAndSwitchToLevel2();
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
    
    private async void EndDialogAndSwitchToLevel2()
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
        
        // Затемняем экран
        if (_fadePanel != null)
        {
            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.SetEase(Tween.EaseType.InOut);
            _currentTween.TweenProperty(_fadePanel, "modulate:a", 1.0f, _fadeDuration);
            await ToSignal(_currentTween, Tween.SignalName.Finished);
        }
        
        // Прикрепляем камеру к игроку на уровне 2
        AttachCameraToPlayerOnLevel2();
        
        // Убираем затемнение
        if (_fadePanel != null)
        {
            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.SetEase(Tween.EaseType.InOut);
            _currentTween.TweenProperty(_fadePanel, "modulate:a", 0f, _fadeDuration);
            await ToSignal(_currentTween, Tween.SignalName.Finished);
        }
        
        _isTransitioning = false;
        
        GD.Print("Dialog ended, camera attached to player on Level2!");
    }
    
    public override void _Process(double delta)
    {
        // Если камера прикреплена, следуем за игроком с учётом границ
        if (_cameraAttached && _gameCamera != null && _player != null)
        {
            Vector2 targetPos = _player.GetPlayerPosition();
            SetCameraPosition(targetPos, _gameCamera.Zoom);
        }
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