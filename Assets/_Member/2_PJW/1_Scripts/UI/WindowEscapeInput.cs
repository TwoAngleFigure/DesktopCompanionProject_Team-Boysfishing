using System;
using UnityEngine;
using UnityEngine.InputSystem;
using DesktopCompanion;   // Win32Native

namespace DesktopCompanion.Views
{
    /// <summary>
    /// ESC 입력을 창 조작으로 연결한다. 우클릭 닫기(WindowRightClickCloser)를 대체한다.
    ///
    /// 규칙은 하나다 — <b>닫을 창이 있으면 닫고, 없으면 노트를 연다.</b>
    /// 노트만 열린 상태에서 ESC를 누르면 그 노트가 닫히므로 결과적으로 토글이 된다.
    ///
    /// 클릭관통 창은 Unity 입력이 갱신되지 않을 수 있어 빌드에서는 Win32 판독을 함께 쓴다.
    /// 단, 전역 키 판독은 다른 앱의 ESC까지 가로채므로 <b>우리 창이 활성일 때만</b> 읽는다.
    /// </summary>
    public class WindowEscapeInput : MonoBehaviour
    {
        [Tooltip("닫을 창이 없을 때 ESC로 열 창(노트 UI). 비우면 닫기 전용으로 동작한다")]
        [SerializeField] private UIWindowBase m_toggleWindow;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private bool m_prevEscapeDown;
#endif

        private void Update()
        {
            if (WasEscapePressed() == false)
            {
                return;
            }

            if (UIManager.RequestCloseTopWindow())
            {
                return;   // 창별 '닫기 입력 허용' 플래그를 존중한다(보호 창은 건너뜀)
            }

            if (m_toggleWindow == null)
            {
                return;
            }

            // 닫을 것이 없었다 = 노트가 닫혀 있거나 보호 창이다. 어느 쪽이든 토글로 처리한다.
            if (m_toggleWindow.IsShown)
            {
                m_toggleWindow.Hide();
            }
            else
            {
                m_toggleWindow.Show();
            }
        }

        /// <summary>이번 프레임 ESC '눌림(엣지)'인지.</summary>
        private bool WasEscapePressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                return true;
            }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // Unity 입력이 오지 않는 경우(클릭관통 창)를 위한 보완 경로.
            bool down = (Win32Native.GetAsyncKeyState(Win32Native.VK_ESCAPE) & 0x8000) != 0;
            bool edge = down && m_prevEscapeDown == false;
            m_prevEscapeDown = down;

            // 우리 창이 활성이 아니면 무시한다 — 다른 앱에서 누른 ESC까지 먹으면 안 된다.
            return edge && Win32Native.GetActiveWindow() != IntPtr.Zero;
#else
            return false;
#endif
        }
    }
}
