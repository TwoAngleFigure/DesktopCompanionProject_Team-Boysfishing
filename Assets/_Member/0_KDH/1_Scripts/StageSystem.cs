using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DesktopCompanion.Domain.World;
using DesktopCompanion.Data;
using DesktopCompanion.Save;
using DesktopCompanion.Views;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 스테이지별 사각형(Width x Height) 소속 해역 설정 구조체
    /// 중심점(StageData.MapPosition + m_centerOffset)을 기준으로 가로(m_width), 세로(m_height) 직사각형 영역을 정의하며,
    /// 영역이 겹치는 교집합 구간에서는 중심점까지의 거리가 더 가까운 스테이지가 우선권을 가집니다.
    /// </summary>
    [Serializable]
    public class StageAreaRectConfig
    {
        [Tooltip("대상 스테이지 ID (예: 600001 ~ 600009)")]
        public int m_stageId;

        [Tooltip("인스펙터 식별용 이름")]
        public string m_stageName;

        [Tooltip("StageData.MapPosition 기준 중심점 추가 오프셋 (기본 0,0)")]
        public Vector2 m_centerOffset = Vector2.zero;

        [Tooltip("사각형 해역의 가로 길이 (월드/맵 픽셀 단위)")]
        public float m_width = 400f;

        [Tooltip("사각형 해역의 세로 길이 (월드/맵 픽셀 단위)")]
        public float m_height = 300f;

        [Tooltip("공식 항구 정박지로 인정할 노드 중심 반경")]
        public float m_portRadius = 28f;
    }

    /// <summary>
    /// [신규 월드 시스템 파사드]: GameManager 및 레거시 시스템들과의 100% 무중단 호환을 유지하면서,
    /// 내부적으로는 2픽셀 육각형(Odd-R Hex) 그리드 도메인(WorldGridDomain) 및 순수 항해 엔진(VoyageEngine)으로 구동되는 시스템
    /// </summary>
    public class StageSystem : SystemBase, ITickable, ISaveable
    {
        public string SaveId => "stage_system";
        public Type StateType => typeof(StageSaveData);

        [Header("월드 맵 텍스처 및 2픽셀 육각형 그리드 설정")]
        [Tooltip("그리드 파싱에 사용할 원본 월드맵 텍스처 (비워두어도 Map_01.png를 자동 탐색하여 바인딩합니다)")]
        [SerializeField] private Texture2D m_mapTexture;

        [Tooltip("그리드 1칸당 픽셀 크기 (기본값: 2픽셀 초정밀 육각 그리드)")]
        [Range(1, 10)]
        [SerializeField] private int m_pixelsPerGrid = 2;

        [Header("스테이지별 사각형(Width x Height) 소속 해역 설정")]
        [Tooltip("각 스테이지 노드를 중심으로 하는 사각형 해역 크기 설정 리스트 (교집합 진입 시 중심점 거리 우선순위 판정)")]
        [SerializeField] private List<StageAreaRectConfig> m_stageAreaRects = new List<StageAreaRectConfig>();

        [Tooltip("체크 시 유니티 Scene 뷰에서 각 스테이지의 사각형(Width x Height) 해역 범위를 기즈모로 표시합니다.")]
        [SerializeField] private bool m_drawAreaGizmos = true;

        // 신규 순수 C# 도메인 엔진 코어
        public WorldGridDomain GridDomain { get; private set; } = new WorldGridDomain();
        public VoyageEngine VoyageEngine { get; private set; } = new VoyageEngine();
        public WorldPathfinder Pathfinder { get; private set; } = new WorldPathfinder();
        public IAreaAccessPolicy AccessPolicy { get; set; } = new AlwaysOpenAccessPolicy();

        public IReadOnlyList<StageAreaRectConfig> StageAreaRects => m_stageAreaRects;

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
                return DataManager.GetData<StageData>(currentAreaId) ?? DataManager.GetData<StageData>(m_lastStageId);
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
        private RecordSystem m_recordSystem;
        private int m_lastStageId = 600001;
        private int m_targetStageId = 0;
        private bool m_hasRestoredPosition = false;
        private Vector2 m_restoredPosition = Vector2.zero;
        private const float MAP_WIDTH = 2048f;
        private const float MAP_HEIGHT = 1024f;

        private void OnValidate()
        {
            if (m_pixelsPerGrid < 1) m_pixelsPerGrid = 2;
            PopulateDefaultStageAreaRects(ref m_stageAreaRects, null);
#if UNITY_EDITOR
            if (m_mapTexture == null)
            {
                m_mapTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resource/UI_Sprite/Map/Map_01.png");
            }
#endif
        }

        public override void Initialize()
        {
            SyncStageAreaRectsFromSceneView();
            PopulateDefaultStageAreaRects(ref m_stageAreaRects, DataManager?.GetAll<StageData>());

            Texture2D mapTex = ResolveMapTexture();
            if (mapTex != null)
            {
                BuildGridFromTexture(mapTex);
            }
            else
            {
                Debug.LogError("[StageSystem] Map_01 텍스처를 찾지 못해 기본 그리드로 폴백합니다. 인스펙터의 m_mapTexture를 확인해주세요.");
                BuildDefaultGrid();
            }

            // 항해 가감속 연출 시간 설정 (가속 5초, 감속 5초, 중단 3초, 최대속도 5)
            VoyageEngine.ConfigureTimings(5f, 5f, 3f, 5f);
        }

        public override void PostInitialize()
        {
            m_recordSystem = SystemManager.GetSystem<RecordSystem>();

            Vector2 initialPos;
            if (m_hasRestoredPosition)
            {
                initialPos = m_restoredPosition;
            }
            else
            {
                var stageData = DataManager.GetData<StageData>(m_lastStageId);
                initialPos = stageData != null ? stageData.MapPosition : Vector2.zero;
            }
            VoyageEngine.SetInitialPosition(initialPos, GridDomain);

            VoyageEngine.OnAreaChanged += (newAreaId, env) =>
            {
                if (newAreaId <= 0) return;
                m_lastStageId = newAreaId;
                var stage = DataManager.GetData<StageData>(newAreaId);
                if (stage != null)
                {
                    OnAreaStageChanged?.Invoke(stage);
                }
                OnStageChanged?.Invoke(newAreaId);
            };

            VoyageEngine.OnAnchored += (anchorType, cell) =>
            {
                if (anchorType != AnchorType.Interrupted)
                {
                    if (m_targetStageId != 0)
                    {
                        m_lastStageId = m_targetStageId;
                        m_targetStageId = 0;
                    }
                    else if (cell.AreaId > 0)
                    {
                        m_lastStageId = cell.AreaId;
                    }

                    OnStageChanged?.Invoke(m_lastStageId);
                    m_recordSystem?.RecordStageArrived(m_lastStageId);
                }
                else
                {
                    if (cell.AreaId > 0)
                    {
                        m_lastStageId = cell.AreaId;
                    }
                    m_targetStageId = 0;
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

            if (targetStageId == 0 && GridDomain.TryGetCell(path[path.Count - 1], out var destCell))
            {
                targetStageId = destCell.PortId > 0 ? destCell.PortId : (destCell.AreaId > 0 ? destCell.AreaId : m_lastStageId);
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
        // 맵 텍스처 안전 로더 & 2픽셀 육각형 그리드 + 사각형 해역 빌더
        // =========================================================================
        public Texture2D ResolveMapTexture()
        {
            if (m_mapTexture != null) return m_mapTexture;

            m_mapTexture = Resources.Load<Texture2D>("Map_01");
            if (m_mapTexture != null) return m_mapTexture;

            // 씬 내 WorldMapUIView 또는 UI Image 컴포넌트에 연결된 Map_01 스프라이트에서 텍스처 추출
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

        private void SyncStageAreaRectsFromSceneView()
        {
            var views = UnityEngine.Object.FindObjectsByType<WorldMapUIView>(FindObjectsInactive.Include);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null && views[i].StageAreaRects != null && views[i].StageAreaRects.Count > 0)
                {
                    m_stageAreaRects = new List<StageAreaRectConfig>(views[i].StageAreaRects);
                    break;
                }
            }
        }

        public static void PopulateDefaultStageAreaRects(ref List<StageAreaRectConfig> targetList, IReadOnlyList<StageData> allStages)
        {
            if (targetList == null)
            {
                targetList = new List<StageAreaRectConfig>();
            }

            int[] defaultIds = { 600001, 600002, 600003, 600004, 600005, 600006, 600007, 600008, 600009 };
            string[] defaultNames =
            {
                "푸른 하천1 (600001)", "갯벌 연안1 (600002)", "냉수대 앞바다1 (600003)",
                "푸른 하천2 (600004)", "갯벌 연안2 (600005)", "냉수대 앞바다2 (600006)",
                "푸른 하천3 (600007)", "갯벌 연안3 (600008)", "냉수대 앞바다3 (600009)"
            };

            for (int i = 0; i < defaultIds.Length; i++)
            {
                int id = defaultIds[i];
                bool exists = false;
                for (int j = 0; j < targetList.Count; j++)
                {
                    if (targetList[j] != null && targetList[j].m_stageId == id)
                    {
                        if (string.IsNullOrEmpty(targetList[j].m_stageName))
                            targetList[j].m_stageName = defaultNames[i];
                        if (targetList[j].m_portRadius <= 12f)
                            targetList[j].m_portRadius = 28f;
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    targetList.Add(new StageAreaRectConfig
                    {
                        m_stageId = id,
                        m_stageName = defaultNames[i],
                        m_centerOffset = Vector2.zero,
                        m_width = 360f,
                        m_height = 260f,
                        m_portRadius = 28f
                    });
                }
            }

            if (allStages != null)
            {
                for (int i = 0; i < allStages.Count; i++)
                {
                    var st = allStages[i];
                    if (st == null) continue;
                    for (int j = 0; j < targetList.Count; j++)
                    {
                        if (targetList[j].m_stageId == st.ID)
                        {
                            targetList[j].m_stageName = $"{st.Name} ({st.ID})";
                            break;
                        }
                    }
                }
            }
        }

        public static Vector2 GetDefaultStageMapPosition(int stageId)
        {
            return stageId switch
            {
                600001 => new Vector2(125f, 125f),
                600002 => new Vector2(-225f, -175f),
                600003 => new Vector2(225f, 200f),
                600004 => new Vector2(0f, 0f),
                600005 => new Vector2(-400f, 225f),
                600006 => new Vector2(525f, 175f),
                600007 => new Vector2(-150f, 0f),
                600008 => new Vector2(-500f, -200f),
                600009 => new Vector2(125f, -150f),
                _ => Vector2.zero
            };
        }

        private void BuildGridFromTexture(Texture2D tex)
        {
            int step = Mathf.Max(1, m_pixelsPerGrid); // 기본 2픽셀
            int width = tex.width / step;             // 2048 / 2 = 1024
            int height = tex.height / step;           // 1024 / 2 = 512
            float cellSize = MAP_WIDTH / width;       // 2.0 월드 유닛
            Vector2 origin = new Vector2(-MAP_WIDTH * 0.5f, -MAP_HEIGHT * 0.5f);

            // GetPixels32() 일괄 로드로 209만 픽셀을 0.05초 내에 초고속 파싱
            Color32[] pixels = tex.GetPixels32();
            int texWidth = tex.width;
            int texHeight = tex.height;

            var cellData = new WorldCellData[width, height];
            var allStages = DataManager.GetAll<StageData>();

            // 스테이지별 사각형 중심 좌표 및 항구 원점 캐싱 (고속 판정용)
            int rectCount = m_stageAreaRects.Count;
            int[] stageIds = new int[rectCount];
            Vector2[] portPositions = new Vector2[rectCount];
            Vector2[] rectCenters = new Vector2[rectCount];
            Vector2[] halfSizes = new Vector2[rectCount];
            float[] portRadiiSq = new float[rectCount];

            for (int i = 0; i < rectCount; i++)
            {
                var cfg = m_stageAreaRects[i];
                stageIds[i] = cfg.m_stageId;
                StageData st = DataManager.GetData<StageData>(cfg.m_stageId);
                Vector2 basePos = st != null ? st.MapPosition : Vector2.zero;
                portPositions[i] = basePos;
                rectCenters[i] = basePos + cfg.m_centerOffset;
                halfSizes[i] = new Vector2(Mathf.Max(1f, cfg.m_width * 0.5f), Mathf.Max(1f, cfg.m_height * 0.5f));
                float pr = Mathf.Max(cellSize * 2f, cfg.m_portRadius);
                portRadiiSq[i] = pr * pr;
            }

            int waterCellCount = 0;

            for (int y = 0; y < height; y++)
            {
                // Odd-R 육각형 그리드: 홀수 행(y & 1 == 1)은 오른쪽으로 0.5칸(+0.5 * step 픽셀) 이동
                float hexRowWorldOffset = ((y & 1) == 1) ? 0.5f * cellSize : 0f;
                int hexRowPixelOffset = ((y & 1) == 1) ? (step >> 1) : 0;
                float cellWorldY = origin.y + (y + 0.5f) * cellSize;

                for (int x = 0; x < width; x++)
                {
                    // 1) 2x2 픽셀 블록 전수 검사 + 5:5 경계 판정 (과반수 초과 원칙 + 중심 픽셀 타이브레이커)
                    bool isWater = EvaluateHexCellWater(pixels, texWidth, texHeight, x * step + hexRowPixelOffset, y * step, step);
                    if (isWater) waterCellCount++;

                    // 2) 사각형(Width x Height) 소속 해역 판정 + 교집합(겹침) 발생 시 중심점 최근접 거리 우선순위
                    float cellWorldX = origin.x + (x + 0.5f) * cellSize + hexRowWorldOffset;
                    Vector2 cellWorldPos = new Vector2(cellWorldX, cellWorldY);

                    int resolvedAreaId = 0;
                    int portId = 0;
                    float bestOverlapDistSq = float.MaxValue;
                    float bestPortDistSq = float.MaxValue;

                    for (int s = 0; s < rectCount; s++)
                    {
                        Vector2 center = rectCenters[s];
                        float dx = Mathf.Abs(cellWorldPos.x - center.x);
                        float dy = Mathf.Abs(cellWorldPos.y - center.y);

                        // 사각형(Width, Height) 내부에 포함되는지 1차 검사
                        if (dx <= halfSizes[s].x && dy <= halfSizes[s].y)
                        {
                            float distSq = dx * dx + dy * dy;

                            // 교집합 구간일 경우 사각형 중심점 거리가 더 가까운 스테이지가 우선권을 가짐
                            if (distSq < bestOverlapDistSq)
                            {
                                bestOverlapDistSq = distSq;
                                resolvedAreaId = stageIds[s];
                            }
                        }

                        // 공식 항구(PortId)는 m_centerOffset과 무관하게 실제 스테이지 노드 좌표(MapPosition) 기준으로 판정
                        Vector2 portPos = portPositions[s];
                        float pdx = cellWorldPos.x - portPos.x;
                        float pdy = cellWorldPos.y - portPos.y;
                        float portDistSq = pdx * pdx + pdy * pdy;
                        if (portDistSq <= portRadiiSq[s] && portDistSq < bestPortDistSq)
                        {
                            bestPortDistSq = portDistSq;
                            portId = stageIds[s];
                        }
                    }

                    // 만약 항구 반경 내부인데 사각형 오프셋 조정으로 AreaId가 비어있다면 해당 항구 스테이지로 보정
                    if (portId > 0 && resolvedAreaId == 0)
                    {
                        resolvedAreaId = portId;
                    }

                    EnvironmentType env = (portId > 0) ? EnvironmentType.Port : EnvironmentType.Ocean;
                    cellData[x, y] = new WorldCellData((short)x, (short)y, isWater, resolvedAreaId, env, portId);
                }
            }

            GridDomain.Initialize(width, height, cellSize, origin, cellData, m_lastStageId);
            Debug.Log($"[StageSystem] 🐝 2픽셀 육각형(Hex) 월드 그리드 초기화 완료: {width}x{height} (셀 크기: {cellSize:F1}px, 항해 가능 물 셀: {waterCellCount:N0}개)");
        }

        /// <summary>
        /// 2x2 (또는 step x step) 픽셀 영역의 물/육지 여부를 판정합니다.
        /// - 물 픽셀이 50%를 초과하면(예: 4픽셀 중 3~4개) 물(true)로 판정
        /// - 물 픽셀이 50% 미만이면(예: 4픽셀 중 0~1개) 땅(false)으로 판정
        /// - 정확히 5:5 동률(예: 4픽셀 중 2:2)일 때는 해안선 침범 방지를 위해 기본적으로 땅(false)으로 판정하되,
        ///   좁은 강줄기 중심부가 끊기지 않도록 정중앙 픽셀이 뚜렷한 물 색상일 때만 물(true)로 인정합니다.
        /// </summary>
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

                    // 기존 b > r + 0.15f (255 기준 약 +38)와 동일한 기준의 연속 물 점수
                    int margin = (int)c.b - ((int)c.r + 38);
                    blueMarginSum += margin;
                    totalCount++;

                    if (margin > 0)
                    {
                        waterCount++;
                    }
                }
            }

            // 1) 과반수 초과 (예: 2x2에서 3픽셀 이상 물) -> 확실한 물
            if (waterCount * 2 > totalCount)
                return true;

            // 2) 과반수 미달 (예: 2x2에서 1픽셀 이하 물) -> 확실한 땅
            if (waterCount * 2 < totalCount)
                return false;

            // 3) 정확히 5:5 동률 (예: 2x2에서 물 2픽셀 : 땅 2픽셀)
            // 해안선 침범을 막기 위해 기본적으로 땅(false)으로 처리하되,
            // 정중앙 픽셀의 푸른 물 농도와 블록 전체 평균이 충분히 높을 때(좁은 강줄기 중심)만 물로 허용
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
            return new StageSaveData
            {
                currentStageDataId = m_lastStageId,
                savedPosX = VoyageEngine.CurrentLogicalPosition.x,
                savedPosY = VoyageEngine.CurrentLogicalPosition.y
            };
        }

        public void RestoreState(object state)
        {
            if (state is StageSaveData saveData)
            {
                m_lastStageId = saveData.currentStageDataId > 0 ? saveData.currentStageDataId : 600001;
                if (saveData.savedPosX != 0f || saveData.savedPosY != 0f)
                {
                    m_restoredPosition = new Vector2(saveData.savedPosX, saveData.savedPosY);
                    m_hasRestoredPosition = true;
                    VoyageEngine.SetInitialPosition(m_restoredPosition, GridDomain);
                }
                else
                {
                    var stage = DataManager.GetData<StageData>(m_lastStageId);
                    Vector2 savedPos = stage != null ? stage.MapPosition : Vector2.zero;
                    VoyageEngine.SetInitialPosition(savedPos, GridDomain);
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!m_drawAreaGizmos || m_stageAreaRects == null) return;

            Color[] palette =
            {
                new Color(0.2f, 0.8f, 1.0f, 0.85f),
                new Color(0.3f, 0.9f, 0.5f, 0.85f),
                new Color(1.0f, 0.7f, 0.2f, 0.85f),
                new Color(0.9f, 0.4f, 0.8f, 0.85f),
                new Color(0.6f, 0.5f, 1.0f, 0.85f),
                new Color(1.0f, 0.4f, 0.4f, 0.85f)
            };

            for (int i = 0; i < m_stageAreaRects.Count; i++)
            {
                var cfg = m_stageAreaRects[i];
                if (cfg == null) continue;

                Vector2 basePos = GetEditorStagePosition(cfg.m_stageId);
                Vector3 portCenter = new Vector3(basePos.x, basePos.y, 0f);
                Vector3 rectCenter = new Vector3(basePos.x + cfg.m_centerOffset.x, basePos.y + cfg.m_centerOffset.y, 0f);
                Vector3 size = new Vector3(cfg.m_width, cfg.m_height, 1f);

                Color c = palette[i % palette.Length];
                Gizmos.color = new Color(c.r, c.g, c.b, 0.18f);
                Gizmos.DrawCube(rectCenter, size);
                Gizmos.color = c;
                Gizmos.DrawWireCube(rectCenter, size);
                Gizmos.DrawWireSphere(portCenter, cfg.m_portRadius);
            }
        }

        private Vector2 GetEditorStagePosition(int stageId)
        {
            if (DataManager != null)
            {
                var st = DataManager.GetData<StageData>(stageId);
                if (st != null) return st.MapPosition;
            }

            return GetDefaultStageMapPosition(stageId);
        }
#endif
    }

    [Serializable]
    public class StageSaveData
    {
        public int currentStageDataId;
        public float savedPosX;
        public float savedPosY;
    }
}
