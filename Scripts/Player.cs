using Godot;

public partial class Player : CharacterBody2D
{
    [Export] public float Speed = 300.0f;
    [Export] public float Gravity = 980.0f;
    [Export] public float JumpVelocity = -400.0f;
    [Export] public bool CanMove = true;
    [Export] public PackedScene BulletScene;
    [Export] public float BulletSpeed = 900.0f;
    [Export] public float ShootCooldown = 0.3f;
    [Export] public float BulletLifeTime = 3.0f;

    private Vector2 _velocity;
    private Sprite2D _sprite;
    private bool _isFacingRight = true;
    private float _shootCooldownTimer = 0.0f;
    
    public override void _Ready()
    {
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        GD.Print($"Player ready at position: {GlobalPosition}");
    }
    
    public override void _PhysicsProcess(double delta)
    {
        float deltaF = (float)delta;
        
        // Гравитация
        if (!IsOnFloor())
        {
            _velocity.Y += Gravity * deltaF;
        }
        else if (_velocity.Y > 0)
        {
            _velocity.Y = 0;
        }
        
        // Горизонтальное движение
        float inputDirection = 0;
        if (CanMove)
        {
            if (Input.IsActionPressed("move_left")) inputDirection = -1;
            if (Input.IsActionPressed("move_right")) inputDirection = 1;
        }
        
        _velocity.X = inputDirection * Speed;
        
        // Прыжок
        if (Input.IsActionJustPressed("jump") && IsOnFloor() && CanMove)
        {
            _velocity.Y = JumpVelocity;
        }

        // Стрельба
        if (Input.IsActionJustPressed("shoot") && _shootCooldownTimer <= 0)
        {
            Shoot();
            _shootCooldownTimer = ShootCooldown;
        }

        if (_shootCooldownTimer > 0)
        {
            _shootCooldownTimer -= deltaF;
        }
        
        // Направление спрайта теперь зависит от положения МЫШИ, а не от движения
        UpdateSpriteFacing();
        
        Velocity = _velocity;
        MoveAndSlide();
    }

    private void UpdateSpriteFacing()
    {
        if (_sprite == null) return;

        // Получаем позицию мыши в мире
        Vector2 mousePosition = GetGlobalMousePosition();

        // Если мышь правее игрока, смотрим вправо. Если левее — влево.
        if (mousePosition.X > GlobalPosition.X && !_isFacingRight)
        {
            _isFacingRight = true;
            _sprite.Scale = new Vector2(0.43f, 0.555f);
        }
        else if (mousePosition.X < GlobalPosition.X && _isFacingRight)
        {
            _isFacingRight = false;
            _sprite.Scale = new Vector2(-0.43f, 0.555f);
        }
    }
    
    public void SetCanMove(bool canMove)
    {
        CanMove = canMove;
        if (!canMove)
        {
            _velocity.X = 0;
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

    private void Shoot()
    {
        if (!CanMove)
            return;

        // 1. Считываем позицию мыши и находим вектор направления
        Vector2 mousePosition = GetGlobalMousePosition();
        Vector2 direction = (mousePosition - GlobalPosition).Normalized();

        // 2. Смещение точки спавна пули (чтобы она вылетала чуть впереди игрока в сторону мыши)
        Vector2 spawnOffset = direction * 16f;
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

        // Передаем точное направление к мыши
        bullet.Initialize(direction, BulletSpeed, BulletLifeTime);
        bullet.GlobalPosition = spawnPosition;

        // Поворачиваем саму пулю (визуально), если у неё будет вытянутый спрайт
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
    }

    public override void _Ready()
    {
        SetupCollisionShape();
        QueueRedraw();
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
        shape.Radius = 4;

        var collisionShape = new CollisionShape2D();
        collisionShape.Shape = shape;
        AddChild(collisionShape);
    }

    public override void _Draw()
    {
        // Рисуем круг. Так как пуля летит во всех направлениях, круг выглядит отлично.
        DrawCircle(Vector2.Zero, 4, Colors.Yellow);
    }
}