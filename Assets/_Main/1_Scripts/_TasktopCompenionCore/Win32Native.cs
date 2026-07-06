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
    }
}
