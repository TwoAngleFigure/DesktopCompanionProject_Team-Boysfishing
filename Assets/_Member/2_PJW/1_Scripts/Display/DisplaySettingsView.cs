using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 화면 옵션(Full/Window · Scale · 모니터) 설정 UI.
    /// 표시 설정은 게임 System과 무관하므로 System-바인딩 VM 없이 DisplayModeController를 직접 구동한다.
    /// UIViewBase를 상속해 UI 프레임워크 수명(자가 등록·Bind)에 올라탄다(전체화면 UI 캔버스 배치).
    /// </summary>
    public class DisplaySettingsView : UIViewBase
    {
        [Header("Controller")]
        [SerializeField] private DisplayModeController m_controller;

        [Header("Mode")]
        [SerializeField] private Button m_fullButton;
        [SerializeField] private Button m_windowButton;

        [Header("Scale")]
        [SerializeField] private Button m_scaleX1Button;
        [SerializeField] private Button m_scaleX1_5Button;
        [SerializeField] private Button m_scaleX2Button;

        [Header("Monitor")]
        [SerializeField] private Button m_monitorPrevButton;
        [SerializeField] private Button m_monitorNextButton;
        [SerializeField] private TMP_Text m_monitorLabel;

        [Header("Window Move")]
        [SerializeField] private Button m_moveButton;
        [SerializeField] private TMP_Text m_moveLabel;

        public override void Bind()
        {
            if (m_controller == null)
            {
                Debug.LogWarning("[DisplaySettingsView] DisplayModeController 미할당");
                return;
            }

            if (m_fullButton != null) m_fullButton.onClick.AddListener(() => { m_controller.SetMode(ScreenMode.Full); RefreshLabel(); });
            if (m_windowButton != null) m_windowButton.onClick.AddListener(() => { m_controller.SetMode(ScreenMode.Window); RefreshLabel(); });

            if (m_scaleX1Button != null) m_scaleX1Button.onClick.AddListener(() => m_controller.SetScale(ViewScale.X1));
            if (m_scaleX1_5Button != null) m_scaleX1_5Button.onClick.AddListener(() => m_controller.SetScale(ViewScale.X1_5));
            if (m_scaleX2Button != null) m_scaleX2Button.onClick.AddListener(() => m_controller.SetScale(ViewScale.X2));

            if (m_monitorPrevButton != null) m_monitorPrevButton.onClick.AddListener(() => ChangeMonitor(-1));
            if (m_monitorNextButton != null) m_monitorNextButton.onClick.AddListener(() => ChangeMonitor(+1));

            if (m_moveButton != null) m_moveButton.onClick.AddListener(() => { m_controller.ToggleWindowMoveMode(); RefreshLabel(); });

            RefreshLabel();
        }

        public override void Unbind()
        {
            m_fullButton?.onClick.RemoveAllListeners();
            m_windowButton?.onClick.RemoveAllListeners();
            m_scaleX1Button?.onClick.RemoveAllListeners();
            m_scaleX1_5Button?.onClick.RemoveAllListeners();
            m_scaleX2Button?.onClick.RemoveAllListeners();
            m_monitorPrevButton?.onClick.RemoveAllListeners();
            m_monitorNextButton?.onClick.RemoveAllListeners();
            m_moveButton?.onClick.RemoveAllListeners();
        }

        private void ChangeMonitor(int delta)
        {
            int count = Mathf.Max(1, m_controller.MonitorCount);
            int next = (m_controller.MonitorIndex + delta % count + count) % count;
            m_controller.SetMonitor(next);
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (m_monitorLabel != null)
            {
                m_monitorLabel.text = $"Monitor {m_controller.MonitorIndex + 1} / {Mathf.Max(1, m_controller.MonitorCount)}";
            }
            if (m_moveLabel != null)
            {
                m_moveLabel.text = m_controller.WindowMoveMode ? "이동 모드: ON" : "이동 모드: OFF";
            }
        }
    }
}
