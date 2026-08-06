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

            float parallaxFactor = 0f;
            StageBlueprint blueprint = GetComponentInParent<StageBlueprint>();
            if (blueprint != null && transform.parent != null)
            {
                if (transform.parent == blueprint.m_farLayer) parallaxFactor = blueprint.m_farParallax;
                else if (transform.parent == blueprint.m_midLayer) parallaxFactor = blueprint.m_midParallax;
                else if (transform.parent == blueprint.m_nearLayer) parallaxFactor = blueprint.m_nearParallax;
            }

            float effectiveFactor = Mathf.Max(1f - parallaxFactor, 0.15f);
            float rawRelativeOffset = transform.position.x - m_mainCamera.transform.position.x;
            float correctedOffset = rawRelativeOffset * effectiveFactor;

            if (correctedOffset > halfWidth + m_destroyMargin)
            {
                Debug.Log($"[WorldProp] {gameObject.name} 카메라 우측 뒤로 완전히 지나침 -> 스스로 파괴");
                Destroy(gameObject);
            }
        }

    }
}