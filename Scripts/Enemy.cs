using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
    [Export] public float PatrolSpeed = 80.0f;
    [Export] public float ChaseSpeed = 140.0f; // Скорость при погоне выше
    [Export] public float Gravity = 980.0f;
    [Export] public float MaxChaseDistance = 400.0f; // Максимальное расстояние для преследования

    private RayCast2D _floorDetector;
    private Sprite2D _sprite;
    private Area2D _detectionArea;

    private int _direction = 1;
    private Vector2 _startPosition; // Исходная позиция патруля
    private bool _isReturningToStart = false; // Возвращается ли враг в исходную точку
    
    // Ссылка на игрока, когда он в зоне видимости
    private Player _targetPlayer = null; 

    public override void _Ready()
    {
        _floorDetector = GetNode<RayCast2D>("RayCast2D");
        _sprite = GetNode<Sprite2D>("Sprite2D");
        
        // Сохраняем исходную позицию для возврата
        _startPosition = GlobalPosition;
        
        // Находим зону обнаружения и подписываемся на ее события (сигналы)
        _detectionArea = GetNode<Area2D>("DetectionArea");
        _detectionArea.BodyEntered += OnPlayerEntered;
        _detectionArea.BodyExited += OnPlayerExited;
        
        // Пытаемся найти игрока через граф сцены
        TryFindPlayer();
        
        GD.Print($"Enemy ready. DetectionArea collision layers: {_detectionArea.CollisionLayer}, mask: {_detectionArea.CollisionMask}");
        GD.Print($"Стартовая позиция врага: {_startPosition}");
    }
    
    // Найти игрока в сцене несколькими способами
    private void TryFindPlayer()
    {
        // Способ 1: Ищем через GetParent (если враг и игрок в одном родителе)
        if (GetParent() is Node2D parent)
        {
            _targetPlayer = parent.GetNode<Player>("Player");
            if (_targetPlayer != null)
            {
                GD.Print($"Игрок найден через родителя: {_targetPlayer.Name}");
                return;
            }
        }
        
        // Способ 2: Ищем через корневую сцену
        try
        {
            _targetPlayer = GetTree().Root.GetNode<Player>("Main/Player");
            if (_targetPlayer != null)
            {
                GD.Print($"Игрок найден через Main/Player: {_targetPlayer.Name}");
                return;
            }
        }
        catch
        {
            GD.PrintErr("Не удалось найти игрока по пути Main/Player");
        }
        
        // Способ 3: Поиск по типу через все узлы сцены
        _targetPlayer = FindPlayerRecursive(GetTree().Root);
        if (_targetPlayer != null)
        {
            GD.Print($"Игрок найден рекурсивным поиском: {_targetPlayer.Name}");
        }
        else
        {
            GD.PrintErr("Не удалось найти игрока в сцене!");
        }
    }
    
    // Рекурсивный поиск игрока в дереве узлов
    private Player FindPlayerRecursive(Node node)
    {
        if (node is Player player)
        {
            return player;
        }
        
        foreach (Node child in node.GetChildren())
        {
            Player found = FindPlayerRecursive(child);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
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

        // Проверяем валидность ссылки на игрока
        if (_targetPlayer != null && !IsInstanceValid(_targetPlayer))
        {
            _targetPlayer = null;
        }
        
        // Если нет цели и она была, пытаемся найти заново
        if (_targetPlayer == null)
        {
            TryFindPlayer();
        }

        // Логика движения
        if (_targetPlayer != null && IsInstanceValid(_targetPlayer))
        {
            // Проверяем расстояние до игрока
            float distanceToPlayer = GlobalPosition.DistanceTo(_targetPlayer.GlobalPosition);
            
            if (distanceToPlayer <= MaxChaseDistance)
            {
                // --- РЕЖИМ ПРЕСЛЕДОВАНИЯ ---
                _isReturningToStart = false;
                
                // Вычисляем направление к игроку по горизонтали (X)
                float directionToPlayer = Mathf.Sign(_targetPlayer.GlobalPosition.X - GlobalPosition.X);
                
                velocity.X = directionToPlayer * ChaseSpeed;

                // Разворачиваем спрайт в сторону игрока
                _sprite.FlipH = directionToPlayer == -1;
            }
            else
            {
                // Игрок слишком далеко - возвращаемся в исходную точку
                _isReturningToStart = true;
                _targetPlayer = null;
            }
        }
        
        if (_isReturningToStart)
        {
            // --- РЕЖИМ ВОЗВРАТА К СТАРТОВОЙ ПОЗИЦИИ ---
            float distanceToStart = GlobalPosition.DistanceTo(_startPosition);
            
            if (distanceToStart > 5.0f) // Если еще не дошли до начальной позиции
            {
                float directionToStart = Mathf.Sign(_startPosition.X - GlobalPosition.X);
                velocity.X = directionToStart * PatrolSpeed;
                _sprite.FlipH = directionToStart == -1;
            }
            else
            {
                // Дошли до стартовой позиции
                velocity.X = 0;
                _isReturningToStart = false;
                GD.Print("Враг вернулся на стартовую позицию");
            }
        }
        else if (_targetPlayer == null)
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
        if (body is Player player)
        {
            // Проверяем, это ли наш целевой игрок (или его нет)
            if (_targetPlayer != player)
            {
                _targetPlayer = player;
                GD.Print("Игрок замечен! Начинаю погоню.");
            }
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