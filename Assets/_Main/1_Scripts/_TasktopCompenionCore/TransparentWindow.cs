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

        /// <summary>창 초기화가 끝나 HWND가 확보된 상태인지.</summary>
        public bool IsReady => _hwnd != IntPtr.Zero;

        private void Start()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            StartCoroutine(InitializeWhenWindowReady());
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator InitializeWhenWindowReady()
        {
            // 스플래시/첫 프레임 직후에는 활성 창 핸들이 아직 없을 수 있어 재시도한다.
            for (int i = 0; i < _hwndRetryFrames; i++)
            {
                _hwnd = Win32Native.GetActiveWindow();
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
            ApplyTransparency();
            if (_coverFullScreen)
            {
                ApplyFullScreenBounds();
            }
            ApplyTopMost(true);
            SetClickThrough(_clickThrough);
            TrySetSquareCorners();
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

            int targetWidth = Mathf.Max(1, screenWidth - _edgeInset);
            int targetHeight = Mathf.Max(1, screenHeight - _edgeInset);

            Screen.SetResolution(targetWidth, targetHeight, FullScreenMode.Windowed);
            Win32Native.SetWindowPos(_hwnd, Win32Native.HWND_TOPMOST, 0, 0, targetWidth, targetHeight,
                Win32Native.SWP_NOACTIVATE | Win32Native.SWP_SHOWWINDOW);
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
        }

        // DWM 유리 프레임을 클라이언트 전체로 확장 → per-pixel alpha 합성
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
