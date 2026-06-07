using Godot;

public partial class PlayerUI : CanvasLayer
{
    [Signal]
    public delegate void HealthChangedEventHandler(int newHealth, int maxHealth);
    
    private ProgressBar _healthBar;
    private Label _healthLabel;
    private int _currentHealth;
    private int _maxHealth;

    public override void _Ready()
    {
        _healthBar = GetNode<ProgressBar>("UIContainer/HealthBar");
        _healthLabel = GetNode<Label>("UIContainer/HealthLabel");
        
        _maxHealth = 100;
        _currentHealth = _maxHealth;
        UpdateUI();
    }
    
    public void UpdateHealth(int newHealth, int maxHealth)
    {
        _currentHealth = newHealth;
        _maxHealth = maxHealth;
        UpdateUI();

        EmitSignal(SignalName.HealthChanged, _currentHealth, _maxHealth);
    }
    
    private void UpdateUI()
    {
        if (_healthBar != null)
        {
            _healthBar.MaxValue = _maxHealth;
            _healthBar.Value = _currentHealth;
        }
        
        if (_healthLabel != null)
        {
            _healthLabel.Text = $"{_currentHealth}/{_maxHealth}";
        }
    }
}