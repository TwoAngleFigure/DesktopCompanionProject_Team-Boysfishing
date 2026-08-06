using UnityEngine;

namespace DesktopCompanion.Views
{
    public class WorldProp : MonoBehaviour
    {
        [Header("소멸 설정")]
        [Tooltip("카메라 우측 화면 밖으로 얼마나 멀어지면 파괴될지 (여유 공간)")]
        public float m_destroyMargin = 5f;

        private Camera m_mainCamera;
        private bool m_isInitialized = false;

        public void Init()
        {
            m_mainCamera = Camera.main;
            m_isInitialized = true;
            Debug.Log($"[WorldProp] {gameObject.name} 생명주기 시작!");
        }

        private void Update()
        {
            if (!m_isInitialized || m_mainCamera == null) return;

            float halfHeight = m_mainCamera.orthographicSize;
            float halfWidth = halfHeight * m_mainCamera.aspect;

            float cameraRightEdgeX = m_mainCamera.transform.position.x + halfWidth;

            if (transform.position.x > cameraRightEdgeX + m_destroyMargin)
            {
                Debug.Log($"[WorldProp] {gameObject.name} 카메라 뒤로 멀어짐 -> 스스로 파괴");
                Destroy(gameObject);
            }
        }
    }
}