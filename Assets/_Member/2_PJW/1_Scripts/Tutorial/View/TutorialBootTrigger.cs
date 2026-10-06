using DesktopCompanion.Systems;
using UnityEngine;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 부팅 카메라 하강이 끝나면 도움말 항목을 1회 해금한다(첫 실행의 게임 개요용).
    /// 하강은 매 실행 재생되지만 해금이 1회성이라 두 번째 실행부터는 아무 일도 일어나지 않는다.
    ///
    /// 하강 완료가 Bind보다 먼저 올 수도 있어 상태(<see cref="BootCameraDrop.IsCompleted"/>)를 먼저 확인한다.
    /// </summary>
    public class TutorialBootTrigger : UIViewBase
    {
        [Tooltip("하강 완료 시 해금할 도움말 항목")]
        [SerializeField] private HelpTopic m_topic = HelpTopic.Overview;

        private TutorialSystem m_tutorialSystem;

        public override void Bind()
        {
            m_tutorialSystem = SystemManager.GetSystem<TutorialSystem>();

            if (m_tutorialSystem == null)
            {
                Debug.LogWarning($"[TutorialBootTrigger] TutorialSystem 미발견 — topic={m_topic}", this);
                return;
            }

            if (BootCameraDrop.IsCompleted)
            {
                HandleDropCompleted();
                return;
            }

            BootCameraDrop.OnDropCompleted += HandleDropCompleted;
        }

        public override void Unbind()
        {
            BootCameraDrop.OnDropCompleted -= HandleDropCompleted;
            m_tutorialSystem = null;
        }

        private void HandleDropCompleted() => m_tutorialSystem?.Unlock(m_topic);
    }
}
