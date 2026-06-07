using Godot;

public partial class Bullet : Area2D
{
    [Export] public float Speed = 600.0f;
    [Export] public float LifeTime = 2.0f;
    [Export] public int Damage = 25;
    [Export] public Node Source = null;

    private Vector2 _velocity;
    private float _timeAlive = 0.0f;
    private bool _initialized = false;

    private AnimatedSprite2D _animatedSprite;
    private Sprite2D _sprite;

    public void Initialize(Vector2 direction, float speed, float lifeTime, int damage, Node source)
    {
        _velocity = direction.Normalized() * speed;
        LifeTime = lifeTime;
        Damage = damage;
        Source = source;
        _initialized = true;
        
        AddToGroup("projectiles");

        UpdateSpriteDirection(direction);
    }

    public override void _Ready()
    {
        SetupCollisionShape();
        BodyEntered += OnBodyEntered;

        _animatedSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");

        if (_animatedSprite != null && _animatedSprite.SpriteFrames != null)
        {
            _animatedSprite.Play("default");
        }
    }

    private void UpdateSpriteDirection(Vector2 direction)
    {
        if (_sprite != null)
        {
            _sprite.FlipH = direction.X < 0;
        }
        
        if (_animatedSprite != null)
        {
            _animatedSprite.FlipH = direction.X < 0;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_initialized)
            return;

        Position += _velocity * (float)delta;
        _timeAlive += (float)delta;

        if (_timeAlive >= LifeTime)
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
        if (body == Source)
            return;
        if (body.IsInGroup("walls") || body.IsInGroup("floor") || body.IsInGroup("environment"))
        {
            return;
        }

        if (Source is Enemy && body is Player player)
        {
            player.TakeDamage(Damage);
            QueueFree();
        }
        else if (Source is Player && body.IsInGroup("enemies"))
        {
            if (body.HasMethod("TakeDamage"))
            {
                body.Call("TakeDamage", Damage);
            }
            QueueFree();
        }
        else
        {
            QueueFree();
        }
    }
}