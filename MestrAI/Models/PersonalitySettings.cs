namespace RPGSessionManager.Models;

public class PersonalitySettings
{
    public int Aggressiveness { get; set; } = 50; // 0-100
    public int Friendliness { get; set; } = 50;   // 0-100
    public int Curiosity { get; set; } = 50;      // 0-100
    public int Caution { get; set; } = 50;        // 0-100
    public int Humor { get; set; } = 50;          // 0-100
    public int Formality { get; set; } = 50;      // 0-100
    public int Empathy { get; set; } = 50;        // 0-100
    
    // Traços customizados específicos do personagem
    public Dictionary<string, int> CustomTraits { get; set; } = new();
    
    // Preferências de resposta
    public List<string> PreferredTopics { get; set; } = new();
    public List<string> AvoidedTopics { get; set; } = new();
    
    // Configurações de comportamento
    public bool RespondsToQuestions { get; set; } = true;
    public bool InitiatesConversation { get; set; } = false;
    public bool ReactsToEmotions { get; set; } = true;
    
    // Método para obter configuração como JSON
    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(this);
    }
    
    // Método para criar a partir de JSON
    public static PersonalitySettings FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return new PersonalitySettings();
            
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<PersonalitySettings>(json) ?? new PersonalitySettings();
        }
        catch
        {
            return new PersonalitySettings();
        }
    }
}

