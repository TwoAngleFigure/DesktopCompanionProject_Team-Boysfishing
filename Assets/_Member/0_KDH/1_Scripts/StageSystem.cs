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
    /// 복잡한 강줄기나 휘어진 해안선을 표현하기 위해 메인 육각 해역에 추가로 이어 붙이는 보조 육각형 도장(Stamp) 설정
    /// </summary>
    [Serializable]
    public class HexStampZone
    {
        [Tooltip("스테이지 기본 위치(MapPosition) 기준 보조 육각 도장의 중심 오프셋")]
        public Vector2 m_offset = Vector2.zero;

        [Tooltip("보조 육각 도장의 반경 (중심에서 육각형 변까지의 거리, 픽셀 단위)")]
        public float m_hexRadius = 120f;

        [Tooltip("육각형의 가로(X) / 세로(Y) 비율 (기본 1, 1)")]
        public Vector2 m_scale = Vector2.one;
    }

    /// <summary>
    /// 스테이지별 육각형 클러스터(메인 육각형 + 보조 육각 스탬프) 소속 해역 설정 구조체
    /// 중심점(StageData.MapPosition + m_centerOffset)을 기준으로 육각형 반경(m_hexRadius)과 비율(m_scale)로 영역을 정의하며,
    /// 영역이 겹치는 교집합 구간에서는 중심점까지의 직선거리(distSq)가 더 가까운 스테이지가 우선권을 가집니다.
    /// </summary>
    [Serializable]
    public class StageAreaRectConfig
    {
        [Tooltip("대상 스테이지 ID (예: 600001 ~ 600009)")]
        public int m_stageId;

        [Tooltip("인스펙터 식별용 이름")]
        public string m_stageName;

        [Header("메인 육각 해역 설정 (방안 1 베이스)")]
        [Tooltip("StageData.MapPosition 기준 메인 육각형 중심점 추가 오프셋 (기본 0,0)")]
        public Vector2 m_centerOffset = Vector2.zero;

        [Tooltip("메인 육각형 해역의 반경 (중심에서 변까지의 거리, 월드 픽셀 단위)")]
        public float m_hexRadius = 180f;

        [Tooltip("메인 육각형의 가로(X)·세로(Y) 비율 (1,1이면 정육각형, 늘리면 납작/길쭉한 육각형)")]
        public Vector2 m_scale = Vector2.one;

        [Header("보조 육각 스탬프 리스트 (방안 2 결합 - 필요한 스테이지만 추가)")]
        [Tooltip("구불구불한 강이나 긴 해안선을 따라 추가로 찍어줄 보조 육각형 도장 리스트")]
        public List<HexStampZone> m_additionalStamps = new List<HexStampZone>();

        [Header("공식 항구 정박 반경")]
        [Tooltip("공식 항구 정박지로 인정할 노드(MapPosition) 중심 반경")]
        public float m_portRadius = 28f;

        // 레거시 직렬화 호환용 필드 (숨김)
        [HideInInspector] public float m_width = 360f;
        [HideInInspector] public float m_height = 260f;

        /// <summary>
        /// 특정 월드 좌표(worldPos)가 이 스테이지의 메인 육각형 또는 보조 육각 스탬프 내부에 포함되는지 판정하고,
        /// 포함될 경우 교집합 우선순위 비교를 위한 가장 가까운 중심점 직선거리 제곱(closestCenterDistSq)을 반환합니다.
        /// </summary>
        public bool TryEvaluateHexInclusion(Vector2 worldPos, Vector2 stageBasePos, out float closestCenterDistSq)
        {
            bool isInside = false;
            closestCenterDistSq = float.MaxValue;

            // 1) 메인 육각형 검사
            Vector2 mainCenter = stageBasePos + m_centerOffset;
            if (IsInsidePointyHex(worldPos, mainCenter, m_hexRadius, m_scale))
            {
                isInside = true;
                closestCenterDistSq = (worldPos - mainCenter).sqrMagnitude;
            }

            // 2) 보조 육각형 스탬프들(방안 2) 검사 (합집합)
            if (m_additionalStamps != null)
            {
                for (int i = 0; i < m_additionalStamps.Count; i++)
                {
                    var stamp = m_additionalStamps[i];
                    if (stamp == null || stamp.m_hexRadius <= 0f) continue;

                    Vector2 stampCenter = stageBasePos + stamp.m_offset;
                    if (IsInsidePointyHex(worldPos, stampCenter, stamp.m_hexRadius, stamp.m_scale))
                    {
                        isInside = true;
                        float dSq = (worldPos - stampCenter).sqrMagnitude;
                        if (dSq < closestCenterDistSq)
                        {
                            closestCenterDistSq = dSq;
                        }
                    }
                }
            }

            return isInside;
        }

        /// <summary>
        /// Odd-R Pointy-Top(위아래가 뾰족한) 육각형 기하학 내부 포함 판정 공식
        /// </summary>
        public static bool IsInsidePointyHex(Vector2 point, Vector2 center, float radius, Vector2 scale)
        {
            if (radius <= 0f) return false;

            float sx = Mathf.Max(0.1f, Mathf.Abs(scale.x));
            float sy = Mathf.Max(0.1f, Mathf.Abs(scale.y));

            // 비율(Scale)이 적용된 로컬 절대 거리
            float dx = Mathf.Abs(point.x - center.x) / sx;
            float dy = Mathf.Abs(point.y - center.y) / sy;

            // Pointy-Top 정육각형의 내접반경(Apothem = radius) 기준 3축 경계 검사:
            // 1) 좌우 수직 변까지의 거리: dx <= radius
            // 2) 상하 대각선(60도) 4개 변까지의 거리: dx * 0.5 + dy * (sqrt(3)/2) <= radius
            const float SIN_60 = 0.8660254f;
            if (dx > radius) return false;
            if (dx * 0.5f + dy * SIN_60 > radius) return false;

            return true;
        }
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

            int[] defaultIds = { 600001, 600002, 600003, 600004, 600005, 600006, 600007, 600008, 600009, 600010 };
            string[] defaultNames =
            {
                "푸른 하천1 (600001)", "갯벌 연안1 (600002)", "냉수대 앞바다1 (600003)",
                "푸른 하천2 (600004)", "갯벌 연안2 (600005)", "냉수대 앞바다2 (600006)",
                "푸른 하천3 (600007)", "갯벌 연안3 (600008)", "냉수대 앞바다3 (600009)",
                "냉수대 앞바다4 (600010)"
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
                        if (targetList[j].m_hexRadius <= 1f)
                            targetList[j].m_hexRadius = 160f;
                        if (targetList[j].m_scale == Vector2.zero)
                            targetList[j].m_scale = Vector2.one;
                        if (targetList[j].m_additionalStamps == null)
                            targetList[j].m_additionalStamps = new List<HexStampZone>();

                        for (int s = 0; s < targetList[j].m_additionalStamps.Count; s++)
                        {
                            var stamp = targetList[j].m_additionalStamps[s];
                            if (stamp == null) continue;
                            // 유니티 인스펙터에서 리스트 '+' 버튼 클릭 시 0으로 초기화되는 현상 자동 보정
                            if (stamp.m_hexRadius <= 0f && stamp.m_scale == Vector2.zero)
                            {
                                stamp.m_hexRadius = 120f;
                                stamp.m_scale = Vector2.one;
                            }
                            else if (stamp.m_scale == Vector2.zero)
                            {
                                stamp.m_scale = Vector2.one;
                            }
                        }
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
                        m_hexRadius = 160f,
                        m_scale = Vector2.one,
                        m_additionalStamps = new List<HexStampZone>(),
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
                    bool found = false;
                    for (int j = 0; j < targetList.Count; j++)
                    {
                        if (targetList[j].m_stageId == st.ID)
                        {
                            targetList[j].m_stageName = $"{st.Name} ({st.ID})";
                            found = true;
                            break;
                        }
                    }

                    // 추후 시트에 신규 스테이지가 추가되더라도 누락 없이 자동 등록
                    if (!found)
                    {
                        targetList.Add(new StageAreaRectConfig
                        {
                            m_stageId = st.ID,
                            m_stageName = $"{st.Name} ({st.ID})",
                            m_centerOffset = Vector2.zero,
                            m_hexRadius = 160f,
                            m_scale = Vector2.one,
                            m_additionalStamps = new List<HexStampZone>(),
                            m_portRadius = 28f
                        });
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
                600010 => new Vector2(375f, -200f),
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

            // 스테이지별 육각형 클러스터 설정 및 항구 원점 캐싱 (고속 판정용)
            int stageCount = m_stageAreaRects.Count;
            int[] stageIds = new int[stageCount];
            Vector2[] portPositions = new Vector2[stageCount];
            float[] portRadiiSq = new float[stageCount];

            for (int i = 0; i < stageCount; i++)
            {
                var cfg = m_stageAreaRects[i];
                stageIds[i] = cfg.m_stageId;
                StageData st = DataManager.GetData<StageData>(cfg.m_stageId);
                Vector2 basePos = st != null ? st.MapPosition : GetDefaultStageMapPosition(cfg.m_stageId);
                portPositions[i] = basePos;
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

                    // 2) 육각형 클러스터(메인 육각형 + 추가 육각 스탬프) 소속 해역 판정
                    //    교집합(겹치는 구간) 진입 시 중심점까지의 직선거리(distSq)가 더 가까운 스테이지가 우선권을 가짐
                    float cellWorldX = origin.x + (x + 0.5f) * cellSize + hexRowWorldOffset;
                    Vector2 cellWorldPos = new Vector2(cellWorldX, cellWorldY);

                    int resolvedAreaId = 0;
                    int portId = 0;
                    float bestOverlapDistSq = float.MaxValue;
                    float bestPortDistSq = float.MaxValue;

                    for (int s = 0; s < stageCount; s++)
                    {
                        var cfg = m_stageAreaRects[s];
                        Vector2 basePos = portPositions[s];

                        if (cfg != null && cfg.TryEvaluateHexInclusion(cellWorldPos, basePos, out float hexCenterDistSq))
                        {
                            // 교집합 구간일 경우 직선거리(hexCenterDistSq)가 더 가까운 스테이지로 깔끔하게 판정
                            if (hexCenterDistSq < bestOverlapDistSq)
                            {
                                bestOverlapDistSq = hexCenterDistSq;
                                resolvedAreaId = stageIds[s];
                            }
                        }

                        // 공식 항구(PortId)는 오프셋과 무관하게 실제 스테이지 노드 좌표(MapPosition) 기준으로 판정
                        float pdx = cellWorldPos.x - basePos.x;
                        float pdy = cellWorldPos.y - basePos.y;
                        float portDistSq = pdx * pdx + pdy * pdy;
                        if (portDistSq <= portRadiiSq[s] && portDistSq < bestPortDistSq)
                        {
                            bestPortDistSq = portDistSq;
                            portId = stageIds[s];
                        }
                    }

                    // 만약 항구 반경 내부인데 육각형 오프셋 조정으로 AreaId가 비어있다면 해당 항구 스테이지로 보정
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

        public static void DrawStageHexAreaGizmos(IReadOnlyList<StageAreaRectConfig> configs)
        {
            if (configs == null) return;

            Color[] palette =
            {
                new Color(0.2f, 0.8f, 1.0f, 0.9f),
                new Color(0.3f, 0.9f, 0.5f, 0.9f),
                new Color(1.0f, 0.7f, 0.2f, 0.9f),
                new Color(0.9f, 0.4f, 0.8f, 0.9f),
                new Color(0.6f, 0.5f, 1.0f, 0.9f),
                new Color(1.0f, 0.4f, 0.4f, 0.9f)
            };

            for (int i = 0; i < configs.Count; i++)
            {
                var cfg = configs[i];
                if (cfg == null) continue;

                Vector2 basePos = GetDefaultStageMapPosition(cfg.m_stageId);
                Vector3 portCenter = new Vector3(basePos.x, basePos.y, 0f);
                Vector3 mainHexCenter = new Vector3(basePos.x + cfg.m_centerOffset.x, basePos.y + cfg.m_centerOffset.y, 0f);

                Color c = palette[i % palette.Length];
                Gizmos.color = c;

                // 1) 메인 육각형 해역 기즈모 그리기
                DrawPointyHexGizmo(mainHexCenter, cfg.m_hexRadius, cfg.m_scale, c);

                // 2) 보조 육각 스탬프(방안 2) 기즈모 및 메인 중심과의 연결 보조선 그리기
                if (cfg.m_additionalStamps != null)
                {
                    for (int s = 0; s < cfg.m_additionalStamps.Count; s++)
                    {
                        var stamp = cfg.m_additionalStamps[s];
                        if (stamp == null || stamp.m_hexRadius <= 0f) continue;

                        Vector3 stampCenter = new Vector3(basePos.x + stamp.m_offset.x, basePos.y + stamp.m_offset.y, 0f);
                        DrawPointyHexGizmo(stampCenter, stamp.m_hexRadius, stamp.m_scale, c);
                        Gizmos.color = new Color(c.r, c.g, c.b, 0.5f);
                        Gizmos.DrawLine(mainHexCenter, stampCenter);
                    }
                }

                // 3) 공식 항구 정박 반경 원 그리기
                Gizmos.color = c;
                Gizmos.DrawWireSphere(portCenter, cfg.m_portRadius);
            }
        }

        private static void DrawPointyHexGizmo(Vector3 center, float apothemRadius, Vector2 scale, Color color)
        {
            if (apothemRadius <= 0f) return;

            float sx = Mathf.Max(0.1f, Mathf.Abs(scale.x));
            float sy = Mathf.Max(0.1f, Mathf.Abs(scale.y));

            // 내접반경(Apothem = apothemRadius)으로부터 외접반경(Circumradius = R / cos(30°)) 도출
            float circumRadius = apothemRadius / 0.8660254f;

            Vector3 prev = Vector3.zero;
            Vector3 first = Vector3.zero;
            Gizmos.color = color;

            for (int v = 0; v < 6; v++)
            {
                // Pointy-Top 육각형 꼭짓점 각도: 30°, 90°, 150°, 210°, 270°, 330°
                float angleRad = (30f + v * 60f) * Mathf.Deg2Rad;
                Vector3 vertex = center + new Vector3(
                    Mathf.Cos(angleRad) * circumRadius * sx,
                    Mathf.Sin(angleRad) * circumRadius * sy,
                    0f
                );

                if (v == 0)
                {
                    first = vertex;
                }
                else
                {
                    Gizmos.DrawLine(prev, vertex);
                }

                // 중심에서 각 꼭짓점으로 옅은 방사형 내부 가이드선 표시
                Gizmos.color = new Color(color.r, color.g, color.b, 0.2f);
                Gizmos.DrawLine(center, vertex);
                Gizmos.color = color;

                prev = vertex;
            }

            Gizmos.DrawLine(prev, first);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!m_drawAreaGizmos || m_stageAreaRects == null) return;
            DrawStageHexAreaGizmos(m_stageAreaRects);
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
