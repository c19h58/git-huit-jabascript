using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
    [Export] public float PatrolSpeed = 80.0f;
    [Export] public float ChaseSpeed = 140.0f; // Скорость при погоне выше
    [Export] public float Gravity = 980.0f;

    private RayCast2D _floorDetector;
    private Sprite2D _sprite;
    private Area2D _detectionArea;

    private int _direction = 1;
    
    // Ссылка на игрока, когда он в зоне видимости
    private Player _targetPlayer = null; 

    public override void _Ready()
    {
        _floorDetector = GetNode<RayCast2D>("RayCast2D");
        _sprite = GetNode<Sprite2D>("Sprite2D");
        
        // Находим зону обнаружения и подписываемся на ее события (сигналы)
        _detectionArea = GetNode<Area2D>("DetectionArea");
        _detectionArea.BodyEntered += OnPlayerEntered;
        _detectionArea.BodyExited += OnPlayerExited;
        
        GD.Print($"Enemy ready. DetectionArea collision layers: {_detectionArea.CollisionLayer}, mask: {_detectionArea.CollisionMask}");
    }
    
    public override void _ExitTree()
    {
        // Отписываемся от сигналов при удалении узла
        if (_detectionArea != null)
        {
            _detectionArea.BodyEntered -= OnPlayerEntered;
            _detectionArea.BodyExited -= OnPlayerExited;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 velocity = Velocity;

        // Гравитация
        if (!IsOnFloor())
        {
            velocity.Y += Gravity * (float)delta;
        }

        if (_targetPlayer != null && !IsInstanceValid(_targetPlayer))
        {
            _targetPlayer = null;
        }

        // Логика движения зависит от того, видим ли мы игрока
        if (_targetPlayer != null)
        {
            // --- РЕЖИМ ПРЕСЛЕДОВАНИЯ ---
            // Вычисляем направление к игроку по горизонтали (X)
            float directionToPlayer = Mathf.Sign(_targetPlayer.GlobalPosition.X - GlobalPosition.X);
            
            velocity.X = directionToPlayer * ChaseSpeed;

            // Разворачиваем спрайт в сторону игрока
            _sprite.FlipH = directionToPlayer == -1;
        }
        else
        {
            // --- РЕЖИМ ПАТРУЛИРОВАНИЯ ---
            if (!_floorDetector.IsColliding() || IsOnWall())
            {
                DirectionFlip();
            }
            velocity.X = _direction * PatrolSpeed;
        }

        Velocity = velocity;
        MoveAndSlide();
    }

    private void DirectionFlip()
    {
        _direction *= -1;
        _sprite.FlipH = _direction == -1;

        Vector2 rayPosition = _floorDetector.Position;
        rayPosition.X = Mathf.Abs(rayPosition.X) * _direction;
        _floorDetector.Position = rayPosition;
    }

    // Триггер: КТО-ТО зашел в зону видимости (благодаря маске — это только игрок)
    private void OnPlayerEntered(Node body)
    {
        if (body is Player player && _targetPlayer == null)
        {
            _targetPlayer = player;
            GD.Print("Игрок замечен! Начинаю погоню.");
        }
    }

    // Триггер: Игрок убежал из зоны видимости
    private void OnPlayerExited(Node body)
    {
        if (body is Player player && player == _targetPlayer)
        {
            _targetPlayer = null;
            GD.Print("Игрок скрылся. Возвращаюсь к патрулированию.");
            
            // Сбрасываем направление патруля в зависимости от текущего поворота спрайта
            _direction = _sprite.FlipH ? -1 : 1;
        }
    }
}