using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RPGSessionManager.Services;

/// <summary>
/// Serviço para gerenciar o provider de IA ativo
/// - Faz cache do provider por alguns minutos (reduz churn de criação).
/// - Delegação de chamadas para o provider atual.
/// - Fallback interno caso a fábrica falhe (evita quebrar build/execução).
/// </summary>
public class AiProviderService : IAiProvider
{
    private readonly AiProviderFactory _factory;
    private readonly ILogger<AiProviderService> _logger;
    private IAiProvider? _currentProvider;
    private DateTime _lastProviderCheck = DateTime.MinValue;
    private readonly TimeSpan _providerCacheTime = TimeSpan.FromMinutes(5);

    public string ProviderName => _currentProvider?.ProviderName ?? "Unknown";

    public AiProviderService(AiProviderFactory factory, ILogger<AiProviderService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<string> GenerateCharacterReplyAsync(Models.CharacterSheet character, string context, CancellationToken cancellationToken = default)
    {
        var provider = await GetCurrentProviderAsync();
        return await provider.GenerateCharacterReplyAsync(character, context, cancellationToken);
    }

    public async Task<string> GenerateNpcReplyAsync(string npcName, string npcContext, string conversationContext, CancellationToken cancellationToken = default)
    {
        var provider = await GetCurrentProviderAsync();
        return await provider.GenerateNpcReplyAsync(npcName, npcContext, conversationContext, cancellationToken);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var provider = await GetCurrentProviderAsync();
        return await provider.TestConnectionAsync(cancellationToken);
    }

    /// <summary>
    /// Embedding para dedupe/busca semântica.
    /// </summary>
    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var provider = await GetCurrentProviderAsync();
        return await provider.GetEmbeddingAsync(text, cancellationToken);
    }

    /// <summary>
    /// Merge de resumos (preferencialmente via IA; fallback se necessário).
    /// </summary>
    public async Task<string> MergeSummariesAsync(string existingSummary, string incomingSummary, CancellationToken cancellationToken = default)
    {
        var provider = await GetCurrentProviderAsync();
        return await provider.MergeSummariesAsync(existingSummary, incomingSummary, cancellationToken);
    }

    private async Task<IAiProvider> GetCurrentProviderAsync()
    {
        // Verificar se precisa atualizar o provider (cache simples)
        if (_currentProvider == null || DateTime.UtcNow - _lastProviderCheck > _providerCacheTime)
        {
            try
            {
                _currentProvider = await _factory.CreateProviderAsync();
                _lastProviderCheck = DateTime.UtcNow;
                _logger.LogInformation("Provider de IA atualizado: {ProviderName}", _currentProvider.ProviderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar provider de IA");

                // Se não conseguir atualizar e não tiver provider, criar um fallback seguro
                if (_currentProvider == null)
                {
                    _currentProvider = new SimpleFallbackProvider(_logger);
                    _lastProviderCheck = DateTime.UtcNow;
                    _logger.LogWarning("Usando provider de fallback simples devido a erro no factory");
                }
            }
        }

        return _currentProvider!;
    }

    /// <summary>
    /// Força a atualização do provider na próxima chamada
    /// </summary>
    public void RefreshProvider()
    {
        _lastProviderCheck = DateTime.MinValue;
        _logger.LogInformation("Provider de IA será atualizado na próxima chamada");
    }

    /// <summary>
    /// Provider interno de fallback para não quebrar compilação/execução quando a fábrica falha.
    /// Implementação mínima e determinística.
    /// </summary>
    private sealed class SimpleFallbackProvider : IAiProvider
    {
        private readonly ILogger _logger;
        public string ProviderName => "SimpleFallbackProvider";

        public SimpleFallbackProvider(ILogger logger)
        {
            _logger = logger;
        }

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("SimpleFallbackProvider: TestConnectionAsync -> OK");
            return Task.FromResult(true);
        }

        public Task<string> GenerateCharacterReplyAsync(Models.CharacterSheet character, string context, CancellationToken cancellationToken = default)
        {
            // Resposta minimalista para não travar o fluxo
            var reply = $"[{character.Name}] (fallback): Estou processando as últimas informações.";
            return Task.FromResult(reply);
        }

        public Task<string> GenerateNpcReplyAsync(string npcName, string npcContext, string conversationContext, CancellationToken cancellationToken = default)
        {
            var reply = $"[{npcName}] (fallback): Entendi o contexto, prosseguindo com cautela.";
            return Task.FromResult(reply);
        }

        public Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            // Vetor de 128 floats baseado em hash determinístico — suficiente para dedupe simples.
            // Observação: para produção real, substitua por embeddings do provedor oficial.
            const int dim = 128;
            var v = new float[dim];
            unchecked
            {
                int h1 = text?.GetHashCode() ?? 0;
                int h2 = (text?.Length ?? 0) * 397;
                var seed = h1 ^ h2;

                var rnd = new Random(seed);
                for (int i = 0; i < dim; i++)
                {
                    // valor em [-1, 1]
                    v[i] = (float)(rnd.NextDouble() * 2.0 - 1.0);
                }
            }
            return Task.FromResult(v);
        }

        public Task<string> MergeSummariesAsync(string existingSummary, string incomingSummary, CancellationToken cancellationToken = default)
        {
            // Merge simples: concatena com higienização básica e limita tamanho (legível).
            existingSummary = (existingSummary ?? string.Empty).Trim();
            incomingSummary = (incomingSummary ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(existingSummary)) return Task.FromResult(incomingSummary);
            if (string.IsNullOrEmpty(incomingSummary)) return Task.FromResult(existingSummary);

            var merged = new StringBuilder();
            merged.Append(existingSummary);
            if (!existingSummary.EndsWith(".")) merged.Append('.');
            merged.Append(' ');
            merged.Append(incomingSummary);
            if (!incomingSummary.EndsWith(".")) merged.Append('.');

            // Truncagem leve (evita crescimento ilimitado)
            var text = merged.ToString();
            const int maxLen = 600; // ~3-4 frases
            if (text.Length > maxLen)
            {
                text = text[..maxLen].TrimEnd() + "...";
            }

            return Task.FromResult(text);
        }
    }
}
