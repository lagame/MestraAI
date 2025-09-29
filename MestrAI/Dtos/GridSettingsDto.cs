namespace RPGSessionManager.Dtos;

public class GridSettingsDto
{
    public int GridSize { get; set; }
    public string? GridColor { get; set; }
    public bool ShowGrid { get; set; }
    public float GridUnitValue { get; set; } // Adicionado para corrigir CS1061
    public string? GridUnit { get; set; }     // Adicionado para corrigir uso em BattlemapService
}

