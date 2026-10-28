using System;
using System.Collections.Generic;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 플레이어 기본값과 장착 효과를 계산하여 반환한다. 저장·결과 보관·알림은 PlayerSystem_New이 담당한다.
    /// PlayerData_New의 수식 문자열을 주스탯으로 계산하고, 장착 보정을 적용한다.
    /// </summary>
    public class StatSystem : SystemBase
    {
        // 기본 계수 미확정: 수식이 없을 때는 이전 합의대로 세부스탯 기본값을 유지한다.
        private const string DefaultDetailFormula = "BaseValue";
        private readonly Dictionary<string, StatFormulaEvaluator> m_formulaCache = new();
        private readonly HashSet<string> m_formulaWarnings = new();

        /// <summary>
        /// 현재 입력에서 전체 항목을 계산하여 반환한다. 결과 보관과 변경 알림은 PlayerSystem_New이 담당한다.
        /// </summary>
        public bool TryCalculateStats(EntityHandle playerHandle, int bonusInventorySize, out StatCalculationResult result)
        {
            result = null;
            var player = EntityManager?.Get<Entity_Player_New>(playerHandle);
            if (player == null)
            {
                return false;
            }

            var calculated = CreateBaseStats(player.BaseData);
            ApplyMainStatFormulas(player, calculated);
            ApplyEquippedModifiers(player, calculated);
            calculated.InventorySize += bonusInventorySize;
            result = calculated;
            return true;
        }

        private static StatCalculationResult CreateBaseStats(PlayerData_New data)
        {
            return new StatCalculationResult
            {
                HealthPoint = data.HealthPoint,
                AttackPoint = data.AttackPoint,
                FlatAttackPoint = data.FlatAttackPoint,
                ArmorPenetration = data.ArmorPenetration,
                MultiHitChance = data.MultiHitChance,
                CriticalChance = data.CriticalChance,
                CriticalDamage = data.CriticalDamage,
                CCDuration = data.CCDuration,
                BiteTime = data.BiteTime,
                HookingTiming = data.HookingTiming,
                SizeModifier = data.SizeModifier,
                RarityProbability = data.RarityProbability,
                GoldGettingMultiply = data.BaseGoldGettingMultiply,
                MapMovementSpeedPerTime = data.BaseMapMovementSpeedPerTime,
                InventorySize = data.BaseInventorySize
            };
        }

        private void ApplyMainStatFormulas(Entity_Player_New player, StatCalculationResult result)
        {
            var formulas = new Dictionary<PlayerStat_New, string>();
            foreach (StatFormula definition in player.BaseData.StatFormulas)
            {
                if (definition == null) continue;
                if (!IsDetailStat(definition.Stat))
                {
                    WarnFormulaOnce(definition.Stat, definition.Formula, "주스탯 수식 대상이 아닌 항목입니다.");
                    continue;
                }
                if (formulas.ContainsKey(definition.Stat))
                {
                    WarnFormulaOnce(definition.Stat, definition.Formula, "중복 수식은 무시하고 첫 번째 정의를 사용합니다.");
                    continue;
                }
                formulas.Add(definition.Stat, definition.Formula);
            }

            result.HealthPoint = CalculateDetailStat(player, formulas, PlayerStat_New.HealthPoint, result.HealthPoint);
            result.AttackPoint = CalculateDetailStat(player, formulas, PlayerStat_New.AttackPoint, result.AttackPoint);
            result.FlatAttackPoint = CalculateDetailStat(player, formulas, PlayerStat_New.FlatAttackPoint, result.FlatAttackPoint);
            result.ArmorPenetration = CalculateDetailStat(player, formulas, PlayerStat_New.ArmorPenetration, result.ArmorPenetration);
            result.MultiHitChance = CalculateDetailStat(player, formulas, PlayerStat_New.MultiHitChance, result.MultiHitChance);
            result.CriticalChance = CalculateDetailStat(player, formulas, PlayerStat_New.CriticalChance, result.CriticalChance);
            result.CriticalDamage = CalculateDetailStat(player, formulas, PlayerStat_New.CriticalDamage, result.CriticalDamage);
            result.CCDuration = CalculateDetailStat(player, formulas, PlayerStat_New.CCDuration, result.CCDuration);
            result.BiteTime = CalculateDetailStat(player, formulas, PlayerStat_New.BiteTime, result.BiteTime);
            result.HookingTiming = CalculateDetailStat(player, formulas, PlayerStat_New.HookingTiming, result.HookingTiming);
            result.SizeModifier = CalculateDetailStat(player, formulas, PlayerStat_New.SizeModifier, result.SizeModifier);
            result.RarityProbability = CalculateDetailStat(player, formulas, PlayerStat_New.RarityProbability, result.RarityProbability);
        }

        private float CalculateDetailStat(Entity_Player_New player, Dictionary<PlayerStat_New, string> formulas, PlayerStat_New stat, float baseValue)
        {
            formulas.TryGetValue(stat, out string expression);
            if (string.IsNullOrWhiteSpace(expression))
            {
                WarnFormulaOnce(stat, expression, "수식이 없어 기본 수식을 사용합니다.");
            }
            else if (TryEvaluateFormula(expression.Trim(), player, baseValue, out float value, out string error))
            {
                return value;
            }
            else
            {
                WarnFormulaOnce(stat, expression, $"수식 적용 실패: {error} 기본 수식을 사용합니다.");
            }

            if (TryEvaluateFormula(DefaultDetailFormula, player, baseValue, out float fallback, out string fallbackError)) return fallback;
            WarnFormulaOnce(stat, DefaultDetailFormula, $"기본 수식 적용 실패: {fallbackError} 원본 기본값을 유지합니다.");
            return baseValue;
        }

        private bool TryEvaluateFormula(string expression, Entity_Player_New player, float baseValue, out float value, out string error)
        {
            value = 0f;
            if (!m_formulaCache.TryGetValue(expression, out StatFormulaEvaluator evaluator))
            {
                if (!StatFormulaEvaluator.TryCompile(expression, out evaluator, out error)) return false;
                m_formulaCache.Add(expression, evaluator);
            }
            return evaluator.TryEvaluate(player.Str, player.Tec, player.Luk, baseValue, out value, out error);
        }

        private void WarnFormulaOnce(PlayerStat_New stat, string expression, string reason)
        {
            string key = $"{stat}|{expression}|{reason}";
            if (m_formulaWarnings.Add(key)) Debug.LogWarning($"[StatSystem] {stat}: {reason} 수식: {expression}");
        }

        private static bool IsDetailStat(PlayerStat_New stat)
        {
            switch (stat)
            {
                case PlayerStat_New.HealthPoint:
                case PlayerStat_New.AttackPoint:
                case PlayerStat_New.FlatAttackPoint:
                case PlayerStat_New.ArmorPenetration:
                case PlayerStat_New.MultiHitChance:
                case PlayerStat_New.CriticalChance:
                case PlayerStat_New.CriticalDamage:
                case PlayerStat_New.CCDuration:
                case PlayerStat_New.BiteTime:
                case PlayerStat_New.HookingTiming:
                case PlayerStat_New.SizeModifier:
                case PlayerStat_New.RarityProbability:
                    return true;
                default:
                    return false;
            }
        }

        private void ApplyEquippedModifiers(Entity_Player_New player, StatCalculationResult result)
        {
            foreach (EntityHandle equipmentHandle in player.Equipped.Values)
            {
                if (equipmentHandle.Value == Guid.Empty) continue;

                Entity entity = EntityManager.Get(equipmentHandle);
                StatModifier[] modifiers = null;
                if (entity is Entity_Equipment equipment && equipment.ItemData != null)
                {
                    modifiers = equipment.CurrentModifiers;
                }
                else if (entity is Entity_Consumables consumable && consumable.ItemData != null)
                {
                    modifiers = consumable.ItemData.Modifiers;
                }

                if (modifiers != null)
                {
                    ApplyModifiers(result, ConvertLegacyModifiers(modifiers));
                }
            }
        }

        private static IEnumerable<StatModifier_New> ConvertLegacyModifiers(StatModifier[] modifiers)
        {
            foreach (StatModifier modifier in modifiers)
            {
                if (modifier == null) continue;
                if (LegacyStatModifierAdapter.TryConvert(modifier, out var converted))
                {
                    yield return converted;
                }
                else
                {
                    Debug.LogWarning($"[StatSystem] 기존 스탯 보정을 변환할 수 없습니다: {modifier.Stat}, {modifier.Operation}");
                }
            }
        }

        private static void ApplyModifiers(StatCalculationResult result, IEnumerable<StatModifier_New> modifiers)
        {
            foreach (StatModifier_New stat in modifiers)
            {
                if (stat == null) continue;
                if (stat.Operation != ModifierOperation_New.Add)
                {
                    Debug.LogWarning($"[StatSystem] 미지원 연산 {stat.Operation}은 적용하지 않습니다. 연산 규칙 연결이 필요합니다.");
                    continue;
                }

                if (float.IsNaN(stat.Value) || float.IsInfinity(stat.Value))
                {
                    Debug.LogWarning($"[StatSystem] 유효하지 않은 스탯 보정은 적용하지 않습니다: {stat.Stat}");
                    continue;
                }

                switch (stat.Stat)
                {
                    case PlayerStat_New.HealthPoint:
                        result.HealthPoint += stat.Value;
                        break;
                    case PlayerStat_New.AttackPoint:
                        result.AttackPoint += stat.Value;
                        break;
                    case PlayerStat_New.FlatAttackPoint:
                        result.FlatAttackPoint += stat.Value;
                        break;
                    case PlayerStat_New.ArmorPenetration:
                        result.ArmorPenetration += stat.Value;
                        break;
                    case PlayerStat_New.MultiHitChance:
                        result.MultiHitChance += stat.Value;
                        break;
                    case PlayerStat_New.CriticalChance:
                        result.CriticalChance += stat.Value;
                        break;
                    case PlayerStat_New.CriticalDamage:
                        result.CriticalDamage += stat.Value;
                        break;
                    case PlayerStat_New.CCDuration:
                        result.CCDuration += stat.Value;
                        break;
                    case PlayerStat_New.BiteTime:
                        result.BiteTime += stat.Value;
                        break;
                    case PlayerStat_New.HookingTiming:
                        result.HookingTiming += stat.Value;
                        break;
                    case PlayerStat_New.SizeModifier:
                        result.SizeModifier += stat.Value;
                        break;
                    case PlayerStat_New.RarityProbability:
                        result.RarityProbability += stat.Value;
                        break;
                    case PlayerStat_New.GoldGettingMultiply:
                        result.GoldGettingMultiply += stat.Value;
                        break;
                    case PlayerStat_New.MapMovementSpeedPerTime:
                        result.MapMovementSpeedPerTime += stat.Value;
                        break;
                    case PlayerStat_New.InventorySize:
                        result.InventorySize += (int)stat.Value;
                        break;
                    default:
                        Debug.LogWarning($"[StatSystem] 지원하지 않는 스탯 보정: {stat.Stat}");
                        break;
                }
            }
        }

    }
}
