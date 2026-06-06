using Godot;
using System;
using System.Threading.Tasks;

public partial class CameraManager : Node2D
{
    [Export] private Camera2D _gameCamera;
    [Export] private Player _player;
    
    [Export] private Vector2 _cameraZoom = new Vector2(1.0f, 1.0f);
    [Export] private float _fadeDuration = 1.0f;
    
    // Границы уровней (X, Y, Width, Height)
    [Export] private Rect2 _level2Bounds = new Rect2(1200, 0, 2663, 648);
    [Export] private Rect2 _level3Bounds = new Rect2(4500, -665, 3440, 1291);
    [Export] private Rect2 _level4Bounds = new Rect2(8200, 0, 2560, 1295);
    
    // Точки появления на уровнях
    [Export] private Vector2 _level2SpawnPoint = new Vector2(1350, 548);
    [Export] private Vector2 _level3SpawnPoint = new Vector2(5363, 507);
    [Export] private Vector2 _level4SpawnPoint = new Vector2(8319, 0);
    
    private int _currentLevel = 1;
    private Rect2 _currentLevelBounds;
    private bool _isTransitioning = false;
    private bool _cameraAttached = false;
    private ColorRect _fadePanel;
    private Vector2 _viewportSize;
    private bool _waitingForDialogCompletion = true;

    public override void _Ready()
    {
        if (_gameCamera == null) _gameCamera = GetNodeOrNull<Camera2D>("Camera2D");
        if (_player == null) _player = GetNodeOrNull<Player>("Player");
        
        if (_gameCamera == null || _player == null)
        {
            GD.PrintErr("CameraManager: Camera2D or Player not found!");
            return;
        }
        
        // Создаём панель затемнения (только для первого перехода)
        _fadePanel = new ColorRect();
        _fadePanel.Color = new Color(0, 0, 0, 0);
        _fadePanel.Size = GetViewport().GetVisibleRect().Size;
        _fadePanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _fadePanel.ZIndex = 100;
        AddChild(_fadePanel);
        
        _viewportSize = GetViewport().GetVisibleRect().Size;
        
        _gameCamera.Zoom = _cameraZoom;
        _gameCamera.PositionSmoothingEnabled = true;
        _gameCamera.PositionSmoothingSpeed = 5.0f;
        _gameCamera.LimitLeft = int.MinValue / 2;
        _gameCamera.LimitRight = int.MaxValue / 2;
        _gameCamera.LimitTop = int.MinValue / 2;
        _gameCamera.LimitBottom = int.MaxValue / 2;
        
        // Начальная позиция камеры (уровень 1)
        _currentLevelBounds = new Rect2(0, 0, 1152, 648);
        SetCameraPosition(new Vector2(576, 324));
        
        GD.Print($"CameraManager ready.");
    }
    
    public async Task AttachToPlayerOnLevel2()
    {
        if (_waitingForDialogCompletion)
        {
            GD.Print("CameraManager: Attaching camera to player on Level 2...");
            _waitingForDialogCompletion = false;
            await PerformLevel2Setup();
        }
    }
    
    private async Task PerformLevel2Setup()
    {
        _isTransitioning = true;
        
        GD.Print("CameraManager: Starting Level 2 setup...");
        
        // 1. Затемнение ТОЛЬКО при первом переходе
        await FadeOut();
        GD.Print("CameraManager: FadeOut completed.");
        
        // 2. Устанавливаем границы уровня 2
        _currentLevelBounds = _level2Bounds;
        _currentLevel = 2;
        _cameraAttached = true;
        
        // 3. Телепортируем игрока
        _player.Teleport(_level2SpawnPoint);
        GD.Print($"CameraManager: Player teleported to {_level2SpawnPoint}");
        
        // 4. Ставим камеру на позицию игрока
        SetCameraPosition(_level2SpawnPoint);
        
        // 5. Включаем движение игрока
        _player.SetCanMove(true);
        GD.Print("CameraManager: Player movement enabled.");
        
        // Небольшая задержка для стабилизации
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        
        // 6. Убираем затемнение
        await FadeIn();
        GD.Print("CameraManager: FadeIn completed.");
        
        _isTransitioning = false;
        GD.Print("CameraManager: Level 2 setup complete!");
        
        // Уничтожаем панель затемнения, так как она больше не нужна
        if (_fadePanel != null)
        {
            _fadePanel.QueueFree();
            _fadePanel = null;
        }
    }
    
    private async Task FadeOut()
    {
        if (_fadePanel == null) return;
        
        _fadePanel.Visible = true;
        _fadePanel.Color = new Color(0, 0, 0, 0);
        
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Quad);
        tween.SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_fadePanel, "color:a", 1.0f, _fadeDuration);
        await ToSignal(tween, Tween.SignalName.Finished);
    }
    
    private async Task FadeIn()
    {
        if (_fadePanel == null) return;
        
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Quad);
        tween.SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_fadePanel, "color:a", 0.0f, _fadeDuration);
        await ToSignal(tween, Tween.SignalName.Finished);
        
        _fadePanel.Visible = false;
    }
    
    private Vector2 GetClampedCameraPosition(Vector2 targetPos)
    {
        float halfWidth = (_viewportSize.X * _cameraZoom.X) / 2;
        float halfHeight = (_viewportSize.Y * _cameraZoom.Y) / 2;
        
        float minX = _currentLevelBounds.Position.X + halfWidth;
        float maxX = _currentLevelBounds.Position.X + _currentLevelBounds.Size.X - halfWidth;
        float minY = _currentLevelBounds.Position.Y + halfHeight;
        float maxY = _currentLevelBounds.Position.Y + _currentLevelBounds.Size.Y - halfHeight;
        
        if (minX > maxX)
        {
            float centerX = _currentLevelBounds.Position.X + _currentLevelBounds.Size.X / 2;
            minX = maxX = centerX;
        }
        
        if (minY > maxY)
        {
            float centerY = _currentLevelBounds.Position.Y + _currentLevelBounds.Size.Y / 2;
            minY = maxY = centerY;
        }
        
        Vector2 clampedPos = targetPos;
        clampedPos.X = Mathf.Clamp(clampedPos.X, minX, maxX);
        clampedPos.Y = Mathf.Clamp(clampedPos.Y, minY, maxY);
        
        return clampedPos;
    }
    
    private void SetCameraPosition(Vector2 targetPos)
    {
        if (_gameCamera == null) return;
        _gameCamera.GlobalPosition = GetClampedCameraPosition(targetPos);
    }
    
    public async void TeleportToLevel(int targetLevel, Vector2 spawnPosition)
    {
        if (_isTransitioning) return;
        
        _isTransitioning = true;
        GD.Print($"CameraManager: Teleporting to Level {targetLevel}");
        
        // Отключаем движение игрока (без затемнения)
        _player.SetCanMove(false);
        
        // Обновляем границы
        switch (targetLevel)
        {
            case 3:
                _currentLevelBounds = _level3Bounds;
                break;
            case 4:
                _currentLevelBounds = _level4Bounds;
                break;
            default:
                _currentLevelBounds = _level2Bounds;
                break;
        }
        
        _currentLevel = targetLevel;
        
        // Телепортируем игрока
        _player.Teleport(spawnPosition);
        
        // Обновляем позицию камеры
        SetCameraPosition(spawnPosition);
        
        // Ждём один кадр для стабилизации
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        
        // Включаем движение игрока
        _player.SetCanMove(true);
        
        _isTransitioning = false;
        GD.Print($"CameraManager: Teleported to Level {targetLevel}");
    }
    
    public override void _Process(double delta)
    {
        if (_cameraAttached && _gameCamera != null && _player != null && !_isTransitioning && !_waitingForDialogCompletion)
        {
            SetCameraPosition(_player.GetPlayerPosition());
        }
    }
    
    public int GetCurrentLevel() => _currentLevel;
}