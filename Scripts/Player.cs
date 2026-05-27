using Godot;

public partial class Player : CharacterBody2D
{
    [Export] public float Speed = 300.0f;
    [Export] public float Gravity = 980.0f;
    [Export] public float JumpVelocity = -400.0f;
    [Export] public bool CanMove = true;
    
    private Vector2 _velocity;
    private Sprite2D _sprite;
    private bool _isFacingRight = true;
    
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
        
        // Направление спрайта
        if (_sprite != null && inputDirection != 0)
        {
            if (inputDirection > 0 && !_isFacingRight)
            {
                _isFacingRight = true;
                _sprite.Scale = new Vector2(0.43f, 0.555f);
            }
            else if (inputDirection < 0 && _isFacingRight)
            {
                _isFacingRight = false;
                _sprite.Scale = new Vector2(-0.43f, 0.555f);
            }
        }
        
        Velocity = _velocity;
        MoveAndSlide();
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
}