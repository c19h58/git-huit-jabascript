using Godot;

public partial class Ladder : Area2D
{
    public override void _Ready()
    {
        AddToGroup("ladder");
    }
}