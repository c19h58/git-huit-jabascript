using Godot;

public partial class VictoryZone : Area2D
{
    [Export] public Texture2D VictoryScreenTexture;
    [Export] public bool IsActive = false;
    
    private bool _victoryTriggered = false;
    private Sprite2D _sprite;
    private Timer _enemyCheckTimer;
    
    private readonly Color InactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
    private readonly Color ActiveColor = new Color(1.0f, 1.0f, 1.0f, 0.4f);
    
    public override void _Ready()
    {
        _sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        
        if (!IsActive)
        {
            DisableZone();
        }

        _enemyCheckTimer = new Timer();
        _enemyCheckTimer.WaitTime = 0.5;
        _enemyCheckTimer.Timeout += CheckEnemiesCount;
        _enemyCheckTimer.Autostart = true;
        AddChild(_enemyCheckTimer);
        
        BodyEntered += OnBodyEntered;
    }
    
    private void CheckEnemiesCount()
    {
        if (IsActive) return;
        
        var enemies = GetTree().GetNodesInGroup("enemies");
        
        int aliveCount = 0;
        foreach (Node enemy in enemies)
        {
            if (IsInstanceValid(enemy) && !enemy.IsQueuedForDeletion())
            {
                aliveCount++;
            }
        }

        if (aliveCount == 0)
        {
            ActivateZone();
        }
    }
    
    public void ActivateZone()
    {
        if (IsActive) return;
        
        IsActive = true;
        
        var collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (collisionShape != null)
        {
            collisionShape.Disabled = false;
        }

        if (_sprite != null)
        {
            _sprite.Modulate = ActiveColor;
        }

        if (_enemyCheckTimer != null)
        {
            _enemyCheckTimer.Stop();
        }
    }
    
    private void DisableZone()
    {
        var collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (collisionShape != null)
        {
            collisionShape.Disabled = true;
        }
        
        if (_sprite != null)
        {
            _sprite.Modulate = InactiveColor;
        }
    }
    
    private void OnBodyEntered(Node2D body)
    {
        if (_victoryTriggered) return;
        
        if (body is Player player && IsActive)
        {
            _victoryTriggered = true;
            ShowVictoryScreen();
        }
    }
    
    private void ShowVictoryScreen()
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null)
        {
            player.SetCanMove(false);
        }
        
        var victoryScreen = new TextureRect();
        victoryScreen.Texture = VictoryScreenTexture;
        victoryScreen.Size = GetViewport().GetVisibleRect().Size;
        victoryScreen.Modulate = new Color(1, 1, 1, 0);
        victoryScreen.MouseFilter = Control.MouseFilterEnum.Ignore;
        
        var canvasLayer = new CanvasLayer();
        canvasLayer.Layer = 100;
        canvasLayer.AddChild(victoryScreen);
        GetTree().Root.AddChild(canvasLayer);
        
        var tween = CreateTween();
        tween.TweenProperty(victoryScreen, "modulate:a", 1.0f, 1.0f);
        
        GetTree().CreateTimer(5.0f).Timeout += () =>
        {
            GetTree().Quit();
        };
    }
}