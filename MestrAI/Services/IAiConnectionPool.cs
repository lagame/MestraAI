using RPGSessionManager.Ai;

namespace RPGSessionManager.Services;

public interface IAiConnectionPool
{
    Task<IAiClient> GetConnectionAsync();
    Task ReturnConnectionAsync(IAiClient connection);
    Task<T> ExecuteWithPooledConnectionAsync<T>(Func<IAiClient, Task<T>> operation);
    Task<string> ExecuteWithPooledConnectionAsync(Func<IAiClient, Task<string>> operation);
    int AvailableConnections { get; }
    int TotalConnections { get; }
}

