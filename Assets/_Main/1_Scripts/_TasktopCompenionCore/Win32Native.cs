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
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        // ---- LayeredWindow ----
        public const uint LWA_COLORKEY = 0x00000001;
        public const uint LWA_ALPHA = 0x00000002;

        // ---- GetSystemMetrics 인덱스 ----
        public const int SM_CXSCREEN = 0; // 주 모니터 너비(px)
        public const int SM_CYSCREEN = 1; // 주 모니터 높이(px)

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
        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        /// <summary>모든 모니터의 경계 rect(가상 데스크톱 좌표)를 열거 순서대로 반환한다.</summary>
        public static System.Collections.Generic.List<RECT> GetMonitorRects()
        {
            var result = new System.Collections.Generic.List<RECT>();
            // EnumDisplayMonitors는 동기 호출이라 지역 델리게이트 수명으로 충분하다.
            MonitorEnumProc callback = (IntPtr hMon, IntPtr hdc, ref RECT rect, IntPtr data) =>
            {
                result.Add(rect);
                return true; // 계속 열거
            };
            try
            {
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            }
            catch (System.Exception)
            {
                // 비Windows/에디터 등에서 실패 시 빈 목록 → 호출부가 폴백 처리.
            }
            return result;
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

        // ---- 전역 마우스 버튼(클릭관통·포커스와 무관하게 판독) ----
        public const int VK_LBUTTON = 0x01;

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
    }
}
