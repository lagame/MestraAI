using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using RPGSessionManager.Data;
using RPGSessionManager.Models;

namespace RPGSessionManager.Services
{
    /// <summary>
    /// Fila/worker para geração de respostas de IA dos NPCs.
    /// - BoundedChannel(1000) com múltiplos leitores/escritores
    /// - Até 3 workers em paralelo
    /// - Shutdown idempotente (TryComplete + aguardando workers)
    /// - Contador de fila preservado em cenários de concorrência
    /// </summary>
    public class AiResponseQueue : BackgroundService, IAiResponseQueue
    {
        private readonly Channel<AiResponseRequest> _queue;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AiResponseQueue> _logger;
        private readonly SemaphoreSlim _processingLimiter;
        private Task[] _workers = Array.Empty<Task>();        

        private int _queueSize;
        private DateTime _lastHealthCheck = DateTime.UtcNow;

        // Flag para garantir Dispose idempotente
        private int _disposeFlag = 0;

        public AiResponseQueue(IServiceProvider serviceProvider, ILogger<AiResponseQueue> logger)
        {
            var options = new BoundedChannelOptions(1000) // Máximo 1000 itens na fila
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,   // múltiplos processadores
                SingleWriter = false
            };

            _queue = Channel.CreateBounded<AiResponseRequest>(options);
            _serviceProvider = serviceProvider;
            _logger = logger;

            // Máximo 3 processamentos simultâneos (mesmo com 3 workers, isso protege caso o número mude)
            _processingLimiter = new SemaphoreSlim(3, 3);            
        }

        public async Task QueueAiResponseAsync(AiResponseRequest request)
        {
            try
            {
                await _queue.Writer.WriteAsync(request);
                IncrementQueueSize(ref _queueSize); // <= preserva contador

                _logger.LogDebug(
                    "Resposta de IA enfileirada para personagem {CharacterName} na sessão {SessionId}. Fila: {QueueSize}",
                    request.CharacterName, request.SessionId, Volatile.Read(ref _queueSize));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Erro ao enfileirar resposta de IA para personagem {CharacterName}",
                    request.CharacterName);
                throw;
            }
        }

        public Task<int> GetQueueSizeAsync()
            => Task.FromResult(Math.Max(0, Volatile.Read(ref _queueSize)));

        public Task<bool> IsHealthyAsync()
        {
            var timeSinceLastCheck = DateTime.UtcNow - _lastHealthCheck;
            var size = Math.Max(0, Volatile.Read(ref _queueSize));
            var isHealthy = timeSinceLastCheck < TimeSpan.FromMinutes(5) && size < 500;
            return Task.FromResult(isHealthy);
        }

        /// <summary>
        /// Sobe os workers e aguarda a conclusão quando o host sinalizar cancelamento.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Serviço de fila de IA iniciado");

            _workers = Enumerable.Range(0, 3)
                .Select(i => ProcessQueueAsync(i, stoppingToken))
                .ToArray();

            try
            {
                await Task.WhenAll(_workers);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Serviço de fila de IA cancelado");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no serviço de fila de IA");
            }
        }

        private async Task ProcessQueueAsync(int workerId, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker {WorkerId} da fila de IA iniciado", workerId);

            await foreach (var request in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                await _processingLimiter.WaitAsync(stoppingToken);
                try
                {
                    _lastHealthCheck = DateTime.UtcNow;

                    await ProcessAiResponseAsync(request, workerId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Worker {WorkerId}: Erro ao processar resposta de IA para personagem {CharacterId}",
                        workerId, request.CharacterId);
                }
                finally
                {
                    // Sempre decrementa ao concluir o processamento do item lido, mesmo com erro
                    DecrementQueueSizeSafe(ref _queueSize); // <= preserva contador (nunca negativo)
                    _processingLimiter.Release();
                }
            }
        }

        private async Task ProcessAiResponseAsync(AiResponseRequest request, int workerId)
        {
            var startTime = DateTime.UtcNow;

            using var scope = _serviceProvider.CreateScope();
            var aiOrchestrator = scope.ServiceProvider.GetRequiredService<AiOrchestrator>();
            var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Verificar se o NPC ainda está ativo e visível
            var currentState = await context.SessionAiCharacters
                .FirstOrDefaultAsync(sac =>
                    sac.SessionId == request.SessionId &&
                    sac.AiCharacterId == request.CharacterId);

            if (currentState == null || !currentState.IsActive || !currentState.IsVisible)
            {
                _logger.LogDebug(
                    "Worker {WorkerId}: NPC {CharacterId} não está mais ativo/visível, ignorando",
                    workerId, request.CharacterId);
                return;
            }

            // 1. Busca o histórico de mensagens desde a última fala da IA
            var conversationHistory = await chatService.GetMessagesSinceLastAiTurnAsync(
                request.SessionId,
                request.CharacterId,
                fallbackMessageCount: 15,
                maxWindowCount: 60);


            // 2. Gera a resposta da IA passando a lista de mensagens
            var aiResponse = await aiOrchestrator.GenerateCharacterReplyAsync(
                request.SessionId,
                request.CharacterId,
                conversationHistory); // CORRIGIDO!

            if (!string.IsNullOrEmpty(aiResponse))
            {
                // Salvar mensagem usando o ChatService
                await chatService.SendMessageAsync(
                    request.SessionId,
                    aiResponse,
                    request.CharacterName,
                    null,
                    MessageType.AiCharacterResponse,
                    request.CharacterId);

                var processingTime = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Worker {WorkerId}: Resposta de IA gerada para {CharacterName} em {ProcessingTime}ms",
                    workerId, request.CharacterName, processingTime.TotalMilliseconds);
            }
            else
            {
                _logger.LogDebug(
                    "Worker {WorkerId}: IA não gerou resposta para {CharacterName}",
                    workerId, request.CharacterName);
            }
        }

        /// <summary>
        /// Shutdown mais suave:
        /// - Tenta completar o Writer sem lançar (TryComplete)
        /// - Aguarda workers terminarem
        /// - Chama base.StopAsync no final
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try { _queue.Writer.TryComplete(); } catch { /* ignore */ }

            try
            {
                if (_workers is { Length: > 0 })
                    await Task.WhenAll(_workers.Where(t => t != null));
            }
            catch
            {
                // Ignorar erros de shutdown; não deve falhar teardown de testes
            }

            await base.StopAsync(cancellationToken);
        }

        /// <summary>
        /// Dispose idempotente (não lança ChannelClosedException).
        /// </summary>
        public override void Dispose()
        {
            // Garante que Dispose rode no máximo uma vez
            if (Interlocked.Exchange(ref _disposeFlag, 1) == 1)
                return;

            try { _queue.Writer.TryComplete(); } catch { /* ignore */ }

            try { _processingLimiter?.Dispose(); } catch { /* ignore */ }

            base.Dispose();
            GC.SuppressFinalize(this);
        }

        // --- Helpers de contador (preservam o contador em cenários de concorrência) ---

        // Incrementa de forma atômica
        private static void IncrementQueueSize(ref int counter)
            => Interlocked.Increment(ref counter);

        // Decrementa sem deixar ir abaixo de zero (CAS loop)
        private static void DecrementQueueSizeSafe(ref int counter)
        {
            while (true)
            {
                var current = Volatile.Read(ref counter);
                if (current == 0) return;           // já está em zero
                var desired = current - 1;
                if (Interlocked.CompareExchange(ref counter, desired, current) == current)
                    return;                          // sucesso
                // senão, alguém alterou no meio; tenta novamente
            }
        }
    }
}
