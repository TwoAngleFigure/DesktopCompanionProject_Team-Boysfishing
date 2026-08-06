using UnityEngine;
using UnityEngine.InputSystem;

namespace DesktopCompanion.Views
{
    public class MapZoomController : MonoBehaviour
    {
        [Header("줌 대상 (지도의 섬들이 들어있는 컨테이너)")]
        public RectTransform m_mapContainer;

        [Header("줌 설정")]
        public float m_zoomSpeed = 0.15f;
        public float m_minZoom = 0.5f;
        public float m_maxZoom = 2.0f;

        [Header("부드러운 효과 설정")]
        [Range(1f, 20f)]
        public float m_smoothSpeed = 10f;

        private float m_targetScale = 1f;

        private void Start()
        {
            if (m_mapContainer != null)
            {
                m_targetScale = m_mapContainer.localScale.x;
            }
        }

        private void Update()
        {
            if (Mouse.current == null || m_mapContainer == null) return;

            float scrollY = Mouse.current.scroll.ReadValue().y;

            if (scrollY != 0f)
            {
                float scrollDirection = Mathf.Sign(scrollY);

                m_targetScale += scrollDirection * m_zoomSpeed;
                m_targetScale = Mathf.Clamp(m_targetScale, m_minZoom, m_maxZoom);
            }

            float currentScaleX = m_mapContainer.localScale.x;

            float smoothedScale = Mathf.Lerp(currentScaleX, m_targetScale, Time.deltaTime * m_smoothSpeed);

            m_mapContainer.localScale = new Vector3(smoothedScale, smoothedScale, 1f);
        }
    }
}