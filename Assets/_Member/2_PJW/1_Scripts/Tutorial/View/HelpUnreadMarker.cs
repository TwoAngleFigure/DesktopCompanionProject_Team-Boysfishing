using DesktopCompanion.Systems;
using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 안 읽은 도움말이 있음을 알리는 표시. 도감을 여는 메뉴 버튼에 붙인다.
    /// 표시만 담당하고 창을 열지는 않는다 — 여는 것은 기존 창 토글 버튼의 몫이다.
    ///
    /// 항상 활성인 오브젝트에 붙여야 한다. 꺼져 있으면 Bind가 풀려 상태 변화를 받지 못한다.
    /// </summary>
    public class HelpUnreadMarker : UIViewBase
    {
        [Tooltip("미열람이 있을 때 켤 표시 오브젝트(점·느낌표 등)")]
        [SerializeField] private GameObject m_markerRoot;

        private TutorialSystem m_tutorialSystem;

        public override void Bind()
        {
            m_tutorialSystem = SystemManager.GetSystem<TutorialSystem>();

            if (m_tutorialSystem == null)
            {
                SetVisible(false);
                return;
            }

            m_tutorialSystem.OnUnreadChanged += SetVisible;
            SetVisible(m_tutorialSystem.HasUnread);   // 복원된 상태를 반영한다
        }

        public override void Unbind()
        {
            if (m_tutorialSystem != null)
            {
                m_tutorialSystem.OnUnreadChanged -= SetVisible;
                m_tutorialSystem = null;
            }
        }

        private void SetVisible(bool visible)
        {
            if (m_markerRoot != null)
            {
                m_markerRoot.SetActive(visible);
            }
        }
    }
}
