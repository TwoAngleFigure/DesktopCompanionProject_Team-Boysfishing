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
        private bool m_isTraveling;
        private int m_targetStageDataId;
        private float m_travelTimer;
        private float m_totalTravelDuration;

        public event Action<int> OnStageChanged;
        public event Action<int, float> OnTravelStarted;

        public string SaveId => "stage_system";
        public Type StateType => typeof(StageSaveData);

        public override void Initialize()
        {
            ForceSetInitialStage(600001);
        }

        private void ForceSetInitialStage(int defaultDataId)
        {
            var stageData = DataManager.GetData<StageData>(defaultDataId);
            if (stageData != null)
            {
                m_currentStageDataId = defaultDataId;
                m_isTraveling = false;
            }
        }

        public void MoveToStage(int targetDataId, int playerLicense, float playerSpeed)
        {
            if (m_isTraveling) return;
            if (m_currentStageDataId == targetDataId) return;

            var currentStageData = DataManager.GetData<StageData>(m_currentStageDataId);
            var targetStageData = DataManager.GetData<StageData>(targetDataId);

            if (currentStageData == null || targetStageData == null)
            {
                Debug.LogError($"[StageSystem] 맵 이동 실패! 데이터를 찾을 수 없습니다. " +
                               $"현재맵 ID({m_currentStageDataId}) 로드: {(currentStageData != null ? "성공" : "실패")} / " +
                               $"목표맵 ID({targetDataId}) 로드: {(targetStageData != null ? "성공" : "실패")}");
                return;
            }

            if (playerLicense < targetStageData.RequiredLicense)
            {
                Debug.LogWarning($"[StageSystem] 라이센스 부족! 요구: {targetStageData.RequiredLicense}, 현재: {playerLicense}");
                return;
            }

            float distance = Vector2.Distance(currentStageData.MapPosition, targetStageData.MapPosition);
            float speed = Mathf.Max(playerSpeed, 0.1f);
            float duration = distance / speed;

            m_targetStageDataId = targetDataId;
            m_totalTravelDuration = duration;
            m_travelTimer = duration;
            m_isTraveling = true;

            OnTravelStarted?.Invoke(targetDataId, duration);
        }

        public void Tick(float dt)
        {
            if (!m_isTraveling) return;

            m_travelTimer -= dt;

            if (m_travelTimer <= 0f)
            {
                m_travelTimer = 0f;
                m_isTraveling = false;

                m_currentStageDataId = m_targetStageDataId;
                m_targetStageDataId = 0;

                OnStageChanged?.Invoke(m_currentStageDataId);
            }
        }

        public StageData CurrentStageData => DataManager.GetData<StageData>(m_currentStageDataId);
        public bool IsTraveling => m_isTraveling;
        public float RemainingTravelTime => m_travelTimer;

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

        public object CaptureState()
        {
            return new StageSaveData { currentStageDataId = m_currentStageDataId };
        }

        public void RestoreState(object state)
        {
            var save = (StageSaveData)state;
            ForceSetInitialStage(save.currentStageDataId);
        }
    }

    [Serializable]
    public class StageSaveData
    {
        public int currentStageDataId;
    }
}