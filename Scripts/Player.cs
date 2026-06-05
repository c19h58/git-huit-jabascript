using Godot;
using System;

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
    [Export] public float BulletDamage = 10.0f;

    private Vector2 _velocity;
    private AnimatedSprite2D _animatedSprite2D;
    private RayCast2D _ladderRayCast;
    private bool _isFacingRight = true;
    private float _shootCooldownTimer = 0.0f;

    public override void _Ready()
    {
        _animatedSprite2D = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _ladderRayCast = GetNodeOrNull<RayCast2D>("LadderRayCast");
        GD.Print($"Player ready at position: {GlobalPosition}");
    }

    public override void _PhysicsProcess(double delta)
    {
        float deltaF = (float)delta;

        var ladderCollider = _ladderRayCast?.GetCollider();

        if (ladderCollider != null)
        {
            LadderClimb(delta);
        }
        else
        {
            Movement(delta);
        }

        SetAnimation();
        UpdateSpriteFacing();

        Velocity = _velocity;
        MoveAndSlide();
    }

    private void LadderClimb(double delta)
    {
        Vector2 direction = Vector2.Zero;

        direction.X = Input.GetAxis("ui_left", "ui_right");
        direction.Y = Input.GetAxis("ui_up", "ui_down");

        if (direction != Vector2.Zero)
        {
            _velocity = direction * (Speed / 2.0f);
        }
        else
        {
            _velocity = Vector2.Zero;
        }

        // if (_animatedSprite2D != null)
        // {
        //     if (_velocity != Vector2.Zero)
        //         _animatedSprite2D.Play("climb");
        //     else
        //         _animatedSprite2D.Stop();
        // }
    }

    private void Movement(double delta)
    {
        float deltaF = (float)delta;

        // Gravity
        if (!IsOnFloor())
        {
            _velocity.Y += Gravity * deltaF;
        }
        else if (_velocity.Y > 0)
        {
            _velocity.Y = 0;
        }

        // Horizontal movement
        float inputDirection = 0;
        if (CanMove)
        {
            if (Input.IsActionPressed("move_left")) inputDirection = -1;
            if (Input.IsActionPressed("move_right")) inputDirection = 1;
        }

        _velocity.X = inputDirection * Speed;

        // Jump
        if (Input.IsActionJustPressed("jump") && IsOnFloor() && CanMove)
        {
            _velocity.Y = JumpVelocity;
        }

        // Shooting
        if (Input.IsActionJustPressed("shoot") && _shootCooldownTimer <= 0)
        {
            Shoot();
            _shootCooldownTimer = ShootCooldown;
        }

        if (_shootCooldownTimer > 0)
        {
            _shootCooldownTimer -= deltaF;
        }
    }

    private void SetAnimation()
    {
        if (_animatedSprite2D == null)
            return;

        // If on ladder, ladder code already handles climb animation/stop
        if (_ladderRayCast?.GetCollider() != null)
            return;

        if (!IsOnFloor())
        {
            _animatedSprite2D.Play("jump");
        }
        else if (Math.Abs(_velocity.X) > 0.01f)
        {
            _animatedSprite2D.Play("run");
        }
        else
        {
            _animatedSprite2D.Play("idle");
        }
    }

    private void UpdateSpriteFacing()
    {
        if (_animatedSprite2D == null) return;

        Vector2 mousePosition = GetGlobalMousePosition();

        if (mousePosition.X > GlobalPosition.X && !_isFacingRight)
        {
            _isFacingRight = true;
        }
        else if (mousePosition.X < GlobalPosition.X && _isFacingRight)
        {
            _isFacingRight = false;
        }

        // Keep a consistent scale and flip horizontally for facing
        _animatedSprite2D.Scale = new Vector2(0.43f, 0.555f);
        _animatedSprite2D.FlipH = !_isFacingRight;
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

        Vector2 mousePosition = GetGlobalMousePosition();
        Vector2 direction = (mousePosition - GlobalPosition).Normalized();

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

        bullet.Initialize(direction, BulletSpeed, BulletLifeTime, BulletDamage);
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
}

public partial class Bullet : Area2D
{
    private Vector2 _velocity = Vector2.Zero;
    private float _lifeTime = 3.0f;
    private float _timeAlive = 0.0f;
    private bool _initialized = false;
    private float _damage = 10.0f;
    private bool _hasHit = false;
    private float _safeTime = 0.05f; // seconds during which bullet won't damage overlapping bodies

    public void Initialize(Vector2 direction, float speed, float lifeTime, float damage = 10.0f)
    {
        _velocity = direction.Normalized() * speed;
        _lifeTime = lifeTime;
        _timeAlive = 0.0f;
        _damage = damage;
        _initialized = true;
    }

    public override void _Ready()
    {
        SetupCollisionShape();
        BodyEntered += OnBodyEntered;
        QueueRedraw();
    }

    public override void _ExitTree()
    {
        BodyEntered -= OnBodyEntered;
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

    private void OnBodyEntered(Node body)
    {
        if (_hasHit)
            return;

        if (!_initialized)
            return;

        // Не наносим урон в первые _safeTime секунд после инициализации (чтобы избежать мгновенных пересечений при спавне)
        if (_timeAlive < _safeTime)
            return;

        if (body == null)
            return;

        // Если это непосредственно Enemy или другой объект с методом TakeDamage
        if (body.HasMethod("TakeDamage"))
        {
            body.Call("TakeDamage", _damage);
            _hasHit = true;
            QueueFree();
            return;
        }

        // Иногда коллайдером может быть дочерний узел (CollisionShape или Area) — пробуем родителя
        var parent = body.GetParent();
        if (parent != null && parent.HasMethod("TakeDamage"))
        {
            parent.Call("TakeDamage", _damage);
            _hasHit = true;
            QueueFree();
        }
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 4, Colors.Yellow);
    }
}