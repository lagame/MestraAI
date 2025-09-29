using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace RPGSessionManager.Services;

public class ChatStreamManager : IChatStreamManager
{
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, Channel<string>>> _sessionConnections = new();
    private readonly ConcurrentDictionary<string, (int SessionId, DateTime ConnectedAt, string UserId)> _connections = new();
    private readonly ILogger<ChatStreamManager> _logger;
    private readonly Timer _cleanupTimer;

    public ChatStreamManager(ILogger<ChatStreamManager> logger)
    {
        _logger = logger;
        
        // Setup cleanup timer to run every 5 minutes
        _cleanupTimer = new Timer(CleanupStaleConnections, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public Task PublishMessageAsync(int sessionId, object messageData)
    {
        if (!_sessionConnections.TryGetValue(sessionId, out var connections))
        {
            _logger.LogDebug("No active connections for session {SessionId}", sessionId);
            return Task.CompletedTask;
        }

        var eventData = FormatSseEvent(messageData);
        var activeConnections = 0;

        foreach (var kvp in connections)
        {
            var connectionId = kvp.Key;
            var channel = kvp.Value;

            if (!channel.Writer.TryWrite(eventData))
            {
                _logger.LogWarning("Failed to write to channel for connection {ConnectionId} in session {SessionId}",
                    connectionId, sessionId);
                RemoveConnection(sessionId, connectionId);
            }
            else
            {
                activeConnections++;
            }
        }

        _logger.LogDebug("Published message to {ActiveCount}/{TotalCount} connections in session {SessionId}",
            activeConnections, connections.Count, sessionId);

        return Task.CompletedTask;
    }


    public Task<IAsyncEnumerable<string>> SubscribeToSessionAsync(int sessionId, string? lastEventId = null)
    {
        var connectionId = Guid.NewGuid().ToString();
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        // Add connection to session
        var sessionConnections = _sessionConnections.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, Channel<string>>());
        sessionConnections[connectionId] = channel;

        // Store connection metadata (userId would need to be passed from controller)
        _connections[connectionId] = (sessionId, DateTime.UtcNow, "unknown");

        _logger.LogInformation(
            "New SSE connection {ConnectionId} for session {SessionId} (total connections: {TotalConnections})",
            connectionId, sessionId, sessionConnections.Count);

        return Task.FromResult(GetEventStreamAsync(channel.Reader, connectionId, sessionId));
    }

    public void RemoveConnection(int sessionId, string connectionId)
    {
        _connections.TryRemove(connectionId, out var connectionInfo);
        
        if (_sessionConnections.TryGetValue(sessionId, out var sessionConnections))
        {
            if (sessionConnections.TryRemove(connectionId, out var channel))
            {
                channel.Writer.TryComplete();
                
                _logger.LogInformation("Removed SSE connection {ConnectionId} for session {SessionId} (remaining: {RemainingConnections})", 
                    connectionId, sessionId, sessionConnections.Count);
            }

            // Clean up empty session
            if (sessionConnections.IsEmpty)
            {
                _sessionConnections.TryRemove(sessionId, out _);
                _logger.LogDebug("Cleaned up empty session {SessionId} from connection manager", sessionId);
            }
        }
    }

    public int GetActiveConnectionCount(int sessionId)
    {
        if (_sessionConnections.TryGetValue(sessionId, out var connections))
        {
            return connections.Count;
        }
        return 0;
    }

    public Dictionary<int, int> GetSessionConnectionCounts()
    {
        return _sessionConnections.ToDictionary(
            kvp => kvp.Key, 
            kvp => kvp.Value.Count
        );
    }

    private void CleanupStaleConnections(object? state)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-30); // Remove connections older than 30 minutes
        var staleConnections = new List<(string ConnectionId, int SessionId)>();

        foreach (var kvp in _connections)
        {
            var connectionId = kvp.Key;
            var (sessionId, connectedAt, userId) = kvp.Value;
            
            if (connectedAt < cutoffTime)
            {
                staleConnections.Add((connectionId, sessionId));
            }
        }

        foreach (var (connectionId, sessionId) in staleConnections)
        {
            _logger.LogInformation("Cleaning up stale connection {ConnectionId} for session {SessionId}", 
                connectionId, sessionId);
            RemoveConnection(sessionId, connectionId);
        }

        if (staleConnections.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} stale connections", staleConnections.Count);
        }
    }

    private string FormatSseEvent(object messageData)
    {
        var json = JsonSerializer.Serialize(messageData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Extract ID for SSE event ID
        var messageId = "";
        if (messageData is ChatMessageEventData eventData)
        {
            messageId = eventData.Id.ToString();
        }

        return $"id: {messageId}\nevent: message\ndata: {json}\n\n";
    }

    private async IAsyncEnumerable<string> GetEventStreamAsync(
        ChannelReader<string> reader, 
        string connectionId,
        int sessionId)
    {
        var keepAliveTimer = new Timer(_ =>
        {
            try
            {
                if (_sessionConnections.TryGetValue(sessionId, out var connections) &&
                    connections.TryGetValue(connectionId, out var channel))
                {
                    if (!channel.Writer.TryWrite(":ping\n\n"))
                    {
                        _logger.LogDebug("Failed to send keep-alive to connection {ConnectionId}, removing", connectionId);
                        RemoveConnection(sessionId, connectionId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending keep-alive for connection {ConnectionId}", connectionId);
                RemoveConnection(sessionId, connectionId);
            }
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        try
        {
            await foreach (var message in reader.ReadAllAsync())
            {
                yield return message;
            }
        }
        finally
        {
            try
            {
                keepAliveTimer?.Dispose();
                RemoveConnection(sessionId, connectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cleanup for connection {ConnectionId} in session {SessionId}", 
                    connectionId, sessionId);
            }
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        
        // Close all channels
        foreach (var sessionConnections in _sessionConnections.Values)
        {
            foreach (var channel in sessionConnections.Values)
            {
                channel.Writer.TryComplete();
            }
        }
        
        _sessionConnections.Clear();
        _connections.Clear();
    }
}

public class ChatMessageEventData
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsAiGenerated { get; set; }
}

