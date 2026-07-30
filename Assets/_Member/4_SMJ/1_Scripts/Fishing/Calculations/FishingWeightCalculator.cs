using DesktopCompanion.Data;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 미끼와 떡밥이 적용되는 단계별 가중치를 계산한다.
    /// </summary>
    internal static class FishingWeightCalculator
    {
        private const float BaitStatScale = 0.01f;

        private static readonly float[] s_baseQualityWeights =
        {
            40f,
            30f,
            15f,
            10f,
            5f
        };

        public static float CalculateQualityWeight(
            ItemQuality quality,
            float baitStat)
        {
            int qualityIndex = (int)quality - 1;
            float baseWeight = s_baseQualityWeights[qualityIndex];

            return CalculateProgressiveWeight(
                baseWeight,
                qualityIndex,
                baitStat,
                BaitStatScale);
        }

        public static float CalculateProgressiveWeight(
            float baseWeight,
            int stepIndex,
            float stat,
            float statScale)
        {
            float stepMultiplier = 1f + stat * statScale;

            return baseWeight * Mathf.Pow(stepMultiplier, stepIndex);
        }
    }
}
