using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 직교 카메라의 로컬 오프셋을 화면비(aspect)에 비례해 재계산하여,
    /// 부모(배)가 항상 화면상 같은 정규화 좌표에 보이도록 고정한다(계획 19).
    ///
    /// 배경: orthographicSize는 세로 절반만 고정하고 가로는 aspect 파생값이라,
    /// 로컬 오프셋이 상수면 해상도(모니터) 변경 시 배가 화면 밖으로 밀려난다.
    /// 계산은 무상태(현재 aspect만 사용) — 이전 해상도·전환 경로와 무관하게 항상 같은 결과.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraAspectAnchor : MonoBehaviour
    {
        [Header("부모(배)의 목표 화면 위치 (정규화: 중앙 0, 우/상 +1)")]
        [Tooltip("에디터에서 카메라를 원하는 배치로 두고 컨텍스트 메뉴 '현재 배치에서 정규화 좌표 역산'으로 채울 수 있다.")]
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

        /// <summary>현재 렌더 타깃 크기 기준으로 로컬 오프셋을 즉시 재계산한다(멱등).</summary>
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

            Vector3 localPos = transform.localPosition;
            localPos.x = -_normalizedX * halfWidth + _worldOffset.x;
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
        /// [에디터] 현재 씬의 카메라 배치를 역산해 정규화 좌표를 채운다.
        /// 기존 배치를 그대로 기준값으로 삼을 때 사용.
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
            _normalizedX = -(transform.localPosition.x - _worldOffset.x) / halfWidth;
            _normalizedY = -(transform.localPosition.y - _worldOffset.y) / size;
            Debug.Log($"[CameraAspectAnchor] 역산 완료: Nx={_normalizedX:F3}, Ny={_normalizedY:F3} (aspect={aspect:F3}, target={width}x{height})");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
