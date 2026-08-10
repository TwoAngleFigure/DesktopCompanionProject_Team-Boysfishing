using DesktopCompanion.Data;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 낚시 대상, 성급, 크기를 추첨한다.
    /// 난수 함수를 교체하면 동일한 입력을 결정론적으로 검증할 수 있다.
    /// </summary>
    public sealed class FishingCatchRoller
    {
        private readonly Func<float, float, float> m_floatRange;
        private readonly Func<int, int, int> m_intRange;
        private readonly float[] m_qualityWeights = new float[5];
        private readonly float[] m_rarityWeights = new float[5];
        private readonly List<BattleFishData>[] m_fishByRarity =
        {
            new(),
            new(),
            new(),
            new(),
            new()
        };

        public FishingCatchRoller(
            Func<float, float, float> floatRange = null,
            Func<int, int, int> intRange = null)
        {
            m_floatRange = floatRange ??
                ((min, max) => UnityEngine.Random.Range(min, max));
            m_intRange = intRange ??
                ((min, max) => UnityEngine.Random.Range(min, max));
        }

        public TierPool SelectHighestTierPool(IReadOnlyList<TierPool> pools)
        {
            TierPool selectedPool = null;

            foreach (TierPool pool in pools)
            {
                if (pool == null)
                {
                    continue;
                }

                if (selectedPool == null || pool.Tier > selectedPool.Tier)
                {
                    selectedPool = pool;
                }
            }

            return selectedPool;
        }

        public BattleFishData SelectBattleFishFromTierPool(
            TierPool pool,
            float groundbaitStat,
            Action<string> debugLog = null)
        {
            if (pool == null || pool.Entries == null || pool.Entries.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < m_fishByRarity.Length; i++)
            {
                m_fishByRarity[i].Clear();
            }

            foreach (FishPoolEntry entry in pool.Entries)
            {
                if (entry == null || entry.Fish == null)
                {
                    continue;
                }

                int rarityIndex = (int)entry.Fish.ItemFish.Rarity;
                m_fishByRarity[rarityIndex].Add(entry.Fish);
            }

            for (int i = 0; i < m_fishByRarity.Length; i++)
            {
                m_rarityWeights[i] = m_fishByRarity[i].Count == 0
                    ? 0f
                    : FishingWeightCalculator.CalculateRarityWeight(
                        (ItemRarity)i,
                        groundbaitStat);
            }

            int selectedRarityIndex = SelectWeightedIndex(
                m_rarityWeights,
                out float totalWeight,
                out float randomValue);

            if (selectedRarityIndex < 0)
            {
                return null;
            }

            List<BattleFishData> selectedRarityFish =
                m_fishByRarity[selectedRarityIndex];
            int fishIndex = m_intRange(0, selectedRarityFish.Count);
            BattleFishData selectedFish = selectedRarityFish[fishIndex];
            ItemRarity selectedRarity = (ItemRarity)selectedRarityIndex;

            debugLog?.Invoke(
                $"[희귀도 추첨] groundbaitStat={groundbaitStat:0.##}, tier={pool.Tier}, " +
                $"weights=" +
                $"Normal:{FormatWeight(m_rarityWeights[0], totalWeight)}, " +
                $"Uncommon:{FormatWeight(m_rarityWeights[1], totalWeight)}, " +
                $"Rare:{FormatWeight(m_rarityWeights[2], totalWeight)}, " +
                $"Epic:{FormatWeight(m_rarityWeights[3], totalWeight)}, " +
                $"Legendary:{FormatWeight(m_rarityWeights[4], totalWeight)}, " +
                $"roll={randomValue:0.###}/{totalWeight:0.###}, " +
                $"selected={selectedRarity}, candidates={selectedRarityFish.Count}, " +
                $"fish={GetFishDebugName(selectedFish)}");

            return selectedFish;
        }

        public ItemQuality RollFishQuality(
            float baitStat,
            Action<string> debugLog = null)
        {
            for (int i = 0; i < m_qualityWeights.Length; i++)
            {
                m_qualityWeights[i] =
                    FishingWeightCalculator.CalculateQualityWeight(
                        (ItemQuality)(i + 1),
                        baitStat);
            }

            int selectedIndex = SelectWeightedIndex(
                m_qualityWeights,
                out float totalWeight,
                out float randomValue);

            ItemQuality selectedQuality = selectedIndex >= 0
                ? (ItemQuality)(selectedIndex + 1)
                : ItemQuality.FiveStar;

            debugLog?.Invoke(
                $"[성급 추첨] baitStat={baitStat:0.##}, " +
                $"weights=" +
                $"1성:{FormatWeight(m_qualityWeights[0], totalWeight)}, " +
                $"2성:{FormatWeight(m_qualityWeights[1], totalWeight)}, " +
                $"3성:{FormatWeight(m_qualityWeights[2], totalWeight)}, " +
                $"4성:{FormatWeight(m_qualityWeights[3], totalWeight)}, " +
                $"5성:{FormatWeight(m_qualityWeights[4], totalWeight)}, " +
                $"roll={randomValue:0.###}/{totalWeight:0.###}, selected={selectedQuality}");

            return selectedQuality;
        }

        public float RollFishSize(BattleFishData fishData, ItemQuality quality)
        {
            fishData.GetSizeRange(
                quality,
                out float minSize,
                out float maxSize);

            return RollSizeInRange(
                minSize,
                maxSize,
                quality == ItemQuality.FiveStar);
        }

        public float RollSizeInRange(
            float minSize,
            float maxSize,
            bool includeMaxSize)
        {
            int minSizeStep = Mathf.RoundToInt(minSize * 10f);
            int maxSizeStepExclusive = Mathf.RoundToInt(maxSize * 10f) +
                (includeMaxSize ? 1 : 0);

            int selectedSizeStep = m_intRange(
                minSizeStep,
                maxSizeStepExclusive);

            return selectedSizeStep / 10f;
        }

        public int SelectWeightedIndex(IReadOnlyList<float> weights)
        {
            return SelectWeightedIndex(weights, out _, out _);
        }

        private int SelectWeightedIndex(
            IReadOnlyList<float> weights,
            out float totalWeight,
            out float randomValue)
        {
            totalWeight = 0f;

            for (int i = 0; i < weights.Count; i++)
            {
                totalWeight += weights[i];
            }

            if (totalWeight <= 0f)
            {
                randomValue = 0f;
                return -1;
            }

            randomValue = m_floatRange(0f, totalWeight);
            float accumulatedWeight = 0f;

            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] == 0f)
                {
                    continue;
                }

                accumulatedWeight += weights[i];

                if (randomValue <= accumulatedWeight)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string FormatWeight(float weight, float totalWeight)
        {
            float probability = weight / totalWeight * 100f;
            return $"{weight:0.###}({probability:0.00}%)";
        }

        private static string GetFishDebugName(BattleFishData fishData)
        {
            return fishData == null
                ? "없음"
                : $"{fishData.Name}(id={fishData.ID})";
        }
    }
}
