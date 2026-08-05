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

            ClearCurrentBattleFish();

            m_waitDuration = 0f;
            m_battleDuration = 0f;
            m_waitTimer = 0f;
            m_battleTimer = 0f;
            m_autoAttackTimer = 0f;

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
            switch (m_fishingSettingSystem.CurrentInventoryFullPolicy)
            {
                case InventoryFullPolicy.StopAndAsk:
                    return true;

                case InventoryFullPolicy.AlwaysSell:
                    return false;

                case InventoryFullPolicy.StopOnRecordUpdate:
                    // 도감 결과를 믿을 수 없으면 자동 판매하지 않는다.
                    if (!isCollectionUpdated)
                    {
                        return true;
                    }

                    // 새 종은 선택한 기준과 관계없이 항상 멈춘다.
                    if (collectionResult.IsRegistered)
                    {
                        return true;
                    }

                    return m_fishingSettingSystem.CurrentRecordStopCriterion
                        == RecordStopCriterion.Quality
                        ? collectionResult.IsBestQualityImproved
                        : collectionResult.IsBestSizeImproved;

                default: return true;
            }
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

            ItemQuality quality = RollFishQuality(m_appliedBaitStat);
            float rolledSize = RollFishSize(fishData, quality);

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
                ClearCurrentBattleFish();
                ScheduleNextFishing();
                OnFishingResult?.Invoke(FishingResultType.Failed);
                return;
            }

            if (m_rewardProcessor == null)
            {
                Debug.LogWarning("[FishingSystem] 보상 처리기를 사용할 수 없어 물고기를 지급할 수 없습니다.");
                ClearCurrentBattleFish();
                ScheduleNextFishing();
                OnFishingResult?.Invoke(FishingResultType.Failed);
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
                ClearCurrentBattleFish();
                ScheduleNextFishing();
                OnFishingResult?.Invoke(resultType);
                return;
            }

            FishCollectionUpdateResult collectionResult = default;
            bool isCollectionUpdated = false;

            if (m_collectionSystem == null)
            {
                Debug.LogWarning(
                    "[FishingSystem] FishCollectionSystem을 사용할 수 없습니다.");
            }
            else if (!m_collectionSystem.TryRegisterCatch(
                         battleFish.BattleData.ItemFish.ID,
                         battleFish.Quality,
                         battleFish.Size,
                         out collectionResult))
            {
                Debug.LogWarning(
                    "[FishingSystem] 포획 물고기의 도감 반영에 실패했습니다.");
            }
            else
            {
                isCollectionUpdated = true;
            }

            OnFishCaughtPresentation?.Invoke(caughtHandle, collectionResult);

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

            FishingRewardResult finalizeResult = m_rewardProcessor.TryFinalizeCaughtFish(caughtHandle);

            if (finalizeResult == FishingRewardResult.InventoryFull)
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

                bool isSold = m_shopSystem != null && m_shopSystem.SellAcquiredItem(caughtHandle, out earnedGold);

                if (!isSold)
                {
                    // 자동 판매에 실패했을 때 물고기를 잃지 않도록
                    // 기본 Pending 흐름으로 되돌린다.
                    Debug.LogWarning(
                        "[FishingSystem] 자동 판매에 실패하여 Pending 처리로 전환합니다.");

                    EnterPendingCatch(caughtHandle);
                    return;
                }

                Debug.Log(
                    $"[FishingSystem] 인벤토리 부족 물고기 자동 판매 완료. " +
                    $"earnedGold={earnedGold}");

                ClearCurrentBattleFish();
                ScheduleNextFishing();

                OnFishingResult?.Invoke(FishingResultType.Success);
                return;
            }

            if (finalizeResult != FishingRewardResult.Success)
            {
                Debug.LogWarning(
                    $"[FishingSystem] 포획 물고기 지급 실패: result={finalizeResult}");

                ClearCurrentBattleFish();
                ScheduleNextFishing();

                OnFishingResult?.Invoke(FishingResultType.Failed);
                return;
            }

            ClearCurrentBattleFish();
            ScheduleNextFishing();
            OnFishingResult?.Invoke(FishingResultType.Success);
        }

        private void FailBattle(string reason)
        {
            Debug.Log($"[FishingSystem] 포획 실패: reason={reason}");

            ClearCurrentBattleFish();
            ScheduleNextFishing();
            OnFishingResult?.Invoke(FishingResultType.Failed);
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

            TierPool selectedPool = SelectHighestTierPool(availablePools);
            BattleFishData selectedFish = SelectBattleFishFromTierPool(selectedPool);

            if (selectedFish != null)
            {
                Debug.Log($"[FishingSystem] StageSystem TierPool 사용: tier={selectedPool.Tier}, fish={selectedFish.Name}");
            }

            return selectedFish;
        }

        private TierPool SelectHighestTierPool(List<TierPool> pools)
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

        private BattleFishData SelectBattleFishFromTierPool(TierPool pool)
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

                ItemRarity rarity = entry.Fish.ItemFish.Rarity;
                int rarityIndex = (int)rarity;

                m_fishByRarity[rarityIndex].Add(entry.Fish);
            }

            float totalWeight = 0f;

            for (int i = 0; i < m_fishByRarity.Length; i++)
            {
                m_rarityWeights[i] = 0f;

                if (m_fishByRarity[i].Count == 0)
                {
                    continue;
                }

                ItemRarity rarity = (ItemRarity)i;
                float weight = FishingWeightCalculator.CalculateRarityWeight(
                    rarity,
                    m_appliedGroundbaitStat);

                m_rarityWeights[i] = weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float randomValue = UnityEngine.Random.Range(0f, totalWeight);
            float accumulatedWeight = 0f;

            for (int i = 0; i < m_fishByRarity.Length; i++)
            {
                if (m_fishByRarity[i].Count == 0)
                {
                    continue;
                }

                accumulatedWeight += m_rarityWeights[i];

                if (randomValue <= accumulatedWeight)
                {
                    List<BattleFishData> selectedRarityFish = m_fishByRarity[i];
                    int fishIndex = UnityEngine.Random.Range(0, selectedRarityFish.Count);
                    BattleFishData selectedFish = selectedRarityFish[fishIndex];
                    ItemRarity selectedRarity = (ItemRarity)i;

                    // 0인 항목은 해당 TierPool에 그 희귀도의 물고기가 없어서 추첨에서 제외된 경우입니다.
                    LogFishingItemDebug(
                        $"[희귀도 추첨] groundbaitStat={m_appliedGroundbaitStat:0.##}, tier={pool.Tier}, " +
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
            }

            return null;
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

        private ItemQuality RollFishQuality(float baitStat)
        {
            float totalWeight = 0f;

            // 1~5성의 가중치를 계산하고 전체 합계를 구한다.
            for (int i = 0; i < m_qualityWeights.Length; i++)
            {
                ItemQuality quality = (ItemQuality)(i + 1);

                float weight =
                    FishingWeightCalculator.CalculateQualityWeight(
                        quality,
                        baitStat);

                m_qualityWeights[i] = weight;
                totalWeight += weight;
            }

            // 전체 가중치 범위에서 랜덤 값을 뽑는다.
            float randomValue =
                UnityEngine.Random.Range(0f, totalWeight);

            float accumulatedWeight = 0f;

            // 누적 가중치로 성급을 결정한다.
            for (int i = 0; i < m_qualityWeights.Length; i++)
            {
                accumulatedWeight += m_qualityWeights[i];

                if (randomValue <= accumulatedWeight)
                {
                    ItemQuality selectedQuality = (ItemQuality)(i + 1);

                    // 각 가중치와 실제 확률, 랜덤 값, 선택 결과를 함께 출력합니다.
                    LogQualityRoll(baitStat, totalWeight, randomValue, selectedQuality);
                    return selectedQuality;
                }
            }

            // 부동소수점 오차에 대한 마지막 반환값
            LogQualityRoll(baitStat, totalWeight, randomValue, ItemQuality.FiveStar);
            return ItemQuality.FiveStar;
        }

        private void LogQualityRoll(
            float baitStat,
            float totalWeight,
            float randomValue,
            ItemQuality selectedQuality)
        {
            LogFishingItemDebug(
                $"[성급 추첨] baitStat={baitStat:0.##}, " +
                $"weights=" +
                $"1성:{FormatWeight(m_qualityWeights[0], totalWeight)}, " +
                $"2성:{FormatWeight(m_qualityWeights[1], totalWeight)}, " +
                $"3성:{FormatWeight(m_qualityWeights[2], totalWeight)}, " +
                $"4성:{FormatWeight(m_qualityWeights[3], totalWeight)}, " +
                $"5성:{FormatWeight(m_qualityWeights[4], totalWeight)}, " +
                $"roll={randomValue:0.###}/{totalWeight:0.###}, selected={selectedQuality}");
        }

        // 가중치 원본 값과 전체 합계 기준 실제 확률을 같이 표시합니다.
        private static string FormatWeight(float weight, float totalWeight)
        {
            float probability = weight / totalWeight * 100f;
            return $"{weight:0.###}({probability:0.00}%)";
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

        private float RollFishSize(BattleFishData fishData, ItemQuality quality)
        {
            fishData.GetSizeRange(
                quality,
                out float minSize,
                out float maxSize);

            int minSizeStep = Mathf.RoundToInt(minSize * 10f);

            int maxSizeStepExclusive = quality == ItemQuality.FiveStar
                ? Mathf.RoundToInt(maxSize * 10f) + 1
                : Mathf.RoundToInt(maxSize * 10f);

            int selectedSizeStep = UnityEngine.Random.Range(
                minSizeStep,
                maxSizeStepExclusive);

            return selectedSizeStep / 10f;
        }

        private float CalculateBattleDuration(BattleFishData fishData, float fishSize)
        {
            if (m_playerSystem == null)
            {
                Debug.Log("[FishingSystem] PlayerSystem을 찾지 못해 기본 전투 시간을 사용합니다.");
                return baseBattleDuration;
            }


            float variableRatio = (float)m_playerSystem.BaseBattleTimeVariable / fishData.BattleTimeVariable;

            float normalizedSize = Mathf.InverseLerp(fishData.MinSize, fishData.MaxSize, fishSize);

            float sizeRatio = Mathf.Lerp(
                1.3f, // 최소 크기: 시간 30% 증가
                0.7f, // 최대 크기: 시간 30% 감소
                normalizedSize);

            float duration =
                baseBattleDuration *
                variableRatio *
                sizeRatio;

            return Mathf.Max(minBattleDuration, duration);
        }

        private float CalculateNextFishingDelay()
        {

            if (m_playerSystem == null)
            {
                Debug.LogWarning("[FishingSystem] PlayerSystem을 찾지 못해 기본 낚시 대기시간을 사용합니다.");
                return 10f;
            }

            float baseDelay = m_playerSystem.BaseAutoBattleCooltime;

            float minDelay = baseDelay * 0.8f;
            float maxDelay = baseDelay * 1.2f;

            return UnityEngine.Random.Range(minDelay, maxDelay);
        }

        private int CalculateManualDamage()
        {
            if (m_playerSystem == null)
            {
                Debug.LogWarning("[FishingSystem] PlayerSystem을 찾지 못해 기본 수동 공격 데미지를 사용합니다.");
                return 1;
            }

            float damage = m_playerSystem.BaseDamagePerClick * m_playerSystem.BaseManualDamagePerHitMultiply;

            bool isCritical = UnityEngine.Random.value < m_playerSystem.BaseCriticalChance;

            if (isCritical)
            {
                damage *= m_playerSystem.BaseCriticalMultiply;
            }

            return Mathf.Max(1, Mathf.RoundToInt(damage));
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

        #endregion

        #region Event Handlers

        private void HandleStageChanged(int stageDataId)
        {
            if (m_state == FishingState.Stopped)
            {
                return;
            }

            ClearCurrentBattleFish();
            m_waitDuration = 0f;
            m_battleDuration = 0f;
            m_waitTimer = 0f;
            m_battleTimer = 0f;
            m_autoAttackTimer = 0f;

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
