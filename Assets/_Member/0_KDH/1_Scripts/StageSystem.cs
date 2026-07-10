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

        private Vector2 m_currentLogicalPosition;
        private float m_currentSpeed;
        private float m_remainingTravelTime;

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

            var playerSystem = SystemManager.GetSystem<PlayerSystem>();

            int playerLicense = playerSystem != null ? playerSystem.StartingLicense : 1;
            float playerSpeed = playerSystem != null ? playerSystem.BaseMapMovementSpeedPerTime : 50f;

            if (playerLicense < targetStageData.RequiredLicense)
            {
                Debug.LogWarning($"[StageSystem] 라이센스 부족! 요구: {targetStageData.RequiredLicense}, 현재: {playerLicense}");
                return;
            }

            float distance = Vector2.Distance(m_currentLogicalPosition, targetStageData.MapPosition);

            m_currentSpeed = Mathf.Max(playerSpeed, 0.1f);
            float duration = distance / m_currentSpeed;

            m_targetStageDataId = targetDataId;
            m_remainingTravelTime = duration;
            m_isTraveling = true;

            OnTravelStarted?.Invoke(targetDataId, duration);
        }

        public void CancelTravel()
        {
            if (!m_isTraveling) return;

            m_isTraveling = false;
            m_targetStageDataId = 0;
            m_remainingTravelTime = 0f;

            OnTravelCanceled?.Invoke();
        }

        public void Tick(float dt)
        {
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