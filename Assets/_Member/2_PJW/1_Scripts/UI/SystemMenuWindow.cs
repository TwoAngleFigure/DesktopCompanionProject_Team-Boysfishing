using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 설정 창들을 탭으로 묶는 컨테이너에 시스템 동작(게임 종료)을 더한 창.
    /// 탭 전환은 <see cref="UITabWindow"/>가 그대로 담당하고, 이 클래스는 어느 탭에도 속하지 않는
    /// 공통 버튼만 얹는다. 설정 항목이 늘어도 개별 설정 창(DisplaySettingsView 등)은 자기 항목만 알면 된다.
    /// </summary>
    public class SystemMenuWindow : UITabWindow
    {
        [Header("System")]
        [Tooltip("게임 종료 버튼. 세이브는 GameManager.OnApplicationQuit이 자동으로 수행한다")]
        [SerializeField] private Button m_quitButton;

        public override void Bind()
        {
            base.Bind();

            if (m_quitButton != null)
            {
                m_quitButton.onClick.AddListener(QuitGame);
            }
        }

        public override void Unbind()
        {
            // 인스펙터에 걸린 다른 리스너를 남기기 위해 이 창이 건 것만 걷어낸다(UITabWindow와 같은 규칙).
            if (m_quitButton != null)
            {
                m_quitButton.onClick.RemoveListener(QuitGame);
            }

            base.Unbind();
        }

        /// <summary>
        /// 게임을 종료한다. 세이브는 GameManager.OnApplicationQuit이 자동으로 수행하므로 여기서 따로 부르지 않는다.
        /// 에디터에서는 Application.Quit이 무시되므로 플레이 모드를 끈다(같은 콜백이 불려 저장도 이뤄진다).
        /// </summary>
        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
