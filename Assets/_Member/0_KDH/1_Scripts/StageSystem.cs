using System;
using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Systems;
using DesktopCompanion.Data;
using DesktopCompanion.Save;

namespace DesktopCompanion.Systems
{
    public enum VoyageState
    {
        Anchored,  
        Departing, 
        Traveling, 
        Arriving 
    }

    public class StageSystem : SystemBase, ISaveable, ITickable
    {
        private int m_currentStageDataId;
        private int m_targetStageDataId;

        private VoyageState m_currentState = VoyageState.Anchored;

        private const float DEPARTURE_DURATION = 9.0f;
        private const float ARRIVAL_DURATION = 9.0f;

        private Vector2 m_currentLogicalPosition;
        private float m_currentSpeed;

        private float m_elapsedTravelTime;
        private float m_remainingTravelTime;
        private float m_totalTravelTime;

        public event Action<int> OnStageChanged;
        public event Action<int, float> OnTravelStarted;
        public event Action OnTravelCanceled;
        public event Action<VoyageState> OnVoyageStateChanged;

        public string SaveId => "stage_system";
        public Type StateType => typeof(StageSaveData);

        public override void Initialize()
        {
            ForceSetInitialStage(600001);
        }
        public override void PostInitialize() { }

        private void ForceSetInitialStage(int defaultDataId)
        {
            var stageData = DataManager.GetData<StageData>(defaultDataId);
            if (stageData != null)
            {
                m_currentStageDataId = defaultDataId;
                m_currentLogicalPosition = stageData.MapPosition;
                ChangeState(VoyageState.Anchored);
            }
        }

        private void ChangeState(VoyageState newState)
        {
            if (m_currentState == newState) return;
            m_currentState = newState;
            OnVoyageStateChanged?.Invoke(m_currentState);
            Debug.Log($"[StageSystem] 배 상태 변경 ➡️ {newState}");
        }

        public void MoveToStage(int targetDataId)
        {
            if (m_currentState == VoyageState.Departing || m_currentState == VoyageState.Arriving)
            {
                Debug.Log($"[StageSystem] 현재 {m_currentState} 상태입니다. 조작할 수 없습니다.");
                return;
            }

            if (m_currentState == VoyageState.Traveling && m_targetStageDataId == targetDataId) return;

            var targetStageData = DataManager.GetData<StageData>(targetDataId);
            if (targetStageData == null) return;

            if (m_currentState == VoyageState.Anchored && Vector2.Distance(m_currentLogicalPosition, targetStageData.MapPosition) <= 0.001f)
            {
                Debug.Log("[StageSystem] 이미 해당 위치에 정박해 있습니다.");
                return;
            }

            var playerSystem = SystemManager.GetSystem<PlayerSystem>();
            float playerSpeed = playerSystem != null ? playerSystem.BaseMapMovementSpeedPerTime : 50f;
            m_currentSpeed = Mathf.Max(playerSpeed, 0.1f);

            int playerLicense = playerSystem != null ? playerSystem.StartingLicense : 1;
            if (playerLicense < targetStageData.RequiredLicense)
            {
                Debug.LogWarning($"[StageSystem] 라이센스 부족! 요구: {targetStageData.RequiredLicense}");
                return;
            }

            float distance = Vector2.Distance(m_currentLogicalPosition, targetStageData.MapPosition);
            float duration = distance / m_currentSpeed;

            m_targetStageDataId = targetDataId;
            m_remainingTravelTime = duration;
            m_totalTravelTime = duration;

            OnTravelStarted?.Invoke(targetDataId, duration);

            if (m_currentState == VoyageState.Anchored)
            {
                m_elapsedTravelTime = 0f;
                ChangeState(VoyageState.Departing);
            }
            else
            {
                ChangeState(VoyageState.Traveling);
            }
        }

        public void CancelTravel()
        {
            if (m_currentState != VoyageState.Traveling)
            {
                Debug.Log($"[StageSystem] 현재 {m_currentState} 상태라 항해를 중단할 수 없습니다.");
                return;
            }

            m_targetStageDataId = 0;
            m_remainingTravelTime = 0f;

            ChangeState(VoyageState.Anchored);

            OnTravelCanceled?.Invoke();
        }

        public void Tick(float dt)
        {
            if (m_currentState == VoyageState.Anchored) return;

            var targetStageData = DataManager.GetData<StageData>(m_targetStageDataId);
            if (targetStageData == null) return;

            m_currentLogicalPosition = Vector2.MoveTowards(m_currentLogicalPosition, targetStageData.MapPosition, m_currentSpeed * dt);
            float distance = Vector2.Distance(m_currentLogicalPosition, targetStageData.MapPosition);

            m_remainingTravelTime = distance / m_currentSpeed;
            m_elapsedTravelTime += dt;

            if (m_currentState == VoyageState.Departing)
            {
                if (m_elapsedTravelTime >= DEPARTURE_DURATION)
                {
                    ChangeState(VoyageState.Traveling);
                }
            }
            else if (m_currentState == VoyageState.Traveling)
            {
                if (m_remainingTravelTime <= ARRIVAL_DURATION)
                {
                    ChangeState(VoyageState.Arriving);
                }
            }
            else if (m_currentState == VoyageState.Arriving)
            {
                if (distance <= 0.001f)
                {
                    m_currentLogicalPosition = targetStageData.MapPosition;
                    m_remainingTravelTime = 0f;
                    m_currentStageDataId = m_targetStageDataId;
                    m_targetStageDataId = 0;

                    ChangeState(VoyageState.Anchored);
                    OnStageChanged?.Invoke(m_currentStageDataId);
                }
            }
        }

        // 기존 프로퍼티 대체
        public StageData CurrentStageData => DataManager.GetData<StageData>(m_currentStageDataId);
        public StageData TargetStageData => DataManager.GetData<StageData>(m_targetStageDataId);
        public bool IsTraveling => m_currentState != VoyageState.Anchored;
        public VoyageState CurrentState => m_currentState;
        public float RemainingTravelTime => m_remainingTravelTime;
        public Vector2 CurrentLogicalPosition => m_currentLogicalPosition;
        public float TravelProgress => (m_currentState != VoyageState.Anchored && m_totalTravelTime > 0f) ? (1f - (m_remainingTravelTime / m_totalTravelTime)) : 0f;

        public List<TierPool> GetAvailableTierPools(int playerLicense)
        {
            var availablePools = new List<TierPool>();
            var stageData = CurrentStageData;

            if (stageData == null || stageData.TierPools == null) return availablePools;

            foreach (var pool in stageData.TierPools)
            {
                if (playerLicense >= pool.RequiredLicense) availablePools.Add(pool);
            }
            return availablePools;
        }

        public IReadOnlyList<StageData> GetAllStageDatas() => DataManager.GetAll<StageData>();

        public object CaptureState()
        {
            return new StageSaveData
            {
                currentStageDataId = m_currentStageDataId,
                savedPosX = m_currentLogicalPosition.x,
                savedPosY = m_currentLogicalPosition.y
            };
        }

        public void RestoreState(object state)
        {
            bool ignoreSaveForTesting = true;
            if (ignoreSaveForTesting)
            {
                ForceSetInitialStage(600001);
                return;
            }

            var save = (StageSaveData)state;
            m_currentStageDataId = save.currentStageDataId;

            if (save.savedPosX != 0 || save.savedPosY != 0)
            {
                m_currentLogicalPosition = new Vector2(save.savedPosX, save.savedPosY);
            }
            else
            {
                ForceSetInitialStage(m_currentStageDataId != 0 ? m_currentStageDataId : 600001);
            }
        }
    }

    [Serializable]
    public class StageSaveData
    {
        public int currentStageDataId;
        public float savedPosX;
        public float savedPosY;
    }
}