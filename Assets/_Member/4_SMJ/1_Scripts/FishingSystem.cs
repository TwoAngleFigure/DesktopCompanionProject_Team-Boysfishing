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

    public class FishingSystem : SystemBase, ITickable
    {
        private int defaultPlayerDataId = 1;
        private int defaultStageDataId = 600001;

        private float baseBattleDuration = 10f;
        private float minBattleDuration = 0.5f;

        private FishingState m_state;
        private int m_currentStageId;
        private EntityHandle m_currentBattleFish;
        private float m_waitTimer;
        private float m_battleTimer;
        private float m_autoAttackTimer;

        private readonly List<EntityHandle> m_caughtFish = new();

        // public event Action<FishingState> OnStateChanged;
        // public event Action<EntityHandle> OnBattleStarted;
        // public event Action<EntityHandle, int, int> OnBattleHpChanged;
        // public event Action<EntityHandle> OnFishCaught;
        // public event Action<EntityHandle> OnBattleFailed;

        public override void Initialize()
        {
            m_state = FishingState.Stopped;
            m_currentStageId = 0;
            m_currentBattleFish = default;
            m_waitTimer = 0f;
            m_battleTimer = 0f;
            m_autoAttackTimer = 0f;

            StartFishing(defaultStageDataId);
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
                PlayerData playerData = GetPlayerData();

                int damage = 1;
                float attackInterval = 1f;

                if (playerData != null)
                {
                    damage = Mathf.RoundToInt(playerData.BaseDamagePerClick * playerData.BaseAutoDamagePerHitMultiply);
                    attackInterval = playerData.BaseAutoSpeedPerTime;
                }

                Debug.Log($"[FishingSystem] 자동 공격: damage={damage}");

                ApplyDamage(damage);

                m_autoAttackTimer = attackInterval;
            }
        }
        public void StartFishing(int stageId)
        {
            StageData stageData = DataManager.GetData<StageData>(stageId);

            if (stageData == null)
            {
                Debug.LogWarning($"[FishingSystem] StageData를 찾지 못했습니다. stageId={stageId}");
                return;
            }

            m_currentStageId = stageId;


            m_waitTimer = 0f;
            ChangeState(FishingState.Waiting);

            Debug.Log($"[FishingSystem] 자동 낚시 시작: stageId={stageId}, 대기시간={m_waitTimer:0.00}초");
        }

        public void StopFishing()
        {
            Debug.Log("[FishingSystem] 자동 낚시 중지");

            ClearCurrentBattleFish();

            m_currentStageId = 0;
            m_waitTimer = 0f;
            m_battleTimer = 0f;
            m_autoAttackTimer = 0f;

            ChangeState(FishingState.Stopped);
        }

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

            m_battleTimer = CalculateBattleDuration(fishData);
            m_autoAttackTimer = 0f;

            ChangeState(FishingState.Battling);

            Debug.Log($"[FishingSystem] 전투 시작: {fishData.Name}, HP={battleFish.CurrentHp}/{fishData.MaxHp}, Size={size:0.00}, Quality={quality}, 제한시간={m_battleTimer:0.00}초");
        }


        private void ApplyDamage(int damage)
        {
            Entity_BattleFish battleFish = EntityManager.Get<Entity_BattleFish>(m_currentBattleFish);

            if (battleFish == null)
            {
                Debug.LogWarning("[FishingSystem] 데미지 적용 실패: 현재 전투 물고기가 없습니다.");
                ClearCurrentBattleFish();
                ScheduleNextFishing();
                return;
            }

            battleFish.ApplyDamage(damage);

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
                return;
            }

            EntityHandle caughtHandle = CreateCaughtFish(battleFish);

            m_caughtFish.Add(caughtHandle);

            Debug.Log($"[FishingSystem] 낚시 성공: {battleFish.BattleData.ItemFish.Name}, " +
                $"Size={battleFish.Size:0.00}, " +
                $"Quality={battleFish.Quality}, " +
                $"총 낚은 수={m_caughtFish.Count}");

            ClearCurrentBattleFish();
            ScheduleNextFishing();
        }

        private void FailBattle(string reason)
        {
            Entity_BattleFish battleFish = EntityManager.Get<Entity_BattleFish>(m_currentBattleFish);
            string fishName = battleFish != null ? battleFish.Name : "Unknown";

            Debug.Log($"[FishingSystem] 포획 실패: {fishName}, reason={reason}");

            ClearCurrentBattleFish();
            ScheduleNextFishing();
        }

        private EntityHandle CreateCaughtFish(Entity_BattleFish battleFish)
        {
            if (battleFish.BattleData.ItemFish == null)
            {
                Debug.LogWarning("[FishingSystem] BattleFishData에 ItemFish가 연결되어 있지 않습니다.");
                return default;
            }

            EntityHandle fishHandle = EntityManager.Create<ItemData_Fish>(battleFish.BattleData.ItemFish.ID);
            Entity_Fish fish = EntityManager.Get<Entity_Fish>(fishHandle);

            if (fish == null)
            {
                return default;
            }

            fish.SetRollResult(battleFish.Size, battleFish.Quality);
            return fishHandle;
        }

        private void ScheduleNextFishing()
        {
            if (m_currentStageId == 0)
            {
                ChangeState(FishingState.Stopped);
                return;
            }

            m_waitTimer = CalculateNextFishingDelay();
            m_battleTimer = 0f;
            m_autoAttackTimer = 0f;

            ChangeState(FishingState.Waiting);

            Debug.Log($"[FishingSystem] 다음 입질 대기: {m_waitTimer:0.00}초");
        }


        private BattleFishData SelectBattleFish()
        {
            StageData stageData = DataManager.GetData<StageData>(m_currentStageId);

            if (stageData != null)
            {
                BattleFishData fishFromStage = SelectBattleFishFromStage(stageData);

                if (fishFromStage != null)
                {
                    Debug.Log($"[FishingSystem] StageData 사용: stage={stageData.Name}, fish={fishFromStage.Name}");
                    return fishFromStage;
                }

                Debug.LogWarning($"[FishingSystem] StageData({stageData.Name})에 선택 가능한 물고기 없음.");
            }

            return null;
        }

        private BattleFishData SelectBattleFishFromStage(StageData stageData)
        {
            if (stageData == null)
            {
                return null;
            }

            if (stageData.TierPools == null || stageData.TierPools.Length == 0)
            {
                return null;
            }

            TierPool pool = stageData.TierPools[0];

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
                if (entry == null || entry.Fish == null || entry.Weight < 0f)
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

        private float RollFishSize(BattleFishData fishData)
        {
            float minSize = fishData.MinSize;
            float maxSize = fishData.MaxSize;

            return UnityEngine.Random.Range(minSize, maxSize);
        }

        private float CalculateBattleDuration(BattleFishData fishData)
        {
            PlayerData playerData = GetPlayerData();

            if (playerData == null)
            {
                Debug.Log("[FishingSystem] PlayerData를 찾지 못해 기본 전투 시간을 사용합니다.");
                return baseBattleDuration;
            }

            float duration = baseBattleDuration + playerData.BaseBattleTimeVariable - fishData.BattleTimeVariable;

            return Mathf.Max(minBattleDuration, duration);
        }

        private float CalculateNextFishingDelay()
        {
            PlayerData playerData = GetPlayerData();

            if (playerData == null)
            {
                Debug.LogWarning("[FishingSystem] PlayerData를 찾지 못해 기본 낚시 대기시간을 사용합니다.");
                return 10f;
            }

            float baseDelay = playerData.BaseAutoBattleCooltime;

            float minDelay = baseDelay * 0.8f;
            float maxDelay = baseDelay * 1.2f;

            return UnityEngine.Random.Range(minDelay, maxDelay);
        }

        private PlayerData GetPlayerData()
        {
            PlayerData playerData = DataManager.GetData<PlayerData>(defaultPlayerDataId);

            if (playerData != null)
            {
                return playerData;
            }

            IReadOnlyList<PlayerData> players = DataManager.GetAll<PlayerData>();
            return players.Count > 0 ? players[0] : null;
        }

        private void ChangeState(FishingState nextState)
        {
            if (m_state == nextState)
            {
                return;
            }

            m_state = nextState;

            Debug.Log($"[FishingSystem] 상태 변경: {nextState}");
        }

        private void ClearCurrentBattleFish()
        {
            EntityManager.Destroy(m_currentBattleFish);
            m_currentBattleFish = default;
        }
    }
}
