using UnityEngine;
using DesktopCompanion.Rendering;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 직교 카메라의 로컬 오프셋을 재계산해 부모 오브젝트의 화면 위치를 고정한다.
    ///  - 가로: 화면 우측 끝을 기준으로 삼는다. 우측 여백을 기준 화면비에서 산출해 고정하므로,
    ///          화면비가 넓어지면 늘어난 폭이 전부 좌측으로 간다.
    ///  - 세로: 정규화 위치 × orthographicSize로 산출한다.
    /// 렌더 타깃 크기 변화를 LateUpdate에서 감시해 갱신하며, 계산은 현재 크기만 사용하는 무상태 연산이다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraAspectAnchor : MonoBehaviour
    {
        [Header("부모(배)의 목표 화면 위치 (기준 화면비 16:9 기준, 중앙 0 · 우/상 +1)")]
        [Tooltip("가로는 여기서 산출한 '우측 여백'이 모든 화면비에서 유지된다. " +
                 "에디터에서 카메라를 원하는 배치로 두고 컨텍스트 메뉴로 역산할 수 있다.")]
        [SerializeField, Range(-1f, 1f)] private float _normalizedX = 0.5f;
        [SerializeField, Range(-1f, 1f)] private float _normalizedY = -0.5f;

        [Header("추가 오프셋 (월드 유닛)")]
        [Tooltip("정규화 계산 결과에 더해지는 고정 오프셋. 해상도와 무관하게 일정한 미세 조정용.")]
        [SerializeField] private Vector2 _worldOffset = Vector2.zero;

        private Camera _camera;
        private int _lastWidth = -1;
        private int _lastHeight = -1;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void OnValidate()
        {
            // 인스펙터에서 값(정규화 좌표·오프셋)을 바꾸면 해상도가 그대로여도 다음 LateUpdate에서 재적용되게 한다.
            _lastWidth = -1;
            _lastHeight = -1;
        }

        private void LateUpdate()
        {
            // ※ Camera.aspect는 카메라가 실제 렌더링할 때 지연 갱신되는 파생값이라 폴링 기준으로 부적합
            //   (에디터에서 카메라를 선택해 Camera Preview가 렌더를 강제해야만 갱신되는 현상의 원인).
            //   원본인 렌더 타깃 크기를 직접 감시한다.
            GetTargetSize(out int width, out int height);
            if (width == _lastWidth && height == _lastHeight)
            {
                return;
            }
            Apply();
        }

        /// <summary>현재 렌더 타깃 크기를 기준으로 로컬 오프셋을 재계산해 적용한다. 반복 호출해도 결과가 같다.</summary>
        public void Apply()
        {
            GetTargetSize(out int width, out int height);
            if (width <= 0 || height <= 0)
            {
                return;
            }

            float size = _camera.orthographicSize;
            float aspect = (float)width / height;   // Camera.aspect 대신 직접 계산(지연 갱신 회피)
            float halfWidth = size * aspect;

            // 가로는 화면 우측 끝을 기준으로 고정한다. 정규화 위치로 잡으면 화면비가 넓어질 때
            // 좌우가 함께 늘어나 우측 여백이 벌어진다.
            float rightMargin = (1f - _normalizedX) * size * PixelGridDesign.ReferenceAspect;

            Vector3 localPos = transform.localPosition;
            localPos.x = rightMargin - halfWidth + _worldOffset.x;
            localPos.y = -_normalizedY * size + _worldOffset.y;   // aspect 무관 — 초기 1회 이후 불변
            transform.localPosition = localPos;

            _lastWidth = width;
            _lastHeight = height;
        }

        // 렌더 타깃(RT가 있으면 RT, 없으면 화면)의 픽셀 크기. aspect의 원본 소스.
        private void GetTargetSize(out int width, out int height)
        {
            RenderTexture rt = _camera.targetTexture;
            if (rt != null)
            {
                width = rt.width;
                height = rt.height;
            }
            else
            {
                width = Screen.width;
                height = Screen.height;
            }
        }

        /// <summary>
        /// 현재 카메라 배치로부터 정규화 좌표를 역산해 채운다. 역산 후 Apply 결과가 현재 배치와 일치한다.
        /// </summary>
        [ContextMenu("현재 배치에서 정규화 좌표 역산")]
        private void CaptureFromCurrentPlacement()
        {
            _camera = GetComponent<Camera>();
            GetTargetSize(out int width, out int height);

            float size = _camera.orthographicSize;
            float aspect = height > 0 ? (float)width / height : 0f;
            float halfWidth = size * aspect;
            if (halfWidth <= 0f)
            {
                Debug.LogError("[CameraAspectAnchor] 역산 실패 — orthographicSize/렌더 타깃 크기가 유효하지 않습니다.");
                return;
            }

            // 오프셋 몫을 제외한 나머지를 정규화 좌표로 환산한다(역산 후 Apply 결과가 현재 배치와 일치하도록).
            // 가로는 우측 여백 → 기준 화면비 기준 정규화 순으로 되돌린다. 어떤 화면비에서 실행해도 같은 값이 나온다.
            float rightMargin = (transform.localPosition.x - _worldOffset.x) + halfWidth;
            _normalizedX = 1f - rightMargin / (size * PixelGridDesign.ReferenceAspect);
            _normalizedY = -(transform.localPosition.y - _worldOffset.y) / size;
            Debug.Log($"[CameraAspectAnchor] 역산 완료: Nx={_normalizedX:F3}, Ny={_normalizedY:F3} " +
                      $"(우측 여백 {rightMargin:F2}유닛, aspect={aspect:F3}, target={width}x{height})");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
