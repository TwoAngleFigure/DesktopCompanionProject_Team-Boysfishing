using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 애플리케이션 프레임레이트 상한을 적용한다. 창이 포커스를 잃으면 유휴 상한으로 낮춘다.
    /// targetFrameRate는 VSync가 꺼져 있어야 적용되므로 vSyncCount=0을 함께 설정한다.
    /// </summary>
    public class FrameRateController : MonoBehaviour
    {
        [Tooltip("포커스 상태(활성) 프레임 상한")]
        [SerializeField, Min(1)] private int m_activeFrameRate = 30;

        [Tooltip("포커스 아웃(유휴) 시 프레임 상한 — 더 낮춰 절전")]
        [SerializeField, Min(1)] private int m_idleFrameRate = 15;

        [Tooltip("창이 포커스 아웃되면 유휴 상한으로 낮출지")]
        [SerializeField] private bool m_lowerWhenUnfocused = true;

        private void Start()
        {
            QualitySettings.vSyncCount = 0;   // targetFrameRate가 적용되도록 VSync 해제
            Apply(m_activeFrameRate);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (m_lowerWhenUnfocused)
            {
                Apply(focused ? m_activeFrameRate : m_idleFrameRate);
            }
        }

        private void Apply(int fps)
        {
            Application.targetFrameRate = Mathf.Max(1, fps);
        }

#if UNITY_EDITOR
        // 인스펙터에서 값을 바꾸면 Play 중 즉시 반영(튜닝 편의).
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                QualitySettings.vSyncCount = 0;
                Apply(m_activeFrameRate);
            }
        }
#endif
    }
}
