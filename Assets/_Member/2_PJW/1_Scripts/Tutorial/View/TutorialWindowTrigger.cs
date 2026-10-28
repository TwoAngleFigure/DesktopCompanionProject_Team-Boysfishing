using System.Collections.Generic;
using DesktopCompanion.Systems;
using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 지정한 창이 열릴 때 도움말 항목을 1회 해금한다(메뉴 설명용).
    /// 창의 활성 상태가 아니라 UIManager의 창 열림 알림을 쓰므로 CanvasGroup 개폐 창에서도 동작한다.
    /// 대상 창을 인스펙터로 지목하므로 이 컴포넌트를 창 오브젝트에 붙일 필요는 없다.
    ///
    /// 항목을 여럿 적으면 <b>적은 순서대로</b> 팝업이 이어진다.
    /// 인벤토리와 플레이어 정보처럼 창이 함께 열리는 경우, 한쪽 창에만 이 컴포넌트를 두고
    /// 두 항목을 순서대로 적는다. 창마다 따로 달면 실제 열린 순서에 따라 순서가 뒤집힐 수 있다.
    /// </summary>
    public class TutorialWindowTrigger : UIViewBase
    {
        [Tooltip("이 창이 열릴 때 도움말을 해금한다")]
        [SerializeField] private UIWindowBase m_window;

        [Tooltip("해금할 도움말 항목. 적은 순서가 곧 팝업 표시 순서다")]
        [SerializeField] private List<HelpTopic> m_topics = new();

        private TutorialSystem m_tutorialSystem;

        public override void Bind()
        {
            m_tutorialSystem = SystemManager.GetSystem<TutorialSystem>();

            if (m_window == null)
            {
                Debug.LogWarning($"[TutorialWindowTrigger] 대상 창 미할당 — topics={m_topics.Count}", this);
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

            m_tutorialSystem?.UnlockRange(m_topics);
        }
    }
}
