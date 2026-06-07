using Godot;

public partial class HealthPickup : Area2D
{
    [Export] public int HealAmount = 10;
    [Export] public float LevitationSpeed = 3.0f;
    [Export] public float LevitationHeight = 30.0f;
    
    private bool _isCollected = false;
    private float _startY;
    private Vector2 _startPosition;
    private Sprite2D _sprite;

    public override void _Ready()
    {
        AddToGroup("pickups");
        
        _startPosition = Position;
        _startY = _startPosition.Y;
        
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        
        BodyEntered += OnBodyEntered;
    }
    
    public override void _Process(double delta)
    {
        if (_isCollected) return;
        
        float offsetY = Mathf.Sin(Time.GetTicksMsec() / 1000.0f * LevitationSpeed) * LevitationHeight;
        Position = new Vector2(_startPosition.X, _startY + offsetY);
    }
    
    private void OnBodyEntered(Node2D body)
    {
        if (_isCollected) return;
        
        if (body.IsInGroup("player") || body is Player)
        {
            Player player = body as Player;
            if (player == null && body.HasMethod("GetPlayer"))
            {
                player = body.GetNodeOrNull<Player>(".");
            }
            
            if (player != null)
            {
                if (player.Heal(HealAmount))
                {
                    _isCollected = true;
                    QueueFree();
                }
            }
        }
    }
}