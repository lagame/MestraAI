using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using System.Collections.Concurrent;

namespace RPGSessionManager.Services
{
    public class InMemoryPresenceService : IPresenceService
    {
        private readonly ApplicationDbContext _context;
       

        // mapeia sessionId -> (userId -> displayName)
        private static readonly ConcurrentDictionary<int, ConcurrentDictionary<string, string>> _sessionPresence
            = new();

        // mapeia connectionId -> (sessionId, userId)
        private static readonly ConcurrentDictionary<string, (int sessionId, string userId)> _connections
            = new();

        public InMemoryPresenceService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<int>> UserDisconnectedAsync(string userId)
        {
            // 1. Encontra todas as conexões e informações de sessão para o usuário especificado.
            var userConnections = _connections
                .Where(pair => pair.Value.userId == userId)
                .Select(pair => new { ConnectionId = pair.Key, SessionId = pair.Value.sessionId })
                .ToList();

            if (!userConnections.Any())
            {
                return Enumerable.Empty<int>(); // Retorna uma coleção vazia se o usuário não tiver conexões.
            }

            // 2. Coleta os IDs únicos das sessões que serão afetadas, ANTES de remover as conexões.
            var affectedSessionIds = userConnections.Select(c => c.SessionId).ToHashSet();

            // 3. Remove cada uma das conexões encontradas, reutilizando a lógica existente.
            foreach (var connection in userConnections)
            {
                await UserLeftAsync(connection.ConnectionId);
            }

            // 4. Retorna a lista de sessões únicas que foram afetadas.
            return affectedSessionIds;
        }
        public async Task UserJoinedAsync(int sessionId, string userId, string displayName, string connectionId)
        {
            // Adicionar presença na coleção em memória
            var sessionDict = _sessionPresence.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, string>());
            sessionDict[userId] = displayName;

            // mapear connection -> (session,user)
            _connections[connectionId] = (sessionId, userId);

            // _logger.LogInformation("User {UserId} ({DisplayName}) joined session {SessionId} with connection {ConnectionId}",
            //     userId, displayName, sessionId, connectionId);

            await Task.CompletedTask;
        }

        public async Task UserLeftAsync(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var info))
            {
                var (sessionId, mappedUserId) = info;

                if (_sessionPresence.TryGetValue(sessionId, out var sessionDict))
                {
                    sessionDict.TryRemove(mappedUserId, out _);
                    if (sessionDict.IsEmpty)
                    {
                        _sessionPresence.TryRemove(sessionId, out _);
                    }
                }
                // _logger.LogInformation("User {UserId} left session {SessionId} (connection {ConnectionId})", mappedUserId, sessionId, connectionId);
            }
            await Task.CompletedTask;
        }

        public async Task RemoveConnectionAsync(string connectionId)
        {
            await UserLeftAsync(connectionId);
        }

        public async Task<List<object>> GetParticipantsAsync(int sessionId, string? requestingUserId = null)
        {
            var participants = new List<object>();

            // Adicionar usuários online
            if (_sessionPresence.TryGetValue(sessionId, out var sessionDict))
            {
                participants.AddRange(sessionDict.Select(kv => new
                {
                    Type = "User",
                    UserId = kv.Key,
                    DisplayName = kv.Value,
                    IsOnline = true,
                    IsNpc = false
                }));
            }

            // Determinar se o usuário solicitante é narrador
            var isNarrator = false;
            if (!string.IsNullOrEmpty(requestingUserId))
            {
                var session = await _context.Sessions.FindAsync(sessionId);
                isNarrator = session?.NarratorId == requestingUserId;
            }

            // Adicionar NPCs baseado nas permissões
            var npcQuery = _context.CharacterSheets
                .Where(cs => cs.SessionId == sessionId && cs.AiEnabled)
                .Join(_context.SessionAiCharacters,
                      cs => cs.Id,
                      sac => sac.AiCharacterId,
                      (cs, sac) => new { CharacterSheet = cs, AiState = sac })
                .Where(x => x.AiState.SessionId == sessionId);

            // Se não for narrador, mostrar apenas NPCs visíveis
            if (!isNarrator)
            {
                npcQuery = npcQuery.Where(x => x.AiState.IsVisible);
            }

            var npcs = await npcQuery
                .Select(x => new
                {
                    Type = "NPC",
                    CharacterId = x.CharacterSheet.Id,
                    DisplayName = x.CharacterSheet.Name,
                    IsOnline = true,
                    IsNpc = true,
                    State = new
                    {
                        x.AiState.IsActive,
                        x.AiState.IsVisible,
                        x.AiState.InteractionFrequency
                    }
                })
                .ToListAsync();

            participants.AddRange(npcs.Cast<object>());

            return participants;
        }    
  }
}
