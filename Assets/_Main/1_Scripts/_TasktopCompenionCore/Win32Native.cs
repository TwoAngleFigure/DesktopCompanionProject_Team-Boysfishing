using System;
using System.Runtime.InteropServices;

namespace DesktopCompanion
{
    /// <summary>
    /// Desktop Companion 창 제어에 필요한 Win32 API(P/Invoke) 선언 모음.
    /// Windows 10 (1903+) / Windows 11 전용. DWM 상시 활성 전제.
    /// 상수/시그니처는 MSDN 원본과 1:1 대응하도록 네이티브 명명을 유지한다.
    /// </summary>
    internal static class Win32Native
    {
        // ---- 윈도우 스타일 인덱스 ----
        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;

        // ---- 윈도우 스타일 ----
        public const uint WS_POPUP = 0x80000000;
        public const uint WS_VISIBLE = 0x10000000;

        // ---- 확장 윈도우 스타일 ----
        public const uint WS_EX_LAYERED = 0x00080000;
        public const uint WS_EX_TRANSPARENT = 0x00000020;
        public const uint WS_EX_TOOLWINDOW = 0x00000080; // 작업표시줄에서 숨김(선택)

        // ---- SetWindowPos z-order ----
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        // SetWindowLong으로 바꾼 스타일은 이 플래그로 SetWindowPos를 호출해야 비클라이언트 영역이
        // 재계산된다. 없으면 스타일 비트만 바뀌고 테두리/캡션이 화면에 그대로 남는다.
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const uint SWP_SHOWWINDOW = 0x0040;

        // ---- LayeredWindow ----
        public const uint LWA_COLORKEY = 0x00000001;
        public const uint LWA_ALPHA = 0x00000002;

        // ---- GetSystemMetrics 인덱스 ----
        public const int SM_CXSCREEN = 0; // 주 모니터 너비(px)
        public const int SM_CYSCREEN = 1; // 주 모니터 높이(px)
        public const int SM_CMONITORS = 80; // 모니터 개수(가상 데스크톱 구성)

        // ---- DWM 둥근 모서리(Windows 11 22000+ 전용) ----
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

        public enum DWM_WINDOW_CORNER_PREFERENCE
        {
            DEFAULT = 0,
            DONOTROUND = 1,
            ROUND = 2,
            ROUNDSMALL = 3,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int attrValue, int attrSize);

        // ---- 모니터 열거(다중 모니터: Full 모니터 선택용) ----
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        // 콜백에 넘어오는 rect가 각 모니터의 가상 데스크톱 좌표(음수 가능).
        // ※ IL2CPP(AOT)에서 네이티브 → 매니지드 역호출이 성립하려면 호출 규약을 명시해야 한다.
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        // ---- 모니터 상세(디바이스 이름·주모니터 여부) ----
        public const uint MONITORINFOF_PRIMARY = 0x00000001;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;   // 예: "\\\\.\\DISPLAY1"
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        /// <summary>모니터 1개의 정보. index는 Windows 디스플레이 번호(szDevice 기준)를 따른다.</summary>
        public struct MonitorInfo
        {
            public RECT rect;
            public bool isPrimary;
            public string deviceName;
            public int displayNumber;   // szDevice 끝 숫자. 조회 실패 시 열거 순서 + 1.

            public int Width => rect.right - rect.left;
            public int Height => rect.bottom - rect.top;
        }

        // ---- 모니터 열거 (IL2CPP 안전) ----
        //
        // IL2CPP는 AOT라 런타임에 델리게이트용 네이티브 thunk를 만들 수 없다. 따라서 역방향 P/Invoke
        // 콜백은 아래 세 조건을 모두 만족해야 한다. 하나라도 어기면 EnumDisplayMonitors가 조용히
        // false를 반환하고(예외조차 없다) 결과가 0개가 된다 — 실제로 그렇게 실패했었다.
        //   (1) static 메서드일 것 (람다/클로저 불가)
        //   (2) [MonoPInvokeCallback]으로 AOT가 thunk를 미리 생성하도록 할 것
        //   (3) 델리게이트 인스턴스를 static 필드로 붙잡아 GC 수거를 막을 것

        private static readonly System.Collections.Generic.List<IntPtr> s_enumHandles = new System.Collections.Generic.List<IntPtr>();
        private static readonly MonitorEnumProc s_enumCallback = OnMonitorEnum;

        [AOT.MonoPInvokeCallback(typeof(MonitorEnumProc))]
        private static bool OnMonitorEnum(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
        {
            // rect는 GetMonitorInfo로 다시 조회하므로 핸들만 모은다.
            s_enumHandles.Add(hMonitor);
            return true; // 계속 열거
        }

        /// <summary>
        /// 모든 모니터 정보를 Windows 디스플레이 번호 순으로 반환한다.
        /// 열거 실패 시 주 모니터 1개로 폴백하므로 항상 1개 이상을 돌려준다.
        /// </summary>
        public static System.Collections.Generic.List<MonitorInfo> GetMonitors()
        {
            var result = new System.Collections.Generic.List<MonitorInfo>();

            // EnumDisplayMonitors는 동기 호출이므로 static 버퍼 재사용이 안전하다.
            s_enumHandles.Clear();
            bool enumOk = false;
            try
            {
                enumOk = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, s_enumCallback, IntPtr.Zero);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[Win32Native] EnumDisplayMonitors 예외: {e.GetType().Name}: {e.Message}\n{e}");
            }

            if (enumOk == false || s_enumHandles.Count == 0)
            {
                UnityEngine.Debug.LogWarning(
                    $"[Win32Native] 모니터 열거 실패(enumOk={enumOk}, count={s_enumHandles.Count}, " +
                    $"lastError={Marshal.GetLastWin32Error()}) — 주 모니터 단독으로 폴백합니다.");
                return new System.Collections.Generic.List<MonitorInfo> { PrimaryMonitorFallback() };
            }

            for (int i = 0; i < s_enumHandles.Count; i++)
            {
                var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf(typeof(MONITORINFOEX)) };
                if (GetMonitorInfo(s_enumHandles[i], ref mi) == false)
                {
                    UnityEngine.Debug.LogWarning($"[Win32Native] GetMonitorInfo 실패(index={i}) — 해당 모니터를 건너뜁니다.");
                    continue;
                }

                result.Add(new MonitorInfo
                {
                    rect = mi.rcMonitor,
                    isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0,
                    deviceName = mi.szDevice,
                    displayNumber = ParseDisplayNumber(mi.szDevice, i + 1),
                });
            }

            if (result.Count == 0)
            {
                return new System.Collections.Generic.List<MonitorInfo> { PrimaryMonitorFallback() };
            }

            // EnumDisplayMonitors의 열거 순서는 문서상 보장되지 않는다.
            // Windows 디스플레이 설정의 "1/2/3"과 UI 번호를 맞추기 위해 디바이스 번호로 정렬한다.
            result.Sort((a, b) => a.displayNumber.CompareTo(b.displayNumber));
            return result;
        }

        // "\\.\DISPLAY2" → 2. 파싱 실패 시 fallback.
        private static int ParseDisplayNumber(string device, int fallback)
        {
            if (string.IsNullOrEmpty(device)) return fallback;

            int end = device.Length;
            while (end > 0 && char.IsDigit(device[end - 1])) end--;
            if (end >= device.Length) return fallback;

            return int.TryParse(device.Substring(end), out int n) ? n : fallback;
        }

        // 열거가 실패해도 최소한 주 모니터로는 동작하도록 한다.
        private static MonitorInfo PrimaryMonitorFallback()
        {
            int w = UnityEngine.Mathf.Max(1, GetSystemMetrics(SM_CXSCREEN));
            int h = UnityEngine.Mathf.Max(1, GetSystemMetrics(SM_CYSCREEN));
            return new MonitorInfo
            {
                rect = new RECT { left = 0, top = 0, right = w, bottom = h },
                isPrimary = true,
                deviceName = "\\\\.\\DISPLAY1",
                displayNumber = 1,
            };
        }

        // ---- 자기 프로세스의 메인 창 찾기 ----
        //
        // GetActiveWindow는 "호출 스레드가 활성 창을 가질 때"만 유효하다. 전체화면 전환 중이거나
        // 포커스가 다른 창에 있으면 0을 반환한다. 창 핸들 확보가 실패하면 투명화·클릭관통·모니터
        // 배치가 전부 무력화되므로, 포커스와 무관한 열거 방식으로 확보한다.

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentProcessId();

        private static readonly System.Collections.Generic.List<IntPtr> s_procWindows = new System.Collections.Generic.List<IntPtr>();
        private static readonly EnumWindowsProc s_enumWindowsCallback = OnEnumWindow;
        private static uint s_targetProcessId;

        // 모니터 열거와 동일한 IL2CPP 제약(static + MonoPInvokeCallback)을 따른다.
        [AOT.MonoPInvokeCallback(typeof(EnumWindowsProc))]
        private static bool OnEnumWindow(IntPtr hWnd, IntPtr lParam)
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == s_targetProcessId && IsWindowVisible(hWnd))
            {
                s_procWindows.Add(hWnd);
            }
            return true;
        }

        /// <summary>
        /// 현재 프로세스의 Unity 메인 창(UnityWndClass) 핸들을 찾는다.
        /// 포커스 상태와 무관하게 동작한다. 실패 시 IntPtr.Zero.
        /// </summary>
        public static IntPtr FindMainWindow()
        {
            s_procWindows.Clear();
            s_targetProcessId = GetCurrentProcessId();
            try
            {
                EnumWindows(s_enumWindowsCallback, IntPtr.Zero);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[Win32Native] EnumWindows 예외: {e.GetType().Name}: {e.Message}");
                return IntPtr.Zero;
            }

            var sb = new System.Text.StringBuilder(256);
            IntPtr fallback = IntPtr.Zero;

            for (int i = 0; i < s_procWindows.Count; i++)
            {
                IntPtr h = s_procWindows[i];
                sb.Length = 0;
                if (GetClassName(h, sb, sb.Capacity) > 0 && sb.ToString() == "UnityWndClass")
                {
                    return h;
                }
                if (fallback == IntPtr.Zero)
                {
                    fallback = h;   // 클래스명이 바뀌었을 때를 대비한 차선책
                }
            }
            return fallback;
        }

        // ---- DPI 인식/배율 진단 ----
        //
        // 창 크기 오차가 상수가 아니라 배율(1.25 / 0.8)로 나타나면 DPI 가상화가 개입한 것이다.
        // 프로세스의 DPI 인식 모드에 따라 Win32 좌표가 물리 픽셀일 수도, 가상화된 논리 픽셀일 수도 있다.

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        // Windows 10 1607+
        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetThreadDpiAwarenessContext();

        [DllImport("user32.dll")]
        public static extern uint GetAwarenessFromDpiAwarenessContext(IntPtr value);

        // Windows 8.1+ (shcore)
        public enum MONITOR_DPI_TYPE { EFFECTIVE = 0, ANGULAR = 1, RAW = 2 }

        [DllImport("shcore.dll")]
        public static extern int GetDpiForMonitor(IntPtr hmonitor, MONITOR_DPI_TYPE dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        public const uint MONITOR_DEFAULTTONEAREST = 2;

        /// <summary>DPI_AWARENESS 값(0=Unaware, 1=System, 2=PerMonitor). 조회 실패 시 -1.</summary>
        public static int GetProcessDpiAwareness()
        {
            try
            {
                return (int)GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext());
            }
            catch (System.Exception)
            {
                return -1;   // Win10 1607 미만
            }
        }

        /// <summary>지정 좌표가 속한 모니터의 유효 DPI. 조회 실패 시 0.</summary>
        public static uint GetMonitorDpi(int x, int y)
        {
            try
            {
                IntPtr hMon = MonitorFromPoint(new POINT { x = x, y = y }, MONITOR_DEFAULTTONEAREST);
                if (hMon == IntPtr.Zero) return 0;
                return GetDpiForMonitor(hMon, MONITOR_DPI_TYPE.EFFECTIVE, out uint dpiX, out _) == 0 ? dpiX : 0;
            }
            catch (System.Exception)
            {
                return 0;
            }
        }

        // ---- 전역 커서 위치(클릭관통 창이 마우스 메시지를 못 받아도 유효) ----
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        // ---- 전역 마우스 버튼(클릭관통·포커스와 무관하게 판독) ----
        public const int VK_LBUTTON = 0x01;
        public const int VK_RBUTTON = 0x02;

        // 키는 전역 판독이 곧 다른 앱의 입력까지 가로채는 것이므로,
        // 반드시 우리 창이 활성일 때만(GetActiveWindow) 읽어야 한다.
        public const int VK_ESCAPE = 0x1B;

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
    }
}
