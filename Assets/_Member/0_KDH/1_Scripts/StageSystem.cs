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
        Arriving,
        Stopping,
        None
    }

    public class StageSystem : SystemBase, ITickable, ISaveable
    {
        private int m_currentStageDataId;
        private int m_targetStageDataId;
        private int m_currentAreaStageDataId;

        public string SaveId => "stage_system";
        public Type StateType => typeof(StageSaveData);

        // UI 및 View에서 요구하는 이벤트들
        public event Action<int> OnStageChanged;
        public event Action<int, float> OnTravelStarted;
        public event Action OnTravelCanceled;
        public event Action<VoyageState> OnVoyageStateChanged;
        // 실시간 위치(영역) 변경 이벤트 추가 (가장 가까운 스테이지 노드 기준 50% 분할)
        public event Action<StageData> OnAreaStageChanged;

        private const float DEPARTURE_DURATION = 21.0f;
        private const float ARRIVAL_DURATION = 21.0f;

        // 성능 최적화: 0.25초 간격으로만 실시간 영역 검사
        private float m_areaCheckTimer = 0f;
        private const float AREA_CHECK_INTERVAL = 0.25f;

        public override void Initialize() { }

        public override void PostInitialize()
        {
            ForceSetInitialStage(600001);

            // VoyageController의 상태 변화를 UI용으로 중계(Proxy)하기 위해 구독
            var voyageCtrl = SystemManager.GetSystem<VoyageSystem>();
            if (voyageCtrl != null)
            {
                voyageCtrl.OnVoyageStateChanged += HandleVoyageStateChanged;
            }
        }

        public void Tick(float dt)
        {
            // 성능 최적화: 0.25초 간격 타임 인터벌 연산
            m_areaCheckTimer += dt;
            if (m_areaCheckTimer >= AREA_CHECK_INTERVAL)
            {
                m_areaCheckTimer = 0f;
                UpdateCurrentAreaStage();
            }
        }

        private void UpdateCurrentAreaStage()
        {
            var voyageCtrl = SystemManager.GetSystem<VoyageSystem>();
            if (voyageCtrl == null) return;

            Vector2 currentPos = voyageCtrl.CurrentLogicalPosition;
            var allStages = GetAllStageDatas();
            if (allStages == null || allStages.Count == 0) return;

            int nearestStageId = m_currentStageDataId;
            float minSqrDistance = float.MaxValue;

            // 최적화: sqrMagnitude (제곱거리)를 사용하여 가장 가까운 스테이지 노드 감지 (50% 경계선 판정)
            for (int i = 0; i < allStages.Count; i++)
            {
                var stage = allStages[i];
                if (stage == null) continue;

                float sqrDst = (stage.MapPosition - currentPos).sqrMagnitude;
                if (sqrDst < minSqrDistance)
                {
                    minSqrDistance = sqrDst;
                    nearestStageId = stage.ID;
                }
            }

            if (nearestStageId != m_currentAreaStageDataId)
            {
                m_currentAreaStageDataId = nearestStageId;
                var newAreaStage = DataManager.GetData<StageData>(m_currentAreaStageDataId);
                if (newAreaStage != null)
                {
                    Debug.Log($"[StageSystem] 영역(Stage) 변경 감지! 가장 가까운 스테이지: {newAreaStage.name} ({m_currentAreaStageDataId})");
                    OnAreaStageChanged?.Invoke(newAreaStage);
                }
            }
        }

        private void ForceSetInitialStage(int defaultDataId)
        {
            var stageData = DataManager.GetData<StageData>(defaultDataId);
            if (stageData != null)
            {
                m_currentStageDataId = defaultDataId;

                // 초기 위치 갱신
                var voyageCtrl = SystemManager.GetSystem<VoyageSystem>();
                if (voyageCtrl != null) voyageCtrl.SetLogicalPosition(stageData.MapPosition);
            }
        }

        // 유저가 목적지를 클릭했을 때 호출되는 핵심 진입점
        public void MoveToStage(int targetDataId)
        {
            var voyageCtrl = SystemManager.GetSystem<VoyageSystem>();
            if (voyageCtrl == null) return;

            // 출발(21초), 도착(21초), 멈춤(9초) 연출 중일 때는 목적지 변경 차단! (순수 Traveling/Anchored 시엔 허용)
            if (voyageCtrl.CurrentState == VoyageState.Departing ||
                voyageCtrl.CurrentState == VoyageState.Arriving ||
                voyageCtrl.CurrentState == VoyageState.Stopping)
            {
                Debug.LogWarning($"[StageSystem] 출발/도착/멈춤 연출 중에는 목적지를 변경할 수 없습니다.");
                return;
            }

            var targetStageData = DataManager.GetData<StageData>(targetDataId);
            var currentStageData = CurrentStageData;
            if (targetStageData == null) return;

            // 2. 라이센스(조건) 체크
            var playerSystem = SystemManager.GetSystem<PlayerSystem>();
            int playerLicense = playerSystem != null ? playerSystem.StartingLicense : 1;
            if (playerLicense < targetStageData.RequiredLicense)
            {
                Debug.LogWarning($"[StageSystem] 라이센스 부족! 요구: {targetStageData.RequiredLicense}");
                return;
            }

            // 3. 배의 실시간 현재 위치(CurrentLogicalPosition) 기반 A* 최단 경로 탐색
            Vector2 startPos = voyageCtrl.CurrentLogicalPosition;
            var pathfinder = SystemManager.GetSystem<PathfindingSystem>();
            List<Vector2> path = pathfinder.FindPath(startPos, targetStageData.MapPosition);

            if (path == null || path.Count == 0)
            {
                Debug.LogWarning("[StageSystem] 목적지까지 갈 수 있는 경로가 없습니다! (장애물 또는 데이터 오류)");
                return;
            }

            // A* 경로의 실제 총 물리적 거리 연산
            float distance = 0f;
            Vector2 prev = startPos;
            for (int i = 0; i < path.Count; i++)
            {
                distance += Vector2.Distance(prev, path[i]);
                prev = path[i];
            }

            float maxSpeed = voyageCtrl.MaxSpeed;

            // 4. 새 목적지까지의 거리가 21초 이동거리(maxSpeed * 21.0f) 이내면 시스템적 차단!
            float minRequiredDist = maxSpeed * 21.0f;
            if (distance < minRequiredDist)
            {
                Debug.LogWarning($"[StageSystem] 새 목적지까지의 거리({distance:F1})가 21초 이동거리({minRequiredDist:F1}) 이내로 너무 가까워 이동할 수 없습니다!");
                return;
            }

            Debug.Log($" [경로 탐색 완료] A* 알고리즘이 총 {path.Count}개의 웨이포인트(경유지)를 찾았습니다! 총거리: {distance:F1}");

            float arrivalTriggerDistance = maxSpeed * ARRIVAL_DURATION * 0.5f;
            float departureDistance = maxSpeed * DEPARTURE_DURATION * 0.5f;

            float pureTravelDistance = distance - arrivalTriggerDistance - departureDistance;
            if (pureTravelDistance < 0f) pureTravelDistance = 0f;

            float pureTravelTime = pureTravelDistance / maxSpeed;
            float totalTravelTime = DEPARTURE_DURATION + pureTravelTime + ARRIVAL_DURATION;

            m_targetStageDataId = targetDataId;

            // UI에 출발 이벤트 방송 및 VoyageController에 명령 하달
            OnTravelStarted?.Invoke(targetDataId, totalTravelTime);
            voyageCtrl.StartVoyage(path, totalTravelTime);
        }

        // UI에서 호출하는 취소 기능 (순수 항해 Traveling 상태일 때만 21초 브레이크 수용)
        public void CancelTravel()
        {
            var voyageCtrl = SystemManager.GetSystem<VoyageSystem>();
            if (voyageCtrl != null && voyageCtrl.CurrentState == VoyageState.Traveling)
            {
                voyageCtrl.CancelVoyage(); // 컨트롤러 정지
                OnTravelCanceled?.Invoke(); // UI 방송
            }
            else
            {
                Debug.LogWarning("[StageSystem] 출발(21초) 또는 도착(21초) 시퀀스 중에는 항해를 중단할 수 없습니다.");
            }
        }

        // VoyageController가 상태를 바꿀 때 데이터를 갱신합니다.
        private void HandleVoyageStateChanged(VoyageState state)
        {
            // UI를 위해 상태 변경 이벤트 중계
            OnVoyageStateChanged?.Invoke(state);

            if (state == VoyageState.Anchored && m_targetStageDataId != 0)
            {
                m_currentStageDataId = m_targetStageDataId;
                m_targetStageDataId = 0;

                OnStageChanged?.Invoke(m_currentStageDataId);
                Debug.Log($"[StageSystem] {m_currentStageDataId} 스테이지에 성공적으로 정박했습니다.");
            }
            else if (state == VoyageState.Anchored && m_targetStageDataId == 0)
            {
                // 취소 후 정박이 완료되었을 때의 처리 (필요 시 확장)
            }
        }

        // UI가 참조하던 상태 Getter들 복구 (VoyageController의 데이터를 대리 반환)
        public StageData CurrentStageData => DataManager.GetData<StageData>(m_currentStageDataId);
        public StageData CurrentAreaStageData => DataManager.GetData<StageData>(m_currentAreaStageDataId > 0 ? m_currentAreaStageDataId : m_currentStageDataId);
        public StageData TargetStageData => DataManager.GetData<StageData>(m_targetStageDataId);
        public IReadOnlyList<StageData> GetAllStageDatas() => DataManager.GetAll<StageData>();

        public VoyageState CurrentState => SystemManager.GetSystem<VoyageSystem>()?.CurrentState ?? VoyageState.Anchored;
        public bool IsTraveling => CurrentState != VoyageState.Anchored;
        public float RemainingTravelTime => SystemManager.GetSystem<VoyageSystem>()?.RemainingTravelTime ?? 0f;
        public float TravelProgress => SystemManager.GetSystem<VoyageSystem>()?.TravelProgress ?? 0f;
        public float RemainingDistance => SystemManager.GetSystem<VoyageSystem>()?.RemainingDistance ?? 0f;
        public float MaxSpeed => SystemManager.GetSystem<VoyageSystem>()?.MaxSpeed ?? 5f;

        public void SyncSpeed(float speed)
        {
        }

        public void SequenceComplete_Departure()
        {
            var voyage = SystemManager.GetSystem<VoyageSystem>();
            if (voyage != null && voyage.CurrentState == VoyageState.Departing)
            {
                m_currentStageDataId = 0;
            }
        }

        public void SequenceComplete_Arrival()
        {
            var voyage = SystemManager.GetSystem<VoyageSystem>();
            if (voyage != null && voyage.CurrentState == VoyageState.Arriving)
            {
                m_currentStageDataId = m_targetStageDataId;
                m_targetStageDataId = 0;
                OnStageChanged?.Invoke(m_currentStageDataId);
            }
        }

        public void SequenceComplete_Stop()
        {
            var voyage = SystemManager.GetSystem<VoyageSystem>();
            if (voyage != null && voyage.CurrentState == VoyageState.Stopping)
            {
                m_targetStageDataId = 0;
            }
        }

        public List<TierPool> GetAvailableTierPools(int playerLicense)
        {
            var availablePools = new List<TierPool>();
            var stageData = CurrentStageData;
            if (stageData == null || stageData.TierPools == null) return availablePools;
            foreach (var pool in stageData.TierPools) { if (playerLicense >= pool.RequiredLicense) availablePools.Add(pool); }
            return availablePools;
        }

        // === Save / Load 로직 ===
        public object CaptureState()
        {
            return new StageSaveData { currentStageDataId = m_currentStageDataId };
        }

        public void RestoreState(object state)
        {
            var save = (StageSaveData)state;
            m_currentStageDataId = save.currentStageDataId;
            if (m_currentStageDataId == 0) ForceSetInitialStage(600001);
            else ForceSetInitialStage(m_currentStageDataId);
        }
    }

    [Serializable]
    public class StageSaveData
    {
        public int currentStageDataId;
    }
}