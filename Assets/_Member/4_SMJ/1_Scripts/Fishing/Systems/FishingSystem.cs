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
        private StageSystem m_stageSystem;
        private PlayerSystem m_playerSystem;
        private FishingRewardProcessor m_rewardProcessor;

        private float baseBattleDuration = 10f;
        private float minBattleDuration = 2f;

        private FishingState m_state;
        private EntityHandle m_currentBattleFish;
        private float m_waitDuration;
        private float m_battleDuration;
        private float m_waitTimer;
        private float m_battleTimer;
        private float m_autoAttackTimer;

        #region Events

        public event Action<FishingState> OnStateChanged;
        public event Action<EntityHandle> OnFishCaughtPresentation;
        public event Action<FishingResultType> OnFishingResult;
        public event Action<EntityHandle, int, int> OnBattleHpChanged;

        #endregion

        #region Properties

        public FishingState State => m_state;

        public float WaitDuration => m_waitDuration; // Debug 남은 시간 확인용
        public float WaitTimeRemaining => m_waitTimer;


        public float BattleDuration => m_battleDuration; // Debug 남은 시간 확인용
        public float BattleTimeRemaining => m_battleTimer;
        public EntityHandle CurrentBattleFish => m_currentBattleFish;

        #endregion

        public override void Initialize()
        {
            m_state = FishingState.Stopped;
            m_currentBattleFish = default;
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
            InventorySystem inventorySystem = SystemManager.GetSystem<InventorySystem>();

            if (inventorySystem != null)
            {
                m_rewardProcessor = new FishingRewardProcessor(
                    EntityManager,
                    inventorySystem);
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
            if (m_state != FishingState.Stopped)
            {
                Debug.Log($"[FishingSystem] 이미 낚시 진행 중입니다. state={m_state}");
                return;
            }

            ScheduleNextFishing();
            ChangeState(FishingState.Waiting);

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

        #region Battle Flow

        private void StartBattle()
        {
            BattleFishData fishData = SelectBattleFish();

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

            float size = RollFishSize(fishData);
            ItemQuality quality = fishData.GetQuality(size);
            battleFish.SetRollResult(size, quality);

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
                FishingResultType resultType =
                    createResult == FishingRewardResult.InventoryFull
                        ? FishingResultType.InventoryFull
                        : FishingResultType.Failed;

                Debug.LogWarning($"[FishingSystem] 포획 물고기 생성 실패: result={createResult}");
                ClearCurrentBattleFish();
                ScheduleNextFishing();
                OnFishingResult?.Invoke(resultType);
                return;
            }

            OnFishCaughtPresentation?.Invoke(caughtHandle);

            FishingRewardResult finalizeResult =
                m_rewardProcessor.TryFinalizeCaughtFish(caughtHandle);

            if (finalizeResult != FishingRewardResult.Success)
            {
                Debug.LogWarning($"[FishingSystem] 포획 물고기 지급 실패: result={finalizeResult}");
                ClearCurrentBattleFish();
                ScheduleNextFishing();
                OnFishingResult?.Invoke(FishingResultType.Failed);
                return;
            }

            if (battleFish.BattleData.IsBoss)
            {
                m_rewardProcessor?.Process(battleFish.BattleData.BossDrops);
            }

            Debug.Log($"[FishingSystem] 낚시 성공 및 인벤토리 지급: {battleFish.BattleData.ItemFish.Name}, " +
                $"Size={battleFish.Size:0.00}, " +
                $"Quality={battleFish.Quality}, ");

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

        #endregion

        #region Fish Selection

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

            float totalWeight = 0f;

            foreach (FishPoolEntry entry in pool.Entries)
            {
                if (entry != null && entry.Fish != null && entry.Weight > 0f)
                {
                    totalWeight += entry.Weight;
                }
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float randomValue = UnityEngine.Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (FishPoolEntry entry in pool.Entries)
            {
                if (entry == null || entry.Fish == null || entry.Weight <= 0f)
                {
                    continue;
                }

                currentWeight += entry.Weight;

                if (randomValue <= currentWeight)
                {
                    return entry.Fish;
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

        private float RollFishSize(BattleFishData fishData)
        {
            float minSize = fishData.MinSize;
            float maxSize = fishData.MaxSize;

            return UnityEngine.Random.Range(minSize, maxSize);
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

        #endregion
    }
}
