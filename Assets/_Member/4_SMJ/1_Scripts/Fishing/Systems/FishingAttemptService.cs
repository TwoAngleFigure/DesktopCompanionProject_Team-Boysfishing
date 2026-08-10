using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    public sealed class FishingAttemptService
    {
        private readonly EntityManager m_entityManager;
        private readonly StageSystem m_stageSystem;
        private readonly PlayerSystem m_playerSystem;
        private readonly FishingCatchRoller m_catchRoller;
        private readonly FishingTimingCalculator m_timingCalculator;
        private readonly float m_baseBattleDuration;
        private readonly float m_minBattleDuration;
        private readonly Action<string> m_debugLog;

        public FishingAttemptService(
            EntityManager entityManager,
            StageSystem stageSystem,
            PlayerSystem playerSystem,
            FishingCatchRoller catchRoller,
            FishingTimingCalculator timingCalculator,
            float baseBattleDuration,
            float minBattleDuration,
            Action<string> debugLog)
        {
            m_entityManager = entityManager;
            m_stageSystem = stageSystem;
            m_playerSystem = playerSystem;
            m_catchRoller = catchRoller;
            m_timingCalculator = timingCalculator;
            m_baseBattleDuration = baseBattleDuration;
            m_minBattleDuration = minBattleDuration;
            m_debugLog = debugLog;
        }

        public FishingAttemptStartResult StartAttempt()
        {
            PrepareConsumables(
                out float appliedBaitStat,
                out float appliedGroundbaitStat,
                out BattleFishData summonTarget);

            BattleFishData fishData = SelectBattleFish(
                summonTarget,
                appliedGroundbaitStat);

            if (fishData == null)
            {
                Debug.LogWarning(
                    "[FishingSystem] 전투 시작 실패: 선택 가능한 BattleFishData가 없습니다.");
                return FishingAttemptStartResult.Failed();
            }

            EntityHandle battleFishHandle =
                m_entityManager.Create<BattleFishData>(fishData.ID);
            Entity_BattleFish battleFish =
                m_entityManager.Get<Entity_BattleFish>(battleFishHandle);

            if (battleFish == null)
            {
                Debug.LogWarning(
                    $"[FishingSystem] Entity_BattleFish 생성 실패: id={fishData.ID}");
                m_entityManager.Destroy(battleFishHandle);
                return FishingAttemptStartResult.Failed();
            }

            ItemQuality quality = m_catchRoller.RollFishQuality(
                appliedBaitStat,
                m_debugLog);
            float rolledSize = m_catchRoller.RollFishSize(fishData, quality);
            float size = Mathf.Round(rolledSize * 10f) / 10f;

            battleFish.SetRollResult(size, quality);

            m_debugLog?.Invoke(
                $"[최종 결과] fish={fishData.Name}(id={fishData.ID}), " +
                $"rarity={fishData.ItemFish.Rarity}, quality={quality}, size={size:0.0}, " +
                $"baitStat={appliedBaitStat:0.##}, " +
                $"groundbaitStat={appliedGroundbaitStat:0.##}");

            float battleDuration = CalculateBattleDuration(fishData, size);
            var attempt = new FishingAttempt(
                fishData,
                size,
                quality,
                battleDuration,
                appliedBaitStat,
                appliedGroundbaitStat);

            return FishingAttemptStartResult.Success(attempt, battleFishHandle);
        }

        public float CalculateNextFishingDelay()
        {
            int playerLicense = GetPlayerLicense();
            List<TierPool> pools =
                m_stageSystem.GetAvailableTierPools(playerLicense);
            TierPool selectedPool =
                m_catchRoller.SelectHighestTierPool(pools);

            return m_timingCalculator.CalculateNextFishingDelay(
                m_playerSystem.BaseAutoBattleCooltime,
                selectedPool.RegionResistance);
        }

        private void PrepareConsumables(
            out float appliedBaitStat,
            out float appliedGroundbaitStat,
            out BattleFishData summonTarget)
        {
            appliedBaitStat = m_playerSystem != null
                ? m_playerSystem.BaseProbabilityAtFishSize
                : 0f;
            appliedGroundbaitStat = m_playerSystem != null
                ? m_playerSystem.BaseProbabilityAtFishRarity
                : 0f;
            summonTarget = null;

            bool consumedBait = false;
            bool consumedGroundbait = false;
            ItemData consumedBaitData = null;
            ItemData consumedGroundbaitData = null;

            if (m_playerSystem != null)
            {
                consumedBait = m_playerSystem.TryConsumeEquippedItem(
                    EquipmentMountingArea.Bait,
                    out consumedBaitData);

                if (consumedBait &&
                    consumedBaitData is ItemData_Consumables baitData)
                {
                    summonTarget = baitData.SummonTarget;
                }

                if (summonTarget == null)
                {
                    consumedGroundbait = m_playerSystem.TryConsumeEquippedItem(
                        EquipmentMountingArea.Groundbait,
                        out consumedGroundbaitData);
                }
            }

            string groundbaitResult = summonTarget != null
                ? "소비 생략(보스 미끼 우선)"
                : $"consumed={consumedGroundbait}, " +
                  $"item={GetItemDebugName(consumedGroundbaitData)}";

            m_debugLog?.Invoke(
                $"[전투 시작 소모품 적용] baitConsumed={consumedBait}, " +
                $"bait={GetItemDebugName(consumedBaitData)}, " +
                $"baitStat={appliedBaitStat:0.##}, " +
                $"groundbait={groundbaitResult}, " +
                $"groundbaitStat={appliedGroundbaitStat:0.##}, " +
                $"summonTarget={GetFishDebugName(summonTarget)}");
        }

        private BattleFishData SelectBattleFish(
            BattleFishData summonTarget,
            float appliedGroundbaitStat)
        {
            if (summonTarget != null)
            {
                m_debugLog?.Invoke(
                    $"[보스 선택] 일반 희귀도 추첨 생략, " +
                    $"target={GetFishDebugName(summonTarget)}, " +
                    $"rarity={summonTarget.ItemFish.Rarity}");
                return summonTarget;
            }

            if (m_stageSystem == null)
            {
                Debug.LogWarning(
                    "[FishingSystem] StageSystem을 찾을 수 없습니다.");
                return null;
            }

            int playerLicense = GetPlayerLicense();
            List<TierPool> availablePools =
                m_stageSystem.GetAvailableTierPools(playerLicense);

            if (availablePools == null || availablePools.Count == 0)
            {
                Debug.LogWarning(
                    $"[FishingSystem] 사용 가능한 TierPool이 없습니다. " +
                    $"playerLicense={playerLicense}");
                return null;
            }

            TierPool selectedPool =
                m_catchRoller.SelectHighestTierPool(availablePools);
            BattleFishData selectedFish =
                m_catchRoller.SelectBattleFishFromTierPool(
                    selectedPool,
                    appliedGroundbaitStat,
                    m_debugLog);

            if (selectedFish != null)
            {
                Debug.Log(
                    $"[FishingSystem] StageSystem TierPool 사용: " +
                    $"tier={selectedPool.Tier}, fish={selectedFish.Name}");
            }

            return selectedFish;
        }

        private float CalculateBattleDuration(
            BattleFishData fishData,
            float fishSize)
        {
            if (m_playerSystem == null)
            {
                Debug.Log(
                    "[FishingSystem] PlayerSystem을 찾지 못해 기본 전투 시간을 사용합니다.");
                return m_baseBattleDuration;
            }

            return m_timingCalculator.CalculateBattleDuration(
                m_baseBattleDuration,
                m_minBattleDuration,
                m_playerSystem.BaseBattleTimeVariable,
                fishData.BattleTimeVariable,
                fishData.MinSize,
                fishData.MaxSize,
                fishSize);
        }

        private int GetPlayerLicense()
        {
            return m_playerSystem == null ? 0 : m_playerSystem.StartingLicense;
        }

        private static string GetItemDebugName(ItemData itemData)
        {
            return itemData == null
                ? "없음"
                : $"{itemData.Name}(id={itemData.ID})";
        }

        private static string GetFishDebugName(BattleFishData fishData)
        {
            return fishData == null
                ? "없음"
                : $"{fishData.Name}(id={fishData.ID})";
        }
    }
}
