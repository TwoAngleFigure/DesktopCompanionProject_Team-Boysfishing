using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// 화면 옵션(모드·출력 배율·UI 배율·크롭 범위·모니터·창 이동) 설정 창.
    /// 위젯 입력을 <see cref="DisplayModeController"/>에 직접 전달하고, 컨트롤러가 보정한 값을 위젯에 되돌린다.
    /// 값을 고르는 항목은 드롭다운으로, 즉시 실행되는 동작(창 이동 모드)만 버튼으로 둔다.
    /// 현재 값은 드롭다운 자신이 표시하므로 별도 값 라벨을 두지 않는다.
    /// 게임 System과 무관한 설정이므로 ViewModel을 두지 않는다.
    /// </summary>
    public class DisplaySettingsView : UIWindowBase
    {
        [Header("Controller")]
        [SerializeField] private DisplayModeController m_controller;

        [Header("Mode")]
        [SerializeField] private TMP_Dropdown m_modeDropdown;

        [Header("Scale (Window 모드 출력 배율)")]
        [SerializeField] private TMP_Dropdown m_scaleDropdown;

        [Header("UI 배율")]
        [Tooltip("UI 픽셀 격자가 깨지지 않도록 1/4 단위 항목만 노출한다.")]
        [SerializeField] private TMP_Dropdown m_uiScaleDropdown;

        [Header("FPS")]
        [SerializeField] private TMP_Dropdown m_fpsDropdown;

        [Header("Crop (Window 모드에서 볼 가로 구간)")]
        [SerializeField] private RangeSlider m_cropSlider;
        [SerializeField] private TMP_Text m_cropLabel;

        [Header("Always On Top")]
        [Tooltip("창 최상단 고정. 화면 모드와 무관하게 항상 쓸 수 있다")]
        [SerializeField] private Toggle m_topMostToggle;

        [Header("Monitor")]
        [SerializeField] private TMP_Dropdown m_monitorDropdown;

        [Header("Window Move")]
        [SerializeField] private Button m_moveButton;
        [SerializeField] private TMP_Text m_moveLabel;

        [Header("Window 전용 항목 비활성 표시")]
        [Tooltip("Full 모드에서 함께 반투명해질 텍스트. 출력 배율·크롭의 항목 제목 등을 넣는다.")]
        [SerializeField] private TMP_Text[] m_windowOnlyTexts;
        [SerializeField, Range(0f, 1f)] private float m_disabledTextAlpha = 0.4f;

        // 순서는 ScreenMode 열거 순서와 일치해야 한다(인덱스를 그대로 캐스팅한다).
        private static readonly string[] ModeLabels = { "전체 화면", "창 모드" };

        public override void Bind()
        {
            if (m_controller == null)
            {
                Debug.LogWarning("[DisplaySettingsView] DisplayModeController 미할당");
                return;
            }

            BindModeDropdown();
            BindScaleDropdown();
            BindUiScaleDropdown();
            BindFpsDropdown();
            BindCropSlider();
            BindTopMostToggle();
            BindMonitorDropdown();

            if (m_moveButton != null)
            {
                m_moveButton.onClick.RemoveListener(HandleMoveClicked);
                m_moveButton.onClick.AddListener(HandleMoveClicked);
            }

            Refresh();
        }

        public override void Unbind()
        {
            // ※ RemoveAllListeners를 쓰면 안 된다. 인스펙터 연결은 남기고 런타임 AddListener로 붙은 것을
            //   전부 지우는데, 위젯의 연출 컴포넌트(ToggleSpriteAnimator 등)가 바로 그 방식으로 구독한다.
            //   이 창이 건 핸들러만 정확히 떼어낸다.
            m_modeDropdown?.onValueChanged.RemoveListener(HandleModeChanged);
            m_scaleDropdown?.onValueChanged.RemoveListener(HandleScaleChanged);
            m_uiScaleDropdown?.onValueChanged.RemoveListener(HandleUiScaleChanged);
            m_fpsDropdown?.onValueChanged.RemoveListener(HandleFpsChanged);
            if (m_cropSlider != null) m_cropSlider.OnValueChanged -= HandleCropChanged;
            m_topMostToggle?.onValueChanged.RemoveListener(HandleTopMostChanged);
            m_monitorDropdown?.onValueChanged.RemoveListener(HandleMonitorChanged);
            m_moveButton?.onClick.RemoveListener(HandleMoveClicked);
        }

        // 옵션 목록을 교체한다. 재바인딩 시 목록이 누적되지 않도록 항상 비우고 채운다.
        private static void SetOptions(TMP_Dropdown dropdown, IEnumerable<string> labels)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(labels));
        }

        // 선택을 알림 없이 반영한다. 알림을 태우면 컨트롤러를 다시 호출해 재귀가 된다.
        private static void Select(TMP_Dropdown dropdown, int index)
        {
            dropdown.SetValueWithoutNotify(Mathf.Clamp(index, 0, Mathf.Max(0, dropdown.options.Count - 1)));
            dropdown.RefreshShownValue();
        }

        private void BindModeDropdown()
        {
            if (m_modeDropdown == null) return;

            SetOptions(m_modeDropdown, ModeLabels);
            Select(m_modeDropdown, (int)m_controller.Mode);
            Subscribe(m_modeDropdown, HandleModeChanged);
        }

        // 중복 구독을 막고 다른 컴포넌트의 구독은 건드리지 않는다.
        private static void Subscribe(TMP_Dropdown dropdown, UnityAction<int> handler)
        {
            dropdown.onValueChanged.RemoveListener(handler);
            dropdown.onValueChanged.AddListener(handler);
        }

        private void HandleModeChanged(int index)
        {
            m_controller.SetMode((ScreenMode)index);
            Refresh();
        }

        private void BindScaleDropdown()
        {
            if (m_scaleDropdown == null) return;

            SetOptions(m_scaleDropdown, ViewScaleRange.Labels());

            // 슬라이더 시절에 저장된 연속값(예: 0.9)은 목록에 없다. 가장 가까운 항목으로 정규화해
            // 표시와 실제 값이 어긋나지 않게 맞춘다.
            int index = ViewScaleRange.NearestIndex(m_controller.Scale);
            m_controller.SetScale(ViewScaleRange.Options[index]);
            Select(m_scaleDropdown, index);
            Subscribe(m_scaleDropdown, HandleScaleChanged);
        }

        private void HandleScaleChanged(int index)
        {
            m_controller.SetScale(ViewScaleRange.Options[index]);
            Refresh();
        }

        // UI 배율은 연속값을 허용하지 않는다. 1/4 단위를 벗어나면 아트 1픽셀이 정수 화면픽셀로 떨어지지 않는다.
        private void BindUiScaleDropdown()
        {
            if (m_uiScaleDropdown == null) return;

            SetOptions(m_uiScaleDropdown, UiScaleRange.Labels());
            Select(m_uiScaleDropdown, UiScaleRange.NearestIndex(m_controller.UiScale));
            Subscribe(m_uiScaleDropdown, HandleUiScaleChanged);
        }

        private void HandleUiScaleChanged(int index)
        {
            m_controller.SetUiScale(UiScaleRange.Options[index]);
            Refresh();
        }

        // 프레임 상한은 화면 모드와 무관하므로 Full에서도 살려 둔다.
        private void BindFpsDropdown()
        {
            if (m_fpsDropdown == null) return;

            SetOptions(m_fpsDropdown, FpsOptions.Labels());
            Select(m_fpsDropdown, FpsOptions.NearestIndex(m_controller.TargetFps));
            Subscribe(m_fpsDropdown, HandleFpsChanged);
        }

        private void HandleFpsChanged(int index)
        {
            m_controller.SetTargetFps(FpsOptions.Options[index]);
            Refresh();
        }

        // 최상단 고정은 화면 모드와 무관하므로 Full에서도 살려 둔다.
        private void BindTopMostToggle()
        {
            if (m_topMostToggle == null) return;

            m_topMostToggle.SetIsOnWithoutNotify(m_controller.TopMost);
            m_topMostToggle.onValueChanged.RemoveListener(HandleTopMostChanged);
            m_topMostToggle.onValueChanged.AddListener(HandleTopMostChanged);
        }

        private void HandleTopMostChanged(bool on)
        {
            m_controller.SetTopMost(on);
            Refresh();
        }

        private void BindMonitorDropdown()
        {
            if (m_monitorDropdown == null) return;

            SetOptions(m_monitorDropdown, m_controller.MonitorLabels());
            Select(m_monitorDropdown, m_controller.MonitorIndex);
            Subscribe(m_monitorDropdown, HandleMonitorChanged);
        }

        private void HandleMonitorChanged(int index)
        {
            m_controller.SetMonitor(index);
            Select(m_monitorDropdown, m_controller.MonitorIndex);   // 컨트롤러가 클램프한 결과를 되돌린다
            Refresh();
        }

        private void HandleMoveClicked()
        {
            m_controller.ToggleWindowMoveMode();
            Refresh();
        }

        // 좌·우 경계를 핸들 두 개로 다루는 단일 슬라이더다. 드래그 중 매 프레임 컨트롤러에 반영된다.
        private void BindCropSlider()
        {
            if (m_cropSlider == null) return;

            m_cropSlider.SetValuesWithoutNotify(m_controller.CropLeft, m_controller.CropRight);
            m_cropSlider.OnValueChanged += HandleCropChanged;
        }

        private void HandleCropChanged(float left, float right)
        {
            m_controller.SetCropRange(left, right);

            // 컨트롤러가 보정한 값(순서 교정·최소 폭)을 되돌린다. 알림 없이 써서 재귀를 막는다.
            m_cropSlider.SetValuesWithoutNotify(m_controller.CropLeft, m_controller.CropRight);
            Refresh();
        }

        private void Refresh()
        {
            RefreshLabel();
            RefreshWindowOnlyOptions();
        }

        private void RefreshLabel()
        {
            if (m_moveLabel != null)
            {
                m_moveLabel.text = m_controller.WindowMoveMode ? "이동 모드: ON" : "이동 모드: OFF";
            }
            if (m_cropLabel != null)
            {
                m_cropLabel.text = $"보이는 구간 {m_controller.CropLeft:0.00} ~ {m_controller.CropRight:0.00}";
            }
        }

        /// <summary>
        /// 출력 배율·크롭·창 이동은 Window 모드에서만 적용된다
        /// (Full은 uv 전체·화면 크기 고정이고, 창 이동은 컨트롤러가 Window 모드에서만 켜 준다).
        /// Full 모드에서는 상호작용을 끄고 텍스트를 반투명으로 낮춰 쓸 수 없는 항목임을 보인다.
        /// </summary>
        private void RefreshWindowOnlyOptions()
        {
            bool windowMode = m_controller.Mode == ScreenMode.Window;
            float alpha = windowMode ? 1f : m_disabledTextAlpha;

            if (m_scaleDropdown != null)
            {
                m_scaleDropdown.interactable = windowMode;
                SetTextAlpha(m_scaleDropdown.captionText, alpha);
            }
            if (m_cropSlider != null) m_cropSlider.Interactable = windowMode;
            SetTextAlpha(m_cropLabel, alpha);

            if (m_moveButton != null) m_moveButton.interactable = windowMode;
            SetTextAlpha(m_moveLabel, alpha);

            // 항목 제목처럼 위젯 밖에 있는 텍스트는 계층을 알 수 없어 인스펙터에서 받는다.
            if (m_windowOnlyTexts != null)
            {
                foreach (var text in m_windowOnlyTexts)
                {
                    SetTextAlpha(text, alpha);
                }
            }
        }

        private static void SetTextAlpha(TMP_Text text, float alpha)
        {
            if (text != null) text.alpha = alpha;
        }
    }
}
