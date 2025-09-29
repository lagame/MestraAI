using Microsoft.AspNetCore.SignalR;
using RPGSessionManager.Data;
using RPGSessionManager.Hubs;
using RPGSessionManager.Models;

namespace RPGSessionManager.Services;

public class SignalRNotificationService : ISignalRNotificationService
{
      private readonly IHubContext<ChatHub> _hubContext;
      private readonly ILogger<SignalRNotificationService> _logger;

      public SignalRNotificationService(IHubContext<ChatHub> hubContext, ILogger<SignalRNotificationService> logger)
      {
        _hubContext = hubContext;
        _logger = logger;
      }

      private string GetSessionGroupName(int sessionId) => $"Session_{sessionId}";

      public async Task NotifyNpcStatusChangedAsync(int sessionId, int characterId, SessionAiCharacter npcState)
      {
        try
        {
          var groupName = GetSessionGroupName(sessionId);
          var statusData = new { /* ... preencha com os dados ... */ };
          await _hubContext.Clients.Group(groupName).SendAsync("NpcStatusUpdated", statusData);
          _logger.LogDebug("Notificação de status do NPC {CharacterId} enviada para sessão {SessionId}", characterId, sessionId);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Erro ao notificar mudança de status do NPC {CharacterId}", characterId);
        }
      }

      // ... (Implementação dos outros métodos da interface) ...
      // Novamente, vamos adicionar implementações vazias ou simples para fazer o código compilar.

      public async Task NotifyNpcMessageAsync(int sessionId, ChatMessage message)
      {
        var groupName = GetSessionGroupName(sessionId);
        await _hubContext.Clients.Group(groupName).SendAsync("ReceiveAiMessage", message);
      }

      public Task UpdateSessionPresenceAsync(int sessionId)
      {
        // TODO: Implementar lógica de atualização de presença
        return Task.CompletedTask;
      }

      public async Task NotifyNpcActivationAsync(int sessionId, int characterId, bool isActive)
      {
        var groupName = GetSessionGroupName(sessionId);
        await _hubContext.Clients.Group(groupName).SendAsync("NpcActivationChanged", new { characterId, isActive });
      }

      public async Task NotifyNpcVisibilityAsync(int sessionId, int characterId, bool isVisible)
      {
        var groupName = GetSessionGroupName(sessionId);
        await _hubContext.Clients.Group(groupName).SendAsync("NpcVisibilityChanged", new { characterId, isVisible });
      }

      public async Task BroadcastSystemMessageAsync(int sessionId, string message, string messageType = "info")
      {
        var groupName = GetSessionGroupName(sessionId);
        await _hubContext.Clients.Group(groupName).SendAsync("SystemMessage", new { message, messageType });
      }

    /// <summary>
    /// Notifica os clientes do grupo da sessão que uma interação de NPC foi registrada.
    /// </summary>
    public async Task NotifyNpcInteractionRecorded(int sessionId, int characterId, double responseTimeMs)
    {
        var payload = new
        {
            sessionId,
            characterId,
            responseTimeMs,
            recordedAtUtc = DateTime.UtcNow
        };

        try
        {
            var groupName = SignalRGroups.GetSessionGroupName(sessionId);
            await _hubContext.Clients.Group(groupName).SendAsync("NpcInteractionRecorded", payload);
        }
        catch (Exception ex)
        {
            // Em telemetria preferimos não derrubar fluxo se falhar envio
            _logger.LogDebug(ex, "Falha ao publicar evento 'NpcInteractionRecorded' no SignalR.");
        }
    }
    /// <summary>
    /// Convenções de nome para grupos do SignalR.
    /// </summary>
    public static class SignalRGroups
    {
        public static string GetSessionGroupName(int sessionId) => $"session-{sessionId}";
    }
}
