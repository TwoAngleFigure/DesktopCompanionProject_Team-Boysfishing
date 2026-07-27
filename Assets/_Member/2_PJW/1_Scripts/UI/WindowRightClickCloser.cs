using UnityEngine;
using UnityEngine.InputSystem;
using DesktopCompanion;   // Win32Native, TransparentWindow

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 지정 범위(Canvas RectTransform) 안에서 우클릭 시 최상단(최근) 창을 닫는다(LIFO).
    /// 단, UIWindowBase의 '닫기 입력 허용'을 끈 보호 창은 건너뛰고 그 아래 창을 닫는다(ESC 등 다른 닫기 수단과 동일 규칙).
    /// 오버레이 빌드는 Mouse.current가 갱신되지 않으므로 클릭관통과 동일한 Win32 전역 입력을 쓴다
    /// (GetCursorPos + GetAsyncKeyState(VK_RBUTTON) + ScreenToClient(Hwnd)). 에디터는 Mouse.current.
    /// </summary>
    public class WindowRightClickCloser : MonoBehaviour
    {
        [Tooltip("우클릭 닫기를 감지할 범위(Canvas RectTransform). 이 영역 안이면 커서 아래가 무엇이든 닫는다.")]
        [SerializeField] private RectTransform m_range;

        [Tooltip("Screen Space-Overlay면 비워둔다(null). Screen Space-Camera면 캔버스 카메라를 지정.")]
        [SerializeField] private Camera m_uiCamera;

        // m_window는 Windows 빌드 블록에서만 읽히므로 에디터에서 CS0414 경고가 뜬다(빌드에선 사용됨). 억제.
#pragma warning disable CS0414
        [Tooltip("빌드에서 커서 좌표 매핑용(비우면 자동 탐색).")]
        [SerializeField] private TransparentWindow m_window;
#pragma warning restore CS0414

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private bool m_prevRightDown;
#endif

        private void Awake()
        {
            if (m_window == null)
            {
                m_window = FindAnyObjectByType<TransparentWindow>();
            }
        }

        private void Update()
        {
            if (!TryGetRightClickDown(out Vector2 screenPos))
            {
                return;
            }
            // 범위 미지정이면 화면 전체 허용. 지정 시 그 rect 안에서만.
            if (m_range != null &&
                !RectTransformUtility.RectangleContainsScreenPoint(m_range, screenPos, m_uiCamera))
            {
                return;
            }
            UIManager.RequestCloseTopWindow();   // 창별 '닫기 입력 허용' 플래그를 존중(보호 창은 건너뜀)
        }

        // 이번 프레임 우클릭 '눌림(엣지)' + 커서 스크린 좌표를 얻는다.
        private bool TryGetRightClickDown(out Vector2 screenPos)
        {
            screenPos = default;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            bool down = (Win32Native.GetAsyncKeyState(Win32Native.VK_RBUTTON) & 0x8000) != 0;
            bool edge = down && !m_prevRightDown;
            m_prevRightDown = down;
            if (!edge || m_window == null || !m_window.IsReady)
            {
                return false;
            }
            if (!Win32Native.GetCursorPos(out Win32Native.POINT p))
            {
                return false;
            }
            Win32Native.ScreenToClient(m_window.Hwnd, ref p);          // 가상데스크톱 → 창 클라이언트(y 아래)
            screenPos = new Vector2(p.x, Screen.height - p.y);          // 클라이언트 → Unity 화면(y 위)
            return true;
#else
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame)
            {
                return false;
            }
            screenPos = mouse.position.ReadValue();
            return true;
#endif
        }
    }
}
