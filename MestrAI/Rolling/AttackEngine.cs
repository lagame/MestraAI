using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RPGSessionManager.Rolling
{
    /// <summary>
    /// Engine especializado para comando #attack e sistema de críticos D&D 5e/3.5.
    /// </summary>
    public class AttackEngine
    {
        private readonly RollEngine _rollEngine;

        public AttackEngine(RollEngine rollEngine)
        {
            _rollEngine = rollEngine;
        }

        /// <summary>
        /// Processa comando #attack <atkExpr> vs <alvo> dmg <dmgExpr> [crit <critSpec>]
        /// </summary>
        public RollResult ProcessAttack(string attackExpr, string userName, string ruleset, Guid seed)
        {
            try
            {
                var match = Regex.Match(attackExpr, 
                    @"attack\s+(.+?)\s+vs\s+(\d+)\s+dmg\s+(.+?)(?:\s+crit\s+(.+?))?$", 
                    RegexOptions.IgnoreCase);

                if (!match.Success)
                {
                    return CreateError("INVALID_ATTACK_SYNTAX", 
                        "Sintaxe: #attack <atkExpr> vs <alvo> dmg <dmgExpr> [crit <critSpec>]", 
                        $"#{attackExpr}", userName, ruleset, seed);
                }

                var atkExpr = match.Groups[1].Value.Trim();
                var targetAC = int.Parse(match.Groups[2].Value);
                var dmgExpr = match.Groups[3].Value.Trim();
                var critSpec = match.Groups[4].Success ? match.Groups[4].Value.Trim() : null;

                // Rolar ataque
                var attackRoll = RollAttackExpression(atkExpr, userName, ruleset, seed);
                if (attackRoll.IsError)
                {
                    return attackRoll;
                }

                // Verificar acerto e crítico
                var attackResult = EvaluateAttack(attackRoll, targetAC, critSpec, ruleset);
                
                // Rolar dano se acertou
                RollResult? damageRoll = null;
                if (attackResult.Hit)
                {
                    damageRoll = RollDamageExpression(dmgExpr, attackResult.Critical, critSpec, userName, ruleset, seed);
                }

                // Combinar resultados
                return CombineAttackResults(attackRoll, damageRoll, attackResult, userName, ruleset, seed);
            }
            catch (Exception ex)
            {
                return CreateError("ATTACK_ERROR", $"Erro no ataque: {ex.Message}", $"#{attackExpr}", userName, ruleset, seed);
            }
        }

        private RollResult RollAttackExpression(string atkExpr, string userName, string ruleset, Guid seed)
        {
            // Processar expressão de ataque (ex: 1d20+7)
            return _rollEngine.ProcessRoll($"#{atkExpr}", userName, ruleset);
        }

        private RollResult RollDamageExpression(string dmgExpr, bool isCritical, string? critSpec, string userName, string ruleset, Guid seed)
        {
            if (!isCritical)
            {
                return _rollEngine.ProcessRoll($"#{dmgExpr}", userName, ruleset);
            }

            // Aplicar regras de crítico
            if (ruleset == "5e")
            {
                return RollCritical5e(dmgExpr, userName, ruleset, seed);
            }
            else if (ruleset == "3.5")
            {
                return RollCritical35(dmgExpr, critSpec, userName, ruleset, seed);
            }

            return _rollEngine.ProcessRoll($"#{dmgExpr}", userName, ruleset);
        }

        private RollResult RollCritical5e(string dmgExpr, string userName, string ruleset, Guid seed)
        {
            // 5e: dados dobrados + modificador uma vez
            // Ex: 1d8+4 vira 2d8+4
            var diceMatch = Regex.Match(dmgExpr, @"(\d*)d(\d+)(?:\+(\d+))?");
            if (!diceMatch.Success)
            {
                return _rollEngine.ProcessRoll($"#{dmgExpr}", userName, ruleset);
            }

            var nStr = diceMatch.Groups[1].Value;
            var faces = diceMatch.Groups[2].Value;
            var modStr = diceMatch.Groups[3].Value;

            int n = string.IsNullOrEmpty(nStr) ? 1 : int.Parse(nStr);
            int modifier = string.IsNullOrEmpty(modStr) ? 0 : int.Parse(modStr);

            // Dobrar os dados
            var critExpr = $"{n * 2}d{faces}";
            if (modifier > 0)
            {
                critExpr += $"+{modifier}";
            }

            return _rollEngine.ProcessRoll($"#{critExpr}", userName, ruleset);
        }

        private RollResult RollCritical35(string dmgExpr, string? critSpec, string userName, string ruleset, Guid seed)
        {
            // 3.5: multiplicador aplicado (ex: x2, x3)
            int multiplier = 2; // padrão

            if (!string.IsNullOrEmpty(critSpec))
            {
                var multMatch = Regex.Match(critSpec, @"x(\d+)$");
                if (multMatch.Success)
                {
                    multiplier = int.Parse(multMatch.Groups[1].Value);
                }
            }

            // Rolar dano base múltiplas vezes
            var results = new List<RollResult>();
            for (int i = 0; i < multiplier; i++)
            {
                var roll = _rollEngine.ProcessRoll($"#{dmgExpr}", userName, ruleset);
                if (roll.IsError)
                {
                    return roll;
                }
                results.Add(roll);
            }

            // Somar todos os resultados
            var totalDamage = results.Sum(r => r.Total);
            var allBlocks = results.SelectMany(r => r.Blocks).ToList();

            return new RollResult
            {
                User = userName,
                Expression = $"{dmgExpr} (crítico x{multiplier})",
                Ruleset = ruleset,
                Seed = seed,
                Blocks = allBlocks,
                Total = totalDamage,
                IsError = false
            };
        }

        private AttackResult EvaluateAttack(RollResult attackRoll, int targetAC, string? critSpec, string ruleset)
        {
            // Encontrar o primeiro d20 para verificar natural
            var firstD20Block = attackRoll.Blocks.FirstOrDefault(b => b.M == 20);
            int naturalRoll = firstD20Block?.Faces.FirstOrDefault()?.Value ?? 0;

            bool hit = attackRoll.Total >= targetAC;
            bool critical = false;

            if (ruleset == "5e")
            {
                // 5e: natural 20 é sempre crítico e acerto automático
                if (naturalRoll == 20)
                {
                    critical = true;
                    hit = true;
                }
                
                // Verificar faixa expandida (ex: Champion 19-20)
                if (!string.IsNullOrEmpty(critSpec) && critSpec.Contains("19-20"))
                {
                    if (naturalRoll >= 19)
                    {
                        critical = true;
                        hit = true;
                    }
                }
            }
            else if (ruleset == "3.5")
            {
                // 3.5: verificar ameaça e confirmação
                var threatRange = GetThreatRange(critSpec);
                if (naturalRoll >= threatRange.min && naturalRoll <= threatRange.max)
                {
                    // Ameaça de crítico - precisa confirmar
                    // TODO: Implementar confirmação
                    critical = true; // Simplificado por enquanto
                }
            }

            return new AttackResult
            {
                Hit = hit,
                Critical = critical,
                NaturalRoll = naturalRoll,
                TotalRoll = attackRoll.Total
            };
        }

        private (int min, int max) GetThreatRange(string? critSpec)
        {
            if (string.IsNullOrEmpty(critSpec))
            {
                return (20, 20); // Padrão 20/x2
            }

            var rangeMatch = Regex.Match(critSpec, @"(\d+)-(\d+)");
            if (rangeMatch.Success)
            {
                return (int.Parse(rangeMatch.Groups[1].Value), int.Parse(rangeMatch.Groups[2].Value));
            }

            return (20, 20);
        }

        private RollResult CombineAttackResults(RollResult attackRoll, RollResult? damageRoll, AttackResult attackResult, string userName, string ruleset, Guid seed)
        {
            var allBlocks = new List<DiceBlock>(attackRoll.Blocks);
            if (damageRoll != null && !damageRoll.IsError)
            {
                allBlocks.AddRange(damageRoll.Blocks);
            }

            var totalDamage = damageRoll?.Total ?? 0;
            var hitText = attackResult.Hit ? "ACERTO" : "ERRO";
            var critText = attackResult.Critical ? " CRÍTICO" : "";

            var expression = $"Ataque: {attackRoll.Total} vs AC - {hitText}{critText}";
            if (attackResult.Hit && damageRoll != null)
            {
                expression += $" - Dano: {totalDamage}";
            }

            return new RollResult
            {
                User = userName,
                Expression = expression,
                Ruleset = ruleset,
                Seed = seed,
                Blocks = allBlocks,
                Total = totalDamage,
                IsError = false
            };
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

    /// <summary>
    /// Resultado de uma avaliação de ataque.
    /// </summary>
    public class AttackResult
    {
        public bool Hit { get; init; }
        public bool Critical { get; init; }
        public int NaturalRoll { get; init; }
        public int TotalRoll { get; init; }
    }
}

