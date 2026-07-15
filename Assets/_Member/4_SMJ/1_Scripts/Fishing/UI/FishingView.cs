using DesktopCompanion.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FishingView : UIViewBase
{
    [SerializeField] private TMP_Text m_stateText;

    [Header("Battle Gauge")]
    [SerializeField] private GameObject m_battleGauge;
    [SerializeField] private TMP_Text m_hpText;
    [SerializeField] private Slider m_hpSlider;
    [SerializeField] private Slider m_timeLimitSlider;

    [Header("State Toggle")]
    [SerializeField] private Button m_toggleFishingButton;
    [SerializeField] private TMP_Text m_toggleFishingButtonText;

    [Header("Manual Attack")]
    [SerializeField] private Button m_manualAttackButton;

    [Header("Debug HUD")]
    [SerializeField] private TMP_Text m_debugWaitTimeText;

    private readonly FishingVM m_vm = new();

    public override void Bind()
    {
        m_vm.Inject(SystemManager, EntityManager);
        m_vm.Bind();

        m_vm.StateText.Bind(OnStateTextChanged);
        m_vm.HpText.Bind(OnHpTextChanged);
        m_vm.HpRatio.Bind(OnHpRatioChanged);
        m_vm.ToggleButtonText.Bind(OnToggleButtonTextChanged);
        m_vm.DebugWaitTimeText.Bind(OnDebugWaitTimeTextChanged);
        m_vm.BattleTimeRemainingRatio.Bind(OnBattleTimeRemainingRatioChanged);
        m_vm.IsBattleGaugeVisible.Bind(OnBattleGaugeVisibleChanged);

        if (m_toggleFishingButton != null)
        {
            m_toggleFishingButton.onClick.AddListener(OnToggleFishingClicked);
        }

        if (m_manualAttackButton != null)
        {
            m_manualAttackButton.onClick.AddListener(OnManualAttackClicked);
        }
    }

    public override void Unbind()
    {
        m_vm.StateText.Unbind(OnStateTextChanged);
        m_vm.HpText.Unbind(OnHpTextChanged);
        m_vm.HpRatio.Unbind(OnHpRatioChanged);
        m_vm.ToggleButtonText.Unbind(OnToggleButtonTextChanged);
        m_vm.DebugWaitTimeText.Unbind(OnDebugWaitTimeTextChanged);
        m_vm.BattleTimeRemainingRatio.Unbind(OnBattleTimeRemainingRatioChanged);
        m_vm.IsBattleGaugeVisible.Unbind(OnBattleGaugeVisibleChanged);

        if (m_toggleFishingButton != null)
        {
            m_toggleFishingButton.onClick.RemoveListener(OnToggleFishingClicked);
        }

        if (m_manualAttackButton != null)
        {
            m_manualAttackButton.onClick.RemoveListener(OnManualAttackClicked);
        }

        m_vm.Unbind();
    }

    private void Update()
    {
        m_vm.RefreshRuntimeValues();
    }

    private void OnStateTextChanged(string value)
    {
        if (m_stateText != null)
        {
            m_stateText.text = value;
        }
    }

    private void OnHpTextChanged(string value)
    {
        if (m_hpText != null)
        {
            m_hpText.text = value;
        }
    }

    private void OnHpRatioChanged(float value)
    {
        if (m_hpSlider != null)
        {
            m_hpSlider.value = Mathf.Clamp01(value);
        }
    }


    private void OnDebugWaitTimeTextChanged(string value)
    {
        if (m_debugWaitTimeText != null)
        {
            m_debugWaitTimeText.text = value;
        }
    }

    private void OnToggleButtonTextChanged(string value)
    {
        if (m_toggleFishingButtonText != null)
        {
            m_toggleFishingButtonText.text = value;
        }
    }

    private void OnToggleFishingClicked()
    {
        m_vm.ToggleFishingState?.Execute();
    }

    private void OnManualAttackClicked()
    {
        m_vm.ManualAttack?.Execute();
    }
    private void OnBattleTimeRemainingRatioChanged(float ratio)
    {
        if (m_timeLimitSlider != null)
        {
            m_timeLimitSlider.SetValueWithoutNotify(
                Mathf.Clamp01(ratio));
        }
    }

    private void OnBattleGaugeVisibleChanged(bool isVisible)
    {
        if (m_battleGauge != null)
        {
            m_battleGauge.SetActive(isVisible);
        }
    }

}

