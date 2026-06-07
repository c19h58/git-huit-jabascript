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
    [Export] public float BulletSpeed = 600.0f;
    [Export] public float ShootCooldown = 0.3f;
    [Export] public float BulletLifeTime = 0.6f;
    [Export] public int BulletDamage = 25;
    

    [Export] public string IdleAnimation = "idle";
    [Export] public string WalkAnimation = "run";
    [Export] public string JumpAnimation = "jump";
    [Export] public string ShootAnimation = "shoot";
    [Export] public float ShootAnimationDuration = 0.5f;
    

    [Export] public float AttackDelay = 0.4f;
    
    [Export] public int MaxHealth = 100;
    private int _currentHealth;

    private PlayerUI _playerUI;
    
    [Export] public Texture2D GameOverTexture;
    private TextureRect _gameOverScreen;
    private bool _isDead = false;

    private Vector2 _velocity;
    private bool _isFacingRight = true;
    private float _shootCooldownTimer = 0.0f;
    
    private bool _isOnLadder = false;
    private bool _isClimbing = false;
    
    private AnimatedSprite2D _animatedSprite;
    private string _currentAnimation = string.Empty;
    private bool _isShooting = false;
    private float _shootAnimationTimer = 0.0f;

    private float _attackDelayTimer = 0.0f;
    private bool _isWaitingForAttack = false;

    public override void _Ready()
    {
        AddToGroup("player");
        
        _animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        
        _currentHealth = MaxHealth;
        
        _playerUI = GetNodeOrNull<PlayerUI>("/root/Main/PlayerUI");
        if (_playerUI == null)
        {
            _playerUI = GetNodeOrNull<PlayerUI>("../PlayerUI");
        }
        
        if (_playerUI != null)
        {
            _playerUI.UpdateHealth(_currentHealth, MaxHealth);
        }
        
        CreateGameOverScreen();
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
    
    private void UpdateAnimation()
    {
        if (_animatedSprite == null || _isDead) return;
        
        if (_isShooting)
        {
            PlayAnimation(ShootAnimation);
            return;
        }
        
        if (!IsOnFloor())
        {
            PlayAnimation(JumpAnimation);
            return;
        }
        
        if (Mathf.Abs(_velocity.X) > 10f)
        {
            PlayAnimation(WalkAnimation);
            return;
        }
        
        PlayAnimation(IdleAnimation);
    }
    
    private void FlipSprite(bool flipH)
    {
        if (_animatedSprite != null)
        {
            _animatedSprite.FlipH = flipH;
        }
    }
    
    
    private void CreateGameOverScreen()
    {
        _gameOverScreen = new TextureRect();
        _gameOverScreen.Texture = GameOverTexture;
        _gameOverScreen.Size = GetViewport().GetVisibleRect().Size;
        _gameOverScreen.Modulate = new Color(1, 1, 1, 0);
        _gameOverScreen.MouseFilter = Control.MouseFilterEnum.Ignore;
        
        var canvasLayer = new CanvasLayer();
        canvasLayer.Layer = 100;
        canvasLayer.AddChild(_gameOverScreen);
        AddChild(canvasLayer);
    }
    
    private void ShowGameOver()
    {
        if (_gameOverScreen == null) return;
        
        var tween = CreateTween();
        tween.TweenProperty(_gameOverScreen, "modulate:a", 1.0f, 1.0f);
        
        GetTree().CreateTimer(4.0f).Timeout += () =>
        {
            ReloadGame();
        };
    }
    
    private void ReloadGame()
    {
        GetTree().ReloadCurrentScene();
    }
    
    private void UpdateHealthUI()
    {
        if (_playerUI != null)
        {
            _playerUI.UpdateHealth(_currentHealth, MaxHealth);
        }
    }

    
    public void TakeDamage(int amount)
    {
        if (_currentHealth <= 0 || _isDead)
            return;
        
        _currentHealth = Math.Max(0, _currentHealth - amount);
        
        UpdateHealthUI();
        
        Modulate = Colors.Red;
        GetTree().CreateTimer(0.1f).Timeout += () => Modulate = Colors.White;
        
        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    
    public bool Heal(int amount)
    {
        if (_currentHealth >= MaxHealth)
            return false;
        
        _currentHealth = Math.Min(MaxHealth, _currentHealth + amount);
        UpdateHealthUI();
        
        return true;
    }

    private void Die()
    {
        if (_isDead) return;
        
        _isDead = true;
        
        SetCanMove(false);
        ShowGameOver();
    }

    
    public override void _PhysicsProcess(double delta)
    {
        if (_isDead) return;
        
        float deltaF = (float)delta;

        if (_shootCooldownTimer > 0)
        {
            _shootCooldownTimer -= deltaF;
        }
        
        if (_shootAnimationTimer > 0)
        {
            _shootAnimationTimer -= deltaF;
            if (_shootAnimationTimer <= 0)
            {
                _isShooting = false;
            }
        }

        if (_isWaitingForAttack)
        {
            _attackDelayTimer -= deltaF;
            if (_attackDelayTimer <= 0)
            {
                _isWaitingForAttack = false;
                ShootInMovementDirection();
                _shootCooldownTimer = ShootCooldown;
            }
        }
        
        CheckForLadder();
        
        Vector2 inputDirection = Vector2.Zero;
        if (CanMove)
        {
            inputDirection.X = Input.GetAxis("move_left", "move_right");
            inputDirection.Y = Input.GetAxis("ui_up", "ui_down");
        }

        
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

        if (Input.IsActionJustPressed("shoot") && _shootCooldownTimer <= 0 && CanMove && !_isDead && !_isShooting && !_isWaitingForAttack)
        {
            _isShooting = true;
            _shootAnimationTimer = ShootAnimationDuration;
            PlayAnimation(ShootAnimation);

            _isWaitingForAttack = true;
            _attackDelayTimer = AttackDelay;
        }

        Velocity = _velocity;
        MoveAndSlide();
        
        UpdateFacingDirection();
        UpdateAnimation();
    }

    
    private void ShootInMovementDirection()
    {
        Vector2 direction = _isFacingRight ? Vector2.Right : Vector2.Left;
        Vector2 spawnOffset = direction * 30f + new Vector2(0, -30f);;
        Vector2 spawnPosition = GlobalPosition + spawnOffset;
        
        var bullet = BulletScene.Instantiate<Bullet>();
        bullet.Initialize(direction, BulletSpeed, BulletLifeTime, BulletDamage, this);
        bullet.GlobalPosition = spawnPosition;
        bullet.GlobalRotation = direction.Angle();
        
        GetTree().Root.AddChild(bullet);
    }

    
    private void UpdateFacingDirection()
    {
        if (Math.Abs(_velocity.X) > 10f)
        {
            _isFacingRight = _velocity.X > 0;
            FlipSprite(!_isFacingRight);
        }
    }

    
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