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
    public class DisplaySettingsView : UIWindowBase
    {
        [Header("Controller")]
        [SerializeField] private DisplayModeController m_controller;

        [Header("Mode")]
        [SerializeField] private Button m_fullButton;
        [SerializeField] private Button m_windowButton;

        [Header("Scale (Window 모드 출력 배율)")]
        [SerializeField] private Slider m_scaleSlider;
        [SerializeField] private TMP_Text m_scaleLabel;

        [Header("Scale 프리셋")]
        [SerializeField] private Button m_scale075Button;
        [SerializeField] private Button m_scale100Button;
        [SerializeField] private Button m_scale125Button;

        [Header("Crop (Window 모드에서 볼 가로 구간)")]
        [SerializeField] private Slider m_cropLeftSlider;
        [SerializeField] private Slider m_cropRightSlider;
        [SerializeField] private TMP_Text m_cropLabel;

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

            BindScaleSlider();
            BindScalePreset(m_scale075Button, 0.75f);
            BindScalePreset(m_scale100Button, 1f);
            BindScalePreset(m_scale125Button, 1.25f);
            BindCropSliders();

            if (m_monitorPrevButton != null) m_monitorPrevButton.onClick.AddListener(() => ChangeMonitor(-1));
            if (m_monitorNextButton != null) m_monitorNextButton.onClick.AddListener(() => ChangeMonitor(+1));

            if (m_moveButton != null) m_moveButton.onClick.AddListener(() => { m_controller.ToggleWindowMoveMode(); RefreshLabel(); });

            RefreshLabel();
        }

        public override void Unbind()
        {
            m_fullButton?.onClick.RemoveAllListeners();
            m_windowButton?.onClick.RemoveAllListeners();
            m_scaleSlider?.onValueChanged.RemoveAllListeners();
            m_scale075Button?.onClick.RemoveAllListeners();
            m_scale100Button?.onClick.RemoveAllListeners();
            m_scale125Button?.onClick.RemoveAllListeners();
            m_cropLeftSlider?.onValueChanged.RemoveAllListeners();
            m_cropRightSlider?.onValueChanged.RemoveAllListeners();
            m_monitorPrevButton?.onClick.RemoveAllListeners();
            m_monitorNextButton?.onClick.RemoveAllListeners();
            m_moveButton?.onClick.RemoveAllListeners();
        }

        private void ChangeMonitor(int delta)
        {
            int count = Mathf.Max(1, m_controller.MonitorCount);
            int next = ((m_controller.MonitorIndex + delta) % count + count) % count;
            m_controller.SetMonitor(next);
            RefreshLabel();
        }

        private void BindScaleSlider()
        {
            if (m_scaleSlider == null) return;

            m_scaleSlider.minValue = ViewScaleRange.Min;
            m_scaleSlider.maxValue = ViewScaleRange.Max;
            m_scaleSlider.SetValueWithoutNotify(m_controller.Scale);
            m_scaleSlider.onValueChanged.AddListener(value =>
            {
                m_controller.SetScale(value);
                RefreshLabel();
            });
        }

        // 프리셋 버튼은 슬라이더와 같은 값을 조작하므로, 누른 뒤 슬라이더를 결과에 동기화한다.
        private void BindScalePreset(Button button, float value)
        {
            if (button == null) return;

            button.onClick.AddListener(() =>
            {
                m_controller.SetScale(value);
                m_scaleSlider?.SetValueWithoutNotify(m_controller.Scale);
                RefreshLabel();
            });
        }

        // 크롭 슬라이더는 서로를 밀어내며(최소 폭 유지) 컨트롤러에 즉시 반영한다.
        private void BindCropSliders()
        {
            if (m_cropLeftSlider != null)
            {
                m_cropLeftSlider.minValue = 0f;
                m_cropLeftSlider.maxValue = 1f;
                m_cropLeftSlider.SetValueWithoutNotify(m_controller.CropLeft);
                m_cropLeftSlider.onValueChanged.AddListener(left =>
                {
                    m_controller.SetCropRange(left, m_controller.CropRight);
                    SyncCropSliders();
                });
            }

            if (m_cropRightSlider != null)
            {
                m_cropRightSlider.minValue = 0f;
                m_cropRightSlider.maxValue = 1f;
                m_cropRightSlider.SetValueWithoutNotify(m_controller.CropRight);
                m_cropRightSlider.onValueChanged.AddListener(right =>
                {
                    m_controller.SetCropRange(m_controller.CropLeft, right);
                    SyncCropSliders();
                });
            }
        }

        // 컨트롤러가 보정한 값(순서 교정·최소 폭)을 슬라이더에 되돌린다. 알림 없이 써서 재귀를 막는다.
        private void SyncCropSliders()
        {
            m_cropLeftSlider?.SetValueWithoutNotify(m_controller.CropLeft);
            m_cropRightSlider?.SetValueWithoutNotify(m_controller.CropRight);
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (m_monitorLabel != null)
            {
                // 번호는 Windows 디스플레이 설정의 번호와 일치시킨다(열거 순서가 아님).
                m_monitorLabel.text = $"Monitor {m_controller.CurrentDisplayNumber()} / {Mathf.Max(1, m_controller.MonitorCount)}";
            }
            if (m_moveLabel != null)
            {
                m_moveLabel.text = m_controller.WindowMoveMode ? "이동 모드: ON" : "이동 모드: OFF";
            }
            if (m_cropLabel != null)
            {
                m_cropLabel.text = $"보이는 구간 {m_controller.CropLeft:0.00} ~ {m_controller.CropRight:0.00}";
            }
            if (m_scaleLabel != null)
            {
                m_scaleLabel.text = $"크기 {m_controller.Scale:0.00}배";
            }
        }
    }
}
