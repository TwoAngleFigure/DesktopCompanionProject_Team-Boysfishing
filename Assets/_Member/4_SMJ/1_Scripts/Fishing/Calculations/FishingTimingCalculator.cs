using System;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 낚시 전투 제한시간과 다음 입질 대기시간을 계산한다.
    /// </summary>
    public sealed class FishingTimingCalculator
    {
        private readonly Func<float, float, float> m_floatRange;

        public FishingTimingCalculator(
            Func<float, float, float> floatRange = null)
        {
            m_floatRange = floatRange ??
                ((min, max) => UnityEngine.Random.Range(min, max));
        }

        public float CalculateBattleDuration(
            float baseBattleDuration,
            float minBattleDuration,
            float playerBattleTimeVariable,
            float fishBattleTimeVariable,
            float fishMinSize,
            float fishMaxSize,
            float fishSize)
        {
            float variableRatio =
                playerBattleTimeVariable / fishBattleTimeVariable;
            float normalizedSize = Mathf.InverseLerp(
                fishMinSize,
                fishMaxSize,
                fishSize);
            float sizeRatio = Mathf.Lerp(1.3f, 0.7f, normalizedSize);
            float duration =
                baseBattleDuration * variableRatio * sizeRatio;

            return Mathf.Max(minBattleDuration, duration);
        }

        public float CalculateNextFishingDelay(
            float autoBattleCooltime,
            int regionResistance)
        {
            float calculatedCooldown =
                30f - (autoBattleCooltime - regionResistance) * 5f;
            float randomMultiplier = m_floatRange(0.8f, 1.2f);
            float randomizedCooldown =
                calculatedCooldown * randomMultiplier;

            return Mathf.Clamp(randomizedCooldown, 3f, 60f);
        }
    }
}
