using NJsonSchema;
using Newtonsoft.Json.Linq;

namespace RPGSessionManager.Services;

public class JsonSchemaValidationService
{
    private readonly ILogger<JsonSchemaValidationService> _logger;

    public JsonSchemaValidationService(ILogger<JsonSchemaValidationService> logger)
    {
        _logger = logger;
    }

    public async Task<(bool IsValid, List<string> Errors)> ValidateJsonAsync(string jsonData, string? jsonSchema)
    {
        if (string.IsNullOrEmpty(jsonSchema))
        {
            return (true, new List<string>());
        }

        try
        {
            var schema = await JsonSchema.FromJsonAsync(jsonSchema);
            var jsonObject = JToken.Parse(jsonData);
            
            var errors = schema.Validate(jsonObject);
            
            if (errors.Any())
            {
                var errorMessages = errors.Select(e => $"{e.Path}: {e.Kind} - {e.Property}").ToList();
                _logger.LogWarning("JSON validation failed: {Errors}", string.Join(", ", errorMessages));
                return (false, errorMessages);
            }

            return (true, new List<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating JSON against schema");
            return (false, new List<string> { "Invalid JSON format or schema" });
        }
    }
}

