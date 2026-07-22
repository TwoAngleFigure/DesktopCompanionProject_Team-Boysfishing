using UnityEngine;
using UnityEngine.UI;
using DesktopCompanion.Rendering;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 화면 모드(Full/Window)·스케일·모니터를 적용하는 컨트롤러(계획 06: RT 크롭 방식).
    ///
    /// 표시 방식 = 단일 카메라 → RenderTexture → RawImage 크롭:
    ///  - World Camera(A): 씬을 항상 투명 RT에 렌더(SW3가 단일 카메라 컨텍스트로 일관 렌더 → 물 일관·AlignToWater 정상).
    ///  - Screen Clear Camera: 화면 백버퍼를 매 프레임 투명 클리어(잔상 방지). 지오메트리 미렌더(SW3 무관).
    ///  - RawImage: RT를 표시. Full=전체·uv 전체, Window=우하단 크롭(uvRect)·크기(크롭×scale)·위치(드래그).
    ///
    /// ※ 캔버스는 Screen Space - Overlay, Canvas Scaler = Constant Pixel Size(scaleFactor 1) 전제(픽셀=UI 단위).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class DisplayModeController : MonoBehaviour
    {
        [Header("World Render")]
        [SerializeField] private Camera m_worldCamera;        // A: 씬 → RT
        [SerializeField] private Camera m_screenClearCamera;  // 화면 백버퍼 투명 클리어
        [SerializeField, Min(1)] private int m_superSample = 2;

        [Header("Presentation")]
        [SerializeField] private RawImage m_worldImage;       // RT 표시(전체화면 캔버스)
        [Tooltip("Window 모드의 세로 크롭 비율(화면 대비). 월드가 화면 하단에 있으므로 하단 고정이다.")]
        [SerializeField, Range(0.05f, 1f)] private float m_cropHeight01 = 1f / 3f;

        /// <summary>크롭 가로 범위의 최소 폭. 0폭으로 접히는 것을 막는다.</summary>
        private const float MinCropWidth = 0.05f;

        [Header("Refs")]
        [SerializeField] private TransparentWindow m_transparentWindow;
        [SerializeField] private ClickThroughManager m_clickThrough;

        private RectTransform m_worldRect;
        private RenderTexture m_rt;
        private DisplaySettingsData m_data;

        public ScreenMode Mode => m_data.Mode;
        public float Scale => m_data.Scale;
        public int MonitorIndex => m_data.MonitorIndex;
        public int MonitorCount { get; private set; } = 1;
        public bool WindowMoveMode { get; private set; }

        private void Start()
        {
            if (m_worldImage != null)
            {
                // 에디터 작업 편의를 위해 RawImage는 꺼둘 수 있다. 런타임 초기화 시 켠다.
                m_worldImage.gameObject.SetActive(true);
                m_worldRect = m_worldImage.rectTransform;
            }

            m_data = DisplaySettings.Load();
            RefreshMonitorCount();
            m_data.MonitorIndex = Mathf.Clamp(m_data.MonitorIndex, 0, Mathf.Max(0, MonitorCount - 1));

            SetupWorldCamera();
            SetupClearCamera();

            ApplyMonitor(m_data.MonitorIndex, save: false);
            EnsureRT();
            Apply(m_data.Mode, m_data.Scale, save: false);
            Save();

            // 클릭관통이 커서→월드카메라 좌표 매핑(RawImage uv→RT)을 쓰도록 연결.
            if (m_clickThrough != null)
            {
                m_clickThrough.ScreenToWorldCameraPoint = MapCursorToWorldCamera;
            }
        }

        private void Update()
        {
            // 해상도/모니터 변경 시 RT 재생성 + 표시 갱신.
            GetRtSize(out int w, out int h);
            if (m_rt != null && (m_rt.width != w || m_rt.height != h))
            {
                EnsureRT();
                ApplyPresentation();
            }
        }

        // ── 렌더 밴드(계획 25) ──
        //
        // 16:9보다 좁은 화면에서도 가로 시야를 확보하기 위해 렌더 영역을 눕힌다.
        // 카메라 크기를 키우지 않으므로 물·조명 스케일이 환경과 무관하게 일정하다.
        // targetTexture가 설정된 카메라의 종횡비는 화면이 아니라 그 텍스처를 따르므로,
        // RT를 밴드 형상으로 만드는 것만으로 카메라가 모니터 해상도에서 분리된다.

        private float BandAspect
        {
            get
            {
                float screenAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
                return Mathf.Max(screenAspect, PixelGridDesign.ReferenceAspect);
            }
        }

        /// <summary>화면에 표시되는 밴드의 높이(픽셀). 화면 하단에 이 높이만큼 월드가 그려진다.</summary>
        private int BandHeightPixels => Mathf.Max(1, Mathf.RoundToInt(Screen.width / BandAspect));

        private void GetRtSize(out int width, out int height)
        {
            width = Mathf.Max(1, Screen.width * m_superSample);
            height = Mathf.Max(1, Mathf.RoundToInt(width / BandAspect));
        }

        // ── 카메라/RT 셋업 ──

        private void SetupWorldCamera()
        {
            if (m_worldCamera == null) return;
            m_worldCamera.clearFlags = CameraClearFlags.SolidColor;
            m_worldCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            // 시야 확보는 RT 종횡비가 담당하므로 카메라 크기는 상수다(계획 25).
            m_worldCamera.orthographic = true;
            m_worldCamera.orthographicSize = PixelGridDesign.ReferenceOrthographicSize;
            m_worldCamera.enabled = true;   // 상시 렌더(RT)
        }

        private void SetupClearCamera()
        {
            if (m_screenClearCamera == null) return;
            m_screenClearCamera.targetTexture = null;              // 화면에 렌더
            m_screenClearCamera.cullingMask = 0;                  // Nothing — 지오메트리 미렌더
            m_screenClearCamera.clearFlags = CameraClearFlags.SolidColor;
            m_screenClearCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            m_screenClearCamera.depth = -100;                     // 가장 먼저
            m_screenClearCamera.enabled = true;
        }

        private void EnsureRT()
        {
            // 높이를 화면이 아닌 밴드 종횡비로 정한다 → 카메라 종횡비가 모니터와 무관해진다.
            GetRtSize(out int w, out int h);
            if (m_rt != null && m_rt.width == w && m_rt.height == h) return;

            if (m_worldCamera != null) m_worldCamera.targetTexture = null;
            if (m_rt != null) { m_rt.Release(); Destroy(m_rt); }

            m_rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "World_RT" };
            m_rt.Create();

            if (m_worldCamera != null) m_worldCamera.targetTexture = m_rt;
            if (m_worldImage != null) m_worldImage.texture = m_rt;
        }

        // ── 모드/스케일 ──

        public void SetMode(ScreenMode mode) => Apply(mode, m_data.Scale, save: true);
        public void SetScale(float scale) => Apply(m_data.Mode, scale, save: true);

        public void Apply(ScreenMode mode, float scale, bool save = true)
        {
            m_data.Mode = mode;
            m_data.Scale = ViewScaleRange.Clamp(scale);

            ApplyPresentation();

            if (mode != ScreenMode.Window) SetWindowMoveMode(false);
            if (save) Save();
        }

        // RawImage(=월드 렌더 표시)의 uv 크롭·크기·위치를 모드/스케일에 맞게 설정.
        private void ApplyPresentation()
        {
            if (m_worldImage == null || m_worldRect == null) return;

            m_worldRect.anchorMin = Vector2.zero;   // 좌하단 기준(픽셀 좌표)
            m_worldRect.anchorMax = Vector2.zero;
            m_worldRect.pivot = Vector2.zero;

            int bandH = BandHeightPixels;

            if (m_data.Mode == ScreenMode.Full)
            {
                m_worldImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                // 밴드는 화면 하단에 붙는다. 위쪽은 렌더되지 않아 바탕화면이 비친다(계획 25).
                m_worldRect.sizeDelta = new Vector2(Screen.width, bandH);
                m_worldRect.anchoredPosition = Vector2.zero;
            }
            else
            {
                // 가로는 Left/Right 경계값이 정한다(폭은 파생값). 세로는 하단 고정.
                NormalizeCropRange(ref m_data.CropLeft, ref m_data.CropRight);
                float cw = m_data.CropRight - m_data.CropLeft;
                float ch = m_cropHeight01;
                float f = ViewScaleRange.Clamp(m_data.Scale);

                m_worldImage.uvRect = new Rect(m_data.CropLeft, 0f, cw, ch);

                // 잘라낸 영역의 픽셀 종횡비를 그대로 유지하므로 왜곡이 없다.
                // 세로 기준은 Screen.height가 아니라 밴드 높이다 — RT가 밴드 형상이므로.
                // cw는 픽셀 블록 크기 식에서 소거되므로 크롭을 바꿔도 픽셀 크기는 변하지 않는다(계획 21).
                Vector2 size = ClampSize(new Vector2(Screen.width * cw * f, bandH * ch * f));
                m_worldRect.sizeDelta = size;

                Vector2 posPx = ClampPos(new Vector2(m_data.WindowRectPos.x * Screen.width, m_data.WindowRectPos.y * Screen.height), size);
                m_worldRect.anchoredPosition = posPx;
                m_data.WindowRectPos = new Vector2(posPx.x / Screen.width, posPx.y / Screen.height);
            }
        }

        // ── 크롭 범위 ──

        public float CropLeft => m_data.CropLeft;
        public float CropRight => m_data.CropRight;

        /// <summary>크롭 가로 경계를 지정한다(슬라이더용).</summary>
        public void SetCropRange(float left, float right)
        {
            m_data.CropLeft = left;
            m_data.CropRight = right;
            ApplyPresentation();
            Save();
        }

        /// <summary>폭을 유지한 채 크롭 범위를 좌우로 민다(드래그용). delta는 0~1 정규화 값.</summary>
        public void ShiftCropRange(float delta01)
        {
            float cw = m_data.CropRight - m_data.CropLeft;
            float left = Mathf.Clamp(m_data.CropLeft + delta01, 0f, Mathf.Max(0f, 1f - cw));
            SetCropRange(left, left + cw);
        }

        /// <summary>크롭 가로 범위를 [0,1]·순서·최소폭 규칙에 맞게 보정한다.</summary>
        private static void NormalizeCropRange(ref float left, ref float right)
        {
            left = Mathf.Clamp01(left);
            right = Mathf.Clamp01(right);
            if (right < left)
            {
                (left, right) = (right, left);
            }
            if (right - left < MinCropWidth)
            {
                right = Mathf.Min(1f, left + MinCropWidth);
                left = Mathf.Max(0f, right - MinCropWidth);
            }
        }

        // 크롭 폭이 가변이므로 표시 크기가 화면을 넘지 않도록 제한한다.
        private static Vector2 ClampSize(Vector2 size) => new Vector2(
            Mathf.Min(size.x, Screen.width),
            Mathf.Min(size.y, Screen.height));

        // ── 창 이동(드래그) ──

        public void SetWindowMoveMode(bool on)
        {
            WindowMoveMode = on && Mode == ScreenMode.Window;
            if (m_clickThrough != null) m_clickThrough.ForceNoClickThrough = WindowMoveMode;
        }

        public void ToggleWindowMoveMode() => SetWindowMoveMode(!WindowMoveMode);

        /// <summary>드래그: RawImage 위치를 픽셀 델타만큼 이동(내용 uv는 고정).</summary>
        public void MoveWindowRect(Vector2 deltaPixels)
        {
            if (Mode != ScreenMode.Window || m_worldRect == null) return;
            Vector2 posPx = ClampPos(m_worldRect.anchoredPosition + deltaPixels, m_worldRect.sizeDelta);
            m_worldRect.anchoredPosition = posPx;
            m_data.WindowRectPos = new Vector2(posPx.x / Screen.width, posPx.y / Screen.height);
        }

        public void EndDragSave() => Save();

        /// <summary>현재 창(RawImage) 화면 rect(픽셀). 드래그 히트테스트용.</summary>
        public Rect WindowRectPixels()
        {
            if (m_worldRect == null) return default;
            return new Rect(m_worldRect.anchoredPosition, m_worldRect.sizeDelta);
        }

        /// <summary>커서(스크린) → World 카메라(=RT) 픽셀 좌표. RawImage 밖이면 null(월드 위 아님).</summary>
        public Vector2? MapCursorToWorldCamera(Vector2 cursor)
        {
            if (m_worldRect == null || m_worldImage == null || m_rt == null) return null;
            Rect wr = WindowRectPixels();
            if (wr.width <= 0 || wr.height <= 0 || wr.Contains(cursor) == false) return null;

            Vector2 t = new Vector2((cursor.x - wr.x) / wr.width, (cursor.y - wr.y) / wr.height); // RawImage 내 0..1
            Rect uv = m_worldImage.uvRect;
            Vector2 uvp = new Vector2(uv.x + t.x * uv.width, uv.y + t.y * uv.height);              // RT uv
            return new Vector2(uvp.x * m_rt.width, uvp.y * m_rt.height);                           // 카메라 A(=RT) 픽셀
        }

        // ── 모니터 ──

        public void SetMonitor(int index)
        {
            ApplyMonitor(index, save: true);
            EnsureRT();          // 해상도 변화 반영
            ApplyPresentation();
        }

        private void ApplyMonitor(int index, bool save)
        {
            // 열거는 한 번만 수행하고 개수·배치에 함께 쓴다(모니터 구성이 중간에 바뀌는 것을 방지).
            var monitors = Win32Native.GetMonitors();
            MonitorCount = Mathf.Max(1, monitors.Count);

            index = Mathf.Clamp(index, 0, MonitorCount - 1);
            m_data.MonitorIndex = index;

            if (m_transparentWindow != null && index < monitors.Count)
            {
                var m = monitors[index];
                m_transparentWindow.ApplyMonitorBounds(m.rect.left, m.rect.top, m.Width, m.Height);
            }
            if (save) Save();
        }

        /// <summary>현재 선택된 모니터의 Windows 디스플레이 번호(UI 표기용). 조회 실패 시 index+1.</summary>
        public int CurrentDisplayNumber()
        {
            var monitors = Win32Native.GetMonitors();
            int i = Mathf.Clamp(m_data.MonitorIndex, 0, Mathf.Max(0, monitors.Count - 1));
            return i < monitors.Count ? monitors[i].displayNumber : i + 1;
        }

        private void RefreshMonitorCount()
        {
            MonitorCount = Mathf.Max(1, Win32Native.GetMonitors().Count);
        }

        private static Vector2 ClampPos(Vector2 pos, Vector2 size)
            => new Vector2(
                Mathf.Clamp(pos.x, 0f, Mathf.Max(0f, Screen.width - size.x)),
                Mathf.Clamp(pos.y, 0f, Mathf.Max(0f, Screen.height - size.y)));

        private void Save() => DisplaySettings.Save(m_data);

        private void OnDestroy()
        {
            if (m_worldCamera != null) m_worldCamera.targetTexture = null;
            if (m_rt != null) { m_rt.Release(); Destroy(m_rt); }
        }
    }
}
