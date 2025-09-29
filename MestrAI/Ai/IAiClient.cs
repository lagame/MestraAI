namespace RPGSessionManager.Ai;

public interface IAiClient
{
    Task<string> GenerateResponseAsync(string prompt);
    Task<string> GenerateResponseAsync(string systemPrompt, string userPrompt);
    string GenerateResponse(string prompt);
    string GenerateResponse(string systemPrompt, string userPrompt);
}

