using System;
using System.Collections.Generic;
using UnityEngine;
using DesktopCompanion.Domain.World;
using DesktopCompanion.Data;
using DesktopCompanion.Save;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 유니티 프레임워크(SystemBase, ITickable, ISaveable)와 순수 도메인 월드 엔진을 연결하는 브릿지 시스템
    /// [타 시스템 무접촉 원칙]: 기존 FishingSystem 및 UI가 호출하던 레거시 API를 100% 동일하게 제공하여 무중단 호환 보장
    /// </summary>
    public class WorldSystemBridge : SystemBase, ITickable, ISaveable
    {
        public string SaveId => "world_system_bridge";
        public Type StateType => typeof(WorldBridgeSaveData);

        // 도메인 엔진 코어 인스턴스
        public WorldGridDomain GridDomain { get; private set; } = new WorldGridDomain();
        public VoyageEngine VoyageEngine { get; private set; } = new VoyageEngine();
        public WorldPathfinder Pathfinder { get; private set; } = new WorldPathfinder();
        public IAreaAccessPolicy AccessPolicy { get; set; } = new AlwaysOpenAccessPolicy();

        // 뷰 계층 및 외부 시스템용 이벤트 중계 (도메인 계약 래핑)
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
        // [레거시 호환 API]: FishingSystem, UI 뷰모델과의 무중단 100% 호환 보장
        // =========================================================================
        public event Action<int> OnStageChanged;
        public event Action<int, float> OnTravelStarted;
        public event Action OnTravelCanceled;

        public VoyageState CurrentState => VoyageEngine.CurrentState;
        public bool IsTraveling => CurrentState != VoyageState.Anchored;
        public float RemainingTravelTime => VoyageEngine.RemainingTravelTime;
        public float RemainingDistance => RemainingTravelTime * MaxSpeed;
        public float TravelProgress => VoyageEngine.TravelProgress;
        public Vector2 CurrentLogicalPosition => VoyageEngine.CurrentLogicalPosition;
        public float MaxSpeed => VoyageEngine.MaxSpeed;

        public StageData GetStageData(int stageId) => DataManager.GetData<StageData>(stageId);

        /// <summary>
        /// 낚시 시스템(FishingSystem)이 참조하는 현재 스테이지(영역) 데이터
        /// </summary>
        public StageData CurrentStageData
        {
            get
            {
                int currentAreaId = VoyageEngine.CurrentCell.AreaId > 0 ? VoyageEngine.CurrentCell.AreaId : m_lastStageId;
                return DataManager.GetData<StageData>(currentAreaId);
            }
        }

        /// <summary>
        /// 낚시 시스템(FishingSystem)이 호출하는 현재 지역의 라이센스별 낚시 어종 티어풀 반환 (기존 시그니처 100% 유지)
        /// </summary>
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
        private const float MAP_WIDTH = 2048f;
        private const float MAP_HEIGHT = 1024f;
        private const int PIXELS_PER_GRID = 10;

        public override void Initialize()
        {
            // 맵 텍스처로부터 그리드 데이터 자동 생성
            Texture2D mapTex = Resources.Load<Texture2D>("Map_01");
            if (mapTex != null)
            {
                BuildGridFromTexture(mapTex);
            }
            else
            {
                Debug.LogWarning("[WorldSystemBridge] Map_01 텍스처를 Resources에서 찾지 못해 기본 그리드로 초기화합니다.");
                BuildDefaultGrid();
            }

            // 항해 가감속 타이밍 설정 (가속 5초, 감속 5초, 중단 3초, 최대속도 5)
            VoyageEngine.ConfigureTimings(5f, 5f, 3f, 5f);
        }

        public override void PostInitialize()
        {
            // 초기 위치 설정 (기본 스폰 600001)
            var stageData = DataManager.GetData<StageData>(m_lastStageId);
            Vector2 initialPos = stageData != null ? stageData.MapPosition : Vector2.zero;
            VoyageEngine.SetInitialPosition(initialPos, GridDomain);

            VoyageEngine.OnAreaChanged += (newAreaId, env) =>
            {
                m_lastStageId = newAreaId;
                OnStageChanged?.Invoke(newAreaId);
            };

            VoyageEngine.OnVoyageStateChanged += (state) =>
            {
                if (state == VoyageState.Anchored)
                {
                    OnStageChanged?.Invoke(m_lastStageId);
                }
            };
        }

        public void Tick(float dt)
        {
            VoyageEngine.Tick(dt, GridDomain);
        }

        /// <summary>
        /// 월드맵의 임의 좌표(Vector2) 또는 노드 좌표로 항해를 요청하는 통합 진입점
        /// </summary>
        public void MoveToWorldPosition(Vector2 targetWorldPos)
        {
            if (CurrentState == VoyageState.Departing || CurrentState == VoyageState.Arriving || CurrentState == VoyageState.Stopping)
            {
                Debug.LogWarning("[WorldSystemBridge] 가감속 중에는 목적지를 변경할 수 없습니다.");
                return;
            }

            Vector2Int startCell = GridDomain.WorldToCell(VoyageEngine.CurrentLogicalPosition);
            Vector2Int targetCell = GridDomain.WorldToCell(targetWorldPos);

            List<Vector2Int> path = Pathfinder.FindPath(startCell, targetCell, GridDomain, AccessPolicy);
            if (path == null || path.Count == 0)
            {
                Debug.LogWarning($"[WorldSystemBridge] 경로 탐색 실패: 목적지({targetWorldPos})로 갈 수 있는 해로가 없습니다.");
                return;
            }

            VoyageEngine.StartVoyage(path, GridDomain);
            OnTravelStarted?.Invoke(GridDomain.TryGetCell(targetCell, out var c) ? c.AreaId : 0, VoyageEngine.TotalTravelTime);
        }

        /// <summary>
        /// 레거시 스테이지 ID 기반 이동 요청 (기존 UI 및 뷰모델 호환)
        /// </summary>
        public void MoveToStage(int targetStageId)
        {
            var targetStage = DataManager.GetData<StageData>(targetStageId);
            if (targetStage != null)
            {
                MoveToWorldPosition(targetStage.MapPosition);
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

                    // 가장 가까운 스테이지 노드로 소속 AreaId 매핑
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
            Debug.Log($"[WorldSystemBridge] 월드 그리드 빌드 완료: {width}x{height} (셀 크기: {cellSize:F1})");
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
            return new WorldBridgeSaveData
            {
                lastPosX = VoyageEngine.CurrentLogicalPosition.x,
                lastPosY = VoyageEngine.CurrentLogicalPosition.y,
                lastStageId = m_lastStageId
            };
        }

        public void RestoreState(object state)
        {
            if (state is WorldBridgeSaveData saveData)
            {
                m_lastStageId = saveData.lastStageId > 0 ? saveData.lastStageId : 600001;
                Vector2 savedPos = new Vector2(saveData.lastPosX, saveData.lastPosY);
                if (savedPos == Vector2.zero)
                {
                    var stage = DataManager.GetData<StageData>(m_lastStageId);
                    if (stage != null) savedPos = stage.MapPosition;
                }
                VoyageEngine.SetInitialPosition(savedPos, GridDomain);
            }
        }
    }

    [Serializable]
    public class WorldBridgeSaveData
    {
        public float lastPosX;
        public float lastPosY;
        public int lastStageId;
    }
}
