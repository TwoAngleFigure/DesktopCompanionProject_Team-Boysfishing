using UnityEngine;
using UnityEngine.UI;

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
        [Tooltip("Window 기준 크롭(화면 대비 비율). 우하단 박스. Scale로 확대되므로 두 축 모두 <1 권장.")]
        [SerializeField] private Vector2 m_cropSize01 = new Vector2(1f / 3f, 1f / 3f);

        [Header("Refs")]
        [SerializeField] private TransparentWindow m_transparentWindow;
        [SerializeField] private ClickThroughManager m_clickThrough;

        private RectTransform m_worldRect;
        private RenderTexture m_rt;
        private DisplaySettingsData m_data;

        public ScreenMode Mode => m_data.Mode;
        public ViewScale Scale => m_data.Scale;
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
            if (m_rt != null && (m_rt.width != Screen.width * m_superSample || m_rt.height != Screen.height * m_superSample))
            {
                EnsureRT();
                ApplyPresentation();
            }
        }

        // ── 카메라/RT 셋업 ──

        private void SetupWorldCamera()
        {
            if (m_worldCamera == null) return;
            m_worldCamera.clearFlags = CameraClearFlags.SolidColor;
            m_worldCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
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
            int w = Mathf.Max(1, Screen.width * m_superSample);
            int h = Mathf.Max(1, Screen.height * m_superSample);
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
        public void SetScale(ViewScale scale) => Apply(m_data.Mode, scale, save: true);

        public void Apply(ScreenMode mode, ViewScale scale, bool save = true)
        {
            m_data.Mode = mode;
            m_data.Scale = scale;

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

            if (m_data.Mode == ScreenMode.Full)
            {
                m_worldImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                m_worldRect.sizeDelta = new Vector2(Screen.width, Screen.height);
                m_worldRect.anchoredPosition = Vector2.zero;
            }
            else
            {
                float cw = m_cropSize01.x;
                float ch = m_cropSize01.y;
                float f = m_data.Scale.ToFactor();

                m_worldImage.uvRect = new Rect(1f - cw, 0f, cw, ch);   // 우하단 크롭
                Vector2 size = new Vector2(Screen.width * cw * f, Screen.height * ch * f);
                m_worldRect.sizeDelta = size;

                Vector2 posPx = ClampPos(new Vector2(m_data.WindowRectPos.x * Screen.width, m_data.WindowRectPos.y * Screen.height), size);
                m_worldRect.anchoredPosition = posPx;
                m_data.WindowRectPos = new Vector2(posPx.x / Screen.width, posPx.y / Screen.height);
            }
        }

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
