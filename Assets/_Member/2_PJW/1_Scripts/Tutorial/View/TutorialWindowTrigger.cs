using DesktopCompanion.Systems;
using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 지정한 창이 열릴 때 튜토리얼 스텝을 1회 발동시킨다(메뉴 설명용).
    /// 창의 활성 상태가 아니라 UIManager의 창 열림 알림을 쓰므로 CanvasGroup 개폐 창에서도 동작한다.
    /// 대상 창을 인스펙터로 지목하므로 이 컴포넌트를 창 오브젝트에 붙일 필요는 없다.
    /// </summary>
    public class TutorialWindowTrigger : UIViewBase
    {
        [Tooltip("이 창이 열릴 때 도움말을 띄운다")]
        [SerializeField] private UIWindowBase m_window;

        [Tooltip("띄울 도움말 스텝")]
        [SerializeField] private TutorialStep m_step = TutorialStep.None;

        private TutorialSystem m_tutorialSystem;

        public override void Bind()
        {
            m_tutorialSystem = SystemManager.GetSystem<TutorialSystem>();

            if (m_window == null)
            {
                Debug.LogWarning($"[TutorialWindowTrigger] 대상 창 미할당 — step={m_step}", this);
                return;
            }

            UIManager.OnWindowShown += HandleWindowShown;
        }

        public override void Unbind()
        {
            UIManager.OnWindowShown -= HandleWindowShown;
            m_tutorialSystem = null;
        }

        private void HandleWindowShown(UIWindowBase window)
        {
            if (window != m_window)
            {
                return;
            }

            m_tutorialSystem?.TryTrigger(m_step);
        }
    }
}
