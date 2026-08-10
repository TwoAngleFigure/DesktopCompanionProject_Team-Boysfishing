using System;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 낚시 전투의 수동 공격 피해를 계산한다.
    /// </summary>
    public sealed class FishingCombatCalculator
    {
        private readonly Func<float> m_randomValue;

        public FishingCombatCalculator(Func<float> randomValue = null)
        {
            m_randomValue = randomValue ?? (() => UnityEngine.Random.value);
        }

        public int CalculateManualDamage(
            int damagePerClick,
            float manualDamageMultiplier,
            float criticalChance,
            float criticalMultiplier)
        {
            float damage = damagePerClick * manualDamageMultiplier;
            bool isCritical = m_randomValue() < criticalChance;

            if (isCritical)
            {
                damage *= criticalMultiplier;
            }

            return Mathf.Max(1, Mathf.RoundToInt(damage));
        }
    }
}
