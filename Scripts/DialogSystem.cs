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
    
    // Позиции для камеры
    [Export] private Vector2 _firstCameraPosition = new Vector2(1350, 300);
    [Export] private Vector2 _originalCameraZoom = new Vector2(1.0f, 1.0f);
    [Export] private Vector2 _smallCameraZoom = new Vector2(2.0f, 2.0f); // 250x250 (250/1920≈0.13, 250/1080≈0.23)
    
    // Размер окна (настройте под ваш проект)
    private const float VIEWPORT_WIDTH = 1920;
    private const float VIEWPORT_HEIGHT = 1080;
    
    // Ссылки на игрока и камеру
    private Camera2D _gameCamera;
    private Node2D _player;
    private Node2D _mainNode;
    private Node2D _level2;
    
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
    private bool _cameraAttachedToPlayer = false;
    
    // Путь для прохода по уровню 2
    private Vector2 _level2StartPoint;
    private Vector2 _level2EndPoint;
    private float _level2Width = 1200; // Ширина уровня 2 для прохода
    
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
        
        // Сохраняем исходный зум
        _originalCameraZoom = _gameCamera.Zoom;
        
        // Поиск игрока
        _player = _mainNode.GetNodeOrNull<Node2D>("Player");
        
        if (_player == null)
        {
            GD.PrintErr("Player not found in Main!");
        }
        
        // Поиск уровня 2
        _level2 = _mainNode.GetNodeOrNull<Node2D>("Level2");
        
        if (_level2 == null)
        {
            GD.PrintErr("Level2 not found in Main!");
        }
        else
        {
            // Настройка точек для прохода по уровню 2
            _level2StartPoint = new Vector2(_level2.GlobalPosition.X - _level2Width / 2, _level2.GlobalPosition.Y);
            _level2EndPoint = new Vector2(_level2.GlobalPosition.X + _level2Width / 2, _level2.GlobalPosition.Y);
        }
        
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
        AddDialogue("Зевс(гг, персонаж)", "Как же я устал от этой работы , хочется спокойно быть дома с детьми а не убивать за деньги , ты меня понимаешь?");
        AddDialogue("бармен", "Дружище , тебе нужно больше отдыхать , может выпишешь ещё ?");
        AddDialogue("Система", "После того как зевс выпивает ещё , к нему подсаживается работяга и начинает разговор");
        AddDialogue("работяга", "Привет , я услышал , что ты работаешь киллером , сможешь помочь мне решить одно дельце , в долгу не останусь?");
        AddDialogue("Зевс(гг, персонаж)", "Ну выкладывай ");
        AddDialogue("работяга:", "Мою сестру за долги похитила корпорация и требует выкуп , ты не подумай , деньги есть , но я не хочу их отдавать , сможешь решить проблему?");
        AddDialogue("Зевс(гг, персонаж)", "Да как нехуй.");
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
        
        // 2. Открепляем камеру от игрока
        DetachCameraFromPlayer();
        
        // 3. Уменьшаем область видимости камеры до 250x250
        _gameCamera.Zoom = _smallCameraZoom;
        
        // 4. Перемещаем камеру на начальную позицию
        _gameCamera.GlobalPosition = _firstCameraPosition;
        
        // 5. Убираем затемнение
        if (_fadePanel != null)
        {
            _currentTween = CreateTween();
            _currentTween.SetTrans(Tween.TransitionType.Quad);
            _currentTween.SetEase(Tween.EaseType.InOut);
            _currentTween.TweenProperty(_fadePanel, "modulate:a", 0f, _fadeDuration);
            await ToSignal(_currentTween, Tween.SignalName.Finished);
        }
        
        // 6. Проходим камерой вдоль уровня 2 с областью видимости 250x250
        await MoveCameraThroughLevel2();
        
        // 7. Возвращаем камеру к игроку и прикрепляем её
        await ReturnAndAttachCameraToPlayer();
        
        GD.Print("Camera passed through Level2 with 250x250 view and attached to player");
    }
    
    private async Task MoveCameraThroughLevel2()
    {
        if (_level2 == null)
        {
            GD.PrintErr("Level2 not found, cannot move camera!");
            return;
        }
        
        // Устанавливаем камеру на начальную точку уровня 2
        _gameCamera.GlobalPosition = _level2StartPoint;
        
        // Небольшая пауза перед началом движения
        await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
        
        // Плавно перемещаем камеру вдоль уровня 2
        float elapsed = 0f;
        float levelMoveDuration = 4.0f; // Время прохода уровня 2 (можно настроить)
        
        GD.Print("Starting camera movement through Level2...");
        
        while (elapsed < levelMoveDuration)
        {
            elapsed += (float)GetProcessDeltaTime();
            float t = elapsed / levelMoveDuration;
            _gameCamera.GlobalPosition = _level2StartPoint.Lerp(_level2EndPoint, t);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        
        // Убеждаемся, что камера точно на конечной точке
        _gameCamera.GlobalPosition = _level2EndPoint;
        
        // Пауза после прохода уровня
        await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
        
        GD.Print("Camera finished moving through Level2");
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
        
        // Небольшая задержка для стабилизации
        await ToSignal(GetTree().CreateTimer(0.1f), SceneTreeTimer.SignalName.Timeout);
        
        // Прикрепляем камеру к игроку
        AttachCameraToPlayer();
        
        _cameraAttachedToPlayer = true;
        
        GD.Print("Camera returned and attached to player at position: " + _player.GlobalPosition);
    }
    
    private void AttachCameraToPlayer()
    {
        if (_player == null || _gameCamera == null) return;
        
        // Делаем камеру дочерней игрока
        var cameraParent = _gameCamera.GetParent();
        if (cameraParent != _player)
        {
            cameraParent.RemoveChild(_gameCamera);
            _player.AddChild(_gameCamera);
            _gameCamera.Position = Vector2.Zero;
        }
        
        GD.Print("Camera attached to player");
    }
    
    private void DetachCameraFromPlayer()
    {
        if (_gameCamera == null) return;
        
        var cameraParent = _gameCamera.GetParent();
        if (cameraParent == _player)
        {
            cameraParent.RemoveChild(_gameCamera);
            _mainNode.AddChild(_gameCamera);
        }
        
        _cameraAttachedToPlayer = false;
    }
    
    // Публичный метод для восстановления исходного размера камеры
    public void ResetCameraZoom()
    {
        if (_gameCamera != null)
        {
            DetachCameraFromPlayer();
            _gameCamera.Zoom = _originalCameraZoom;
            GD.Print("Camera zoom restored to original");
        }
    }
    
    // Публичный метод для получения текущего размера камеры в пикселях
    public Vector2 GetCurrentCameraSizeInPixels()
    {
        if (_gameCamera == null) return Vector2.Zero;
        
        float currentWidth = VIEWPORT_WIDTH * _gameCamera.Zoom.X;
        float currentHeight = VIEWPORT_HEIGHT * _gameCamera.Zoom.Y;
        return new Vector2(currentWidth, currentHeight);
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