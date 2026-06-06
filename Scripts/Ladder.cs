using Godot;

public partial class Ladder : Area2D
{
    public override void _Ready()
    {
        // Добавляем в группу для поиска
        AddToGroup("ladder");
    }
}