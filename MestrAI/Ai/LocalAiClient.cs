using Microsoft.Extensions.Options;
using RPGSessionManager.Services;
using System.Text.Json;

namespace RPGSessionManager.Ai;

/// <summary>
/// Cliente AI que usa LLM local via Context Service
/// </summary>
public class LocalAiClient : IAiClient
{
    private readonly IContextService _contextService;
    private readonly AiConfig _config;
    private readonly ILogger<LocalAiClient> _logger;

    public LocalAiClient(
        IContextService contextService,
        IOptions<AiConfig> config,
        ILogger<LocalAiClient> logger)
    {
        _contextService = contextService;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            // Verifica se o Context Service está disponível
            var isAvailable = await _contextService.IsAvailableAsync(cancellationToken);
            if (!isAvailable)
            {
                _logger.LogWarning("Context Service não está disponível. Retornando resposta padrão.");
                return GenerateFallbackResponse(prompt);
            }

            // Para agora, vamos usar uma resposta simples baseada no prompt
            // Em uma implementação completa, isso seria enviado para o LLM local
            var response = await GenerateLocalLlmResponse(prompt, cancellationToken);
            
            _logger.LogDebug("Resposta gerada com sucesso para prompt de {Length} caracteres", prompt.Length);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar resposta com LLM local");
            return GenerateFallbackResponse(prompt);
        }
    }

    public async Task<string> GenerateResponseAsync(string prompt)
    {
        return await GenerateResponseAsync(prompt, CancellationToken.None);
    }

    public async Task<string> GenerateResponseAsync(string systemPrompt, string userPrompt)
    {
        var combinedPrompt = $"Sistema: {systemPrompt}\n\nUsuário: {userPrompt}";
        return await GenerateResponseAsync(combinedPrompt, CancellationToken.None);
    }

    public string GenerateResponse(string prompt)
    {
        return GenerateResponseAsync(prompt).GetAwaiter().GetResult();
    }

    public string GenerateResponse(string systemPrompt, string userPrompt)
    {
        return GenerateResponseAsync(systemPrompt, userPrompt).GetAwaiter().GetResult();
    }

    public async Task<string> GenerateResponseWithContextAsync(
        string prompt, 
        int gameTabletopId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Busca contexto relevante da memória da Mesa
            var relevantMemories = await _contextService.SearchConversationsAsync(
                gameTabletopId, 
                prompt, 
                limit: 5, 
                cancellationToken);

            // Constrói prompt com contexto
            var contextualPrompt = BuildContextualPrompt(prompt, relevantMemories);
            
            // Gera resposta com contexto
            var response = await GenerateResponseAsync(contextualPrompt, cancellationToken);
            
            _logger.LogDebug("Resposta contextual gerada para Mesa {GameTabletopId} com {MemoryCount} memórias", 
                gameTabletopId, relevantMemories.Count);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar resposta contextual para Mesa {GameTabletopId}", gameTabletopId);
            return await GenerateResponseAsync(prompt, cancellationToken);
        }
    }

    private async Task<string> GenerateLocalLlmResponse(string prompt, CancellationToken cancellationToken)
    {
        // TODO: Implementar chamada real para LLM local via HTTP
        // Por enquanto, simula uma resposta baseada no prompt
        
        await Task.Delay(500, cancellationToken); // Simula latência do LLM
        
        // Análise simples do prompt para gerar resposta apropriada
        var lowerPrompt = prompt.ToLowerInvariant();
        
        if (lowerPrompt.Contains("narrador") || lowerPrompt.Contains("narrator"))
        {
            return "Como Narrador, vou conduzir esta história com cuidado. " +
                   "Baseando-me no contexto da nossa aventura, posso dizer que...";
        }
        
        if (lowerPrompt.Contains("personagem") || lowerPrompt.Contains("character"))
        {
            return "Interpretando meu personagem, acredito que a melhor ação seria...";
        }
        
        if (lowerPrompt.Contains("combate") || lowerPrompt.Contains("battle"))
        {
            return "Em situação de combate, é importante considerar as táticas disponíveis. " +
                   "Analisando a situação atual...";
        }
        
        if (lowerPrompt.Contains("história") || lowerPrompt.Contains("story"))
        {
            return "Continuando nossa narrativa, os eventos se desenrolam de forma interessante. " +
                   "Considerando o que aconteceu até agora...";
        }
        
        // Resposta genérica
        return "Entendo sua solicitação. Baseando-me no contexto da nossa sessão de RPG, " +
               "posso sugerir que consideremos as opções disponíveis e tomemos uma decisão " +
               "que faça sentido para a narrativa em desenvolvimento.";
    }

    private string BuildContextualPrompt(string originalPrompt, List<Models.ConversationMemory> memories)
    {
        if (!memories.Any())
        {
            return originalPrompt;
        }

        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("Contexto da sessão anterior:");
        contextBuilder.AppendLine();

        foreach (var memory in memories.OrderBy(m => m.CreatedAt))
        {
            contextBuilder.AppendLine($"[{memory.SpeakerType}] {memory.SpeakerName}: {memory.Content}");
            if (!string.IsNullOrEmpty(memory.Context))
            {
                contextBuilder.AppendLine($"  Contexto: {memory.Context}");
            }
            contextBuilder.AppendLine();
        }

        contextBuilder.AppendLine("---");
        contextBuilder.AppendLine("Prompt atual:");
        contextBuilder.AppendLine(originalPrompt);

        return contextBuilder.ToString();
    }

    private string GenerateFallbackResponse(string prompt)
    {
        _logger.LogInformation("Usando resposta de fallback para prompt");
        
        return "Desculpe, o sistema de IA local não está disponível no momento. " +
               "Por favor, tente novamente mais tarde ou continue a sessão manualmente.";
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return await _contextService.IsAvailableAsync(cancellationToken);
    }
}

