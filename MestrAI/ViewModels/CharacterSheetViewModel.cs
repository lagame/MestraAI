namespace RPGSessionManager.ViewModels;

public class CharacterSheetViewModel
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DataJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public string SessionName { get; set; } = string.Empty;
    public string OwnerUserName { get; set; } = string.Empty;
}

public class CharacterSheetNarratorViewModel : CharacterSheetViewModel
{
    public bool AiEnabled { get; set; } = false;
}

