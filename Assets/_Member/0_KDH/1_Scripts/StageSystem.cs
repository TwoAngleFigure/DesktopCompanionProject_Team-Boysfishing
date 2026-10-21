using System;
using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Domain.World;
using DesktopCompanion.Data;
using DesktopCompanion.Save;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// [신규 월드 시스템 파사드]: GameManager 및 레거시 시스템들과의 100% 무중단 호환을 유지하면서,
    /// 내부적으로는 신규 그리드 도메인(WorldGridDomain) 및 순수 항해 엔진(VoyageEngine)으로 100% 구동되는 브릿지 시스템
    /// </summary>
    public class StageSystem : SystemBase, ITickable, ISaveable
    {
        public string SaveId => "stage_system";
        public Type StateType => typeof(StageSaveData);

        // 신규 순수 C# 도메인 엔진 코어
        public WorldGridDomain GridDomain { get; private set; } = new WorldGridDomain();
        public VoyageEngine VoyageEngine { get; private set; } = new VoyageEngine();
        public WorldPathfinder Pathfinder { get; private set; } = new WorldPathfinder();
        public IAreaAccessPolicy AccessPolicy { get; set; } = new AlwaysOpenAccessPolicy();

        // 도메인 브로드캐스팅 이벤트 중계
        public event Action<WorldCellData> OnCellEntered
        {
            add => VoyageEngine.OnCellEntered += value;
            remove => VoyageEngine.OnCellEntered -= value;
        }

        public event Action<int, EnvironmentType> OnAreaChanged
        {
            add => VoyageEngine.OnAreaChanged += value;
            remove => VoyageEngine.OnAreaChanged -= value;
        }

        public event Action<VoyageState> OnVoyageStateChanged
        {
            add => VoyageEngine.OnVoyageStateChanged += value;
            remove => VoyageEngine.OnVoyageStateChanged -= value;
        }

        public event Action<AnchorType, WorldCellData> OnAnchored
        {
            add => VoyageEngine.OnAnchored += value;
            remove => VoyageEngine.OnAnchored -= value;
        }

        // =========================================================================
        // 레거시 UI 및 낚시 시스템 호환 이벤트 / 프로퍼티 (기존 시그니처 100% 유지)
        // =========================================================================
        public event Action<int> OnStageChanged;
        public event Action<int, float> OnTravelStarted;
        public event Action OnTravelCanceled;
        public event Action<StageData> OnAreaStageChanged;

        public VoyageState CurrentState => VoyageEngine.CurrentState;
        public bool IsTraveling => CurrentState != VoyageState.Anchored;
        public float RemainingTravelTime => VoyageEngine.RemainingTravelTime;
        public float RemainingDistance => RemainingTravelTime * MaxSpeed;
        public float TravelProgress => VoyageEngine.TravelProgress;
        public Vector2 CurrentLogicalPosition => VoyageEngine.CurrentLogicalPosition;
        public float MaxSpeed => VoyageEngine.MaxSpeed;
        public float CurrentSpeed => VoyageEngine.CurrentSpeed;

        public StageData GetStageData(int stageId) => DataManager.GetData<StageData>(stageId);

        public StageData CurrentStageData
        {
            get
            {
                int currentAreaId = VoyageEngine.CurrentCell.AreaId > 0 ? VoyageEngine.CurrentCell.AreaId : m_lastStageId;
                return DataManager.GetData<StageData>(currentAreaId);
            }
        }

        public StageData CurrentAreaStageData => CurrentStageData;
        public StageData TargetStageData => DataManager.GetData<StageData>(m_targetStageId);

        public List<TierPool> GetAvailableTierPools(int playerLicense)
        {
            var availablePools = new List<TierPool>();
            var stageData = CurrentStageData;
            if (stageData == null || stageData.TierPools == null) return availablePools;

            foreach (var pool in stageData.TierPools)
            {
                if (playerLicense >= pool.RequiredLicense)
                {
                    availablePools.Add(pool);
                }
            }
            return availablePools;
        }

        public IReadOnlyList<StageData> GetAllStageDatas() => DataManager.GetAll<StageData>();

        // 내부 상태 변수
        private int m_lastStageId = 600001;
        private int m_targetStageId = 0;
        private const float MAP_WIDTH = 2048f;
        private const float MAP_HEIGHT = 1024f;
        private const int PIXELS_PER_GRID = 10;

        public override void Initialize()
        {
            Texture2D mapTex = Resources.Load<Texture2D>("Map_01");
            if (mapTex != null)
            {
                BuildGridFromTexture(mapTex);
            }
            else
            {
                BuildDefaultGrid();
            }

            // 항해 가감속 연출 시간 설정 (가속 5초, 감속 5초, 중단 3초, 최대속도 5)
            VoyageEngine.ConfigureTimings(5f, 5f, 3f, 5f);
        }

        public override void PostInitialize()
        {
            var stageData = DataManager.GetData<StageData>(m_lastStageId);
            Vector2 initialPos = stageData != null ? stageData.MapPosition : Vector2.zero;
            VoyageEngine.SetInitialPosition(initialPos, GridDomain);

            VoyageEngine.OnAreaChanged += (newAreaId, env) =>
            {
                m_lastStageId = newAreaId;
                var stage = DataManager.GetData<StageData>(newAreaId);
                if (stage != null)
                {
                    OnAreaStageChanged?.Invoke(stage);
                }
                OnStageChanged?.Invoke(newAreaId);
            };

            VoyageEngine.OnVoyageStateChanged += (state) =>
            {
                if (state == VoyageState.Anchored)
                {
                    if (m_targetStageId != 0)
                    {
                        m_lastStageId = m_targetStageId;
                        m_targetStageId = 0;
                    }
                    OnStageChanged?.Invoke(m_lastStageId);
                }
            };
        }

        public void Tick(float dt)
        {
            VoyageEngine.Tick(dt, GridDomain);
        }

        public void MoveToWorldPosition(Vector2 targetWorldPos, int targetStageId = 0)
        {
            if (CurrentState == VoyageState.Departing || CurrentState == VoyageState.Arriving || CurrentState == VoyageState.Stopping)
            {
                Debug.LogWarning("[StageSystem] 가감속 연출 중에는 목적지를 변경할 수 없습니다.");
                return;
            }

            Vector2Int startCell = GridDomain.WorldToCell(VoyageEngine.CurrentLogicalPosition);
            Vector2Int targetCell = GridDomain.WorldToCell(targetWorldPos);

            List<Vector2Int> path = Pathfinder.FindPath(startCell, targetCell, GridDomain, AccessPolicy);
            if (path == null || path.Count == 0)
            {
                Debug.LogWarning($"[StageSystem] 경로 탐색 실패: 목적지({targetWorldPos})로 갈 수 있는 해로가 없습니다.");
                return;
            }

            m_targetStageId = targetStageId;
            VoyageEngine.StartVoyage(path, GridDomain);
            OnTravelStarted?.Invoke(targetStageId, VoyageEngine.TotalTravelTime);
        }

        public void MoveToStage(int targetStageId)
        {
            var targetStage = DataManager.GetData<StageData>(targetStageId);
            if (targetStage != null)
            {
                MoveToWorldPosition(targetStage.MapPosition, targetStageId);
            }
        }

        public void CancelTravel()
        {
            if (CurrentState == VoyageState.Traveling)
            {
                VoyageEngine.CancelVoyage();
                OnTravelCanceled?.Invoke();
            }
        }

        // =========================================================================
        // 레거시 StageWorldView 호환용 스텁 메서드 (점진적 전환용)
        // =========================================================================
        public void SyncSpeed(float speed) { }
        public void SequenceComplete_Departure() { }
        public void SequenceComplete_Arrival() { }
        public void SequenceComplete_Stop() { }

        // =========================================================================
        // 그리드 빌더 및 세이브/로드
        // =========================================================================
        private void BuildGridFromTexture(Texture2D tex)
        {
            int width = tex.width / PIXELS_PER_GRID;
            int height = tex.height / PIXELS_PER_GRID;
            float cellSize = MAP_WIDTH / width;
            Vector2 origin = new Vector2(-MAP_WIDTH * 0.5f, -MAP_HEIGHT * 0.5f);

            var cellData = new WorldCellData[width, height];
            var allStages = DataManager.GetAll<StageData>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float u = (float)x / (width - 1);
                    float v = (float)y / (height - 1);
                    int px = Mathf.Clamp(Mathf.FloorToInt(u * tex.width), 0, tex.width - 1);
                    int py = Mathf.Clamp(Mathf.FloorToInt(v * tex.height), 0, tex.height - 1);

                    Color pixel = tex.GetPixel(px, py);
                    bool isWater = pixel.b > (pixel.r + 0.15f);

                    Vector2 cellWorldPos = origin + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);

                    int nearestAreaId = 600001;
                    int portId = 0;
                    float minDst = float.MaxValue;

                    if (allStages != null)
                    {
                        for (int s = 0; s < allStages.Count; s++)
                        {
                            var stage = allStages[s];
                            if (stage == null) continue;
                            float d = Vector2.Distance(cellWorldPos, stage.MapPosition);
                            if (d < minDst)
                            {
                                minDst = d;
                                nearestAreaId = stage.ID;
                            }
                            if (d <= cellSize)
                            {
                                portId = stage.ID;
                            }
                        }
                    }

                    EnvironmentType env = (portId > 0) ? EnvironmentType.Port : EnvironmentType.Ocean;
                    cellData[x, y] = new WorldCellData((short)x, (short)y, isWater, nearestAreaId, env, portId);
                }
            }

            GridDomain.Initialize(width, height, cellSize, origin, cellData, m_lastStageId);
            Debug.Log($"[StageSystem] 월드 그리드 초기화 완료: {width}x{height} (셀 크기: {cellSize:F1})");
        }

        private void BuildDefaultGrid()
        {
            int width = 204;
            int height = 102;
            float cellSize = 10f;
            Vector2 origin = new Vector2(-1020f, -510f);
            var cellData = new WorldCellData[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    cellData[x, y] = new WorldCellData((short)x, (short)y, true, 600001, EnvironmentType.Ocean);
                }
            }
            GridDomain.Initialize(width, height, cellSize, origin, cellData, 600001);
        }

        public object CaptureState()
        {
            return new StageSaveData
            {
                currentStageDataId = m_lastStageId
            };
        }

        public void RestoreState(object state)
        {
            if (state is StageSaveData saveData)
            {
                m_lastStageId = saveData.currentStageDataId > 0 ? saveData.currentStageDataId : 600001;
                var stage = DataManager.GetData<StageData>(m_lastStageId);
                Vector2 savedPos = stage != null ? stage.MapPosition : Vector2.zero;
                VoyageEngine.SetInitialPosition(savedPos, GridDomain);
            }
        }
    }

    [Serializable]
    public class StageSaveData
    {
        public int currentStageDataId;
    }
}