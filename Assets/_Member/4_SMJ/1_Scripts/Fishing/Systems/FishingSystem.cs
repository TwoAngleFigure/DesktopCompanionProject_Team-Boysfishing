using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    public enum FishingState
    {
        Stopped,
        Waiting,
        Battling
    }
    public enum FishingResultType
    {
        Success,
        Failed,
        InventoryFull
    }

    public class FishingSystem : SystemBase, ITickable
    {
        // 미끼/떡밥 확률 검증용 로그 스위치입니다.
        // 테스트가 끝난 뒤 false로 바꾸면 낚시 디버그 로그만 끌 수 있습니다.
        private const bool EnableFishingItemDebugLog = true;

        private StageSystem m_stageSystem;
        private PlayerSystem m_playerSystem;
        private InventorySystem m_inventorySystem;
        private FishCollectionSystem m_collectionSystem;
        private FishingSettingSystem m_fishingSettingSystem;
        private FishingRewardProcessor m_rewardProcessor;
        private ShopSystem m_shopSystem;
        private FishingCatchRoller m_catchRoller;
        private FishingTimingCalculator m_timingCalculator;
        private FishingCombatCalculator m_combatCalculator;

        private float baseBattleDuration = 10f;
        private float minBattleDuration = 2f;

        private FishingState m_state;
        private EntityHandle m_currentBattleFish;
        private EntityHandle m_pendingCatch;
        private float m_waitDuration;
        private float m_battleDuration;
        private float m_waitTimer;
        private float m_battleTimer;
        private float m_autoAttackTimer;
        private bool m_isResolvingPending;
        private float m_appliedBaitStat;
        private float m_appliedGroundbaitStat;
        private BattleFishData m_appliedSummonTarget;

        #region Events

        public event Action<FishingState> OnStateChanged;
        public event Action<EntityHandle, FishCollectionUpdateResult> OnFishCaughtPresentation;
        public event Action<FishingResultType> OnFishingResult;
        public event Action<EntityHandle, int, int> OnBattleHpChanged;
        public event Action OnPendingCatchChanged;
        public event Action<FishingGrantedDropInfo> OnItemDropped;

        #endregion

        #region Properties

        public FishingState State => m_state;

        public float WaitDuration => m_waitDuration; // Debug 남은 시간 확인용
        public float WaitTimeRemaining => m_waitTimer;


        public float BattleDuration => m_battleDuration; // Debug 남은 시간 확인용
        public float BattleTimeRemaining => m_battleTimer;
        public EntityHandle CurrentBattleFish => m_currentBattleFish;

        public bool HasPendingCatch => m_pendingCatch.Value != Guid.Empty;

        public EntityHandle PendingCatch => m_pendingCatch;

        #endregion

        public override void Initialize()
        {
            m_state = FishingState.Stopped;
            m_currentBattleFish = default;
            m_pendingCatch = default;
            m_isResolvingPending = false;
            m_waitDuration = 0f;
            m_battleDuration = 0f;
            m_waitTimer = 0f;
            m_battleTimer = 0f;
            m_autoAttackTimer = 0f;
            m_catchRoller = new FishingCatchRoller();
            m_timingCalculator = new FishingTimingCalculator();
            m_combatCalculator = new FishingCombatCalculator();
        }

        public override void PostInitialize()
        {
            m_stageSystem = SystemManager.GetSystem<StageSystem>();
            m_playerSystem = SystemManager.GetSystem<PlayerSystem>();
            m_collectionSystem = SystemManager.GetSystem<FishCollectionSystem>();
            m_fishingSettingSystem = SystemManager.GetSystem<FishingSettingSystem>();
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();
            m_shopSystem = SystemManager.GetSystem<ShopSystem>();

            if (m_inventorySystem != null)
            {
                m_rewardProcessor = new FishingRewardProcessor(EntityManager, m_inventorySystem);

                m_inventorySystem.OnInventoryChanged += HandleInventoryChanged;
            }

            if (m_stageSystem != null)
            {
                m_stageSystem.OnStageChanged += HandleStageChanged;
            }
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            if (m_state == FishingState.Waiting)
            {
                Waiting(deltaTime);
            }

            if (m_state == FishingState.Battling)
            {
                Battle(deltaTime);
            }
        }

        private void Waiting(float deltaTime)
        {
            m_waitTimer -= deltaTime;

            if (m_waitTimer <= 0f)
            {
                StartBattle();
            }
        }

        private void Battle(float deltaTime)
        {
            m_battleTimer -= deltaTime;

            if (m_battleTimer <= 0f)
            {
                FailBattle("제한시간 초과");
                return;
            }

            m_autoAttackTimer -= deltaTime;

            if (m_autoAttackTimer <= 0f)
            {
                int damage = 1;
                float attackInterval = 1f;

                if (m_playerSystem != null)
                {
                    damage = Mathf.RoundToInt(m_playerSystem.BaseDamagePerClick * m_playerSystem.BaseAutoDamagePerHitMultiply);
                    attackInterval = m_playerSystem.BaseAutoSpeedPerTime;
                }

                Debug.Log($"[FishingSystem] 자동 공격: damage={damage}");

                ApplyDamage(damage);

                m_autoAttackTimer = attackInterval;
            }
        }

        public void ManualAttack()
        {
            if (m_state != FishingState.Battling)
            {
                Debug.Log("[FishingSystem] 수동 공격 실패: 전투 중이 아닙니다.");
                return;
            }

            int damage = CalculateManualDamage();

            Debug.Log($"[FishingSystem] 수동 공격: damage={damage}");

            ApplyDamage(damage);
        }

        public void StartFishing()
        {
            if (HasPendingCatch)
            {
                Debug.LogWarning(
                    "[FishingSystem] Pending 물고기를 먼저 처리해야 합니다.");

                return;
            }

            if (m_state != FishingState.Stopped)
            {
                Debug.Log($"[FishingSystem] 이미 낚시 진행 중입니다. state={m_state}");
                return;
            }

            ScheduleNextFishing();

            Debug.Log($"[FishingSystem] 자동 낚시 시작 대기시간={m_waitTimer:0.00}초");
        }

        public void StopFishing()
        {
            Debug.Log("[FishingSystem] 자동 낚시 중지");

            ResetFishingProgress();

            ChangeState(FishingState.Stopped);
        }

        public ItemData GetItemData(ItemType itemType, int dataId)
        {
            if (dataId <= 0)
            {
                return null;
            }

            return itemType switch
            {
                ItemType.Fish => DataManager.GetData<ItemData_Fish>(dataId),

                ItemType.Materials => DataManager.GetData<ItemData_Materials>(dataId),

                ItemType.Consumables => DataManager.GetData<ItemData_Consumables>(dataId),

                ItemType.Equipment => DataManager.GetData<ItemData_Equipment>(dataId),

                _ => null
            };
        }

        #region Pending

        private bool ShouldCreatePendingCatch(bool isCollectionUpdated, FishCollectionUpdateResult collectionResult)
        {
            return FishingInventoryFullPolicyEvaluator.ShouldCreatePendingCatch(
                m_fishingSettingSystem.CurrentInventoryFullPolicy,
                m_fishingSettingSystem.CurrentRecordStopCriterion,
                isCollectionUpdated,
                collectionResult);
        }
        private void EnterPendingCatch(EntityHandle caughtHandle)
        {
            m_pendingCatch = caughtHandle;

            StopFishing();

            OnPendingCatchChanged?.Invoke();
            OnFishingResult?.Invoke(FishingResultType.InventoryFull);
        }

        public bool TryClaimPendingCatch()
        {
            if (!HasPendingCatch ||
                m_rewardProcessor == null ||
                m_isResolvingPending)
            {
                return false;
            }

            m_isResolvingPending = true;

            EntityHandle pendingHandle = m_pendingCatch;

            FishingRewardResult result = m_rewardProcessor.TryFinalizeCaughtFish(pendingHandle);

            if (result != FishingRewardResult.Success)
            {
                m_isResolvingPending = false;
                return false;
            }

            m_pendingCatch = default;
            m_isResolvingPending = false;

            OnPendingCatchChanged?.Invoke();

            Debug.Log("[FishingSystem] Pending 물고기 지급 완료. 낚시를 재개합니다.");

            StartFishing();
            return true;
        }

        public bool TrySellPendingCatch(out int earnedGold)
        {
            earnedGold = 0;

            if (!HasPendingCatch || m_shopSystem == null)
            {
                return false;
            }

            EntityHandle pendingHandle = m_pendingCatch;

            if (!m_shopSystem.SellAcquiredItem(pendingHandle, out earnedGold))
            {
                Debug.LogWarning("[FishingSystem] Pending 물고기 판매에 실패했습니다.");
                return false;
            }

            m_pendingCatch = default;

            OnPendingCatchChanged?.Invoke();

            StartFishing();
            return true;
        }
        #endregion


        #region Battle Flow

        private void StartBattle()
        {
            PrepareConsumablesForBattle();

            BattleFishData fishData = SelectBattleFishForCurrentAttempt();

            if (fishData == null)
            {
                Debug.LogWarning("[FishingSystem] 전투 시작 실패: 선택 가능한 BattleFishData가 없습니다.");
                ScheduleNextFishing();
                return;
            }

            m_currentBattleFish = EntityManager.Create<BattleFishData>(fishData.ID);

            Entity_BattleFish battleFish = EntityManager.Get<Entity_BattleFish>(m_currentBattleFish);
            if (battleFish == null)
            {
                Debug.LogWarning($"[FishingSystem] Entity_BattleFish 생성 실패: id={fishData.ID}");
                ClearCurrentBattleFish();
                ScheduleNextFishing();
                return;
            }

            ItemQuality quality = m_catchRoller.RollFishQuality(
                m_appliedBaitStat,
                LogFishingItemDebug);
            float rolledSize = m_catchRoller.RollFishSize(fishData, quality);

            float size = Mathf.Round(rolledSize * 10f) / 10f;

            battleFish.SetRollResult(size, quality);

            // 한 번의 낚시에 실제 적용된 소모품 스냅샷과 최종 결과를 한 줄로 확인합니다.
            LogFishingItemDebug(
                $"[최종 결과] fish={fishData.Name}(id={fishData.ID}), " +
                $"rarity={fishData.ItemFish.Rarity}, quality={quality}, size={size:0.0}, " +
                $"baitStat={m_appliedBaitStat:0.##}, groundbaitStat={m_appliedGroundbaitStat:0.##}");

            m_battleDuration = CalculateBattleDuration(fishData, size);
            m_battleTimer = m_battleDuration;
            m_autoAttackTimer = 0f;

            ChangeState(FishingState.Battling);

            OnBattleHpChanged?.Invoke(m_currentBattleFish, battleFish.CurrentHp, fishData.MaxHp);
            Debug.Log($"[FishingSystem] 전투 시작: {fishData.Name}, HP={battleFish.CurrentHp}/{fishData.MaxHp}, Size={size:0.00}, Quality={quality}, 제한시간={m_battleTimer:0.00}초");
        }

        private void ApplyDamage(int damage)
        {
            Entity_BattleFish battleFish = EntityManager.Get<Entity_BattleFish>(m_currentBattleFish);

            if (battleFish == null)
            {
                Debug.LogWarning("[FishingSystem] 데미지 적용 실패: 현재 전투 물고기가 없습니다.");
                FailBattle("현재 전투 물고기 없음");
                return;
            }

            battleFish.ApplyDamage(damage);

            OnBattleHpChanged?.Invoke(m_currentBattleFish, battleFish.CurrentHp, battleFish.BattleData.MaxHp);

            Debug.Log($"[FishingSystem] 물고기 HP : {battleFish.Name} HP={battleFish.CurrentHp}/{battleFish.BattleData.MaxHp}");

            if (battleFish.CurrentHp <= 0)
            {
                CompleteBattle();
            }
        }

        private void CompleteBattle()
        {
            Entity_BattleFish battleFish = EntityManager.Get<Entity_BattleFish>(m_currentBattleFish);

            if (battleFish == null)
            {
                FinishCurrentAttempt(FishingResultType.Failed);
                return;
            }

            if (m_rewardProcessor == null)
            {
                Debug.LogWarning("[FishingSystem] 보상 처리기를 사용할 수 없어 물고기를 지급할 수 없습니다.");
                FinishCurrentAttempt(FishingResultType.Failed);
                return;
            }

            FishingRewardResult createResult = m_rewardProcessor.TryCreateCaughtFish(
                battleFish.BattleData.ItemFish,
                battleFish.Size,
                battleFish.Quality,
                out EntityHandle caughtHandle);

            if (createResult != FishingRewardResult.Success)
            {
                FishingResultType resultType = createResult == FishingRewardResult.InventoryFull
                        ? FishingResultType.InventoryFull
                        : FishingResultType.Failed;

                Debug.LogWarning($"[FishingSystem] 포획 물고기 생성 실패: result={createResult}");
                FinishCurrentAttempt(resultType);
                return;
            }

            bool isCollectionUpdated = TryUpdateCollection(
                battleFish,
                out FishCollectionUpdateResult collectionResult);

            OnFishCaughtPresentation?.Invoke(caughtHandle, collectionResult);
            PublishGrantedDrops(battleFish);
            FinalizeCaughtFish(caughtHandle, isCollectionUpdated, collectionResult);
        }

        private bool TryUpdateCollection(
            Entity_BattleFish battleFish,
            out FishCollectionUpdateResult collectionResult)
        {
            collectionResult = default;

            if (m_collectionSystem == null)
            {
                Debug.LogWarning(
                    "[FishingSystem] FishCollectionSystem을 사용할 수 없습니다.");
                return false;
            }

            if (!m_collectionSystem.TryRegisterCatch(
                    battleFish.BattleData.ItemFish.ID,
                    battleFish.Quality,
                    battleFish.Size,
                    out collectionResult))
            {
                Debug.LogWarning(
                    "[FishingSystem] 포획 물고기의 도감 반영에 실패했습니다.");
                return false;
            }

            return true;
        }

        private void PublishGrantedDrops(Entity_BattleFish battleFish)
        {
            IReadOnlyList<FishingGrantedDropInfo> grantedDrops = m_rewardProcessor.Process(
                battleFish.BattleData.Drops,
                battleFish.Quality,
                battleFish.BattleData.IsBoss);

            foreach (FishingGrantedDropInfo grantedDrop in grantedDrops)
            {
                if (!grantedDrop.IsValid)
                {
                    continue;
                }

                OnItemDropped?.Invoke(grantedDrop);
            }
        }

        private void FinalizeCaughtFish(
            EntityHandle caughtHandle,
            bool isCollectionUpdated,
            FishCollectionUpdateResult collectionResult)
        {
            FishingRewardResult finalizeResult =
                m_rewardProcessor.TryFinalizeCaughtFish(caughtHandle);

            if (finalizeResult == FishingRewardResult.InventoryFull)
            {
                HandleInventoryFull(
                    caughtHandle,
                    isCollectionUpdated,
                    collectionResult);
                return;
            }

            if (finalizeResult != FishingRewardResult.Success)
            {
                Debug.LogWarning(
                    $"[FishingSystem] 포획 물고기 지급 실패: result={finalizeResult}");

                FinishCurrentAttempt(FishingResultType.Failed);
                return;
            }

            FinishCurrentAttempt(FishingResultType.Success);
        }

        private void HandleInventoryFull(
            EntityHandle caughtHandle,
            bool isCollectionUpdated,
            FishCollectionUpdateResult collectionResult)
        {
            if (HasPendingCatch)
            {
                Debug.LogError(
                    "[FishingSystem] 이미 Pending 물고기가 존재합니다.");

                EntityManager.Destroy(caughtHandle);

                ClearCurrentBattleFish();
                StopFishing();

                OnFishingResult?.Invoke(FishingResultType.Failed);
                return;
            }

            if (ShouldCreatePendingCatch(isCollectionUpdated, collectionResult))
            {
                EnterPendingCatch(caughtHandle);
                return;
            }

            int earnedGold = 0;
            bool isSold = m_shopSystem != null &&
                m_shopSystem.SellAcquiredItem(caughtHandle, out earnedGold);

            if (!isSold)
            {
                Debug.LogWarning(
                    "[FishingSystem] 자동 판매에 실패하여 Pending 처리로 전환합니다.");

                EnterPendingCatch(caughtHandle);
                return;
            }

            Debug.Log(
                $"[FishingSystem] 인벤토리 부족 물고기 자동 판매 완료. " +
                $"earnedGold={earnedGold}");

            FinishCurrentAttempt(FishingResultType.Success);
        }

        private void FailBattle(string reason)
        {
            Debug.Log($"[FishingSystem] 포획 실패: reason={reason}");

            FinishCurrentAttempt(FishingResultType.Failed);
        }

        private void FinishCurrentAttempt(FishingResultType resultType)
        {
            ClearCurrentBattleFish();
            ScheduleNextFishing();
            OnFishingResult?.Invoke(resultType);
        }

        private void ScheduleNextFishing()
        {
            m_waitDuration = CalculateNextFishingDelay();
            m_waitTimer = m_waitDuration;
            m_battleDuration = 0f;
            m_battleTimer = 0f;
            m_autoAttackTimer = 0f;

            ChangeState(FishingState.Waiting);

            Debug.Log($"[FishingSystem] 다음 입질 대기: {m_waitTimer:0.00}초");
        }

        private void PrepareConsumablesForBattle()
        {
            // 소비로 마지막 아이템이 장착 해제되기 전에 현재 스탯을 저장
            m_appliedBaitStat = m_playerSystem != null ? m_playerSystem.BaseProbabilityAtFishSize : 0f;
            m_appliedGroundbaitStat = m_playerSystem != null ? m_playerSystem.BaseProbabilityAtFishRarity : 0f;
            m_appliedSummonTarget = null;

            bool consumedBait = false;
            bool consumedGroundbait = false;
            ItemData consumedBaitData = null;
            ItemData consumedGroundbaitData = null;

            if (m_playerSystem != null)
            {
                // 전투 시작 순간 장착된 미끼를 소비하므로 대기 중 교체한 미끼를 적용
                consumedBait = m_playerSystem.TryConsumeEquippedItem(
                    EquipmentMountingArea.Bait,
                    out consumedBaitData);

                if (consumedBait && consumedBaitData is ItemData_Consumables baitData)
                {
                    m_appliedSummonTarget = baitData.SummonTarget;
                }

                if (m_appliedSummonTarget == null)
                {
                    consumedGroundbait = m_playerSystem.TryConsumeEquippedItem(
                        EquipmentMountingArea.Groundbait,
                        out consumedGroundbaitData);
                }
            }

            string groundbaitResult = m_appliedSummonTarget != null
                ? "소비 생략(보스 미끼 우선)"
                : $"consumed={consumedGroundbait}, item={GetItemDebugName(consumedGroundbaitData)}";

            LogFishingItemDebug(
                $"[전투 시작 소모품 적용] baitConsumed={consumedBait}, " +
                $"bait={GetItemDebugName(consumedBaitData)}, baitStat={m_appliedBaitStat:0.##}, " +
                $"groundbait={groundbaitResult}, groundbaitStat={m_appliedGroundbaitStat:0.##}, " +
                $"summonTarget={GetFishDebugName(m_appliedSummonTarget)}");
        }

        #endregion

        #region Fish Selection

        private BattleFishData SelectBattleFishForCurrentAttempt()
        {
            if (m_appliedSummonTarget == null)
            {
                return SelectBattleFish();
            }

            BattleFishData summonTarget = m_appliedSummonTarget;
            m_appliedSummonTarget = null;

            // 보스 미끼는 TierPool과 희귀도 가중치 추첨을 거치지 않고 지정 대상을 사용합니다.
            LogFishingItemDebug(
                $"[보스 선택] 일반 희귀도 추첨 생략, target={GetFishDebugName(summonTarget)}, " +
                $"rarity={summonTarget.ItemFish.Rarity}");
            return summonTarget;
        }

        private BattleFishData SelectBattleFish()
        {
            StageSystem stageSystem = SystemManager.GetSystem<StageSystem>();

            if (stageSystem == null)
            {
                Debug.LogWarning("[FishingSystem] StageSystem을 찾을 수 없습니다.");
                return null;
            }

            int playerLicense = GetPlayerLicense();
            List<TierPool> availablePools = stageSystem.GetAvailableTierPools(playerLicense);

            if (availablePools == null || availablePools.Count == 0)
            {
                Debug.LogWarning($"[FishingSystem] 사용 가능한 TierPool이 없습니다. playerLicense={playerLicense}");
                return null;
            }

            TierPool selectedPool =
                m_catchRoller.SelectHighestTierPool(availablePools);
            BattleFishData selectedFish =
                m_catchRoller.SelectBattleFishFromTierPool(
                    selectedPool,
                    m_appliedGroundbaitStat,
                    LogFishingItemDebug);

            if (selectedFish != null)
            {
                Debug.Log($"[FishingSystem] StageSystem TierPool 사용: tier={selectedPool.Tier}, fish={selectedFish.Name}");
            }

            return selectedFish;
        }

        #endregion

        #region Calculations

        private int GetPlayerLicense()
        {
            if (m_playerSystem == null)
            {
                return 0;
            }

            return m_playerSystem.StartingLicense;
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

        private static void LogFishingItemDebug(string message)
        {
            if (EnableFishingItemDebugLog)
            {
                Debug.Log($"[FishingDebug] {message}");
            }
        }

        private float CalculateBattleDuration(BattleFishData fishData, float fishSize)
        {
            if (m_playerSystem == null)
            {
                Debug.Log("[FishingSystem] PlayerSystem을 찾지 못해 기본 전투 시간을 사용합니다.");
                return baseBattleDuration;
            }


            return m_timingCalculator.CalculateBattleDuration(
                baseBattleDuration,
                minBattleDuration,
                m_playerSystem.BaseBattleTimeVariable,
                fishData.BattleTimeVariable,
                fishData.MinSize,
                fishData.MaxSize,
                fishSize);
        }

        private float CalculateNextFishingDelay()
        {
            int playerLicense = GetPlayerLicense();
            List<TierPool> pools = m_stageSystem.GetAvailableTierPools(playerLicense);

            TierPool selectedPool =
                m_catchRoller.SelectHighestTierPool(pools);

            return m_timingCalculator.CalculateNextFishingDelay(
                m_playerSystem.BaseAutoBattleCooltime,
                selectedPool.RegionResistance);
        }

        private int CalculateManualDamage()
        {
            if (m_playerSystem == null)
            {
                Debug.LogWarning("[FishingSystem] PlayerSystem을 찾지 못해 기본 수동 공격 데미지를 사용합니다.");
                return 1;
            }

            return m_combatCalculator.CalculateManualDamage(
                m_playerSystem.BaseDamagePerClick,
                m_playerSystem.BaseManualDamagePerHitMultiply,
                m_playerSystem.BaseCriticalChance,
                m_playerSystem.BaseCriticalMultiply);
        }


        #endregion

        #region State Helpers

        private void ChangeState(FishingState nextState)
        {
            if (m_state == nextState)
            {
                return;
            }

            m_state = nextState;

            OnStateChanged?.Invoke(nextState);

            Debug.Log($"[FishingSystem] 상태 변경: {nextState}");
        }

        private void ClearCurrentBattleFish()
        {
            EntityManager.Destroy(m_currentBattleFish);
            m_currentBattleFish = default;
        }

        private void ResetFishingProgress()
        {
            ClearCurrentBattleFish();
            m_waitDuration = 0f;
            m_battleDuration = 0f;
            m_waitTimer = 0f;
            m_battleTimer = 0f;
            m_autoAttackTimer = 0f;
        }

        #endregion

        #region Event Handlers

        private void HandleStageChanged(int stageDataId)
        {
            if (m_state == FishingState.Stopped)
            {
                return;
            }

            ResetFishingProgress();

            ChangeState(FishingState.Waiting);

            Debug.Log($"[FishingSystem] 스테이지 변경 감지: stageId={stageDataId}, 낚시 풀 갱신");
        }

        private void HandleInventoryChanged()
        {
            if (!HasPendingCatch || m_isResolvingPending)
            {
                return;
            }

            TryClaimPendingCatch();
        }

        #endregion
    }
}
