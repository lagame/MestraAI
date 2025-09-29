using RPGSessionManager.Rolling;
using RPGSessionManager.Data;
using Microsoft.EntityFrameworkCore;

namespace RPGSessionManager.Services
{
    /// <summary>
    /// Serviço responsável por processar rolagens de dados e integrar com o sistema de chat.
    /// </summary>
    public interface IRollService
    {
        Task<RollResult> ProcessRollAsync(string expression, string userName, int sessionId);
        Task<string> GetSessionRulesetAsync(int sessionId);
    }

    public class RollService : IRollService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RollService> _logger;
        private readonly RollEngine _rollEngine;

        public RollService(ApplicationDbContext context, ILogger<RollService> logger)
        {
            _context = context;
            _logger = logger;

            // Usar modo determinístico em desenvolvimento
            //var isDev = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            _rollEngine = new RollEngine(isDeterministic: false);
        }

        public async Task<RollResult> ProcessRollAsync(string expression, string userName, int sessionId)
        {
            try
            {
                var ruleset = await GetSessionRulesetAsync(sessionId);
                var result = _rollEngine.ProcessRoll(expression, userName, ruleset);

                // Log da rolagem
                _logger.LogInformation("Roll processed: User={User}, Session={SessionId}, Expression={Expression}, Result={Result}, Seed={Seed}",
                    userName, sessionId, expression, result.Total, result.Seed);

                if (result.IsError)
                {
                    _logger.LogWarning("Roll error: {ErrorCode} - {ErrorMessage}", result.ErrorCode, result.ErrorMessage);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing roll: {Expression} for user {User} in session {SessionId}", 
                    expression, userName, sessionId);
                
                return new RollResult
                {
                    User = userName,
                    Expression = expression,
                    Ruleset = "5e",
                    Seed = Guid.NewGuid(),
                    Blocks = new List<DiceBlock>(),
                    Total = 0,
                    IsError = true,
                    ErrorCode = "INTERNAL_ERROR",
                    ErrorMessage = "Erro interno do sistema de rolagens"
                };
            }
        }

        public async Task<string> GetSessionRulesetAsync(int sessionId)
        {
            try
            {
                // Por enquanto, retornar 5e como padrão
                // TODO: Implementar configuração de ruleset por sessão no banco
                var session = await _context.Sessions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == sessionId);

                // Verificar se há configuração específica nos metadados da sessão
                // TODO: Implementar campo Metadata na entidade Session se necessário
                // Por enquanto, usar padrão 5e

                return "5e"; // Padrão
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ruleset for session {SessionId}", sessionId);
                return "5e"; // Fallback
            }
        }
    }
}

