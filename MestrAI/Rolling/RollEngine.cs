using RPGSessionManager.Models;
using RPGSessionManager.Pages.Sessions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RPGSessionManager.Rolling
{
    /// <summary>
    /// Engine principal para parsing e execução de rolagens de dados.
    /// Suporta sintaxe: #rerolar=<v> NdM[M<k>], expressões matemáticas, comando #attack.
    /// </summary>
    public class RollEngine
    {
        private readonly bool _isDeterministic;
        //private readonly Random? _deterministicRandom;
        private static readonly Random _deterministicSingleton = new Random(42);

        // Altere o(s) construtor(es) para aceitar ILogger opcionalmente
        public RollEngine(bool isDeterministic = false)
        {
            _isDeterministic = isDeterministic;   
        }


        /// <summary>
        /// Gera um número aleatório entre min (inclusive) e max (inclusive)
        /// </summary>
        //private int GetRandomNumber(int min, int max)
        //{
        //    if (_isDeterministic && _deterministicRandom != null)
        //    {
        //        return _deterministicRandom.Next(min, max + 1);
        //    }

        //    // Usar RandomNumberGenerator para aleatoriedade criptograficamente segura
        //    return RandomNumberGenerator.GetInt32(min, max + 1);
        //}

        private int GetRandomInclusive(int min, int max)
        {
            if (min > max) throw new ArgumentOutOfRangeException(nameof(min));
            if (max == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(max), "max não pode ser int.MaxValue em intervalo inclusivo.");
            int result = _isDeterministic
            ? _deterministicSingleton.Next(min, max + 1)
            : RandomNumberGenerator.GetInt32(min, max + 1);
            return result;
        }


        /// <summary>
        /// Processa uma expressão de rolagem iniciada com #.
        /// </summary>
        public RollResult ProcessRoll(string expression, string userName, string ruleset = "5e")
        {
            var seed = Guid.NewGuid();
            
            try
            {
                if (string.IsNullOrWhiteSpace(expression) || !expression.StartsWith("#"))
                {
                    return CreateError("INVALID_SYNTAX", "Expressão deve começar com #", expression, userName, ruleset, seed);
                }

                var cleanExpr = expression.Substring(1).Trim();
                
                // Verificar se é comando #attack
                if (cleanExpr.StartsWith("attack ", StringComparison.OrdinalIgnoreCase))
                {
                    return ProcessAttackCommand(cleanExpr, userName, ruleset, seed);
                }

                // Processar expressão normal
                return ProcessNormalExpression(cleanExpr, expression, userName, ruleset, seed);
            }
            catch (Exception ex)
            {
                return CreateError("PARSE_ERROR", $"Erro interno: {ex.Message}", expression, userName, ruleset, seed);
            }
        }

        private RollResult ProcessNormalExpression(string cleanExpr, string originalExpr, string userName, string ruleset, Guid seed)
        {
            // Parser simples para rerolar=<v> NdM[M<k>]
            var rerollMatch = Regex.Match(cleanExpr, @"^rerolar=(\d+)\s+(.+)$");
            int? rerollValue = null;
            string diceExpr = cleanExpr;

            if (rerollMatch.Success)
            {
                if (!int.TryParse(rerollMatch.Groups[1].Value, out var rv))
                {
                    return CreateError("INVALID_REROLL", $"Valor de rerrolagem inválido: {rerollMatch.Groups[1].Value}", originalExpr, userName, ruleset, seed);
                }
                rerollValue = rv;
                diceExpr = rerollMatch.Groups[2].Value;
            }

            // Parser para NdM[M<k>] ou NdM[H<k>]
            var diceMatch = Regex.Match(diceExpr, @"^(\d*)d(\d+)(?:[MH](\d+))?(?:\s*\+\s*(\d+))?$", RegexOptions.IgnoreCase);
            if (!diceMatch.Success)
            {
                return CreateError("INVALID_DICE", $"Sintaxe de dados inválida: {diceExpr}", originalExpr, userName, ruleset, seed);
            }

            var nStr = diceMatch.Groups[1].Value;
            var mStr = diceMatch.Groups[2].Value;
            var kStr = diceMatch.Groups[3].Value;
            var modStr = diceMatch.Groups[4].Value;

            int n = string.IsNullOrEmpty(nStr) ? 1 : int.Parse(nStr);
            int m = int.Parse(mStr);
            int? k = string.IsNullOrEmpty(kStr) ? null : int.Parse(kStr);
            int modifier = string.IsNullOrEmpty(modStr) ? 0 : int.Parse(modStr);

            // Validações
            if (n < 1 || n > 100)
            {
                return CreateError("INVALID_DICE_COUNT", $"Quantidade de dados deve estar entre 1 e 100 (atual: {n})", originalExpr, userName, ruleset, seed);
            }
            if (m < 2 || m > 1000)
            {
                return CreateError("INVALID_DICE_FACES", $"Faces do dado devem estar entre 2 e 1000 (atual: {m})", originalExpr, userName, ruleset, seed);
            }
            if (k.HasValue && (k.Value < 1 || k.Value > n))
            {
                return CreateError("INVALID_KEEP_COUNT", $"Quantidade a manter deve estar entre 1 e {n} (atual: {k.Value})", originalExpr, userName, ruleset, seed);
            }
            if (rerollValue.HasValue && (rerollValue.Value < 1 || rerollValue.Value > m))
            {
                return CreateError("INVALID_REROLL_VALUE", $"Valor de rerrolagem deve estar entre 1 e {m} (atual: {rerollValue.Value})", originalExpr, userName, ruleset, seed);
            }

            // Executar rolagem
            var block = RollDiceBlock(n, m, k, rerollValue, diceExpr);
            var total = block.Sum + modifier;

            return new RollResult
            {
                User = userName,
                Expression = originalExpr,
                Ruleset = ruleset,
                Seed = seed,
                Blocks = new List<DiceBlock> { block },
                Total = total,
                IsError = false
            };
        }

        private DiceBlock RollDiceBlock(int n, int m, int? keepHighest, int? rerollEquals, string notation)
        {
            var faces = new List<DiceFace>();
            
            for (int i = 0; i < n; i++)
            {
                int value = GetRandomInclusive(1, m);
                bool rerolled = false;
                int? rerolledFrom = null;

                if (rerollEquals.HasValue && value == rerollEquals.Value)
                {
                    rerolledFrom = value;
                    value = GetRandomInclusive(1, m);
                    rerolled = true;
                }

                faces.Add(new DiceFace
                {
                    Value = value,
                    Rerolled = rerolled,
                    RerolledFrom = rerolledFrom
                });
            }

            var keptValues = new List<int>();
            int sum;

            if (keepHighest.HasValue)
            {
                keptValues = faces.Select(f => f.Value).OrderByDescending(v => v).Take(keepHighest.Value).ToList();
                sum = keptValues.Sum();
            }
            else
            {
                keptValues = faces.Select(f => f.Value).ToList();
                sum = keptValues.Sum();
            }

            // Normalizar H para M na notação
            var normalizedNotation = notation.Replace("H", "M", StringComparison.OrdinalIgnoreCase);

            return new DiceBlock
            {
                Notation = normalizedNotation,
                N = n,
                M = m,
                KeepHighest = keepHighest,
                RerollEquals = rerollEquals,
                Faces = faces,
                KeptValues = keptValues,
                Sum = sum
            };
        }

        private RollResult ProcessAttackCommand(string attackExpr, string userName, string ruleset, Guid seed)
        {
            var attackEngine = new AttackEngine(this);
            return attackEngine.ProcessAttack(attackExpr, userName, ruleset, seed);
        }

        private RollResult CreateError(string errorCode, string message, string expression, string userName, string ruleset, Guid seed)
        {
            return new RollResult
            {
                User = userName,
                Expression = expression,
                Ruleset = ruleset,
                Seed = seed,
                Blocks = new List<DiceBlock>(),
                Total = 0,
                IsError = true,
                ErrorCode = errorCode,
                ErrorMessage = message
            };
        }
    }
}

