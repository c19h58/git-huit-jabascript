using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
    [Export] public float PatrolSpeed = 80.0f;
    [Export] public float ChaseSpeed = 140.0f;
    [Export] public float Gravity = 980.0f;
    [Export] public float MaxChaseDistance = 400.0f;
    [Export] public float AttackRange = 300.0f;
    [Export] public float ShootInterval = 1.5f;
    [Export] public float ShootAnimationDuration = 0.35f;
    [Export] public float MaxHealth = 30.0f;
    [Export] public string IdleAnimation = "idle";
    [Export] public string WalkAnimation = "run";
    [Export] public string ShootAnimation = "shootenemy";
    [Export] public PackedScene BulletScene;
    [Export] public float BulletSpeed = 500.0f;
    [Export] public float BulletLifeTime = 2.0f;
    [Export] public int BulletDamage = 10;

    private RayCast2D _floorDetector;
    private Sprite2D _sprite;
    private AnimatedSprite2D _animatedSprite;
    private Area2D _detectionArea;

    private int _direction = 1;
    private Vector2 _startPosition;
    private bool _isReturningToStart = false;
    private float _shootCooldown = 0.0f;
    private float _shootAnimationTimer = 0.0f;
    private bool _isShooting = false;
    private string _currentAnimation = string.Empty;
    private float _health;
    
    private Player _targetPlayer = null;

    public override void _Ready()
    {
        _floorDetector = GetNode<RayCast2D>("RayCast2D");
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        _animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        
        _health = MaxHealth;
        _startPosition = GlobalPosition;
        
        AddToGroup("enemies");
        
        _detectionArea = GetNode<Area2D>("DetectionArea");
        _detectionArea.BodyEntered += OnPlayerEntered;
        _detectionArea.BodyExited += OnPlayerExited;
        
        TryFindPlayer();
    }
    
    private void TryFindPlayer()
    {
        if (GetParent() is Node2D parent)
        {
            _targetPlayer = parent.GetNode<Player>("Player");
            if (_targetPlayer != null) return;
        }
        
        try
        {
            _targetPlayer = GetTree().Root.GetNode<Player>("Main/Player");
            if (_targetPlayer != null) return;
        }
        catch { }
        
        _targetPlayer = FindPlayerRecursive(GetTree().Root);
    }
    
    private Player FindPlayerRecursive(Node node)
    {
        if (node is Player player) return player;
        
        foreach (Node child in node.GetChildren())
        {
            Player found = FindPlayerRecursive(child);
            if (found != null) return found;
        }
        return null;
    }
    
    public override void _ExitTree()
    {
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

        if (!IsOnFloor())
        {
            velocity.Y += Gravity * deltaF;
        }

        if (_shootCooldown > 0) _shootCooldown -= deltaF;
        if (_shootAnimationTimer > 0)
        {
            _shootAnimationTimer -= deltaF;
            if (_shootAnimationTimer <= 0) _isShooting = false;
        }

        if (_targetPlayer != null && !IsInstanceValid(_targetPlayer)) _targetPlayer = null;
        if (_targetPlayer == null) TryFindPlayer();

        bool isChasing = false;

        if (_targetPlayer != null && IsInstanceValid(_targetPlayer))
        {
            float distanceToPlayer = GlobalPosition.DistanceTo(_targetPlayer.GlobalPosition);
            
            if (distanceToPlayer <= MaxChaseDistance)
            {
                if (distanceToPlayer <= AttackRange && _shootCooldown <= 0 && !_isShooting)
                {
                    velocity.X = 0;
                    _isShooting = true;
                    _shootAnimationTimer = ShootAnimationDuration;
                    _shootCooldown = ShootInterval;
                    TryShootAtPlayer();
                }
                else if (!_isShooting)
                {
                    _isReturningToStart = false;
                    isChasing = true;
                    
                    float directionToPlayer = Mathf.Sign(_targetPlayer.GlobalPosition.X - GlobalPosition.X);
                    velocity.X = directionToPlayer * ChaseSpeed;
                    FlipSprite(directionToPlayer == -1);
                }
                else
                {
                    velocity.X = 0;
                }
            }
            else
            {
                _isReturningToStart = true;
                _targetPlayer = null;
            }
        }
        
        if (_isReturningToStart)
        {
            float distanceToStart = GlobalPosition.DistanceTo(_startPosition);
            
            if (distanceToStart > 5.0f)
            {
                float directionToStart = Mathf.Sign(_startPosition.X - GlobalPosition.X);
                velocity.X = directionToStart * PatrolSpeed;
                FlipSprite(directionToStart == -1);
            }
            else
            {
                velocity.X = 0;
                _isReturningToStart = false;
            }
        }
        else if (_targetPlayer == null && !_isShooting)
        {
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
        if (_sprite != null) _sprite.FlipH = flipH;
        if (_animatedSprite != null) _animatedSprite.FlipH = flipH;
    }

    private void UpdateAnimation(Vector2 velocity, bool isChasing)
    {
        if (_animatedSprite == null) return;

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
        if (_animatedSprite == null || string.IsNullOrEmpty(animationName)) return;
        if (_currentAnimation == animationName) return;

        if (_animatedSprite.SpriteFrames != null && _animatedSprite.SpriteFrames.HasAnimation(animationName))
        {
            _animatedSprite.Play(animationName);
            _currentAnimation = animationName;
        }
    }

    private void TryShootAtPlayer()
    {
        if (_targetPlayer == null) return;

        var direction = (_targetPlayer.GlobalPosition - GlobalPosition).Normalized();
        var spawnOffset = direction * 20.0f;
        var spawnPosition = GlobalPosition + spawnOffset;

        if (BulletScene == null) return;

        var bullet = BulletScene.Instantiate<Bullet>();
        bullet.Initialize(direction, BulletSpeed, BulletLifeTime, BulletDamage, this);
        bullet.GlobalPosition = spawnPosition;
        bullet.GlobalRotation = direction.Angle();

        GetTree().Root.AddChild(bullet);
    }

    private void OnPlayerEntered(Node body)
    {
        if (body is Player player)
        {
            if (_targetPlayer != player) _targetPlayer = player;
        }
    }

    private void OnPlayerExited(Node body)
    {
        if (body is Player player && player == _targetPlayer)
        {
            _targetPlayer = null;
            bool currentFlip = _animatedSprite != null ? _animatedSprite.FlipH : (_sprite != null ? _sprite.FlipH : false);
            _direction = currentFlip ? -1 : 1;
        }
    }

    public void TakeDamage(int damage)
    {
        _health -= damage;
        if (_health <= 0) Die();
    }

    private void Die()
    {
        QueueFree();
    }
}