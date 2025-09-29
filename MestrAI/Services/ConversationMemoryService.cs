using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RPGSessionManager.Services;

/// <summary>
/// Serviço para gerenciar memória de conversas de personagens AI
/// </summary>
public interface IConversationMemoryService
{
    /// <summary>
    /// Salva uma conversa na memória local e no Context Service
    /// </summary>
    Task<ConversationMemory?> SaveConversationAsync(
        int gameTabletopId,
        int? sessionId,
        string speakerName,
        string speakerType,
        string content,
        string? context = null,
        int importance = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca conversas relevantes para um contexto
    /// </summary>
    Task<List<ConversationMemory>> GetRelevantConversationsAsync(
        int gameTabletopId,
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém histórico de conversas de uma Mesa
    /// </summary>
    Task<List<ConversationMemory>> GetConversationHistoryAsync(
        int gameTabletopId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Limpa memória de uma Mesa
    /// </summary>
    Task<bool> ClearGameTabletopMemoryAsync(
        int gameTabletopId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém estatísticas de memória de uma Mesa
    /// </summary>
    Task<MemoryStats> GetMemoryStatsAsync(
        int gameTabletopId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Arquiva conversas antigas para otimizar performance
    /// </summary>
    Task<int> ArchiveOldConversationsAsync(
        int gameTabletopId,
        DateTime olderThan,
        CancellationToken cancellationToken = default);
}

public class ConversationMemoryService : IConversationMemoryService
{
    private readonly ApplicationDbContext _context;
    private readonly IContextService _contextService;
    private readonly ILogger<ConversationMemoryService> _logger;

    public ConversationMemoryService(
        ApplicationDbContext context,
        IContextService contextService,
        ILogger<ConversationMemoryService> logger)
    {
        _context = context;
        _contextService = contextService;
        _logger = logger;
    }

    public async Task<ConversationMemory?> SaveConversationAsync(
        int gameTabletopId,
        int? sessionId,
        string speakerName,
        string speakerType,
        string content,
        string? context = null,
        int importance = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Gera hash do conteúdo para evitar duplicatas
            var contentHash = GenerateContentHash(content);

            // Verifica se já existe uma conversa idêntica recente (últimas 24h)
            var recentDuplicate = await _context.ConversationMemories
                .Where(cm => cm.GameTabletopId == gameTabletopId &&
                           cm.ContentHash == contentHash &&
                           cm.CreatedAt > DateTime.UtcNow.AddDays(-1))
                .FirstOrDefaultAsync(cancellationToken);

            if (recentDuplicate != null)
            {
                _logger.LogDebug("Conversa duplicada detectada, ignorando salvamento");
                return recentDuplicate;
            }

            // Cria nova entrada de memória
            var memory = new ConversationMemory
            {
                GameTabletopId = gameTabletopId,
                SessionId = sessionId,
                SpeakerName = speakerName,
                SpeakerType = speakerType,
                Content = content,
                Context = context,
                Importance = Math.Clamp(importance, 1, 10),
                ContentHash = contentHash,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            // Salva no banco local
            _context.ConversationMemories.Add(memory);
            await _context.SaveChangesAsync(cancellationToken);

            // Tenta salvar no Context Service para busca semântica
            try
            {
                var documentId = await _contextService.AddConversationAsync(
                    gameTabletopId,
                    sessionId,
                    speakerName,
                    speakerType,
                    content,
                    context,
                    importance,
                    cancellationToken);

                if (!string.IsNullOrEmpty(documentId))
                {
                    // Salva o ID do documento nos metadados
                    memory.Metadata = JsonSerializer.Serialize(new { DocumentId = documentId });
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao salvar conversa no Context Service, mantendo apenas localmente");
            }

            _logger.LogDebug("Conversa salva: Mesa {GameTabletopId}, Falante {SpeakerName} ({SpeakerType})",
                gameTabletopId, speakerName, speakerType);

            return memory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar conversa na memória");
            return null;
        }
    }

    public async Task<List<ConversationMemory>> GetRelevantConversationsAsync(
        int gameTabletopId,
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Primeiro tenta busca semântica via Context Service
            var contextResults = await _contextService.SearchConversationsAsync(
                gameTabletopId,
                query,
                limit,
                cancellationToken);

            if (contextResults.Any())
            {
                _logger.LogDebug("Encontradas {Count} conversas relevantes via Context Service", contextResults.Count);
                return contextResults;
            }

            // Fallback: busca textual no banco local
            var localResults = await _context.ConversationMemories
                .Where(cm => cm.GameTabletopId == gameTabletopId && cm.IsActive)
                .Where(cm => EF.Functions.Like(cm.Content, $"%{query}%") ||
                           EF.Functions.Like(cm.SpeakerName, $"%{query}%") ||
                           (cm.Context != null && EF.Functions.Like(cm.Context, $"%{query}%")))
                .OrderByDescending(cm => cm.Importance)
                .ThenByDescending(cm => cm.CreatedAt)
                .Take(limit)
                .Include(cm => cm.GameTabletop)
                .Include(cm => cm.Session)
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Encontradas {Count} conversas relevantes via busca local", localResults.Count);
            return localResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar conversas relevantes");
            return new List<ConversationMemory>();
        }
    }

    public async Task<List<ConversationMemory>> GetConversationHistoryAsync(
        int gameTabletopId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.ConversationMemories
                .Where(cm => cm.GameTabletopId == gameTabletopId && cm.IsActive)
                .OrderByDescending(cm => cm.CreatedAt)
                .Skip(skip)
                .Take(take)
                .Include(cm => cm.GameTabletop)
                .Include(cm => cm.Session)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter histórico de conversas");
            return new List<ConversationMemory>();
        }
    }

    public async Task<bool> ClearGameTabletopMemoryAsync(
        int gameTabletopId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Limpa no Context Service
            await _contextService.ClearGameTabletopMemoryAsync(gameTabletopId, cancellationToken);

            // Marca conversas como inativas no banco local (soft delete)
            var conversations = await _context.ConversationMemories
                .Where(cm => cm.GameTabletopId == gameTabletopId && cm.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var conversation in conversations)
            {
                conversation.IsActive = false;
                conversation.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Memória da Mesa {GameTabletopId} limpa ({Count} conversas arquivadas)",
                gameTabletopId, conversations.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao limpar memória da Mesa {GameTabletopId}", gameTabletopId);
            return false;
        }
    }

    public async Task<MemoryStats> GetMemoryStatsAsync(
        int gameTabletopId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var conversations = await _context.ConversationMemories
                .Where(cm => cm.GameTabletopId == gameTabletopId)
                .ToListAsync(cancellationToken);

            var activeConversations = conversations.Where(c => c.IsActive).ToList();

            return new MemoryStats
            {
                TotalConversations = conversations.Count,
                ActiveConversations = activeConversations.Count,
                OldestConversation = conversations.Any() ? conversations.Min(c => c.CreatedAt) : null,
                NewestConversation = conversations.Any() ? conversations.Max(c => c.CreatedAt) : null,
                AverageImportance = activeConversations.Any() ? activeConversations.Average(c => c.Importance) : 0,
                SpeakerTypeCounts = activeConversations
                    .GroupBy(c => c.SpeakerType)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter estatísticas de memória");
            return new MemoryStats();
        }
    }

    public async Task<int> ArchiveOldConversationsAsync(
        int gameTabletopId,
        DateTime olderThan,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var oldConversations = await _context.ConversationMemories
                .Where(cm => cm.GameTabletopId == gameTabletopId &&
                           cm.IsActive &&
                           cm.CreatedAt < olderThan &&
                           cm.Importance < 7) // Mantém conversas importantes
                .ToListAsync(cancellationToken);

            foreach (var conversation in oldConversations)
            {
                conversation.IsActive = false;
                conversation.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Arquivadas {Count} conversas antigas da Mesa {GameTabletopId}",
                oldConversations.Count, gameTabletopId);

            return oldConversations.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao arquivar conversas antigas");
            return 0;
        }
    }

    private string GenerateContentHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

