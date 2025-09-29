using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;

namespace RPGSessionManager.Services;

/// <summary>
/// Factory para criar providers de IA baseado nas configurações do banco
/// </summary>
public class AiProviderFactory
{
    private readonly ApplicationDbContext _context;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiProviderFactory> _logger;

    public AiProviderFactory(
        ApplicationDbContext context,
        IServiceProvider serviceProvider,
        ILogger<AiProviderFactory> logger)
    {
        _context = context;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<IAiProvider> CreateProviderAsync()
    {
        try
        {
            var settings = await _context.AiSettings
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            if (settings == null)
            {
                _logger.LogWarning("Nenhuma configuração de IA encontrada, usando Gemini como padrão");
                return CreateGeminiProvider();
            }

            return settings.Provider.ToLower() switch
            {
                "gemini" => CreateGeminiProvider(settings),
                "local" => CreateLocalProvider(settings),
                _ => CreateGeminiProvider(settings)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar provider de IA, usando Gemini como fallback");
            return CreateGeminiProvider();
        }
    }

    private IAiProvider CreateGeminiProvider(Models.AiSettings? settings = null)
    {
        var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
        var configuration = CreateConfiguration(settings, "Gemini");
        var logger = _serviceProvider.GetRequiredService<ILogger<GeminiProvider>>();
        
        return new GeminiProvider(httpClient, configuration, logger);
    }

    private IAiProvider CreateLocalProvider(Models.AiSettings settings)
    {
        var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
        var configuration = CreateConfiguration(settings, "Local");
        var logger = _serviceProvider.GetRequiredService<ILogger<LocalProvider>>();
        
        return new LocalProvider(httpClient, configuration, logger);
    }

    private IConfiguration CreateConfiguration(Models.AiSettings? settings, string providerType)
    {
        var configDict = new Dictionary<string, string?>();

        if (settings != null)
        {
            if (providerType == "Gemini")
            {
                configDict["AiSettings:GeminiProject"] = settings.GeminiProject ?? "your-project-id";
                configDict["AiSettings:GeminiLocation"] = settings.GeminiLocation ?? "southamerica-east1";
                configDict["AiSettings:GeminiModel"] = settings.GeminiModel ?? "gemini-2.5-flash-lite";
            }
            else if (providerType == "Local")
            {
                configDict["AiSettings:LocalEndpoint"] = settings.LocalEndpoint ?? "http://localhost:8080";
                configDict["AiSettings:LocalApiKey"] = settings.LocalApiKey;
            }
        }
        else
        {
            // Valores padrão para Gemini
            configDict["AiSettings:GeminiProject"] = "your-project-id";
            configDict["AiSettings:GeminiLocation"] = "southamerica-east1";
            configDict["AiSettings:GeminiModel"] = "gemini-2.5-flash-lite";
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();
    }
}

