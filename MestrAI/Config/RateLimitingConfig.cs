using AspNetCoreRateLimit;

namespace RPGSessionManager.Config;

public static class RateLimitingConfig
{
    public static void ConfigureRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        // Configurar rate limiting
        services.Configure<IpRateLimitOptions>(options =>
        {
            options.EnableEndpointRateLimiting = true;
            options.StackBlockedRequests = false;
            options.HttpStatusCode = 429;
            options.RealIpHeader = "X-Real-IP";
            options.ClientIdHeader = "X-ClientId";
            
            // Regras gerais
            options.GeneralRules = new List<RateLimitRule>
            {
                // Rate limit específico para endpoints de NPC
                new RateLimitRule
                {
                    Endpoint = "POST:/api/AiNpc/set-status",
                    Period = "1m",
                    Limit = 30 // Máximo 30 mudanças por minuto por IP
                },
                new RateLimitRule
                {
                    Endpoint = "GET:/api/AiNpc/session/*/npcs",
                    Period = "1m",
                    Limit = 60 // Máximo 60 consultas por minuto por IP
                },
                new RateLimitRule
                {
                    Endpoint = "GET:/api/AiNpc/session/*/metrics",
                    Period = "1m",
                    Limit = 20 // Máximo 20 consultas de métricas por minuto por IP
                }
            };
        });

        services.Configure<IpRateLimitPolicies>(options =>
        {
            options.IpRules = new List<IpRateLimitPolicy>();
        });

        // Adicionar serviços necessários
        services.AddMemoryCache();
        services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
        services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
        services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
    }
}

