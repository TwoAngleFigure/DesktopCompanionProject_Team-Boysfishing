using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// [프로토타입] RT 크롭 전환(계획 06)의 최대 리스크 검증용.
    /// 목적: Camera를 '투명 RenderTexture'에 렌더하고 전체화면 RawImage로 표시했을 때,
    ///       URP에서 per-pixel 알파가 보존되어 데스크톱 비침(투명 합성)이 되는지 확인한다.
    ///
    /// 범위: 오직 "투명 RT → 화면 표시"만. 모드/스케일/드래그/클릭관통/모니터는 포함하지 않는다.
    /// 테스트 시: 이 씬에서 Camera Swap(DisplayModeController·Camera B·Clear)은 비활성화하고,
    ///           월드 카메라(A) 1대 + 전체화면 RawImage만 남긴 뒤 이 컴포넌트를 붙인다.
    /// </summary>
    public class RtCropPrototype : MonoBehaviour
    {
        [Tooltip("씬을 렌더할 월드 카메라(예: 기존 Camera A)")]
        [SerializeField] private Camera m_worldCamera;

        [Tooltip("전체화면 캔버스(Screen Space Overlay, 배경 없음)의 RawImage")]
        [SerializeField] private RawImage m_worldImage;

        [Tooltip("화면(백버퍼)을 매 프레임 투명 클리어하는 카메라. 없으면 잔상 발생. " +
                 "Camera A는 RT에 렌더하므로 화면을 지우는 카메라가 별도로 필요하다.")]
        [SerializeField] private Camera m_screenClearCamera;

        [Tooltip("RT 슈퍼샘플 배율(선명도 검증용). 1이면 화면 해상도")]
        [SerializeField, Min(1)] private int m_superSample = 1;

        private RenderTexture m_rt;

        private void Start()
        {
            if (m_worldCamera == null || m_worldImage == null)
            {
                Debug.LogError("[RtCropPrototype] m_worldCamera / m_worldImage 미할당");
                return;
            }

            CreateRT();

            // 투명 클리어: 지오메트리 없는 곳의 알파가 0이 되도록.
            m_worldCamera.clearFlags = CameraClearFlags.SolidColor;
            m_worldCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            m_worldCamera.targetTexture = m_rt;

            // 화면 클리어 카메라: 백버퍼를 매 프레임 투명으로 지워 잔상 방지(RT 방식 필수).
            // 지오메트리를 그리지 않으므로(cull Nothing) 물/SW3에 관여하지 않는다.
            if (m_screenClearCamera != null)
            {
                m_screenClearCamera.targetTexture = null;            // 화면에 렌더
                m_screenClearCamera.cullingMask = 0;                 // Nothing
                m_screenClearCamera.clearFlags = CameraClearFlags.SolidColor;
                m_screenClearCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                m_screenClearCamera.depth = -100;                    // 가장 먼저
                m_screenClearCamera.enabled = true;
            }
            else
            {
                Debug.LogWarning("[RtCropPrototype] 화면 클리어 카메라 미할당 — 투명 영역에 잔상이 남습니다.");
            }

            // RawImage로 RT 표시(알파 유지). 전체화면·uv 전체.
            m_worldImage.texture = m_rt;
            m_worldImage.color = Color.white;
            m_worldImage.uvRect = new Rect(0f, 0f, 1f, 1f);

            Debug.Log($"[RtCropPrototype] RT {m_rt.width}x{m_rt.height} ARGB32 → {m_worldCamera.name} → RawImage. " +
                      "빌드에서 데스크톱이 투명 영역으로 비치는지 확인하세요.");
        }

        private void CreateRT()
        {
            int w = Mathf.Max(1, Screen.width * m_superSample);
            int h = Mathf.Max(1, Screen.height * m_superSample);

            if (m_rt != null)
            {
                m_worldCamera.targetTexture = null;
                m_rt.Release();
                Destroy(m_rt);
            }
            m_rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                name = "RtCropPrototype_RT",
            };
            m_rt.Create();
        }

        private void OnDestroy()
        {
            if (m_worldCamera != null)
            {
                m_worldCamera.targetTexture = null;
            }
            if (m_rt != null)
            {
                m_rt.Release();
                Destroy(m_rt);
            }
        }
    }
}
