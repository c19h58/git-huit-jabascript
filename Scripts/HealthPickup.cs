using Godot;

public partial class HealthPickup : Area2D
{
    [Export] public int HealAmount = 10;
    [Export] public float LevitationSpeed = 2.0f;
    [Export] public float LevitationHeight = 5.0f;
    
    private bool _isCollected = false;
    private float _startY;
    private Vector2 _startPosition;
    private Sprite2D _sprite;

    public override void _Ready()
    {
        AddToGroup("pickups");
        
        // Запоминаем начальную позицию
        _startPosition = Position;
        _startY = _startPosition.Y;
        
        // Получаем спрайт (если есть)
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        
        // Подключаем сигнал входа в зону
        BodyEntered += OnBodyEntered;
        
        GD.Print($"HealthPickup ready at position: {GlobalPosition}");
    }
    
    public override void _Process(double delta)
    {
        if (_isCollected) return;
        
        // Левитация: движение вверх-вниз
        float offsetY = Mathf.Sin(Time.GetTicksMsec() / 1000.0f * LevitationSpeed) * LevitationHeight;
        Position = new Vector2(_startPosition.X, _startY + offsetY);
    }
    
    private void OnBodyEntered(Node2D body)
    {
        if (_isCollected) return;
        
        GD.Print($"HealthPickup: Body entered - {body.Name}");
        
        // Проверяем, что вошедший объект - игрок (по группе или по типу)
        if (body.IsInGroup("player") || body is Player)
        {
            GD.Print("HealthPickup: Player detected!");
            
            // Пытаемся получить компонент Player
            Player player = body as Player;
            if (player == null && body.HasMethod("GetPlayer"))
            {
                // Альтернативный способ получения ссылки на игрока
                player = body.GetNodeOrNull<Player>(".");
            }
            
            if (player != null)
            {
                bool healed = player.Heal(HealAmount);
                if (healed)
                {
                    _isCollected = true;
                    GD.Print($"HealthPickup: Player healed! +{HealAmount} HP");
                    QueueFree();
                }
                else
                {
                    GD.Print("HealthPickup: Player already at max health!");
                }
            }
            else
            {
                GD.Print("HealthPickup: Could not get Player reference!");
            }
        }
    }
}