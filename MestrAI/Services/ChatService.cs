using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;

namespace RPGSessionManager.Services;

public class ChatService : IChatService
{
    private readonly ApplicationDbContext _context;
    IServiceProvider _serviceProvider;
    private readonly IChatStreamManager _streamManager;
    private readonly IConversationMemoryService _memoryService;
    private readonly ILogger<ChatService> _logger;
    private readonly INpcStateCache _npcCache;
    private readonly INpcMemoryService _npcMemoryService;
    private readonly ISignalRNotificationService _signalRNotificationService;

  // Rate limiting: track last message time per user
  private readonly ConcurrentDictionary<string, DateTime> _lastMessageTimes = new();
    
    // Duplicate prevention: track recent message hashes
    private readonly ConcurrentDictionary<string, DateTime> _recentMessageHashes = new();
    
    private readonly TimeSpan _rateLimitInterval = TimeSpan.FromSeconds(1); // 1 second between messages
    private readonly TimeSpan _duplicateCheckWindow = TimeSpan.FromMinutes(5); // 5 minute window for duplicates

    public ChatService(
        IServiceProvider serviceProvider,
        ApplicationDbContext context,        
        IChatStreamManager streamManager,
        IConversationMemoryService memoryService,
        ILogger<ChatService> logger,
        INpcStateCache npcCache,        
        INpcMemoryService npcMemoryService,
        ISignalRNotificationService signalRNotificationService
        )
    {
        _context = context;
        _serviceProvider = serviceProvider;
        _streamManager = streamManager;
        _memoryService = memoryService;
        _logger = logger;
        _npcCache = npcCache;
        _npcMemoryService = npcMemoryService;
        _signalRNotificationService = signalRNotificationService;

    // Setup cleanup timer for rate limiting and duplicate tracking
    var cleanupTimer = new Timer(CleanupOldEntries, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public async Task<ChatMessage> SendMessageAsync(
        int sessionId,
        string content,
        string senderName,
        string? senderUserId = null,
        MessageType messageType = MessageType.PlayerMessage,
        int? characterId = null)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Message content cannot be empty", nameof(content));
        }
        
        if (content.Length > 1000)
        {
            throw new ArgumentException("Message content too long (max 1000 characters)", nameof(content));
        }

        // Detectar rolagens de dados
        if (content.Trim().StartsWith("#") && messageType == MessageType.PlayerMessage)
        {
            messageType = MessageType.DiceRoll;
        }

        var userKey = senderUserId ?? senderName;
        var now = DateTime.UtcNow;
        
        // Rate limiting check (skip for system messages)
        if (messageType != MessageType.SystemMessage && !string.IsNullOrEmpty(senderUserId))
        {
            if (_lastMessageTimes.TryGetValue(userKey, out var lastTime))
            {
                var timeSinceLastMessage = now - lastTime;
                if (timeSinceLastMessage < _rateLimitInterval)
                {
                    var waitTime = _rateLimitInterval - timeSinceLastMessage;
                    _logger.LogWarning("Rate limit exceeded for user {UserId} in session {SessionId}, must wait {WaitTime}ms", 
                        senderUserId, sessionId, waitTime.TotalMilliseconds);
                    throw new InvalidOperationException($"Rate limit exceeded. Please wait {waitTime.TotalSeconds:F1} seconds.");
                }
            }
        }
        
        // Duplicate message check
        var messageHash = GenerateMessageHash(sessionId, content.Trim(), senderName, messageType);
        if (_recentMessageHashes.ContainsKey(messageHash))
        {
            _logger.LogWarning("Duplicate message detected for user {UserId} in session {SessionId}", 
                senderUserId, sessionId);
            throw new InvalidOperationException("Duplicate message detected. Please wait before sending the same message again.");
        }

        var message = new ChatMessage
        {
            SessionId = sessionId,
            Content = content.Trim(),
            SenderName = senderName,
            SenderUserId = senderUserId,
            Type = messageType,
            CharacterId = characterId,
            CreatedAt = now
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        // Salvar na memória de conversas para AI
        await SaveToConversationMemoryAsync(message);

        // Update tracking dictionaries
        if (!string.IsNullOrEmpty(senderUserId))
        {
            _lastMessageTimes[userKey] = now;
        }
        _recentMessageHashes[messageHash] = now;

        // Publish to SSE stream
        var eventData = new ChatMessageEventData
        {
            Id = message.Id,
            SessionId = message.SessionId,
            AuthorName = message.SenderName,
            Content = message.Content,
            MessageType = message.Type.ToString(),
            CreatedAt = message.CreatedAt,
            IsAiGenerated = message.IsAiGenerated
        };

        await _streamManager.PublishMessageAsync(sessionId, eventData);

        _logger.LogInformation("Message sent to session {SessionId} by {SenderName}", sessionId, senderName);

        return message;
    }

    public async Task<List<ChatMessage>> GetRecentMessagesAsync(int sessionId, int count = 50)
    {
        return await _context.ChatMessages
            .Include(m => m.SenderUser)
            .Include(m => m.Character)
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(count)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> TriggerAiResponsesAsync(int sessionId, string triggerMessage)
    {
        try
        {
            var aiQueue = _serviceProvider.GetRequiredService<IAiResponseQueue>(); // <-- resolução tardia

            var activeNpcs = await _npcCache.GetSessionActiveNpcsAsync(sessionId);
            if (!activeNpcs.Any())
            {
                _logger.LogDebug("Nenhum NPC ativo encontrado na sessão {SessionId}", sessionId);
                return false;
            }

            var recentMessages = await GetRecentMessagesAsync(sessionId, 10);
            var context = string.Join("\n", recentMessages.Select(m => $"{m.SenderName}: {m.Content}"));

            var responsesQueued = 0;

            foreach (var npcState in activeNpcs)
            {
                var relevantMemories = await _npcMemoryService.GetRelevantMemoriesAsync(npcState.AiCharacterId, context);
                var memoryContext = string.Join("\n", relevantMemories.Select(m => $"Memória ({m.MemoryType}, Importância: {m.Importance}): {m.Content}"));
                var fullContext = $"{context}\n\nMemórias Relevantes:\n{memoryContext}";

                try
                {
                    if (ShouldCharacterRespond(npcState.AiCharacter, triggerMessage, context, npcState))
                    {
                        await aiQueue.QueueAiResponseAsync(new AiResponseRequest
                        {
                            SessionId = sessionId,
                            CharacterId = npcState.AiCharacterId,
                            CharacterName = npcState.AiCharacter.Name,
                            TriggerMessage = triggerMessage,
                            Context = fullContext,
                            NpcState = npcState,
                            Priority = CalculateResponsePriority(npcState, triggerMessage)
                        });

                        responsesQueued++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar NPC {CharacterId}", npcState.AiCharacterId);
                    continue;
                }
            }

            _logger.LogInformation("Enfileiradas {Count} respostas de IA para sessão {SessionId}", responsesQueued, sessionId);
            return responsesQueued > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao disparar respostas de IA para sessão {SessionId}", sessionId);
            return false;
        }
    }

    // Método aprimorado para determinar se NPC deve responder
    private bool ShouldCharacterRespond(CharacterSheet character, string triggerMessage, string context, SessionAiCharacter npcState)
    {
        var characterName = character.Name.ToLowerInvariant();
        var message = triggerMessage.ToLowerInvariant();

        // Responder se mencionado pelo nome
        if (message.Contains(characterName))
        {
            _logger.LogDebug("NPC {CharacterName} vai responder: mencionado pelo nome", character.Name);
            return true;
        }

        // Responder se for pergunta ou direcionamento geral
        if (message.Contains("?") || message.Contains("todos") || message.Contains("alguém"))
        {
            _logger.LogDebug("NPC {CharacterName} vai responder: pergunta ou direcionamento geral", character.Name);
            return true;
        }

        // Usar frequência de interação configurada
        var random = new Random();
        var threshold = npcState.InteractionFrequency / 100.0;
        var shouldRespond = random.NextDouble() < threshold;
        
        if (shouldRespond)
        {
            _logger.LogDebug("NPC {CharacterName} vai responder: chance aleatória ({Frequency}%)", 
                character.Name, npcState.InteractionFrequency);
        }
        
        return shouldRespond;
    }

    private int CalculateResponsePriority(SessionAiCharacter npcState, string triggerMessage)
    {
        var priority = 5; // Prioridade base
        
        // Aumentar prioridade se mencionado pelo nome
        if (triggerMessage.ToLowerInvariant().Contains(npcState.AiCharacter.Name.ToLowerInvariant()))
        {
            priority += 3;
        }
        
        // Aumentar prioridade se for pergunta
        if (triggerMessage.Contains("?"))
        {
            priority += 2;
        }
        
        return Math.Clamp(priority, 1, 10);
    }

    public async Task<List<CharacterSheet>> GetSessionCharactersAsync(int sessionId)
    {
        return await _context.CharacterSheets
            .Include(c => c.Player)
            .Where(c => c.SessionId == sessionId)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<string>> GetSessionParticipantsAsync(int sessionId)
    {
        var session = await _context.Sessions.FindAsync(sessionId);
        if (session == null || string.IsNullOrEmpty(session.Participants))
        {
            return new List<string>();
        }

        var participantIds = JsonSerializer.Deserialize<List<string>>(session.Participants) ?? new List<string>();
        
        var participants = await _context.Users
            .Where(u => participantIds.Contains(u.Id))
            .Select(u => u.DisplayName)
            .ToListAsync();

        return participants;
    }

    public async Task AddSystemMessageAsync(int sessionId, string content)
    {
        await SendMessageAsync(
            sessionId,
            content,
            "Sistema",
            messageType: MessageType.SystemMessage);
    }

    private string GenerateMessageHash(int sessionId, string content, string senderName, MessageType messageType)
    {
        var hashInput = $"{sessionId}:{content}:{senderName}:{messageType}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(hashInput));
    }

    // Em ChatService.cs
    public async Task<List<ChatMessage>> GetMessagesSinceLastAiTurnAsync(
    int sessionId,
    int characterId,
    int fallbackMessageCount = 15,
    int maxWindowCount = 60)
    {
        // Tipos sempre incluídos
        var allowedTypes = new[]
        {
        MessageType.NarratorDescription,
        MessageType.PlayerMessage,
        MessageType.CharacterAction
    };

        // Base query (sem tracking p/ leitura)
        var baseQuery = _context.ChatMessages
            .AsNoTracking()
            .Include(m => m.SenderUser)
            .Where(m => m.SessionId == sessionId);

        // Último turno da PRÓPRIA IA (fonte da verdade: AiCharacterResponse; IsAiGenerated como fallback legado)
        var lastSelfAiTurn = await baseQuery
            .Where(m => m.CharacterId == characterId &&
                   (m.Type == MessageType.AiCharacterResponse || m.IsAiGenerated))
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Select(m => new { m.CreatedAt, m.Id })
            .FirstOrDefaultAsync();

        // Predicado: mensagem é válida pro contexto?
        // 1) Qualquer allowedType (Narrator/Player/Action)
        // 2) Mensagens de IA de OUTROS personagens (AiCharacterResponse || IsAiGenerated) COM CharacterId != characterId
        IQueryable<ChatMessage> ApplyContextFilter(IQueryable<ChatMessage> q) =>
            q.Where(m =>
                allowedTypes.Contains(m.Type) ||
                (
                    (m.Type == MessageType.AiCharacterResponse || m.IsAiGenerated) &&
                    m.CharacterId != characterId
                )
            );

        List<ChatMessage> batch;

        if (lastSelfAiTurn != null)
        {
            // Mensagens estritamente APÓS o último turno da própria IA
            var afterLastTurn = baseQuery
                .Where(m =>
                    (m.CreatedAt > lastSelfAiTurn.CreatedAt) ||
                    (m.CreatedAt == lastSelfAiTurn.CreatedAt && m.Id > lastSelfAiTurn.Id));

            afterLastTurn = ApplyContextFilter(afterLastTurn);

            batch = await afterLastTurn
                .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
                .Take(maxWindowCount)
                .ToListAsync();

            // Reordena crescente para o prompt ficar cronológico
            batch = batch.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToList();
        }
        else
        {
            // Sem turno anterior da própria IA: pega uma janela curta do final
            var tail = baseQuery;
            tail = ApplyContextFilter(tail);

            batch = await tail
                .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
                .Take(Math.Max(fallbackMessageCount, maxWindowCount))
                .ToListAsync();

            batch = batch.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToList();
        }

        return batch;
    }



    private void CleanupOldEntries(object? state)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(_duplicateCheckWindow);
        
        // Clean up old rate limiting entries
        var expiredRateLimitEntries = _lastMessageTimes
            .Where(kvp => kvp.Value < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var key in expiredRateLimitEntries)
        {
            _lastMessageTimes.TryRemove(key, out _);
        }

        // Clean up old duplicate hash entries
        var expiredHashEntries = _recentMessageHashes
            .Where(kvp => kvp.Value < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var key in expiredHashEntries)
        {
            _recentMessageHashes.TryRemove(key, out _);
        }

        if (expiredRateLimitEntries.Count > 0 || expiredHashEntries.Count > 0)
        {
            _logger.LogDebug("Cleaned up {RateLimitCount} rate limit entries and {HashCount} duplicate hash entries", 
                expiredRateLimitEntries.Count, expiredHashEntries.Count);
        }
    }
    private async Task SaveToConversationMemoryAsync(ChatMessage message)
    {
        try
        {
            // 0) Ignorar rolagens
            if (message.Type == MessageType.DiceRoll)
            {
                _logger.LogDebug("Ignorando rolagem na memória (Sessão {SessionId}, MsgId {MessageId})",
                    message.SessionId, message.Id);
                return;
            }

            // 1) Buscar somente o necessário e projetar GameTabletopId como int? (nullable)
            var session = await _context.Sessions
                .Where(s => s.Id == message.SessionId)
                .Select(s => new { GameTabletopId = (int?)s.GameTabletopId }) // força nullable
                .FirstOrDefaultAsync();

            // 2) Decidir tabletopId a ser salvo (int). Se não houver, usamos sentinel negativo
            //    (evita conflitar com IDs reais). Por exemplo: -sessionId.
            int tabletopIdToSave;
            if (session?.GameTabletopId.HasValue == true)
            {
                tabletopIdToSave = session.GameTabletopId.Value;
            }
            else
            {
                tabletopIdToSave = -Math.Abs(message.SessionId); // sentinel negativo
                _logger.LogWarning(
                    "Sessão {SessionId} sem GameTabletopId; usando sentinel {Sentinel} para salvar memória",
                    message.SessionId, tabletopIdToSave);
            }

            // 3) Tipo do falante (inclui NPC)
            var speakerType = message.Type switch
            {
                MessageType.PlayerMessage => "Player",
                MessageType.NarratorDescription => "Narrator",
                MessageType.SystemMessage => "System",
                MessageType.CharacterAction => "Character",
                MessageType.AiCharacterResponse => "NPC",
                _ => "Unknown"
            };

            // 4) Importância
            var importance = message.Type switch
            {
                MessageType.NarratorDescription => 8,
                MessageType.CharacterAction => 7,
                MessageType.PlayerMessage => 6,
                MessageType.AiCharacterResponse => 6,
                MessageType.SystemMessage => 3,
                _ => 5
            };

            // 5) Normalizações
            var speakerName = string.IsNullOrWhiteSpace(message.SenderName)
                ? (speakerType == "NPC" ? "NPC" : "Desconhecido")
                : message.SenderName;
            var content = message.Content ?? string.Empty;

            // 6) Persistir usando a assinatura atual que espera int
            await _memoryService.SaveConversationAsync(
                gameTabletopId: tabletopIdToSave,
                sessionId: message.SessionId,
                speakerName: speakerName,
                speakerType: speakerType,
                content: content,
                context: null,
                importance: importance);

            _logger.LogDebug(
                "Conversa salva na memória: GameTabletopId {TabletopId}, Sessão {SessionId}, Falante {SpeakerName}, Tipo {Type}, Importância {Importance}",
                tabletopIdToSave, message.SessionId, speakerName, message.Type, importance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar conversa na memória para mensagem {MessageId}", message.Id);
        }
    }


    // Adicione este novo método à sua classe ChatService

    public async Task SendAiGeneratedMessageAsync(int sessionId, int characterId, string characterName, string content)
    {
      var aiMessage = new ChatMessage
      {
        SessionId = sessionId,
        Content = content,
        SenderName = characterName,
        CharacterId = characterId,
        Type = MessageType.AiCharacterResponse,
        IsAiGenerated = true,
        CreatedAt = DateTime.UtcNow
        // Preencha outras propriedades como MessageId, SenderType etc. se necessário
      };

      _context.ChatMessages.Add(aiMessage);
      await _context.SaveChangesAsync();

      // Salvar na memória de conversas de curto prazo (já existente)
      await SaveToConversationMemoryAsync(aiMessage);

      // =================================================================
      //  USANDO O NOVO SERVIÇO DE MEMÓRIA DE LONGO PRAZO
      // =================================================================
      try
      {
        await _npcMemoryService.SaveMemoryAsync(
            characterId: characterId,
            sessionId: sessionId,
            memoryType: "Interaction",
            content: content,
            importance: 5);

        _logger.LogDebug("Memória de longo prazo salva para NPC {CharacterId}", characterId);

        // Chamar EvolvePersonalityAsync após a interação
        await _npcMemoryService.EvolvePersonalityAsync(characterId, content);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Erro ao salvar memória de longo prazo ou evoluir personalidade para NPC {CharacterId}", characterId);
      }

      // =================================================================
      //  USANDO O NOVO SERVIÇO DE NOTIFICAÇÃO SIGNALR
      // =================================================================
      try
      {
        // Notifica todos os clientes via SignalR
        await _signalRNotificationService.NotifyNpcMessageAsync(sessionId, aiMessage);
        _logger.LogInformation("Mensagem de IA notificada via SignalR para sessão {SessionId}", sessionId);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Falha ao notificar mensagem de IA via SignalR para sessão {SessionId}", sessionId);
      }
    }

  public ChatServiceStats GetStats()
    {
        return new ChatServiceStats
        {
            ActiveRateLimitEntries = _lastMessageTimes.Count,
            ActiveDuplicateHashes = _recentMessageHashes.Count,
            RateLimitInterval = _rateLimitInterval,
            DuplicateCheckWindow = _duplicateCheckWindow
        };
    }
}

public class ChatServiceStats
{
    public int ActiveRateLimitEntries { get; set; }
    public int ActiveDuplicateHashes { get; set; }
    public TimeSpan RateLimitInterval { get; set; }
    public TimeSpan DuplicateCheckWindow { get; set; }
}

