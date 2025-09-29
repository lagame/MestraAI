using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;

namespace RPGSessionManager.Services
{
    /// <summary>
    /// Orquestrador de IA com chamada única:
    /// - Gera a fala do NPC (reply)
    /// - Retorna decisão estruturada sobre salvar memória (memoryDecision)
    /// - Processa a memória em background (Task.Run) com retry/backoff
    /// - Dedup semântico simples via embeddings on-the-fly (para baixo volume)
    ///
    /// IMPORTANTE:
    /// - Em produção, prefira mover o processamento para um IHostedService + fila dedicada.
    /// - Este arquivo evita dependências extras e já entrega funcionalidade end-to-end.
    /// </summary>
    public class AiOrchestrator
    {
        private readonly ApplicationDbContext _context;
        private readonly IAiProvider _aiProvider;
        private readonly ILogger<AiOrchestrator> _logger;
        private readonly UserManager<ApplicationUser> _users;
        private readonly IConfiguration _configuration;

        // Configs
        private readonly double _saveConfidenceThreshold;
        private readonly double _dedupSimilarityThreshold;
        private readonly int _maxSimilarToCheck;

        public AiOrchestrator(
            ApplicationDbContext context,
            IAiProvider aiProvider,
            ILogger<AiOrchestrator> logger,
            UserManager<ApplicationUser> users,
            IConfiguration configuration)
        {
            _context = context;
            _aiProvider = aiProvider;
            _logger = logger;
            _users = users;
            _configuration = configuration;

            // Thresholds configuráveis em appsettings: "MemoryGeneration:*"
            _saveConfidenceThreshold = _configuration.GetValue<double>("MemoryGeneration:SaveConfidenceThreshold", 0.70);
            _dedupSimilarityThreshold = _configuration.GetValue<double>("MemoryGeneration:DedupSimilarityThreshold", 0.85);
            _maxSimilarToCheck = Math.Clamp(_configuration.GetValue<int>("MemoryGeneration:MaxSimilarToCheck", 25), 5, 200);
        }

        /// <summary>
        /// Gera a fala do NPC e, na mesma chamada de IA, recebe decisão de salvar memória.
        /// Faz o retorno da fala imediatamente e dispara o processamento de memória em background.
        /// </summary>
        public async Task<string> GenerateCharacterReplyAsync(int sessionId, int characterId, List<ChatMessage> chatMessages)
        {
            try
            {
                // 1) Carregar a ficha do personagem
                var character = await _context.CharacterSheets
                    .Include(cs => cs.Session)
                    .FirstOrDefaultAsync(cs => cs.Id == characterId && cs.Session.Id == sessionId);

                if (character == null)
                    throw new ArgumentException("Personagem não encontrado ou não está na sessão especificada");

                if (!character.AiEnabled)
                    throw new InvalidOperationException("A IA não está habilitada para este personagem");

                // 2) Construir o histórico de conversa recente para o prompt
                // (Opcional) limite de janelas para não explodir o prompt
                int maxHistory = _configuration.GetValue<int>("AiSettings:MaxHistoryMessages", 60);

                // Se tiver campo de tempo, ordene; caso contrário, use como veio.
                IEnumerable<ChatMessage> ordered =
                    chatMessages.OrderBy(m => m.CreatedAt); // ajuste para CreatedAt/Data se for o seu campo

                // Pegue só as últimas N para reduzir custo
                ordered = ordered.TakeLast(maxHistory);

                var conversationBuilder = new StringBuilder();

                foreach (var message in ordered)
                {
                    // Ignorar rolagens de dados no prompt
                    if (message.Type == MessageType.DiceRoll) continue;

                    var senderName = string.IsNullOrWhiteSpace(message.SenderName) ? "Desconhecido" : message.SenderName;
                    var content = message.Content ?? string.Empty;

                    switch (message.Type)
                    {
                        case MessageType.NarratorDescription:
                            conversationBuilder.AppendLine($"[Narração do Mestre]: {content}");
                            break;

                        // Jogadores e ações de personagens (PCs)
                        case MessageType.PlayerMessage:
                        case MessageType.CharacterAction:
                            conversationBuilder.AppendLine($"[{senderName}]: {content}");
                            break;

                        // >>> IMPORTANTE: incluir falas das outras IAs/NPCs (e do próprio NPC)
                        case MessageType.AiCharacterResponse:
                            // Se quiser identificar claramente que é um NPC/IA:
                            conversationBuilder.AppendLine($"[{senderName}]: {content}");
                            break;

                        // Mensagens de sistema geralmente são meta (não in-lore). Mantemos fora.
                        case MessageType.SystemMessage:
                            continue;
                    }
                }

                var conversationContext = conversationBuilder.ToString();
                if (string.IsNullOrWhiteSpace(conversationContext))
                {
                    conversationContext = "A cena está começando ou o Mestre pediu sua intervenção. Apresente-se ou reaja ao ambiente.";
                }


                // 3) Criar estado mínimo (participantes, etc.) para auxiliar a IA a extrair entities
                var minimalState = await CreateMinimalStateAsync(character.Session);

                // antes de montar o finalPrompt
                var maxMems = _configuration.GetValue<int>("MemoryGeneration:MaxMemoriesForDedupe", 6);

                var recentMems = await _context.NpcLongTermMemories
                    .AsNoTracking()
                    .Where(m => m.CharacterId == character.Id && !m.IsDeleted)
                    .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
                    .Select(m => new { id = m.Id, summary = m.Content })
                    .Take(_configuration.GetValue<int>("MemoryGeneration:MaxMemoriesForDedupe", 6))
                    .ToListAsync();

                var recentMemsJson = JsonSerializer.Serialize(recentMems);


                // 4) Montar prompt final + instrução de JSON estrito
                var finalPrompt = $@"
Sessão de RPG: {character.Session.Name}
Estado Geral (JSON): 
{minimalState}

--- Histórico da Conversa Recente ---
{conversationContext}
--- Fim do Histórico ---
Memórias recentes do NPC (para deduplicação):
{recentMemsJson}

Instruções para você, {character.Name}:
- Você é o personagem '{character.Name}'. Aja estritamente de acordo com sua personalidade, memórias e objetivos.
- Responda curto e impactante (1 a 3 frases) por padrão.
- Se o histórico indicar pedido de detalhe, ação complexa ou fala dramática, você pode estender até 2 parágrafos curtos.
- Nunca ultrapasse 160–200 palavras no total.
- Responda diretamente, sem prefixar com seu nome.

Agora, responda estritamente com JSON válido e nada mais (sem texto extra fora do JSON).
Formato exigido:
{{
  ""reply"": ""<oque o personagem dirá - 1 a 3 frases>"",
  ""memoryDecision"": {{
    ""shouldSave"": true|false,
    ""memoryType"": ""fact|goal|relationship|secret|event|rumor"",
    ""summary"": ""<resumo factual no passado, 1-2 frases>"",
    ""tags"": [""tag1"",""tag2""],
    ""relatedEntities"": [{{""type"":""Player"",""id"":""u-123"",""name"":""Rafael""}}],
    ""importance"": <1-10 inteiro>,
    ""confidence"": <0.0-1.0>
  }}
}}";

                // 5) Chamada única ao provedor de IA (mantém compat com providers antigos: se vier texto, fazemos fallback)
                var rawResponse = await _aiProvider.GenerateCharacterReplyAsync(character, finalPrompt);

                // 6) Extrair apenas o primeiro JSON válido
                var json = ExtractFirstJsonObject(rawResponse);
                AiCharacterResponse ai;
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        ai = JsonSerializer.Deserialize<AiCharacterResponse>(json, JsonOptions()) ?? new AiCharacterResponse();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao desserializar JSON da IA. Usando raw como reply.");
                        ai = new AiCharacterResponse { Reply = rawResponse };
                    }
                }
                else
                {
                    // Provider antigo / não seguiu instrução → usar raw como reply
                    ai = new AiCharacterResponse { Reply = rawResponse };
                }

                var replyText = ai.Reply?.Trim() ?? string.Empty;

                // 7) Processamento de memória em background (Task.Run c/ retry/backoff)
                if (ai.MemoryDecision != null &&
                    ai.MemoryDecision.ShouldSave &&
                    ai.MemoryDecision.Confidence >= _saveConfidenceThreshold &&
                    !string.IsNullOrWhiteSpace(ai.MemoryDecision.Summary))
                {
                    var decision = ai.MemoryDecision;
                    // Capturar variáveis por valor
                    _ = Task.Run(async () =>
                    {
                        await ExecuteWithRetriesAsync(async () =>
                        {
                            await HandleAiMemoryDecisionAsync(
                                characterId: character.Id,
                                sessionId: sessionId,
                                decision: decision
                            );
                        });
                    });
                }

                _logger.LogInformation("Generated AI response for character {CharacterId} in session {SessionId}", characterId, sessionId);
                return replyText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate character reply for character {CharacterId} in session {SessionId}", characterId, sessionId);
                throw;
            }
        }

        #region ---- Background / Retry / Helpers ----

        private async Task ExecuteWithRetriesAsync(Func<Task> work)
        {
            int attempts = 0;
            const int max = 5;
            while (true)
            {
                try
                {
                    await work();
                    return;
                }
                catch (Exception ex) when (attempts++ < max)
                {
                    var jitter = Random.Shared.Next(0, 500);
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempts)) + TimeSpan.FromMilliseconds(jitter);
                    _logger.LogWarning(ex, "Memory task failed, retrying in {Delay}s (attempt {Attempt})", delay.TotalSeconds, attempts);
                    await Task.Delay(delay);
                }
            }
        }

        private static JsonSerializerOptions JsonOptions() => new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private static string? ExtractFirstJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            int start = text.IndexOf('{');
            if (start < 0) return null;

            int depth = 0;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{') depth++;
                else if (c == '}') depth--;

                if (depth == 0)
                {
                    return text.Substring(start, i - start + 1);
                }
            }
            return null;
        }

        #endregion

        #region ---- Memory Handling (save/merge + dedup por embeddings) ----

        private async Task HandleAiMemoryDecisionAsync(int characterId, int sessionId, AiMemoryDecision decision)
        {
            // 1) Validação básica
            var character = await _context.CharacterSheets
                .Include(c => c.Session)
                .FirstOrDefaultAsync(c => c.Id == characterId && c.Session.Id == sessionId);

            if (character == null)
            {
                _logger.LogWarning("Character or session not found for memory handling. char={CharId}, session={SessionId}", characterId, sessionId);
                return;
            }

            // 2) Preparar nova memória
            var newContent = (decision.Summary ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(newContent))
                return;

            var tagsCsv = decision.Tags != null && decision.Tags.Length > 0
                ? string.Join(",", decision.Tags.Select(t => t.Trim()).Where(t => t.Length > 0))
                : null;

            var relatedJson = decision.RelatedEntities != null && decision.RelatedEntities.Length > 0
                ? JsonSerializer.Serialize(decision.RelatedEntities)
                : null;

            // 3) *** LLM-DEDUPE *** (SEM EMBEDDINGS)
            // A própria IA já sinaliza duplicidade:
            // - MergeTargetId: id da memória existente (ou null)
            // - MergedSummary: resumo final coeso (se for merge)
            // - DedupeScore: 0..1 de confiança/similaridade
            var dedupTh = _configuration.GetValue<double>("MemoryGeneration:DedupSimilarityThreshold", 0.85);
            var targetId = decision.MergeTargetId;
            var score = decision.DedupeScore ?? 0.0;

            if (targetId.HasValue && score >= dedupTh)
            {
                // Garantia extra: o target deve pertencer ao mesmo personagem e não estar deletado
                var exists = await _context.NpcLongTermMemories
                    .AsNoTracking()
                    .AnyAsync(m => m.Id == targetId.Value && m.CharacterId == characterId && !m.IsDeleted);

                if (exists)
                {
                    var mergedText = string.IsNullOrWhiteSpace(decision.MergedSummary)
                        ? newContent                       // se não veio merged, usa o texto novo
                        : decision.MergedSummary!.Trim();  // senão usa o texto mesclado sugerido

                    await MergeMemoryAsync(
                        existingId: targetId.Value,
                        newContent: mergedText,
                        tags: decision.Tags ?? Array.Empty<string>(),
                        suggestedImportance: Math.Clamp(decision.Importance, 1, 10));

                    _logger.LogInformation("LLM-dedupe: merged into {Id} (score={Score:F2} >= {Th})", targetId.Value, score, dedupTh);
                    return;
                }

                // Se o ID indicado não existe/pertence a outro char, faz fallback para salvar novo
                _logger.LogWarning("LLM-dedupe target {Id} not found/invalid. Saving as new.", targetId.Value);
            }

            // 4) Não é duplicata (ou não atingiu o threshold) → salvar novo
            await SaveNewMemoryAsync(characterId, sessionId, decision, newContent, tagsCsv, relatedJson);
        }


        private async Task SaveNewMemoryAsync(int characterId, int sessionId, AiMemoryDecision decision, string content, string? tagsCsv, string? relatedJson)
        {
            var mem = new NpcLongTermMemory
            {
                CharacterId = characterId,
                SessionId = sessionId,
                MemoryType = string.IsNullOrWhiteSpace(decision.MemoryType) ? "fact" : decision.MemoryType!,
                Content = content,
                Tags = tagsCsv,
                RelatedEntities = relatedJson,
                Importance = Math.Clamp(decision.Importance, 1, 10),
                IsActive = true,
                IsDeleted = false,
                AccessCount = 0,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };

            _context.NpcLongTermMemories.Add(mem);
            await _context.SaveChangesAsync();
            _logger.LogDebug("Saved long-term memory {Id} for character {CharId}", mem.Id, characterId);
        }

        private async Task MergeMemoryAsync(long existingId, string newContent, string[] tags, int suggestedImportance)
        {
            var existing = await _context.NpcLongTermMemories.FirstOrDefaultAsync(m => m.Id == existingId);
            if (existing == null) return;

            // 1) Tentar mesclar via IA para manter coesão
            string merged;
            try
            {
                merged = await _aiProvider.MergeSummariesAsync(existing.Content ?? string.Empty, newContent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI merge failed, falling back to concat");
                merged = $"{existing.Content}\n\n[Update {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}] {newContent}";
            }

            // 2) Atualizar campos
            existing.Content = merged;
            existing.Tags = MergeTags(existing.Tags, tags);
            existing.Importance = Math.Max(existing.Importance, suggestedImportance);
            existing.LastAccessedAt = DateTime.UtcNow;
            existing.AccessCount += 1;

            _context.NpcLongTermMemories.Update(existing);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Merged memory {Id}", existingId);
        }

        private static string? MergeTags(string? existingCsv, string[] newTags)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(existingCsv))
            {
                foreach (var t in existingCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    set.Add(t.Trim());
            }
            foreach (var t in newTags ?? Array.Empty<string>())
            {
                var tt = t?.Trim();
                if (!string.IsNullOrWhiteSpace(tt)) set.Add(tt!);
            }
            return set.Count == 0 ? null : string.Join(",", set);
        }

        private static double CosineSimilarity(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length == 0 || b.Length == 0) return 0d;
            int len = Math.Min(a.Length, b.Length);
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < len; i++)
            {
                var av = a[i];
                var bv = b[i];
                dot += av * bv;
                na += av * av;
                nb += bv * bv;
            }
            if (na == 0 || nb == 0) return 0;
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }

        #endregion

        #region ---- Estado mínimo / utilidades existentes ----

        private async Task<string> CreateMinimalStateAsync(Session session)
        {
            // 1. Desserializa a string JSON para uma lista real de IDs.
            List<string> participantIds = new();

            if (!string.IsNullOrWhiteSpace(session.Participants))
            {
                try
                {
                    participantIds = JsonSerializer
                        .Deserialize<List<string>>(session.Participants) ?? new List<string>();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex,
                        "Falha ao desserializar a lista de participantes. Conteúdo: {Participants}",
                        session.Participants);

                    // Continua com lista vazia
                    participantIds = new List<string>();
                }
            }

            // 2. Cria lista de tarefas para buscar os nomes.
            var participantNameTasks = participantIds.Select(id => GetUserDisplayNameAsync(id));

            // 3. Aguarda todas as buscas em paralelo.
            var participantNames = await Task.WhenAll(participantNameTasks);

            // 4. Monta o objeto de estado.
            var state = new
            {
                scene = "Current scene in progress",
                location = "Current location",
                time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
                session_name = session.Name,
                participants = participantNames
            };

            // 5. Serializa em JSON indentado.
            return JsonSerializer.Serialize(
                state,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }

        private async Task<string> GetUserDisplayNameAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return userId;
            var user = await _users.FindByIdAsync(userId);
            if (user == null) return userId;
            return user.DisplayName ?? user.UserName ?? userId;
        }

        #endregion
    }

    #region ---- DTOs (resposta da IA) ----

    public class AiCharacterResponse
    {
        [JsonPropertyName("reply")]
        public string? Reply { get; set; }

        [JsonPropertyName("memoryDecision")]
        public AiMemoryDecision? MemoryDecision { get; set; }
    }

    public class AiMemoryDecision
    {
        [JsonPropertyName("shouldSave")]
        public bool ShouldSave { get; set; }

        [JsonPropertyName("memoryType")]
        public string? MemoryType { get; set; } = "fact";

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("tags")]
        public string[] Tags { get; set; } = Array.Empty<string>();

        [JsonPropertyName("relatedEntities")]
        public AiEntity[] RelatedEntities { get; set; } = Array.Empty<AiEntity>();

        [JsonPropertyName("importance")]
        public int Importance { get; set; } = 5;

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; } = 0.5;

        // >>> Novos campos para LLM-dedupe <<<
        [JsonPropertyName("mergeTargetId")]
        public long? MergeTargetId { get; set; }   // ID da memória existente (ou null)

        [JsonPropertyName("mergedSummary")]
        public string? MergedSummary { get; set; } // resumo final coeso (se for merge)

        [JsonPropertyName("dedupeScore")]
        public double? DedupeScore { get; set; }   // 0..1 de confiança na duplicidade
    }


    public class AiEntity
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    #endregion
}
