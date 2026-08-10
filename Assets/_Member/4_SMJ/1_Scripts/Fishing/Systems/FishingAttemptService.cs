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
        private readonly Action<string> m_warningLog;

        public bool IsReady =>
            m_entityManager != null &&
            m_stageSystem != null &&
            m_playerSystem != null &&
            m_catchRoller != null &&
            m_timingCalculator != null;

        public FishingAttemptService(
            EntityManager entityManager,
            StageSystem stageSystem,
            PlayerSystem playerSystem,
            FishingCatchRoller catchRoller,
            FishingTimingCalculator timingCalculator,
            float baseBattleDuration,
            float minBattleDuration,
            Action<string> debugLog,
            Action<string> warningLog = null)
        {
            m_entityManager = entityManager;
            m_stageSystem = stageSystem;
            m_playerSystem = playerSystem;
            m_catchRoller = catchRoller;
            m_timingCalculator = timingCalculator;
            m_baseBattleDuration = baseBattleDuration;
            m_minBattleDuration = minBattleDuration;
            m_debugLog = debugLog;
            m_warningLog = warningLog ?? Debug.LogWarning;
        }

        public FishingAttemptStartResult StartAttempt()
        {
            if (!IsReady)
            {
                m_warningLog(
                    "[FishingSystem] 전투 시작 실패: 필수 시스템을 사용할 수 없습니다.");
                return FishingAttemptStartResult.Failed();
            }

            if (!TryPrepareConsumables(out ConsumablePreparation consumables))
            {
                m_warningLog(
                    "[FishingSystem] 전투 시작 실패: 장착 소모품 상태가 유효하지 않습니다.");
                return FishingAttemptStartResult.Failed();
            }

            BattleFishData fishData = SelectBattleFish(
                consumables.SummonTarget,
                consumables.AppliedGroundbaitStat);

            if (fishData == null)
            {
                m_warningLog(
                    "[FishingSystem] 전투 시작 실패: 선택 가능한 BattleFishData가 없습니다.");
                return FishingAttemptStartResult.Failed();
            }

            EntityHandle battleFishHandle =
                m_entityManager.Create<BattleFishData>(fishData.ID);
            Entity_BattleFish battleFish =
                m_entityManager.Get<Entity_BattleFish>(battleFishHandle);

            if (battleFish == null)
            {
                m_warningLog(
                    $"[FishingSystem] Entity_BattleFish 생성 실패: id={fishData.ID}");
                m_entityManager.Destroy(battleFishHandle);
                return FishingAttemptStartResult.Failed();
            }

            if (!TryConsumePreparedItems(consumables))
            {
                m_warningLog(
                    "[FishingSystem] 전투 시작 실패: 장착 소모품 소비에 실패했습니다.");
                m_entityManager.Destroy(battleFishHandle);
                return FishingAttemptStartResult.Failed();
            }

            LogAppliedConsumables(consumables);

            ItemQuality quality = m_catchRoller.RollFishQuality(
                consumables.AppliedBaitStat,
                m_debugLog);
            float rolledSize = m_catchRoller.RollFishSize(fishData, quality);
            float size = Mathf.Round(rolledSize * 10f) / 10f;

            battleFish.SetRollResult(size, quality);

            m_debugLog?.Invoke(
                $"[최종 결과] fish={fishData.Name}(id={fishData.ID}), " +
                $"rarity={fishData.ItemFish.Rarity}, quality={quality}, size={size:0.0}, " +
                $"baitStat={consumables.AppliedBaitStat:0.##}, " +
                $"groundbaitStat={consumables.AppliedGroundbaitStat:0.##}");

            float battleDuration = CalculateBattleDuration(fishData, size);
            var attempt = new FishingAttempt(
                fishData,
                size,
                quality,
                battleDuration,
                consumables.AppliedBaitStat,
                consumables.AppliedGroundbaitStat);

            return FishingAttemptStartResult.Success(attempt, battleFishHandle);
        }

        public bool TryCalculateNextFishingDelay(out float delay)
        {
            delay = 0f;

            if (!IsReady)
            {
                m_warningLog(
                    "[FishingSystem] 입질 대기시간 계산 실패: 필수 시스템을 사용할 수 없습니다.");
                return false;
            }

            int playerLicense = GetPlayerLicense();
            List<TierPool> pools =
                m_stageSystem.GetAvailableTierPools(playerLicense);
            TierPool selectedPool =
                m_catchRoller.SelectHighestTierPool(pools);

            if (selectedPool == null)
            {
                m_warningLog(
                    $"[FishingSystem] 입질 대기시간 계산 실패: " +
                    $"사용 가능한 TierPool이 없습니다. playerLicense={playerLicense}");
                return false;
            }

            delay = m_timingCalculator.CalculateNextFishingDelay(
                m_playerSystem.BaseAutoBattleCooltime,
                selectedPool.RegionResistance);
            return true;
        }

        private bool TryPrepareConsumables(
            out ConsumablePreparation preparation)
        {
            preparation = default;

            if (!TryGetEquippedItem(
                    EquipmentMountingArea.Bait,
                    out EquippedItemSnapshot bait))
            {
                return false;
            }

            BattleFishData summonTarget =
                (bait.ItemData as ItemData_Consumables)?.SummonTarget;
            EquippedItemSnapshot groundbait = default;

            if (summonTarget == null &&
                !TryGetEquippedItem(
                    EquipmentMountingArea.Groundbait,
                    out groundbait))
            {
                return false;
            }

            preparation = new ConsumablePreparation(
                bait,
                groundbait,
                m_playerSystem.BaseProbabilityAtFishSize,
                m_playerSystem.BaseProbabilityAtFishRarity,
                summonTarget);
            return true;
        }

        private bool TryGetEquippedItem(
            EquipmentMountingArea area,
            out EquippedItemSnapshot snapshot)
        {
            snapshot = default;
            EntityHandle handle = m_playerSystem.GetEquippedItemHandle(area);

            if (handle.Value == Guid.Empty)
            {
                return true;
            }

            Entity entity = m_entityManager.Get(handle);

            if (entity is Entity_Consumables consumable &&
                consumable.Quantity > 0 &&
                consumable.ItemData != null)
            {
                snapshot = new EquippedItemSnapshot(consumable.ItemData);
                return true;
            }

            if (entity is Entity_Materials material &&
                material.Quantity > 0 &&
                material.ItemData != null)
            {
                snapshot = new EquippedItemSnapshot(material.ItemData);
                return true;
            }

            return false;
        }

        private bool TryConsumePreparedItems(
            ConsumablePreparation preparation)
        {
            if (preparation.Bait.HasItem &&
                !m_playerSystem.TryConsumeEquippedItem(
                    EquipmentMountingArea.Bait,
                    out _))
            {
                return false;
            }

            if (preparation.Groundbait.HasItem &&
                !m_playerSystem.TryConsumeEquippedItem(
                    EquipmentMountingArea.Groundbait,
                    out _))
            {
                return false;
            }

            return true;
        }

        private void LogAppliedConsumables(
            ConsumablePreparation preparation)
        {
            string groundbaitResult = preparation.SummonTarget != null
                ? "소비 생략(보스 미끼 우선)"
                : $"consumed={preparation.Groundbait.HasItem}, " +
                  $"item={GetItemDebugName(preparation.Groundbait.ItemData)}";

            m_debugLog?.Invoke(
                $"[전투 시작 소모품 적용] " +
                $"baitConsumed={preparation.Bait.HasItem}, " +
                $"bait={GetItemDebugName(preparation.Bait.ItemData)}, " +
                $"baitStat={preparation.AppliedBaitStat:0.##}, " +
                $"groundbait={groundbaitResult}, " +
                $"groundbaitStat={preparation.AppliedGroundbaitStat:0.##}, " +
                $"summonTarget={GetFishDebugName(preparation.SummonTarget)}");
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
                m_warningLog(
                    "[FishingSystem] StageSystem을 찾을 수 없습니다.");
                return null;
            }

            int playerLicense = GetPlayerLicense();
            List<TierPool> availablePools =
                m_stageSystem.GetAvailableTierPools(playerLicense);

            if (availablePools == null || availablePools.Count == 0)
            {
                m_warningLog(
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

        private readonly struct EquippedItemSnapshot
        {
            public bool HasItem { get; }
            public ItemData ItemData { get; }

            public EquippedItemSnapshot(ItemData itemData)
            {
                HasItem = true;
                ItemData = itemData;
            }
        }

        private readonly struct ConsumablePreparation
        {
            public EquippedItemSnapshot Bait { get; }
            public EquippedItemSnapshot Groundbait { get; }
            public float AppliedBaitStat { get; }
            public float AppliedGroundbaitStat { get; }
            public BattleFishData SummonTarget { get; }

            public ConsumablePreparation(
                EquippedItemSnapshot bait,
                EquippedItemSnapshot groundbait,
                float appliedBaitStat,
                float appliedGroundbaitStat,
                BattleFishData summonTarget)
            {
                Bait = bait;
                Groundbait = groundbait;
                AppliedBaitStat = appliedBaitStat;
                AppliedGroundbaitStat = appliedGroundbaitStat;
                SummonTarget = summonTarget;
            }
        }
    }
}
