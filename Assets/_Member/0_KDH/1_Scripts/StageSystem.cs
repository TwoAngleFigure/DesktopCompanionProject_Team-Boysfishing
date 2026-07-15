using System;
using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Systems;
using DesktopCompanion.Data;
using DesktopCompanion.Save;

namespace DesktopCompanion.Systems
{
    public class StageSystem : SystemBase, ISaveable, ITickable
    {
        private int m_currentStageDataId;
        private int m_targetStageDataId;
        private bool m_isTraveling;

        private float m_departureLockTimer = 0f;
        private const float DEPARTURE_LOCK_LOGICAL_DISTANCE = 80f;
        private const float PORT_ENTRY_LOGICAL_DISTANCE = 74f;

        private Vector2 m_currentLogicalPosition;
        private float m_currentSpeed;
        private float m_remainingTravelTime;
        private float m_totalTravelTime;

        public event Action<int> OnStageChanged;
        public event Action<int, float> OnTravelStarted;
        public event Action OnTravelCanceled;

        public string SaveId => "stage_system";
        public Type StateType => typeof(StageSaveData);

        public override void Initialize()
        {
            ForceSetInitialStage(600001);
        }
        public override void PostInitialize()
        {
            //start
        }


        private void ForceSetInitialStage(int defaultDataId)
        {
            var stageData = DataManager.GetData<StageData>(defaultDataId);
            if (stageData != null)
            {
                m_currentStageDataId = defaultDataId;
                m_currentLogicalPosition = stageData.MapPosition;
                m_isTraveling = false;
            }
        }

        public void MoveToStage(int targetDataId)
        {
            var playerSystem = SystemManager.GetSystem<PlayerSystem>();
            float playerSpeed = playerSystem != null ? playerSystem.BaseMapMovementSpeedPerTime : 50f;
            float speed = Mathf.Max(playerSpeed, 0.1f);

            float entryTimeTrigger = PORT_ENTRY_LOGICAL_DISTANCE / speed;

            if (m_departureLockTimer > 0f)
            {
                Debug.Log("[StageSystem] 출발 직후입니다. 맵을 완전히 벗어날 때까지 대기하세요.");
                return;
            }

            if (m_isTraveling && m_targetStageDataId != targetDataId)
            {
                if (m_remainingTravelTime <= entryTimeTrigger)
                {
                    Debug.Log($"[StageSystem] 이미 항만 진입 구역(남은 시간 {entryTimeTrigger:F1}초 이하)에 들어섰습니다. 회항할 수 없습니다.");
                    return;
                }
            }

            if (m_isTraveling && m_targetStageDataId == targetDataId) return;

            var targetStageData = DataManager.GetData<StageData>(targetDataId);
            if (targetStageData == null)
            {
                Debug.LogError($"[StageSystem] 목표맵 ID({targetDataId}) 데이터를 찾을 수 없습니다.");
                return;
            }

            if (!m_isTraveling && Vector2.Distance(m_currentLogicalPosition, targetStageData.MapPosition) <= 0.001f)
            {
                Debug.Log("[StageSystem] 이미 해당 위치에 있습니다.");
                return;
            }

            int playerLicense = playerSystem != null ? playerSystem.StartingLicense : 1;
            if (playerLicense < targetStageData.RequiredLicense)
            {
                Debug.LogWarning($"[StageSystem] 라이센스 부족! 요구: {targetStageData.RequiredLicense}, 현재: {playerLicense}");
                return;
            }

            float distance = Vector2.Distance(m_currentLogicalPosition, targetStageData.MapPosition);

            bool isStartingFromAnchor = false;
            var currentStage = CurrentStageData;
            if (currentStage != null)
            {
                float distToAnchor = Vector2.Distance(m_currentLogicalPosition, currentStage.MapPosition);
                isStartingFromAnchor = distToAnchor <= 0.001f;
            }

            bool isUturn = m_isTraveling && m_targetStageDataId != targetDataId;

            m_currentSpeed = speed;
            float duration = distance / m_currentSpeed;

            m_targetStageDataId = targetDataId;
            m_remainingTravelTime = duration;
            m_totalTravelTime = duration;
            m_isTraveling = true;

            OnTravelStarted?.Invoke(targetDataId, duration);

            if (isStartingFromAnchor || isUturn)
            {
                float departureLockDuration = DEPARTURE_LOCK_LOGICAL_DISTANCE / m_currentSpeed;
                m_departureLockTimer = departureLockDuration;
                Debug.Log($"[StageSystem] 조작 잠금 활성화: {departureLockDuration:F1}초 (현재 속도: {m_currentSpeed})");
            }
        }

        public void CancelTravel()
        {
            if (m_departureLockTimer > 0f)
            {
                Debug.Log("[StageSystem] 출발 직후입니다. 맵을 완전히 벗어날 때까지 중단할 수 없습니다.");
                return;
            }

            if (m_isTraveling)
            {
                float entryTimeTrigger = PORT_ENTRY_LOGICAL_DISTANCE / m_currentSpeed;

                if (m_remainingTravelTime <= entryTimeTrigger)
                {
                    Debug.Log($"[StageSystem] 이미 항만 진입 구역(남은 시간 {entryTimeTrigger:F1}초 이하)에 들어섰습니다. 정박 과정을 중단할 수 없습니다.");
                    return;
                }
            }

            if (!m_isTraveling) return;

            m_isTraveling = false;
            m_targetStageDataId = 0;
            m_remainingTravelTime = 0f;

            OnTravelCanceled?.Invoke();
        }

        public void Tick(float dt)
        {
            if (m_departureLockTimer > 0f)
            {
                m_departureLockTimer -= dt;
            }

            if (!m_isTraveling) return;

            var targetStageData = DataManager.GetData<StageData>(m_targetStageDataId);
            if (targetStageData == null) return;

            m_currentLogicalPosition = Vector2.MoveTowards(m_currentLogicalPosition, targetStageData.MapPosition, m_currentSpeed * dt);

            float distance = Vector2.Distance(m_currentLogicalPosition, targetStageData.MapPosition);
            m_remainingTravelTime = distance / m_currentSpeed;

            if (distance <= 0.001f)
            {
                m_currentLogicalPosition = targetStageData.MapPosition;
                m_remainingTravelTime = 0f;
                m_isTraveling = false;

                m_currentStageDataId = m_targetStageDataId;
                m_targetStageDataId = 0;

                OnStageChanged?.Invoke(m_currentStageDataId);
            }
        }

        public StageData CurrentStageData => DataManager.GetData<StageData>(m_currentStageDataId);
        public StageData TargetStageData => DataManager.GetData<StageData>(m_targetStageDataId);
        public float TravelProgress => m_isTraveling && m_totalTravelTime > 0f ? (1f - (m_remainingTravelTime / m_totalTravelTime)) : 0f;
        public bool IsTraveling => m_isTraveling;
        public float RemainingTravelTime => m_remainingTravelTime;
        public Vector2 CurrentLogicalPosition => m_currentLogicalPosition;

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

        public IReadOnlyList<StageData> GetAllStageDatas()
        {
            return DataManager.GetAll<StageData>();
        }

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
            bool ignoreSaveForTesting = true; //테스트용, 배포시에는 false로 변경 필수//

            if (ignoreSaveForTesting)
            {
                Debug.Log("[StageSystem] 테스트 모드 켜짐: 세이브 위치를 무시하고 600001로 강제 초기화합니다.");
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