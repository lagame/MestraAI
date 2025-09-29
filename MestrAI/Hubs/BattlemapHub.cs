using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Dtos;
using RPGSessionManager.Models;
using RPGSessionManager.Services;
using System.Security.Claims;

namespace RPGSessionManager.Hubs;

[Authorize]
public class BattlemapHub : Hub
{
    private readonly IBattlemapService _battlemapService;
    private readonly ILogger<BattlemapHub> _logger;

    public BattlemapHub(IBattlemapService battlemapService, ILogger<BattlemapHub> logger)
    {
        _battlemapService = battlemapService;
        _logger = logger;
    }

    public async Task JoinMap(int sessionId)
    {
        var groupName = GetBattlemapGroupName(sessionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("User {UserId} joined battlemap for session {SessionId}", Context.UserIdentifier, sessionId);
    }

    public async Task LeaveMap(int sessionId)
    {
        var groupName = GetBattlemapGroupName(sessionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("User {UserId} left battlemap for session {SessionId}", Context.UserIdentifier, sessionId);
    }

    public async Task MoveToken(int sessionId, Guid tokenId, float x, float y)
    {
        var userId = Context.UserIdentifier;
        if (userId == null) return;

        var updatedToken = await _battlemapService.MoveTokenAsync(sessionId, tokenId, x, y, userId);

        if (updatedToken != null)
        {
            var groupName = GetBattlemapGroupName(sessionId);
            await Clients.Group(groupName).SendAsync("TokenMoved", new
            {
                TokenId = updatedToken.Id,
                X = updatedToken.X,
                Y = updatedToken.Y,
                MovedBy = userId,
                UpdatedAt = updatedToken.UpdatedAt
            });
        }
        else
        {
            await Clients.Caller.SendAsync("TokenMoveError", "Não foi possível mover o token.");
        }
    }

    public async Task AddToken(int sessionId, int battleMapId, string name, string? imageUrl, float x, float y)
    {
        var userId = Context.UserIdentifier;
        if (userId == null) return;

        var newToken = await _battlemapService.AddTokenAsync(sessionId, battleMapId, name, imageUrl, x, y, userId);

        if (newToken != null)
        {
            var groupName = GetBattlemapGroupName(sessionId);
            await Clients.Group(groupName).SendAsync("TokenAdded", new
            {
                Token = new
                {
                    Id = newToken.Id,
                    Name = newToken.Name,
                    ImageUrl = newToken.ImageUrl,
                    OwnerId = newToken.OwnerId,
                    X = newToken.X,
                    Y = newToken.Y,
                    Scale = newToken.Scale,
                    Rotation = newToken.Rotation,
                    IsVisible = newToken.IsVisible,
                    Z = newToken.Z,
                    UpdatedAt = newToken.UpdatedAt
                },
                AddedBy = userId
            });
        }
        else
        {
            await Clients.Caller.SendAsync("TokenAddError", "Não foi possível adicionar o token.");
        }
    }

    public async Task RemoveToken(int sessionId, Guid tokenId)
    {
        var userId = Context.UserIdentifier;
        if (userId == null) return;

        var success = await _battlemapService.RemoveTokenAsync(sessionId, tokenId, userId);

        if (success)
        {
            var groupName = GetBattlemapGroupName(sessionId);
            await Clients.Group(groupName).SendAsync("TokenRemoved", new
            {
                TokenId = tokenId,
                RemovedBy = userId
            });
        }
        else
        {
            await Clients.Caller.SendAsync("TokenRemoveError", "Não foi possível remover o token.");
        }
    }

    public async Task ChangeMapImage(int sessionId, int battleMapId, string imageUrl)
    {
        var userId = Context.UserIdentifier;
        if (userId == null) return;

        var updatedMap = await _battlemapService.SetMapImageAsync(sessionId, battleMapId, imageUrl, userId);

        if (updatedMap != null)
        {
            var groupName = GetBattlemapGroupName(sessionId);
            await Clients.Group(groupName).SendAsync("MapImageChanged", new
            {
                ImageUrl = updatedMap.BackgroundUrl,
                UpdatedBy = userId,
                UpdatedAt = updatedMap.UpdatedAt
            });
        }
        else
        {
            await Clients.Caller.SendAsync("MapImageError", "Não foi possível alterar a imagem do mapa.");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // A lógica de limpeza de grupo pode ser mais complexa dependendo dos requisitos
        await base.OnDisconnectedAsync(exception);
    }

    private static string GetBattlemapGroupName(int sessionId) => $"Battlemap_{sessionId}";

    public async Task UpdateGridSettings(int sessionId, int battleMapId, GridSettingsDto settings)
    {
        var userId = Context.UserIdentifier; // Pega o ID do usuário da conexão

        if (userId == null) return;

        try
        {
            // 1. Chama o serviço para executar a lógica de negócio
            var updatedBattleMap = await _battlemapService.UpdateGridSettingsAsync(sessionId, battleMapId, settings, userId);

            if (updatedBattleMap != null)
            {
                _logger.LogInformation("User {UserId} updated grid settings in session {SessionId}", userId, sessionId);

                // 2. Notifica os OUTROS clientes sobre a mudança bem-sucedida
                await Clients.GroupExcept($"Session_{sessionId}", Context.ConnectionId)
                             .SendAsync("GridSettingsUpdated", settings);
            }
            else
            {
                _logger.LogWarning("UpdateGridSettings failed for user {UserId} in session {SessionId}. Either map not found or no permission.", userId, sessionId);
                // Opcional: notificar o usuário que fez a chamada sobre o erro.
                // await Clients.Caller.SendAsync("GridUpdateFailed", "Não foi possível alterar o grid. Verifique suas permissões.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Hub method UpdateGridSettings for session {SessionId}", sessionId);
        }
    }
    public async Task UpdateToken(int sessionId, Guid tokenId, UpdateTokenDto updates)
    {
      var userId = Context.UserIdentifier; // Pega o ID do usuário conectado
      if (userId == null) return;

      var updatedToken = await _battlemapService.UpdateTokenAsync(sessionId, tokenId, updates, userId);

      // Em BattlemapHub.cs, dentro do método UpdateToken
      if (updatedToken != null)
      {
        // CORREÇÃO: Enviando um objeto anônimo (DTO) apenas com os dados necessários.
        // Isso quebra o ciclo de referência.
        await Clients.Group(GetBattlemapGroupName(sessionId)).SendAsync("TokenUpdated", new
        {
          updatedToken.Id,
          updatedToken.X,
          updatedToken.Y,
          updatedToken.Scale,
          updatedToken.Rotation,
          updatedToken.IsVisible,
          updatedToken.Name,
          updatedToken.ImageUrl
          // Adicione aqui qualquer outra propriedade simples que o frontend precise
        });
      }
  }
}

