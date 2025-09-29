// SPDX-License-Identifier: MIT
// Roll display models + PT-BR formatter for RPGSessionManager
// Comentários explicativos inclusos para facilitar manutenção.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RPGSessionManager.Rolling
{
    /// <summary>
    /// Um valor individual de um dado.
    /// </summary>
    public sealed class DiceFace
    {
        public int Value { get; init; }
        public bool Rerolled { get; init; }
        public int? RerolledFrom { get; init; }
    }

    /// <summary>
    /// Um bloco NdM (ex.: 5d6M3 com opcional rerolar=<x>).
    /// </summary>
    public sealed class DiceBlock
    {
        public string Notation { get; init; } = "";    // "5d6M3" (ou "5d6H3" aceito como alias)
        public int N { get; init; }                    // quantidade de dados
        public int M { get; init; }                    // faces por dado
        public int? KeepHighest { get; init; }         // k em M<k> (ou H<k>)
        public int? RerollEquals { get; init; }        // valor que ocasiona rerrolagem (ex.: 1)
        public List<DiceFace> Faces { get; init; } = new(); // lista na ordem sorteada
        public List<int> KeptValues { get; init; } = new(); // valores mantidos para somar (se houver KeepHighest)
        public int Sum { get; init; }                  // soma final deste bloco (após filtros)
    }

    /// <summary>
    /// Resultado agregado de uma expressão de rolagem.
    /// </summary>
    public sealed class RollResult
    {
        public string User { get; init; } = "";
        public string Expression { get; init; } = "";    // texto digitado pelo usuário (#rerolar=1 5d6M3)
        public string Ruleset { get; init; } = "5e";     // "5e" ou "3.5"
        public Guid Seed { get; init; }                  // GUID gerado para a mensagem
        public List<DiceBlock> Blocks { get; init; } = new();
        public int Total { get; init; }

        // Sinalização de erro
        public bool IsError { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
    }

    /// <summary>
    /// Formatter PT-BR para exibir a rolagem de forma legível.
    /// Gera:
    ///   "Ronassic rolou rerolar=1 5d6M3 — rerrolagens: 1→6 — dados: 6; 3; 6; 6; 4 — 3 maiores: 6+6+4 = 18"
    /// Além do resumo, também gera linhas detalhadas por bloco.
    /// </summary>
    public static class RollFormatter
    {
        public static string FormatSummaryPtBr(RollResult r)
        {
            if (r.IsError)
            {
                return $"[ERRO] {r.ErrorCode ?? "RollEngine"}: {r.ErrorMessage ?? "erro na expressão"}";
            }

            var sb = new StringBuilder();
            sb.Append($"{r.User} rolou {r.Expression} — ");

            // Se houver apenas um bloco NdM, detalhar melhor no resumo.
            if (r.Blocks.Count == 1)
            {
                var b = r.Blocks[0];
                var rerolls = b.Faces
                .Where(f => f.Rerolled && f.RerolledFrom.HasValue)
                .Select(f => f.RerolledFrom!.Value + "→" + f.Value)
                .ToList();

                if (rerolls.Count > 0)
                {
                    sb.Append("rerrolagens: ");
                    sb.Append(string.Join(", ", rerolls));
                    sb.Append(" — ");
                }

                sb.Append("dados: ");
                sb.Append(string.Join("; ", b.Faces.Select(FaceToString)));
                if (b.KeepHighest is int k)
                {
                    sb.Append($" — {k} maiores: ");
                    sb.Append(string.Join("+", b.KeptValues));
                    sb.Append($" = {b.Sum}");
                }
                else
                {
                    sb.Append($" = {b.Sum}");
                }
            }
            else
            {
                sb.Append($"total = {r.Total}");
            }

            return sb.ToString();
        }

        public static IEnumerable<string> FormatDetailsPtBr(RollResult r)
        {
            if (r.IsError)
            {
                yield break;
            }

            yield return $"Seed: {r.Seed}  •  Regra: {r.Ruleset}";
            foreach (var b in r.Blocks)
            {
                yield return FormatBlockPtBr(b);
            }
        }

        public static string FormatBlockPtBr(DiceBlock b)
        {
            var sb = new StringBuilder();
            sb.Append(b.Notation);
            if (b.RerollEquals is int rr)
            {
                sb.Append($" (rerolar={rr})");
            }
            sb.Append(" → [");
            sb.Append(string.Join(", ", b.Faces.Select(FaceToVerboseString)));
            sb.Append("]");

            if (b.KeepHighest is int k)
            {
                sb.Append(" ⇒ maiores [");
                var kept = b.KeptValues.ToList();
                sb.Append(string.Join(",", kept));
                sb.Append($"] = {b.Sum}");
            }
            else
            {
                sb.Append($" = {b.Sum}");
            }

            return sb.ToString();
        }

        private static string FaceToString(DiceFace f)
            => f.Rerolled && f.RerolledFrom.HasValue ? $"{f.Value}*"
               : f.Value.ToString();

        private static string FaceToVerboseString(DiceFace f)
            => f.Rerolled && f.RerolledFrom.HasValue ? $"{f.RerolledFrom.Value}r→{f.Value}"
               : f.Value.ToString();
    }
}

