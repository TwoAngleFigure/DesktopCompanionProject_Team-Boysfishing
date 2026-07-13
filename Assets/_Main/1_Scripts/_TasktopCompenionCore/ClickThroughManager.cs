using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DesktopCompanion
{
    /// <summary>
    /// 커서가 상호작용 대상(UI 또는 클릭 가능한 월드 오브젝트) 위인지 판정해 클릭 관통을 동적으로 토글한다.
    /// 대상 위 = 클릭 가능(관통 해제), 그 외(빈/투명 영역) = 관통(데스크톱 조작).
    ///
    /// [핵심] 커서 위치는 반드시 Win32 GetCursorPos(전역 커서)로 읽는다.
    /// 클릭관통(WS_EX_TRANSPARENT) 창은 마우스 메시지를 받지 못해 Unity의 Mouse.current.position이
    /// 갱신되지 않는다(첫 프레임부터 관통 상태이므로 stale). 그 값으로 판정하면 콘텐츠 위를 영영 감지하지
    /// 못해 관통이 절대 해제되지 않는 교착이 생긴다(UI·오브젝트 모두 클릭 불가). GetCursorPos는 관통과
    /// 무관하게 유효하다. UI 판정도 같은 이유로 IsPointerOverGameObject(Mouse.current 의존) 대신
    /// 해당 좌표로 GraphicRaycaster를 직접 돌린다.
    /// </summary>
    [RequireComponent(typeof(TransparentWindow))]
    public class ClickThroughManager : MonoBehaviour
    {
        // 아래 SerializeField는 Windows Standalone 빌드 블록에서만 읽히므로 에디터 컴파일 경고를 억제한다.
#pragma warning disable CS0414, CS0649
        [Tooltip("월드 오브젝트 히트테스트에 쓸 카메라(미지정 시 Camera.main)")]
        [SerializeField] private Camera _worldCamera;

        [Tooltip("클릭을 막을 오브젝트 레이어. 바다 등 관통시킬 대상은 제외할 것.")]
        [SerializeField] private LayerMask _clickableMask = ~0;

        private TransparentWindow _window;
        private bool _currentClickThrough = true;
#pragma warning restore CS0414, CS0649

        /// <summary>true면 판정과 무관하게 관통을 항상 해제한다(예: 창 이동 모드 동안 전체 오버레이가 입력을 잡도록).</summary>
        public bool ForceNoClickThrough { get; set; }

        /// <summary>
        /// 커서 스크린 좌표를 월드 카메라 좌표로 매핑한다(RT 크롭: RawImage uv → RT 픽셀).
        /// null이면 커서 좌표를 그대로 사용. 반환이 null이면 커서가 월드 표시 영역(창) 밖이다.
        /// </summary>
        public System.Func<Vector2, Vector2?> ScreenToWorldCameraPoint;

        private void Awake()
        {
            _window = GetComponent<TransparentWindow>();
        }

        /// <summary>레이캐스트에 쓸 월드 카메라를 런타임에 갱신한다(Camera Swap 모드 전환 연동).</summary>
        public void SetWorldCamera(Camera cam)
        {
            _worldCamera = cam;
        }

        private void OnEnable()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            StartCoroutine(UpdateClickThroughLoop());
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static readonly List<RaycastResult> s_uiResults = new List<RaycastResult>();
        private PointerEventData _pointerData;

        private IEnumerator UpdateClickThroughLoop()
        {
            while (enabled)
            {
                yield return null;
                ProcessFrame();
            }
        }

        private void ProcessFrame()
        {
            if (_window.IsReady == false)
            {
                return;
            }

            try
            {
                bool interactive = ForceNoClickThrough || IsCursorOverInteractive();
                bool wantClickThrough = interactive == false; // 대상 위(또는 강제 해제)면 관통 해제
                if (wantClickThrough != _currentClickThrough)
                {
                    _window.SetClickThrough(wantClickThrough);
                    _currentClickThrough = wantClickThrough;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ClickThroughManager] 프레임 처리 예외(무시하고 계속): {e.Message}");
            }
        }

        // 관통 상태에서도 유효한 전역 커서 위치를 Unity 화면 좌표로 변환한다.
        private bool TryGetCursorScreenPos(out Vector2 pos)
        {
            pos = default;
            if (Win32Native.GetCursorPos(out Win32Native.POINT p) == false)
            {
                return false;
            }
            // 화면(가상 데스크톱) → 창 클라이언트(좌상단 원점, y 아래)
            Win32Native.ScreenToClient(_window.Hwnd, ref p);
            // 클라이언트 → Unity 화면(좌하단 원점, y 위)
            pos = new Vector2(p.x, Screen.height - p.y);
            return true;
        }

        private bool IsCursorOverInteractive()
        {
            if (TryGetCursorScreenPos(out Vector2 pos) == false)
            {
                return false;
            }
            if (pos.x < 0 || pos.y < 0 || pos.x >= Screen.width || pos.y >= Screen.height)
            {
                return false;
            }

            // 1) UI — 해당 좌표로 GraphicRaycaster 직접 레이캐스트(Mouse.current 비의존).
            if (EventSystem.current != null)
            {
                if (_pointerData == null)
                {
                    _pointerData = new PointerEventData(EventSystem.current);
                }
                _pointerData.position = pos;
                s_uiResults.Clear();
                EventSystem.current.RaycastAll(_pointerData, s_uiResults);
                if (s_uiResults.Count > 0)
                {
                    return true;
                }
            }

            // 2) 클릭 가능한 월드 오브젝트(콜라이더) — 커서를 월드 카메라(RT) 좌표로 매핑 후 레이캐스트.
            Camera cam = _worldCamera != null ? _worldCamera : Camera.main;
            if (cam != null)
            {
                Vector2 rayPos = pos;
                if (ScreenToWorldCameraPoint != null)
                {
                    Vector2? mapped = ScreenToWorldCameraPoint(pos);
                    if (mapped.HasValue == false)
                    {
                        return false;   // 월드 표시 영역(창) 밖 → 월드 히트 없음
                    }
                    rayPos = mapped.Value;
                }
                if (Physics.Raycast(cam.ScreenPointToRay(rayPos), Mathf.Infinity, _clickableMask))
                {
                    return true;
                }
            }

            return false;
        }
#endif
    }
}
