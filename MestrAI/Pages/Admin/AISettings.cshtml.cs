using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using RPGSessionManager.Services;
using System.ComponentModel.DataAnnotations;

namespace RPGSessionManager.Pages.Admin;

[Authorize(Roles = "Admin")]
public class AISettingsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AISettingsModel> _logger;

    public AISettingsModel(ApplicationDbContext context, ILogger<AISettingsModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    [BindProperty]
    public AiSettingsViewModel Settings { get; set; } = new();

    public string? StatusMessage { get; set; }
    public bool IsTestingConnection { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var aiSettings = await _context.AiSettings
            .Where(s => s.IsActive)
            .FirstOrDefaultAsync();

        if (aiSettings != null)
        {
            Settings = new AiSettingsViewModel
            {
                Provider = aiSettings.Provider,
                GeminiProject = aiSettings.GeminiProject ?? "",
                GeminiLocation = aiSettings.GeminiLocation ?? "",
                GeminiModel = aiSettings.GeminiModel ?? "",
                LocalEndpoint = aiSettings.LocalEndpoint ?? "",
                LocalApiKey = aiSettings.LocalApiKey ?? ""
            };
        }
        else
        {
            // Valores padrão
            Settings = new AiSettingsViewModel
            {
                Provider = "Gemini",
                GeminiProject = "",
                GeminiLocation = "southamerica-east1",
                GeminiModel = "gemini-2.5-flash-lite",
                LocalEndpoint = "http://localhost:8080",
                LocalApiKey = ""
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!ModelState.IsValid)
        {
            StatusMessage = "Por favor, corrija os erros no formulário.";
            return Page();
        }

        try
        {
            // Desativar configurações existentes
            var existingSettings = await _context.AiSettings
                .Where(s => s.IsActive)
                .ToListAsync();

            foreach (var setting in existingSettings)
            {
                setting.IsActive = false;
                setting.UpdatedAt = DateTime.UtcNow;
            }

            // Criar nova configuração
            var newSettings = new AiSettings
            {
                Provider = Settings.Provider,
                GeminiProject = Settings.Provider == "Gemini" ? Settings.GeminiProject : null,
                GeminiLocation = Settings.Provider == "Gemini" ? Settings.GeminiLocation : null,
                GeminiModel = Settings.Provider == "Gemini" ? Settings.GeminiModel : null,
                LocalEndpoint = Settings.Provider == "Local" ? Settings.LocalEndpoint : null,
                LocalApiKey = Settings.Provider == "Local" ? Settings.LocalApiKey : null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.AiSettings.Add(newSettings);
            await _context.SaveChangesAsync();

            StatusMessage = "Configurações salvas com sucesso!";
            _logger.LogInformation("Configurações de IA atualizadas para provider: {Provider}", Settings.Provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar configurações de IA");
            StatusMessage = "Erro ao salvar configurações. Tente novamente.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostTestConnectionAsync()
    {
        if (!ModelState.IsValid)
        {
            StatusMessage = "Por favor, corrija os erros no formulário antes de testar.";
            return Page();
        }

        IsTestingConnection = true;

        try
        {
            bool connectionSuccess = false;
            string testMessage = "";

            if (Settings.Provider == "Gemini")
            {
                connectionSuccess = await TestGeminiConnectionAsync();
                testMessage = connectionSuccess ? 
                    "Conexão com Gemini estabelecida com sucesso!" : 
                    "Falha ao conectar com Gemini. Verifique as credenciais e configurações.";
            }
            else if (Settings.Provider == "Local")
            {
                connectionSuccess = await TestLocalConnectionAsync();
                testMessage = connectionSuccess ? 
                    "Conexão com LLM local estabelecida com sucesso!" : 
                    "Falha ao conectar com LLM local. Verifique se o serviço está rodando e o endpoint está correto.";
            }

            StatusMessage = testMessage;
            _logger.LogInformation("Teste de conexão {Provider}: {Success}", Settings.Provider, connectionSuccess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante teste de conexão {Provider}", Settings.Provider);
            StatusMessage = $"Erro durante teste de conexão: {ex.Message}";
        }
        finally
        {
            IsTestingConnection = false;
        }

        return Page();
    }

    private async Task<bool> TestGeminiConnectionAsync()
    {
        try
        {
            // Verificar se o arquivo de credenciais existe
            var credentialsPath = Path.Combine(Directory.GetCurrentDirectory(), "secrets", "google-credentials.json");
            if (!System.IO.File.Exists(credentialsPath))
            {
                _logger.LogWarning("Arquivo de credenciais do Google não encontrado: {Path}", credentialsPath);
                return false;
            }

            // Criar um provider temporário para teste
            using var httpClient = new HttpClient();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AiSettings:GeminiProject"] = Settings.GeminiProject,
                    ["AiSettings:GeminiLocation"] = Settings.GeminiLocation,
                    ["AiSettings:GeminiModel"] = Settings.GeminiModel
                })
                .Build();

            var provider = new GeminiProvider(httpClient, configuration, 
                new LoggerFactory().CreateLogger<GeminiProvider>());
            return await provider.TestConnectionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao testar conexão Gemini");
            return false;
        }
    }

    private async Task<bool> TestLocalConnectionAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AiSettings:LocalEndpoint"] = Settings.LocalEndpoint,
                    ["AiSettings:LocalApiKey"] = Settings.LocalApiKey
                })
                .Build();

            var provider = new LocalProvider(httpClient, configuration, 
                new LoggerFactory().CreateLogger<LocalProvider>());
            return await provider.TestConnectionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao testar conexão Local");
            return false;
        }
    }
}

public class AiSettingsViewModel
{
    [Required(ErrorMessage = "Selecione um provedor de IA")]
    public string Provider { get; set; } = "Gemini";

    [RequiredIf(nameof(Provider), "Gemini", ErrorMessage = "ID do projeto é obrigatório para Gemini")]
    [StringLength(200, ErrorMessage = "ID do projeto deve ter no máximo 200 caracteres")]
    public string GeminiProject { get; set; } = "";

    [RequiredIf(nameof(Provider), "Gemini", ErrorMessage = "Localização é obrigatória para Gemini")]
    [StringLength(100, ErrorMessage = "Localização deve ter no máximo 100 caracteres")]
    public string GeminiLocation { get; set; } = "";

    [RequiredIf(nameof(Provider), "Gemini", ErrorMessage = "Modelo é obrigatório para Gemini")]
    [StringLength(100, ErrorMessage = "Modelo deve ter no máximo 100 caracteres")]
    public string GeminiModel { get; set; } = "";

    [RequiredIf(nameof(Provider), "Local", ErrorMessage = "Endpoint é obrigatório para LLM local")]
    [StringLength(500, ErrorMessage = "Endpoint deve ter no máximo 500 caracteres")]
    [Url(ErrorMessage = "Endpoint deve ser uma URL válida")]
    public string? LocalEndpoint { get; set; } = "";

    [StringLength(500, ErrorMessage = "Chave de API deve ter no máximo 500 caracteres")]
    public string? LocalApiKey { get; set; } = "";
}

public class RequiredIfAttribute : ValidationAttribute
{
    private readonly string _propertyName;
    private readonly object _value;

    public RequiredIfAttribute(string propertyName, object value)
    {
        _propertyName = propertyName;
        _value = value;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var property = validationContext.ObjectType.GetProperty(_propertyName);
        if (property == null)
            return new ValidationResult($"Propriedade {_propertyName} não encontrada");

        var propertyValue = property.GetValue(validationContext.ObjectInstance);
        
        if (Equals(propertyValue, _value))
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return new ValidationResult(ErrorMessage ?? $"{validationContext.DisplayName} é obrigatório");
            }
        }

        return ValidationResult.Success;
    }
}

