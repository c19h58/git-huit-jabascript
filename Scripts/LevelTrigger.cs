using Godot;

public partial class LevelTrigger : Area2D
{
    [Export] public int TargetLevel = 3;
    [Export] public Vector2 TargetPosition = Vector2.Zero;
    
    private CameraManager _cameraManager;
    
    public override void _Ready()
    {
        Node2D parent = GetParent<Node2D>();
        
        if (parent != null)
        {
            _cameraManager = parent.GetNodeOrNull<CameraManager>("CameraManager");
        }
        
        if (_cameraManager == null)
        {
            return;
        }
        
        BodyEntered += OnBodyEntered;
    }
    
    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player)
        {
            _cameraManager.TeleportToLevel(TargetLevel, TargetPosition);
        }
    }
}