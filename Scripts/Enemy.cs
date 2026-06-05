using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
    [Export] public float PatrolSpeed = 80.0f;
    [Export] public float ChaseSpeed = 140.0f; // Скорость при погоне выше
    [Export] public float Gravity = 980.0f;
    [Export] public float MaxChaseDistance = 400.0f; // Максимальное расстояние для преследования
    [Export] public float AttackRange = 220.0f; // Дистанция, на которой враг может стрелять
    [Export] public float ShootInterval = 1.5f; // Интервал между выстрелами
    [Export] public float ShootAnimationDuration = 0.35f;
    [Export] public float MaxHealth = 30.0f; // Максимальное здоровье врага
    [Export] public string IdleAnimation = "idle";
    [Export] public string WalkAnimation = "run";
    [Export] public string ShootAnimation = "shootenemy";
    [Export] public PackedScene BulletScene;
    [Export] public float BulletSpeed = 650.0f;
    [Export] public float BulletLifeTime = 2.0f;

    private RayCast2D _floorDetector;
    private Sprite2D _sprite;
    private AnimatedSprite2D _animatedSprite;
    private Area2D _detectionArea;

    private int _direction = 1;
    private Vector2 _startPosition; // Исходная позиция патруля
    private bool _isReturningToStart = false; // Возвращается ли враг в исходную точку
    private float _shootCooldown = 0.0f;
    private float _shootAnimationTimer = 0.0f;
    private bool _isShooting = false;
    private string _currentAnimation = string.Empty;
    private float _health;
    private ulong _lastPlayerDetectedMs = 0;
    
    // Ссылка на игрока, когда он в зоне видимости
    private Player _targetPlayer = null; 

    public override void _Ready()
    {
        _floorDetector = GetNode<RayCast2D>("RayCast2D");
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        _animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        
        // Инициализируем здоровье
        _health = MaxHealth;
        
        // Сохраняем исходную позицию для возврата
        _startPosition = GlobalPosition;
        
        // Находим зону обнаружения и подписываемся на ее события (сигналы)
        _detectionArea = GetNode<Area2D>("DetectionArea");
        _detectionArea.BodyEntered += OnPlayerEntered;
        _detectionArea.BodyExited += OnPlayerExited;
        
        // Пытаемся найти игрока через граф сцены
        TryFindPlayer();
        
        GD.Print($"Enemy ready. DetectionArea collision layers: {_detectionArea.CollisionLayer}, mask: {_detectionArea.CollisionMask}");
        GD.Print($"Стартовая позиция врага: {_startPosition}");
    }
    
    // Найти игрока в сцене несколькими способами
    private void TryFindPlayer()
    {
        // Способ 1: Ищем через GetParent (если враг и игрок в одном родителе)
        if (GetParent() is Node2D parent)
        {
            _targetPlayer = parent.GetNode<Player>("Player");
            if (_targetPlayer != null)
            {
                GD.Print($"Игрок найден через родителя: {_targetPlayer.Name}");
                return;
            }
        }
        
        // Способ 2: Ищем через корневую сцену
        try
        {
            _targetPlayer = GetTree().Root.GetNode<Player>("Main/Player");
            if (_targetPlayer != null)
            {
                GD.Print($"Игрок найден через Main/Player: {_targetPlayer.Name}");
                return;
            }
        }
        catch
        {
            GD.PrintErr("Не удалось найти игрока по пути Main/Player");
        }
        
        // Способ 3: Поиск по типу через все узлы сцены
        _targetPlayer = FindPlayerRecursive(GetTree().Root);
        if (_targetPlayer != null)
        {
            GD.Print($"Игрок найден рекурсивным поиском: {_targetPlayer.Name}");
        }
        else
        {
            GD.PrintErr("Не удалось найти игрока в сцене!");
        }
    }
    
    // Рекурсивный поиск игрока в дереве узлов
    private Player FindPlayerRecursive(Node node)
    {
        if (node is Player player)
        {
            return player;
        }
        
        foreach (Node child in node.GetChildren())
        {
            Player found = FindPlayerRecursive(child);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
    
    public override void _ExitTree()
    {
        // Отписываемся от сигналов при удалении узла
        if (_detectionArea != null)
        {
            _detectionArea.BodyEntered -= OnPlayerEntered;
            _detectionArea.BodyExited -= OnPlayerExited;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float deltaF = (float)delta;
        Vector2 velocity = Velocity;

        // Гравитация
        if (!IsOnFloor())
        {
            velocity.Y += Gravity * deltaF;
        }

        // Таймеры для стрельбы и анимации
        if (_shootCooldown > 0)
        {
            _shootCooldown -= deltaF;
        }

        if (_shootAnimationTimer > 0)
        {
            _shootAnimationTimer -= deltaF;
            if (_shootAnimationTimer <= 0)
            {
                _isShooting = false;
            }
        }

        // Проверяем валидность ссылки на игрока
        if (_targetPlayer != null && !IsInstanceValid(_targetPlayer))
        {
            _targetPlayer = null;
        }
        
        // Если нет цели и она была, пытаемся найти заново
        if (_targetPlayer == null)
        {
            TryFindPlayer();
        }

        bool isChasing = false;

        // Логика движения
        if (_targetPlayer != null && IsInstanceValid(_targetPlayer))
        {
            // Проверяем расстояние до игрока
            float distanceToPlayer = GlobalPosition.DistanceTo(_targetPlayer.GlobalPosition);
            
            if (distanceToPlayer <= MaxChaseDistance)
            {
                // --- РЕЖИМ ПРЕСЛЕДОВАНИЯ ---
                _isReturningToStart = false;
                isChasing = true;
                
                // Вычисляем направление к игроку по горизонтали (X)
                float directionToPlayer = Mathf.Sign(_targetPlayer.GlobalPosition.X - GlobalPosition.X);
                
                velocity.X = directionToPlayer * ChaseSpeed;

                // Разворачиваем спрайт в сторону игрока
                FlipSprite(directionToPlayer == -1);

                // Стрельба, когда игрок достаточно близко
                if (distanceToPlayer <= AttackRange && _shootCooldown <= 0)
                {
                    _isShooting = true;
                    _shootAnimationTimer = ShootAnimationDuration;
                    _shootCooldown = ShootInterval;
                    TryShootAtPlayer();
                }
            }
            else
            {
                // Игрок слишком далеко - возвращаемся в исходную точку
                _isReturningToStart = true;
                _targetPlayer = null;
            }
        }
        
        if (_isReturningToStart)
        {
            // --- РЕЖИМ ВОЗВРАТА К СТАРТОВОЙ ПОЗИЦИИ ---
            float distanceToStart = GlobalPosition.DistanceTo(_startPosition);
            
            if (distanceToStart > 5.0f) // Если еще не дошли до начальной позиции
            {
                float directionToStart = Mathf.Sign(_startPosition.X - GlobalPosition.X);
                velocity.X = directionToStart * PatrolSpeed;
                FlipSprite(directionToStart == -1);
            }
            else
            {
                // Дошли до стартовой позиции
                velocity.X = 0;
                _isReturningToStart = false;
                GD.Print("Враг вернулся на стартовую позицию");
            }
        }
        else if (_targetPlayer == null)
        {
            // --- РЕЖИМ ПАТРУЛИРОВАНИЯ ---
            if (!_floorDetector.IsColliding() || IsOnWall())
            {
                DirectionFlip();
            }
            velocity.X = _direction * PatrolSpeed;
        }

        Velocity = velocity;
        MoveAndSlide();

        UpdateAnimation(velocity, isChasing);
    }

    private void DirectionFlip()
    {
        _direction *= -1;
        FlipSprite(_direction == -1);

        Vector2 rayPosition = _floorDetector.Position;
        rayPosition.X = Mathf.Abs(rayPosition.X) * _direction;
        _floorDetector.Position = rayPosition;
    }

    private void FlipSprite(bool flipH)
    {
        if (_sprite != null)
            _sprite.FlipH = flipH;
        if (_animatedSprite != null)
            _animatedSprite.FlipH = flipH;
    }

    private void UpdateAnimation(Vector2 velocity, bool isChasing)
    {
        if (_animatedSprite == null)
            return;

        if (_isShooting)
        {
            PlayAnimation(ShootAnimation);
            return;
        }

        if (!IsOnFloor())
        {
            PlayAnimation(IdleAnimation);
            return;
        }

        if (Mathf.Abs(velocity.X) > 0.01f)
        {
            PlayAnimation(WalkAnimation);
            return;
        }

        PlayAnimation(IdleAnimation);
    }

    private void PlayAnimation(string animationName)
    {
        if (_animatedSprite == null || string.IsNullOrEmpty(animationName))
            return;

        if (_currentAnimation == animationName)
            return;

        if (_animatedSprite.SpriteFrames != null && _animatedSprite.SpriteFrames.HasAnimation(animationName))
        {
            _animatedSprite.Play(animationName);
            _currentAnimation = animationName;
        }
    }

    private void TryShootAtPlayer()
    {
        if (_targetPlayer == null)
            return;

        var direction = (_targetPlayer.GlobalPosition - GlobalPosition).Normalized();
        var spawnOffset = direction * 16.0f;
        var spawnPosition = GlobalPosition + spawnOffset;

        Bullet bullet = null;
        if (BulletScene != null)
        {
            var instance = BulletScene.Instantiate();
            bullet = instance as Bullet;
            if (bullet == null)
            {
                GD.PrintErr("BulletScene must be a Bullet node with the Bullet script attached.");
                return;
            }
        }
        else
        {
            bullet = new Bullet();
        }

        bullet.Initialize(direction, BulletSpeed, BulletLifeTime);
        bullet.GlobalPosition = spawnPosition;
        bullet.GlobalRotation = direction.Angle();

        var parent = GetParent();
        if (parent != null)
        {
            parent.AddChild(bullet);
        }
        else
        {
            GetTree().Root.AddChild(bullet);
        }
    }

    // Триггер: КТО-ТО зашел в зону видимости (благодаря маске — это только игрок)
    private void OnPlayerEntered(Node body)
    {
        if (body is Player player)
        {
            // Проверяем, это ли наш целевой игрок (или его нет)
            if (_targetPlayer != player)
            {
                _targetPlayer = player;
                // Логируем обнаружение не чаще, чем раз в 20 секунд
                ulong now = (ulong)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
                if (now - _lastPlayerDetectedMs >= 20000ul)
                {
                    GD.Print("Игрок замечен! Начинаю погоню.");
                    _lastPlayerDetectedMs = now;
                }
            }
        }
    }

    // Триггер: Игрок убежал из зоны видимости
    private void OnPlayerExited(Node body)
    {
        if (body is Player player && player == _targetPlayer)
        {
            _targetPlayer = null;
            GD.Print("Игрок скрылся. Возвращаюсь к патрулированию.");
            
            // Сбрасываем направление патруля в зависимости от текущего поворота спрайта
            bool currentFlip = _animatedSprite != null ? _animatedSprite.FlipH : (_sprite != null ? _sprite.FlipH : false);
            _direction = currentFlip ? -1 : 1;
        }
    }

    public void TakeDamage(float damage)
    {
        _health -= damage;
        GD.Print($"Враг получил урон: {damage}. Здоровье: {_health}/{MaxHealth}");
        
        if (_health <= 0)
        {
            Die();
        }
    }

    public float GetHealth()
    {
        return _health;
    }

    public float GetMaxHealth()
    {
        return MaxHealth;
    }

    private void Die()
    {
        GD.Print("Враг повержен!");
        QueueFree();
    }
}