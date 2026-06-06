using Godot;
using System;

public partial class Player : CharacterBody2D
{
    [Export] public float Speed = 300.0f;
    [Export] public float Gravity = 980.0f;
    [Export] public float JumpVelocity = -500.0f;
    [Export] public float ClimbSpeed = 150.0f;
    [Export] public float LadderHorizontalSpeed = 50.0f;
    [Export] public bool CanMove = true;
    [Export] public PackedScene BulletScene;
    [Export] public float BulletSpeed = 900.0f;
    [Export] public float ShootCooldown = 0.3f;
    [Export] public float BulletLifeTime = 3.0f;
    
    // ============================================
    // ЗДОРОВЬЕ
    // ============================================
    [Export] public int MaxHealth = 100;
    private int _currentHealth;
    
    // Ссылка на интерфейс
    private PlayerUI _playerUI;

    private Vector2 _velocity;
    private bool _isFacingRight = true;
    private float _shootCooldownTimer = 0.0f;
    
    // Переменные для лестницы
    private bool _isOnLadder = false;
    private bool _isClimbing = false;

    public override void _Ready()
    {
        // Добавляем игрока в группу
        AddToGroup("player");
        
        GD.Print($"Player ready at position: {GlobalPosition}");
        
        // Инициализация здоровья
        _currentHealth = MaxHealth;
        
        // Поиск интерфейса в сцене
        _playerUI = GetNodeOrNull<PlayerUI>("/root/Main/PlayerUI");
        if (_playerUI == null)
        {
            _playerUI = GetNodeOrNull<PlayerUI>("../PlayerUI");
        }
        
        if (_playerUI != null)
        {
            _playerUI.UpdateHealth(_currentHealth, MaxHealth);
            GD.Print("PlayerUI found and connected!");
        }
        else
        {
            GD.PrintErr("PlayerUI not found!");
        }
    }
    
    // ============================================
    // ОБНОВЛЕНИЕ ИНТЕРФЕЙСА
    // ============================================
    
    private void UpdateHealthUI()
    {
        if (_playerUI != null)
        {
            _playerUI.UpdateHealth(_currentHealth, MaxHealth);
        }
    }
    
    // ============================================
    // ПОЛУЧЕНИЕ УРОНА
    // ============================================
    
    public void TakeDamage(int amount)
    {
        if (_currentHealth <= 0)
            return;
        
        _currentHealth = Math.Max(0, _currentHealth - amount);
        GD.Print($"Player took {amount} damage! Health: {_currentHealth}/{MaxHealth}");
        
        UpdateHealthUI();
        
        // Эффект получения урона (мигание)
        Modulate = Colors.Red;
        GetTree().CreateTimer(0.1).Timeout += () => Modulate = Colors.White;
        
        if (_currentHealth <= 0)
        {
            Die();
        }
    }
    
    // ============================================
    // ЛЕЧЕНИЕ
    // ============================================
    
    public bool Heal(int amount)
    {
        if (_currentHealth >= MaxHealth)
            return false;
        
        _currentHealth = Math.Min(MaxHealth, _currentHealth + amount);
        UpdateHealthUI();
        
        GD.Print($"Player healed! Health: {_currentHealth}/{MaxHealth}");
        
        return true;
    }

    private void Die()
    {
        GD.Print("Player died!");
        SetCanMove(false);
    }

    // ============================================
    // ФИЗИКА И ДВИЖЕНИЕ
    // ============================================
    
    public override void _PhysicsProcess(double delta)
    {
        float deltaF = (float)delta;
        
        CheckForLadder();
        
        Vector2 inputDirection = Vector2.Zero;
        if (CanMove)
        {
            inputDirection.X = Input.GetAxis("move_left", "move_right");
            inputDirection.Y = Input.GetAxis("ui_up", "ui_down");
        }

        // ============================================
        // ЛОГИКА ЛЕСТНИЦЫ
        // ============================================
        
        if (_isOnLadder && !_isClimbing)
        {
            if (Input.IsActionPressed("ui_up") || Input.IsActionPressed("jump"))
            {
                _isClimbing = true;
            }
            else if (Input.IsActionPressed("ui_down"))
            {
                _isClimbing = true;
            }
        }
        
        if (_isClimbing)
        {
            float verticalInput = 0;
            if (Input.IsActionPressed("ui_up"))
                verticalInput = -1;
            else if (Input.IsActionPressed("ui_down"))
                verticalInput = 1;
            
            float horizontalInput = inputDirection.X;
            
            _velocity.Y = verticalInput * ClimbSpeed;
            _velocity.X = horizontalInput * LadderHorizontalSpeed;
            
            if (Math.Abs(verticalInput) < 0.01f && Math.Abs(horizontalInput) < 0.01f)
            {
                _velocity.X = 0;
                _velocity.Y = 0;
            }
        }
        else
        {
            // ============================================
            // ОБЫЧНОЕ ДВИЖЕНИЕ
            // ============================================
            
            if (!IsOnFloor())
            {
                _velocity.Y += Gravity * deltaF;
            }
            else if (_velocity.Y > 0)
            {
                _velocity.Y = 0;
            }

            float inputDirectionX = 0;
            if (CanMove)
            {
                inputDirectionX = Input.GetAxis("move_left", "move_right");
            }

            _velocity.X = inputDirectionX * Speed;

            if (Input.IsActionJustPressed("jump") && IsOnFloor() && CanMove)
            {
                _velocity.Y = JumpVelocity;
            }
        }

        // ============================================
        // СТРЕЛЬБА (в направлении движения)
        // ============================================
        if (Input.IsActionJustPressed("shoot") && _shootCooldownTimer <= 0 && CanMove)
        {
            ShootInMovementDirection();
            _shootCooldownTimer = ShootCooldown;
        }

        if (_shootCooldownTimer > 0)
        {
            _shootCooldownTimer -= deltaF;
        }

        Velocity = _velocity;
        MoveAndSlide();
        
        UpdateFacingDirection();
    }

    // ============================================
    // АТАКА В НАПРАВЛЕНИИ ДВИЖЕНИЯ
    // ============================================
    
    private void ShootInMovementDirection()
    {
        // Направление атаки: туда, куда смотрит персонаж
        Vector2 direction = _isFacingRight ? Vector2.Right : Vector2.Left;
        Vector2 spawnOffset = direction * 20f;
        Vector2 spawnPosition = GlobalPosition + spawnOffset;
        
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
        
        // Поворот пули в направлении выстрела
        bullet.GlobalRotation = direction.X > 0 ? 0 : Mathf.Pi;
        
        var parent = GetParent();
        if (parent != null)
            parent.AddChild(bullet);
        else
            GetTree().Root.AddChild(bullet);
    }

    // ============================================
    // ОБНОВЛЕНИЕ НАПРАВЛЕНИЯ ВЗГЛЯДА
    // ============================================
    
    private void UpdateFacingDirection()
    {
        // Меняем направление в зависимости от горизонтального движения
        if (Math.Abs(_velocity.X) > 10f)
        {
            _isFacingRight = _velocity.X > 0;
        }
        
        // Поворот спрайта (если есть AnimatedSprite2D)
        // var sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        // if (sprite != null)
        //     sprite.FlipH = !_isFacingRight;
    }

    // ============================================
    // ПОИСК ЛЕСТНИЦЫ
    // ============================================
    
    private void CheckForLadder()
    {
        bool onLadder = false;
        
        var ladders = GetTree().GetNodesInGroup("ladder");
        
        foreach (Area2D ladder in ladders)
        {
            if (ladder == null) continue;
            
            var overlappingBodies = ladder.GetOverlappingBodies();
            if (overlappingBodies.Contains(this))
            {
                onLadder = true;
                break;
            }
        }
        
        if (onLadder && !_isOnLadder)
        {
            _isOnLadder = true;
        }
        else if (!onLadder && _isOnLadder)
        {
            _isOnLadder = false;
            _isClimbing = false;
        }
    }

    // ============================================
    // ПУБЛИЧНЫЕ МЕТОДЫ
    // ============================================
    
    public void SetCanMove(bool canMove)
    {
        CanMove = canMove;
        if (!canMove)
        {
            _velocity.X = 0;
            _isClimbing = false;
        }
    }

    public Vector2 GetPlayerPosition()
    {
        return GlobalPosition;
    }

    public void Teleport(Vector2 newPosition)
    {
        GlobalPosition = newPosition;
    }
}

public partial class Bullet : Area2D
{
    private Vector2 _velocity = Vector2.Zero;
    private float _lifeTime = 3.0f;
    private float _timeAlive = 0.0f;
    private bool _initialized = false;

    public void Initialize(Vector2 direction, float speed, float lifeTime)
    {
        _velocity = direction.Normalized() * speed;
        _lifeTime = lifeTime;
        _timeAlive = 0.0f;
        _initialized = true;
        
        AddToGroup("projectiles");
    }

    public override void _Ready()
    {
        SetupCollisionShape();
        QueueRedraw();
        
        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_initialized)
            return;

        Position += _velocity * (float)delta;
        _timeAlive += (float)delta;

        if (_timeAlive >= _lifeTime)
        {
            QueueFree();
        }
    }
    
    private void SetupCollisionShape()
    {
        if (GetNodeOrNull<CollisionShape2D>("CollisionShape2D") != null)
            return;

        var shape = new CircleShape2D();
        shape.Radius = 6;
        
        var collisionShape = new CollisionShape2D();
        collisionShape.Shape = shape;
        AddChild(collisionShape);
    }
    
    private void OnBodyEntered(Node2D body)
    {
        if (body.IsInGroup("enemies"))
        {
            if (body.HasMethod("TakeDamage"))
            {
                body.Call("TakeDamage", 25);
            }
            QueueFree();
        }
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 6, Colors.Yellow);
    }
}