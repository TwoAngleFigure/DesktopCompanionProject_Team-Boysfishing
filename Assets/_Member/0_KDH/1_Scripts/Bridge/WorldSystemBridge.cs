using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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

        [Header("단독 구동 시 폴백 맵 텍스처 및 픽셀 크기")]
        [SerializeField] private Texture2D m_mapTexture;
        [Range(1, 10)]
        [SerializeField] private int m_pixelsPerGrid = 2;

        private StageSystem m_stageSystem;
        private readonly WorldGridDomain m_fallbackGridDomain = new WorldGridDomain();
        private readonly VoyageEngine m_fallbackVoyageEngine = new VoyageEngine();
        private readonly WorldPathfinder m_fallbackPathfinder = new WorldPathfinder();
        private IAreaAccessPolicy m_fallbackAccessPolicy = new AlwaysOpenAccessPolicy();

        // 도메인 엔진 코어 인스턴스 (StageSystem과 100% 단일 인스턴스 공유)
        public WorldGridDomain GridDomain => m_stageSystem != null ? m_stageSystem.GridDomain : m_fallbackGridDomain;
        public VoyageEngine VoyageEngine => m_stageSystem != null ? m_stageSystem.VoyageEngine : m_fallbackVoyageEngine;
        public WorldPathfinder Pathfinder => m_stageSystem != null ? m_stageSystem.Pathfinder : m_fallbackPathfinder;
        public IAreaAccessPolicy AccessPolicy
        {
            get => m_stageSystem != null ? m_stageSystem.AccessPolicy : m_fallbackAccessPolicy;
            set
            {
                if (m_stageSystem != null) m_stageSystem.AccessPolicy = value;
                else m_fallbackAccessPolicy = value;
            }
        }

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
                if (m_stageSystem != null) return m_stageSystem.CurrentStageData;
                int currentAreaId = VoyageEngine.CurrentCell.AreaId > 0 ? VoyageEngine.CurrentCell.AreaId : m_lastStageId;
                return DataManager.GetData<StageData>(currentAreaId) ?? DataManager.GetData<StageData>(m_lastStageId);
            }
        }

        /// <summary>
        /// 낚시 시스템(FishingSystem)이 호출하는 현재 지역의 라이센스별 낚시 어종 티어풀 반환 (기존 시그니처 100% 유지)
        /// </summary>
        public List<TierPool> GetAvailableTierPools(int playerLicense)
        {
            if (m_stageSystem != null) return m_stageSystem.GetAvailableTierPools(playerLicense);

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

        public override void Initialize()
        {
            m_stageSystem = SystemManager.GetSystem<StageSystem>();
            if (m_stageSystem != null)
            {
                // StageSystem과 단일 코어 엔진을 공유하므로 중복 초기화를 생략합니다.
                return;
            }

            Texture2D mapTex = ResolveMapTexture();
            if (mapTex != null)
            {
                BuildGridFromTexture(mapTex);
            }
            else
            {
                Debug.LogWarning("[WorldSystemBridge] Map_01 텍스처를 찾지 못해 기본 그리드로 초기화합니다.");
                BuildDefaultGrid();
            }

            // 항해 가감속 타이밍 설정 (가속 5초, 감속 5초, 중단 3초, 최대속도 5)
            VoyageEngine.ConfigureTimings(5f, 5f, 3f, 5f);
        }

        public override void PostInitialize()
        {
            m_stageSystem ??= SystemManager.GetSystem<StageSystem>();
            if (m_stageSystem != null)
            {
                m_stageSystem.OnStageChanged += stageId =>
                {
                    m_lastStageId = stageId;
                    OnStageChanged?.Invoke(stageId);
                };
                m_stageSystem.OnTravelStarted += (targetId, duration) => OnTravelStarted?.Invoke(targetId, duration);
                m_stageSystem.OnTravelCanceled += () => OnTravelCanceled?.Invoke();
                return;
            }

            var stageData = DataManager.GetData<StageData>(m_lastStageId);
            Vector2 initialPos = stageData != null ? stageData.MapPosition : Vector2.zero;
            VoyageEngine.SetInitialPosition(initialPos, GridDomain);

            VoyageEngine.OnAreaChanged += (newAreaId, env) =>
            {
                if (newAreaId <= 0) return;
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
            if (m_stageSystem != null) return;
            VoyageEngine.Tick(dt, GridDomain);
        }

        public void MoveToWorldPosition(Vector2 targetWorldPos)
        {
            if (m_stageSystem != null)
            {
                m_stageSystem.MoveToWorldPosition(targetWorldPos);
                return;
            }

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
            OnTravelStarted?.Invoke(GridDomain.TryGetCell(path[path.Count - 1], out var c) && c.AreaId > 0 ? c.AreaId : m_lastStageId, VoyageEngine.TotalTravelTime);
        }

        public void MoveToStage(int targetStageId)
        {
            if (m_stageSystem != null)
            {
                m_stageSystem.MoveToStage(targetStageId);
                return;
            }

            var targetStage = DataManager.GetData<StageData>(targetStageId);
            if (targetStage != null)
            {
                MoveToWorldPosition(targetStage.MapPosition);
            }
        }

        public void CancelTravel()
        {
            if (m_stageSystem != null)
            {
                m_stageSystem.CancelTravel();
                return;
            }

            if (CurrentState == VoyageState.Traveling)
            {
                VoyageEngine.CancelVoyage();
                OnTravelCanceled?.Invoke();
            }
        }

        private Texture2D ResolveMapTexture()
        {
            if (m_mapTexture != null) return m_mapTexture;

            m_mapTexture = Resources.Load<Texture2D>("Map_01");
            if (m_mapTexture != null) return m_mapTexture;

            var images = UnityEngine.Object.FindObjectsByType<Image>(FindObjectsInactive.Include);
            for (int i = 0; i < images.Length; i++)
            {
                var sp = images[i] != null ? images[i].sprite : null;
                if (sp != null && sp.texture != null && sp.texture.name.Contains("Map_01"))
                {
                    m_mapTexture = sp.texture;
                    return m_mapTexture;
                }
            }

#if UNITY_EDITOR
            m_mapTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resource/UI_Sprite/Map/Map_01.png");
#endif
            return m_mapTexture;
        }

        private void BuildGridFromTexture(Texture2D tex)
        {
            int step = Mathf.Max(1, m_pixelsPerGrid);
            int width = tex.width / step;
            int height = tex.height / step;
            float cellSize = MAP_WIDTH / width;
            Vector2 origin = new Vector2(-MAP_WIDTH * 0.5f, -MAP_HEIGHT * 0.5f);

            Color32[] pixels = tex.GetPixels32();
            int texWidth = tex.width;
            int texHeight = tex.height;

            var cellData = new WorldCellData[width, height];
            var allStages = DataManager.GetAll<StageData>();

            for (int y = 0; y < height; y++)
            {
                float hexRowWorldOffset = ((y & 1) == 1) ? 0.5f * cellSize : 0f;
                int hexRowPixelOffset = ((y & 1) == 1) ? (step >> 1) : 0;
                float cellWorldY = origin.y + (y + 0.5f) * cellSize;

                for (int x = 0; x < width; x++)
                {
                    bool isWater = EvaluateHexCellWater(pixels, texWidth, texHeight, x * step + hexRowPixelOffset, y * step, step);
                    float cellWorldX = origin.x + (x + 0.5f) * cellSize + hexRowWorldOffset;
                    Vector2 cellWorldPos = new Vector2(cellWorldX, cellWorldY);

                    int resolvedAreaId = 0;
                    int portId = 0;
                    float bestOverlapDistSq = float.MaxValue;

                    if (allStages != null)
                    {
                        for (int s = 0; s < allStages.Count; s++)
                        {
                            var stage = allStages[s];
                            if (stage == null) continue;

                            float dx = Mathf.Abs(cellWorldPos.x - stage.MapPosition.x);
                            float dy = Mathf.Abs(cellWorldPos.y - stage.MapPosition.y);

                            float distSq = dx * dx + dy * dy;
                            if (dx <= 180f && dy <= 130f)
                            {
                                if (distSq < bestOverlapDistSq)
                                {
                                    bestOverlapDistSq = distSq;
                                    resolvedAreaId = stage.ID;
                                }
                            }
                            if (distSq <= 784f) // 반경 28px 항구 정박 범위
                            {
                                portId = stage.ID;
                            }
                        }
                    }

                    EnvironmentType env = (portId > 0) ? EnvironmentType.Port : EnvironmentType.Ocean;
                    cellData[x, y] = new WorldCellData((short)x, (short)y, isWater, resolvedAreaId, env, portId);
                }
            }

            GridDomain.Initialize(width, height, cellSize, origin, cellData, m_lastStageId);
            Debug.Log($"[WorldSystemBridge] 🐝 2픽셀 육각형(Hex) 월드 그리드 빌드 완료: {width}x{height} (셀 크기: {cellSize:F1}px)");
        }

        private static bool EvaluateHexCellWater(Color32[] pixels, int texWidth, int texHeight, int basePx, int basePy, int step)
        {
            int waterCount = 0;
            int totalCount = 0;
            int blueMarginSum = 0;

            for (int dy = 0; dy < step; dy++)
            {
                int py = Mathf.Clamp(basePy + dy, 0, texHeight - 1);
                int rowIndex = py * texWidth;

                for (int dx = 0; dx < step; dx++)
                {
                    int px = Mathf.Clamp(basePx + dx, 0, texWidth - 1);
                    Color32 c = pixels[rowIndex + px];
                    int margin = (int)c.b - ((int)c.r + 38);
                    blueMarginSum += margin;
                    totalCount++;

                    if (margin > 0)
                    {
                        waterCount++;
                    }
                }
            }

            if (waterCount * 2 > totalCount) return true;
            if (waterCount * 2 < totalCount) return false;

            int centerPx = Mathf.Clamp(basePx + (step >> 1), 0, texWidth - 1);
            int centerPy = Mathf.Clamp(basePy + (step >> 1), 0, texHeight - 1);
            Color32 centerColor = pixels[centerPy * texWidth + centerPx];
            int centerMargin = (int)centerColor.b - ((int)centerColor.r + 48);

            return (centerMargin > 0) && (blueMarginSum > totalCount * 6);
        }

        private void BuildDefaultGrid()
        {
            int step = Mathf.Max(1, m_pixelsPerGrid);
            int width = Mathf.RoundToInt(MAP_WIDTH) / step;
            int height = Mathf.RoundToInt(MAP_HEIGHT) / step;
            float cellSize = step;
            Vector2 origin = new Vector2(-MAP_WIDTH * 0.5f, -MAP_HEIGHT * 0.5f);
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
