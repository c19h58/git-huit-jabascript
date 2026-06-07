using Godot;

public partial class DeathZone : Area2D
{
    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }
    
    private void OnBodyEntered(Node2D body)
    {
        if (body is Player player)
        {
            player.TakeDamage(player.MaxHealth);
        }
    }
}