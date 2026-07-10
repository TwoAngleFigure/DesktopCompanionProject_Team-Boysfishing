using UnityEngine;
using UnityEngine.InputSystem;

namespace DesktopCompanion.Views
{
    public class MapZoomController : MonoBehaviour
    {
        [Header("줌 대상 (지도의 섬들이 들어있는 컨테이너)")]
        public RectTransform m_mapContainer;

        [Header("줌 설정")]
        public float m_zoomSpeed = 0.1f;
        public float m_minZoom = 0.5f;
        public float m_maxZoom = 2.0f;

        private void Update()
        {
            if (Mouse.current == null) return;

            float scrollY = Mouse.current.scroll.ReadValue().y;

            if (scrollY != 0f && m_mapContainer != null)
            {
                float scrollDirection = Mathf.Sign(scrollY);

                Vector3 currentScale = m_mapContainer.localScale;

                float newScale = currentScale.x + (scrollDirection * m_zoomSpeed);

                newScale = Mathf.Clamp(newScale, m_minZoom, m_maxZoom);

                m_mapContainer.localScale = new Vector3(newScale, newScale, 1f);
            }
        }
    }
}