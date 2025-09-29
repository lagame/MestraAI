using System.Collections.Concurrent;
using RPGSessionManager.Ai;

namespace RPGSessionManager.Services;

public class AiConnectionPool : IAiConnectionPool, IDisposable
{
    private readonly ConcurrentQueue<IAiClient> _connections;
    private readonly SemaphoreSlim _semaphore;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiConnectionPool> _logger;
    private readonly int _maxConnections;
    private readonly int _minConnections;
    private int _currentConnections;
    private bool _disposed;

    public AiConnectionPool(IServiceProvider serviceProvider, ILogger<AiConnectionPool> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _maxConnections = 10; // Configurável via appsettings
        _minConnections = 2;
        _connections = new ConcurrentQueue<IAiClient>();
        _semaphore = new SemaphoreSlim(_maxConnections, _maxConnections);
        
        // Pré-popular com conexões mínimas
        _ = Task.Run(InitializeMinimumConnectionsAsync);
    }

    public int AvailableConnections => _connections.Count;
    public int TotalConnections => _currentConnections;

    private async Task InitializeMinimumConnectionsAsync()
    {
        for (int i = 0; i < _minConnections; i++)
        {
            try
            {
                var connection = await CreateConnectionAsync();
                _connections.Enqueue(connection);
                Interlocked.Increment(ref _currentConnections);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar conexão inicial {Index} no pool de IA", i);
            }
        }
        
        _logger.LogInformation("Pool de conexões IA inicializado com {Count} conexões", _currentConnections);
    }

    private async Task<IAiClient> CreateConnectionAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var aiClient = scope.ServiceProvider.GetRequiredService<IAiClient>();
        return await Task.FromResult(aiClient);
    }

    public async Task<IAiClient> GetConnectionAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AiConnectionPool));

        await _semaphore.WaitAsync();

        if (_connections.TryDequeue(out var connection))
        {
            _logger.LogDebug("Conexão IA obtida do pool. Disponíveis: {Available}", _connections.Count);
            return connection;
        }

        // Criar nova conexão se pool estiver vazio
        if (_currentConnections < _maxConnections)
        {
            try
            {
                connection = await CreateConnectionAsync();
                Interlocked.Increment(ref _currentConnections);
                _logger.LogDebug("Nova conexão IA criada. Total: {Total}", _currentConnections);
                return connection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar nova conexão IA");
                _semaphore.Release();
                throw;
            }
        }

        // Fallback - criar conexão temporária
        _logger.LogWarning("Pool de conexões IA esgotado, criando conexão temporária");
        try
        {
            return await CreateConnectionAsync();
        }
        catch (Exception ex)
        {
            _semaphore.Release();
            throw new InvalidOperationException("Não foi possível obter conexão IA", ex);
        }
    }
    public Task ReturnConnectionAsync(IAiClient connection)
    {
        if (_disposed || connection == null)
        {
            _semaphore.Release();
            return Task.CompletedTask; // retorna uma Task já concluída
        }

        if (_currentConnections <= _maxConnections)
        {
            _connections.Enqueue(connection);
            _logger.LogDebug("Conexão IA retornada ao pool. Disponíveis: {Available}", _connections.Count);
        }
        else
        {
            // Pool cheio, descartar conexão
            if (connection is IDisposable disposable)
            {
                disposable.Dispose();
            }
            Interlocked.Decrement(ref _currentConnections);
        }

        _semaphore.Release();
        return Task.CompletedTask;
    }


    public async Task<T> ExecuteWithPooledConnectionAsync<T>(Func<IAiClient, Task<T>> operation)
    {
        var connection = await GetConnectionAsync();
        try
        {
            return await operation(connection);
        }
        finally
        {
            await ReturnConnectionAsync(connection);
        }
    }

    public async Task<string> ExecuteWithPooledConnectionAsync(Func<IAiClient, Task<string>> operation)
    {
        return await ExecuteWithPooledConnectionAsync<string>(operation);
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        
        while (_connections.TryDequeue(out var connection))
        {
            if (connection is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        
        _semaphore?.Dispose();
        _logger.LogInformation("Pool de conexões IA descartado");
    }
}

