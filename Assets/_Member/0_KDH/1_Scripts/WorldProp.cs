using UnityEngine;

namespace DesktopCompanion.Views
{
    public class WorldProp : MonoBehaviour
    {
        [Header("소멸 설정")]
        [Tooltip("카메라 우측 화면 밖으로 얼마나 멀어지면 파괴될지 (여유 공간)")]
        public float m_destroyMargin = 50f;

        private Camera m_mainCamera;
        private bool m_isInitialized = false;

        public void Init()
        {
            m_mainCamera = Camera.main;
            m_isInitialized = true;
            Debug.Log($"[WorldProp] 🟢 '{gameObject.name}' 생성 및 초기화 완료! (시작 월드X: {transform.position.x:F2})");
        }

        private void Update()
        {
            if (!m_isInitialized || m_mainCamera == null) return;

            float halfHeight = m_mainCamera.orthographicSize;
            float halfWidth = halfHeight * m_mainCamera.aspect;

            float cameraRightEdgeX = m_mainCamera.transform.position.x + halfWidth;

            // 🎯 배가 왼쪽(-X)으로 이동하므로, 지나쳐서 카메라 우측 뒤로 멀어진 오브젝트만 파괴
            if (transform.position.x > cameraRightEdgeX + m_destroyMargin)
            {
                Debug.Log($"[WorldProp] 🧹 '{gameObject.name}' 카메라 뒤(우측)로 멀어짐 (오브젝트X: {transform.position.x:F2}, 카메라X: {m_mainCamera.transform.position.x:F2}) -> 파괴 실행");
                Destroy(gameObject);
            }
        }
    }
}