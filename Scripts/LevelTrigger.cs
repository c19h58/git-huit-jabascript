using Godot;

public partial class LevelTrigger : Area2D
{
    [Export] public int TargetLevel = 3;
    [Export] public Vector2 TargetPosition = Vector2.Zero;
    
    private CameraManager _cameraManager;
    
    public override void _Ready()
    {
        // CameraManager находится в Main (родительский узел)
        Node2D parent = GetParent<Node2D>();
        
        if (parent != null)
        {
            _cameraManager = parent.GetNodeOrNull<CameraManager>("CameraManager");
        }
        
        if (_cameraManager == null)
        {
            GD.PrintErr($"LevelTrigger '{Name}': CameraManager not found!");
            return;
        }
        
        BodyEntered += OnBodyEntered;
        
        GD.Print($"LevelTrigger '{Name}' ready at position: {GlobalPosition}");
        GD.Print($"  -> Teleports to Level {TargetLevel} at position {TargetPosition}");
    }
    
    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player)
        {
            GD.Print($"========================================");
            GD.Print($"Player entered trigger '{Name}' at position: {GlobalPosition}");
            GD.Print($"Teleporting to Level {TargetLevel} at position {TargetPosition}");
            GD.Print($"========================================");
            
            _cameraManager.TeleportToLevel(TargetLevel, TargetPosition);
        }
    }
}