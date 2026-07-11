using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// Window 모드 + '창 이동 모드'에서 보트 창(월드 렌더 영역)을 드래그로 옮긴다.
    ///
    /// 입력은 빌드에서 Win32 전역(GetCursorPos + GetAsyncKeyState)으로 읽는다.
    /// 클릭관통(WS_EX_TRANSPARENT) 오버레이는 Unity의 Mouse.current가 갱신되지 않으므로
    /// 그것으로는 빌드에서 드래그가 동작하지 않는다. 에디터에서는 Mouse.current로 폴백한다.
    /// </summary>
    public class WorldRegionDragHandler : MonoBehaviour
    {
        [SerializeField] private DisplayModeController m_controller;
        [SerializeField] private TransparentWindow m_window;   // 전역 커서 → 클라이언트 좌표 변환용(빌드)

        private bool m_dragging;
        private Vector2 m_lastPos;

        private void Update()
        {
            if (m_controller == null ||
                m_controller.Mode != ScreenMode.Window ||
                m_controller.WindowMoveMode == false)   // 옵션의 '창 이동' 버튼으로 켜야 드래그 가능
            {
                m_dragging = false;
                return;
            }

            if (TryGetCursorPos(out Vector2 pos) == false)
            {
                return;
            }
            bool down = IsLeftButtonDown();

            if (down)
            {
                if (m_dragging == false)
                {
                    if (m_controller.WindowRectPixels().Contains(pos))
                    {
                        m_dragging = true;
                        m_lastPos = pos;
                    }
                }
                else
                {
                    Vector2 delta = pos - m_lastPos;
                    m_lastPos = pos;
                    m_controller.MoveWindowRect(delta);   // 픽셀 델타(RawImage anchoredPosition 이동)
                }
            }
            else if (m_dragging)
            {
                m_dragging = false;
                m_controller.EndDragSave();
            }
        }

        private bool IsLeftButtonDown()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return (Win32Native.GetAsyncKeyState(Win32Native.VK_LBUTTON) & 0x8000) != 0;
#else
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.leftButton.isPressed;
#endif
        }

        private bool TryGetCursorPos(out Vector2 pos)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            pos = default;
            if (m_window == null || Win32Native.GetCursorPos(out Win32Native.POINT p) == false)
            {
                return false;
            }
            Win32Native.ScreenToClient(m_window.Hwnd, ref p);
            pos = new Vector2(p.x, Screen.height - p.y);
            return true;
#else
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m == null) { pos = default; return false; }
            pos = m.position.ReadValue();
            return true;
#endif
        }
    }
}
