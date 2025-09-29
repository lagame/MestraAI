namespace RPGSessionManager.Services;

public interface IChatStreamManager : IDisposable
{
    Task PublishMessageAsync(int sessionId, object messageData);
    Task<IAsyncEnumerable<string>> SubscribeToSessionAsync(int sessionId, string? lastEventId = null);
    void RemoveConnection(int sessionId, string connectionId);
    int GetActiveConnectionCount(int sessionId);
    Dictionary<int, int> GetSessionConnectionCounts();
}

