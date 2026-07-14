using UnityEngine;

namespace DesktopCompanion.Views
{
    public class WaterTreadmill : MonoBehaviour
    {
        [Header("물 Plane 오브젝트")]
        public Transform m_planeA;
        public Transform m_planeB;

        [Header("Plane 1개의 X축 가로 길이")]
        public float m_planeWidth = 100f;

        private Transform m_cameraTransform;

        private void Start()
        {
            if (Camera.main != null)
                m_cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            if (m_cameraTransform == null) return;

            float camX = m_cameraTransform.position.x;

            if (camX < m_planeA.position.x - m_planeWidth)
            {
                m_planeA.position = new Vector3(m_planeB.position.x - m_planeWidth, m_planeA.position.y, m_planeA.position.z);
            }
            else if (camX < m_planeB.position.x - m_planeWidth)
            {
                m_planeB.position = new Vector3(m_planeA.position.x - m_planeWidth, m_planeB.position.y, m_planeB.position.z);
            }
        }
    }
}