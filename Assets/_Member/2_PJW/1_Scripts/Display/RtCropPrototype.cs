using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 카메라를 투명 RenderTexture에 렌더하고 전체화면 RawImage로 표시해,
    /// per-pixel 알파가 보존되어 데스크톱이 비치는지 확인하는 프로토타입 컴포넌트.
    /// 투명 RT 생성·카메라 클리어 설정·RawImage 바인딩만 수행하며, 모드·배율·드래그·모니터는 다루지 않는다.
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

            // 화면 클리어 카메라: 백버퍼를 매 프레임 투명으로 지워 잔상을 막는다.
            // 지오메트리를 그리지 않으므로(cull Nothing) 씬 렌더에 관여하지 않는다.
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
