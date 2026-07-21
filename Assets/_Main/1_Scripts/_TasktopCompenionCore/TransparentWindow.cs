using System;
using System.Collections;
using UnityEngine;

namespace DesktopCompanion
{
    /// <summary>
    /// Unity Standalone 창을 Desktop Companion용으로 구성한다.
    /// 배경 투명화 · 최상단 고정 · 클릭 관통(초기 상태)을 적용하며,
    /// 런타임 클릭 관통 토글은 <see cref="ClickThroughManager"/>가 담당한다.
    /// Windows 10 (1903+) / Windows 11 전용.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class TransparentWindow : MonoBehaviour
    {
        // 아래 SerializeField 필드들은 Windows Standalone 빌드(#if UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        // 안에서만 읽힌다. 에디터는 해당 블록을 컴파일에서 제외하므로 "할당했지만 미사용(CS0414)" 경고가
        // 뜨지만, 실제 빌드에서는 모두 사용되므로 필드를 삭제하면 안 된다. 에디터 경고만 억제한다.
#pragma warning disable CS0414
        [Tooltip("시작 시 클릭 관통 상태로 둘지 여부")]
        [SerializeField] private bool _clickThrough = true;

        [Tooltip("작업표시줄에서 창을 숨길지 여부")]
        [SerializeField] private bool _hideFromTaskbar = false;

        [Tooltip("HWND 확보 실패 시 재시도할 최대 프레임 수")]
        [SerializeField] private int _hwndRetryFrames = 30;

        [Tooltip("창을 주 모니터 전체 크기로 자동 배치할지 여부")]
        [SerializeField] private bool _coverFullScreen = true;

        [Tooltip("전체화면 최적화(투명화 깨짐) 회피용 가장자리 인셋(px). 화면을 정확히 꽉 채우지 않도록 함")]
        [SerializeField] private int _edgeInset = 1;
#pragma warning restore CS0414

        private IntPtr _hwnd = IntPtr.Zero;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private Coroutine _monitorBoundsRoutine;

        // HWND 확보 전에 도착한 배치 요청을 보관했다가 초기화 완료 시 적용한다.
        private bool _hasPendingBounds;
        private RectInt _pendingBounds;
#endif
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // [진단] 마지막으로 요청한 창 영역. 실제 결과와 대조해 DPI 재조정 여부를 판별한다.
        private RectInt _lastRequested;
#endif

        /// <summary>창 초기화가 끝나 HWND가 확보된 상태인지.</summary>
        public bool IsReady => _hwnd != IntPtr.Zero;

        /// <summary>확보된 창 핸들(전역 커서 좌표 변환 등에 사용). 미확보 시 IntPtr.Zero.</summary>
        public IntPtr Hwnd => _hwnd;

        private void Start()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            StartCoroutine(InitializeWhenWindowReady());
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator InitializeWhenWindowReady()
        {
            // 0) 창 모드를 먼저 강제한다.
            //    Unity는 마지막 화면 모드를 레지스트리에 저장했다가 다음 실행에 복원한다.
            //    한 번이라도 전체화면(Alt+Enter 포함)이 되면 이후 실행이 전체화면으로 시작하고,
            //    그 상태에서는 DWM 투명화가 성립하지 않는다. 매 기동 시 창 모드로 되돌린다.
            if (Screen.fullScreen)
            {
                Screen.fullScreenMode = FullScreenMode.Windowed;
                for (int i = 0; i < _hwndRetryFrames && Screen.fullScreen; i++)
                {
                    yield return null;
                }
            }

            // 1) 창 핸들 확보. 포커스에 의존하는 GetActiveWindow는 전체화면 전환 중 0을 반환하므로
            //    프로세스 창 열거를 우선 사용하고, 실패 시에만 GetActiveWindow로 폴백한다.
            for (int i = 0; i < _hwndRetryFrames; i++)
            {
                _hwnd = Win32Native.FindMainWindow();
                if (_hwnd == IntPtr.Zero)
                {
                    _hwnd = Win32Native.GetActiveWindow();
                }
                if (_hwnd != IntPtr.Zero)
                {
                    break;
                }
                yield return null;
            }

            if (_hwnd == IntPtr.Zero)
            {
                Debug.LogError("[TransparentWindow] HWND 확보 실패 — 창 설정을 적용하지 못했습니다.");
                yield break;
            }

            ApplyBorderless();

            // 배치 루틴이 크기 확정 후 투명화·최상단·클릭관통까지 마무리한다(순서가 중요).
            // HWND 확보 전에 들어온 배치 요청(저장된 모니터)이 있으면 그것을 우선한다.
            if (_hasPendingBounds)
            {
                _hasPendingBounds = false;
                ApplyMonitorBounds(_pendingBounds.x, _pendingBounds.y, _pendingBounds.width, _pendingBounds.height);
            }
            else if (_coverFullScreen)
            {
                ApplyFullScreenBounds();
            }
            else
            {
                ApplyTransparency();
                TrySetSquareCorners();
                ApplyTopMost(true);
                SetClickThrough(_clickThrough);
            }
        }

        // 주 모니터 전체 크기로 창을 배치한다(해상도 독립).
        // 전체화면 최적화가 DWM 합성을 우회해 투명화를 깨지 않도록 _edgeInset만큼 살짝 줄인다.
        private void ApplyFullScreenBounds()
        {
            int screenWidth = Win32Native.GetSystemMetrics(Win32Native.SM_CXSCREEN);
            int screenHeight = Win32Native.GetSystemMetrics(Win32Native.SM_CYSCREEN);
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                Debug.LogWarning("[TransparentWindow] 화면 크기 조회 실패 — 전체화면 배치를 건너뜁니다.");
                return;
            }

            // 배치·스타일 복구 순서를 모니터 전환과 동일하게 맞춘다(_edgeInset는 루틴 내부에서 적용).
            _monitorBoundsRoutine = StartCoroutine(ApplyMonitorBoundsRoutine(0, 0, screenWidth, screenHeight));
        }

        private void ApplyBorderless()
        {
            uint style = Win32Native.WS_POPUP | Win32Native.WS_VISIBLE;
            Win32Native.SetWindowLong(_hwnd, Win32Native.GWL_STYLE, style);

            uint exStyle = (uint)Win32Native.GetWindowLong(_hwnd, Win32Native.GWL_EXSTYLE);
            // WS_EX_LAYERED는 클릭 관통(WS_EX_TRANSPARENT)이 동작하는 데 필요하다.
            // 불투명의 원인은 layered가 아니라 Flip Model 스왑체인(알파 무시)이므로 layered는 유지한다.
            exStyle |= Win32Native.WS_EX_LAYERED;
            if (_hideFromTaskbar)
            {
                exStyle |= Win32Native.WS_EX_TOOLWINDOW;
            }
            Win32Native.SetWindowLong(_hwnd, Win32Native.GWL_EXSTYLE, exStyle);

            // SetWindowLong만으로는 스타일 비트만 바뀌고 비클라이언트 영역이 재계산되지 않는다.
            // SWP_FRAMECHANGED로 프레임 재계산을 강제해야 테두리·캡션이 실제로 사라진다.
            Win32Native.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0,
                Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE | Win32Native.SWP_NOZORDER |
                Win32Native.SWP_NOACTIVATE | Win32Native.SWP_FRAMECHANGED);
        }

        // DWM 유리 프레임을 클라이언트 전체로 확장 → per-pixel alpha 합성
        // ※ 이 설정은 창이 재구성되면(해상도 변경 등) 풀리므로 그때마다 다시 적용해야 한다.
        private void ApplyTransparency()
        {
            var margins = new Win32Native.MARGINS
            {
                cxLeftWidth = -1,
                cxRightWidth = -1,
                cyTopHeight = -1,
                cyBottomHeight = -1,
            };
            Win32Native.DwmExtendFrameIntoClientArea(_hwnd, ref margins);
        }

        // Windows 11(빌드 22000+)에서만 유효. 하위 OS에서는 호출해도 무시된다.
        private void TrySetSquareCorners()
        {
            if (Environment.OSVersion.Version.Build < 22000)
            {
                return;
            }
            int preference = (int)Win32Native.DWM_WINDOW_CORNER_PREFERENCE.DONOTROUND;
            Win32Native.DwmSetWindowAttribute(
                _hwnd, Win32Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }
#endif

        /// <summary>
        /// 창을 지정한 모니터 영역(가상 데스크톱 좌표)으로 재배치한다(다중 모니터: Full 모니터 선택용).
        /// 에디터에서는 no-op. 좌표는 <see cref="Win32Native.GetMonitors"/>의 rect를 사용한다.
        /// </summary>
        public void ApplyMonitorBounds(int x, int y, int width, int height)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_hwnd == IntPtr.Zero)
            {
                // 초기화가 아직 끝나지 않았다. 요청을 보관했다가 HWND 확보 후 적용한다.
                _hasPendingBounds = true;
                _pendingBounds = new RectInt(x, y, width, height);
                return;
            }
            // 연타 시 이전 배치 코루틴이 뒤늦게 좌표를 덮어쓰지 않도록 취소한다.
            if (_monitorBoundsRoutine != null)
            {
                StopCoroutine(_monitorBoundsRoutine);
            }
            _monitorBoundsRoutine = StartCoroutine(ApplyMonitorBoundsRoutine(x, y, width, height));
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // 창 배치의 순서 규약(검증으로 확정된 것):
        //   테두리 제거 → 크기·위치 확정 → (반영 대기) → DWM 투명화.
        // 투명화를 먼저 걸면 이후 리사이즈가 이를 무효화해 배경이 불투명해진다.
        private IEnumerator ApplyMonitorBoundsRoutine(int x, int y, int width, int height)
        {
            int w = Mathf.Max(1, width - _edgeInset);
            int h = Mathf.Max(1, height - _edgeInset);

            // 0) 전체화면 상태(Alt+Enter 등)면 창 모드로 되돌린다. 전체화면에서는 DWM 투명화가 성립하지 않는다.
            if (Screen.fullScreen)
            {
                Screen.fullScreenMode = FullScreenMode.Windowed;
                for (int i = 0; i < 10 && Screen.fullScreen; i++)
                {
                    yield return null;
                }
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _lastRequested = new RectInt(x, y, w, h);
#endif

            // 1) 테두리 제거를 먼저 확정한다.
            //    프레임이 남은 상태로 크기를 잡으면 클라이언트가 비클라이언트 크기만큼 작아진다.
            ApplyBorderless();

            // 2) 창 위치·크기를 확정한다.
            //    Screen.SetResolution은 의도적으로 쓰지 않는다 — Unity는 "클라이언트 크기" 기준으로
            //    창 크기를 역산하고 우리는 "창 크기"를 직접 지정하므로, 둘을 함께 쓰면 비클라이언트
            //    크기만큼 서로 밀어내며 경합한다. 창 크기만 지정하면 Unity가 WM_SIZE를 따라온다.
            //
            //    ※ 이 프로세스는 PerMonitor DPI 인식이다. DPI가 다른 모니터로 창을 옮기면 Windows가
            //      WM_DPICHANGED와 함께 "권장 rect"를 보내고, 그 결과 창이 (대상DPI/이전DPI) 배율만큼
            //      다시 조정된다. 예: 96dpi → 120dpi 이동 시 1.25배로 부풀려진다.
            //      따라서 한 번의 SetWindowPos로는 부족하고, 실제 rect를 확인해 목표 물리 픽셀로
            //      다시 맞춰야 한다. 이동이 끝나 DPI가 안정되면 두 번째 지정이 그대로 유지된다.
            const int maxCorrections = 4;
            for (int attempt = 0; attempt < maxCorrections; attempt++)
            {
                Win32Native.SetWindowPos(_hwnd, Win32Native.HWND_TOPMOST, x, y, w, h,
                    Win32Native.SWP_NOACTIVATE | Win32Native.SWP_SHOWWINDOW | Win32Native.SWP_FRAMECHANGED);

                // DPI 변경 메시지가 처리될 시간을 준다.
                yield return null;
                yield return new WaitForEndOfFrame();

                if (Win32Native.GetWindowRect(_hwnd, out var r) == false)
                {
                    break;
                }
                if (r.left == x && r.top == y && r.right - r.left == w && r.bottom - r.top == h)
                {
                    break;   // 목표 물리 픽셀에 도달
                }
            }

            // 3) Unity 백버퍼(Screen)가 창 크기를 따라올 때까지 기다린다.
            const int maxWaitFrames = 10;
            for (int i = 0; i < maxWaitFrames; i++)
            {
                if (Screen.width == w && Screen.height == h)
                {
                    break;
                }
                yield return null;
            }
            yield return new WaitForEndOfFrame();

            // 4) DWM 유리 프레임은 반드시 마지막에 적용한다.
            //    리사이즈·스타일 변경이 이 설정을 무효화하므로, 크기가 확정된 뒤에 걸어야 유지된다.
            //    (이 순서가 어긋나 배경이 하얗게 남았고, F12 수동 재적용으로만 복구됐다.)
            ApplyTransparency();
            TrySetSquareCorners();
            ApplyTopMost(true);
            SetClickThrough(_clickThrough);
        }
#endif

        /// <summary>
        /// 테두리 없는 창 스타일·DWM 투명화를 다시 적용한다.
        /// 창 크기가 바뀌면 DWM 프레임 확장이 무효화되므로, 리사이즈 후에는 반드시 다시 적용해야 한다.
        /// </summary>
        public void ReapplyWindowStyles()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }
            ApplyBorderless();
            ApplyTransparency();
            TrySetSquareCorners();
            ApplyTopMost(true);
            SetClickThrough(_clickThrough);
#endif
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>[진단] 현재 창 스타일·크기·DPI 상태를 문자열로 덤프한다(개발 빌드 전용).</summary>
        public string DumpWindowState()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_hwnd == IntPtr.Zero)
            {
                return "  HWND 미확보";
            }
            uint style = (uint)Win32Native.GetWindowLong(_hwnd, Win32Native.GWL_STYLE);
            uint ex = (uint)Win32Native.GetWindowLong(_hwnd, Win32Native.GWL_EXSTYLE);

            string rects = "  창 rect 조회 실패";
            if (Win32Native.GetWindowRect(_hwnd, out var wr) && Win32Native.GetClientRect(_hwnd, out var cr))
            {
                int aw = wr.right - wr.left;
                int ah = wr.bottom - wr.top;
                bool match = aw == _lastRequested.width && ah == _lastRequested.height;
                rects =
                    $"  요청한 창 크기 : {_lastRequested.width}x{_lastRequested.height} @ ({_lastRequested.x},{_lastRequested.y})\n" +
                    $"  실제 창 rect   : {aw}x{ah} @ ({wr.left},{wr.top})   {(match ? "← 일치" : $"← 불일치(배율 {(float)aw / Mathf.Max(1, _lastRequested.width):0.###})")}\n" +
                    $"  실제 client    : {cr.right - cr.left}x{cr.bottom - cr.top}   (Unity Screen: {Screen.width}x{Screen.height})";
            }

            uint winDpi = Win32Native.GetDpiForWindow(_hwnd);
            int awareness = Win32Native.GetProcessDpiAwareness();
            string awarenessName = awareness switch
            {
                0 => "Unaware(가상화됨)",
                1 => "System",
                2 => "PerMonitor",
                _ => "조회 실패",
            };

            return
                $"  GWL_STYLE   =0x{style:X8}  POPUP={(style & Win32Native.WS_POPUP) != 0}  VISIBLE={(style & Win32Native.WS_VISIBLE) != 0}\n" +
                $"  GWL_EXSTYLE =0x{ex:X8}  LAYERED={(ex & Win32Native.WS_EX_LAYERED) != 0}  TRANSPARENT={(ex & Win32Native.WS_EX_TRANSPARENT) != 0}\n" +
                rects + "\n" +
                $"  DPI 인식 모드  : {awarenessName}({awareness})\n" +
                $"  창 DPI         : {winDpi} ({(winDpi > 0 ? winDpi / 96f : 0f):0.##}배)   Unity Screen.dpi={Screen.dpi:0.#}";
#else
            return "  (에디터: 창 스타일 미적용)";
#endif
        }
#endif

        /// <summary>창을 최상단으로 고정하거나 해제한다.</summary>
        public void ApplyTopMost(bool on)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }
            IntPtr insertAfter = on ? Win32Native.HWND_TOPMOST : Win32Native.HWND_NOTOPMOST;
            Win32Native.SetWindowPos(_hwnd, insertAfter, 0, 0, 0, 0,
                Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE |
                Win32Native.SWP_NOACTIVATE | Win32Native.SWP_SHOWWINDOW);
#endif
        }

        /// <summary>마우스 클릭 관통(WS_EX_TRANSPARENT) 상태를 설정한다.</summary>
        public void SetClickThrough(bool on)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }
            uint exStyle = (uint)Win32Native.GetWindowLong(_hwnd, Win32Native.GWL_EXSTYLE);
            if (on)
            {
                exStyle |= Win32Native.WS_EX_TRANSPARENT;
            }
            else
            {
                exStyle &= ~Win32Native.WS_EX_TRANSPARENT;
            }
            Win32Native.SetWindowLong(_hwnd, Win32Native.GWL_EXSTYLE, exStyle);
#endif
        }
    }
}
