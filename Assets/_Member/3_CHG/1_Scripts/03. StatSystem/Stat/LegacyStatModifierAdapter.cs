using DesktopCompanion.Data;

namespace DesktopCompanion.Systems
{
    /// <summary>기존 장비·소모품 정의를 새 식별자로 변환하는 경계. 원본 데이터는 변경하지 않는다.</summary>
    internal static class LegacyStatModifierAdapter
    {
        internal static bool TryConvert(StatModifier source, out StatModifier_New result)
        {
            result = null;
            if (source == null || float.IsNaN(source.Value) || float.IsInfinity(source.Value)) return false;

            float value = source.Value;
            PlayerStat_New stat;
            switch (source.Stat)
            {
                case PlayerStat.DamagePerClick: stat = PlayerStat_New.AttackPoint; value = (int)value; break;
                case PlayerStat.BattleTimeVariable: stat = PlayerStat_New.HealthPoint; value = (int)value; break;
                case PlayerStat.CriticalChance: stat = PlayerStat_New.CriticalChance; break;
                case PlayerStat.CriticalMultiply: stat = PlayerStat_New.CriticalDamage; break;
                case PlayerStat.AutoBattleCooltime: stat = PlayerStat_New.BiteTime; value = -value; break;
                case PlayerStat.MapMovementSpeedPerTime: stat = PlayerStat_New.MapMovementSpeedPerTime; break;
                case PlayerStat.InventorySize: stat = PlayerStat_New.InventorySize; break;
                case PlayerStat.ProbabilityAtFishSize: stat = PlayerStat_New.SizeModifier; break;
                case PlayerStat.ProbabilityAtFishRarity: stat = PlayerStat_New.RarityProbability; break;
                case PlayerStat.GoldGettingMultiply: stat = PlayerStat_New.GoldGettingMultiply; break;
                default: return false;
            }

            ModifierOperation_New operation;
            switch (source.Operation)
            {
                case ModifierOperation.Add: operation = ModifierOperation_New.Add; break;
                case ModifierOperation.Multiply: operation = ModifierOperation_New.Multiply; break;
                default: return false;
            }
            result = new StatModifier_New(stat, operation, value);
            return true;
        }
    }
}
