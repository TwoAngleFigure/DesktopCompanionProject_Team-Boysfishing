using DesktopCompanion.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FishingView : UIViewBase
{
    [SerializeField] private TMP_Text m_stateText;
    [SerializeField] private TMP_Text m_fishNameText;
    [SerializeField] private TMP_Text m_hpText;
    [SerializeField] private Slider m_hpSlider;
    [SerializeField] private TMP_Text m_resultText;

    [Header("State Toggle")]
    [SerializeField] private Button m_toggleFishingButton;
    [SerializeField] private TMP_Text m_toggleFishingButtonText;

    [Header("Debug HUD")]
    [SerializeField] private TMP_Text m_debugWaitTimeText;
    [SerializeField] private TMP_Text m_debugBattleTimeText;

    private readonly FishingVM m_vm = new();

    public override void Bind()
    {
        m_vm.Inject(SystemManager);
        m_vm.Bind();

        m_vm.StateText.Bind(OnStateTextChanged);
        m_vm.BattleFishNameText.Bind(OnFishNameTextChanged);
        m_vm.HpText.Bind(OnHpTextChanged);
        m_vm.HpRatio.Bind(OnHpRatioChanged);
        m_vm.ResultText.Bind(OnResultTextChanged);
        m_vm.ToggleButtonText.Bind(OnToggleButtonTextChanged);
        m_vm.DebugWaitTimeText.Bind(OnDebugWaitTimeTextChanged);
        m_vm.DebugBattleTimeText.Bind(OnDebugBattleTimeTextChanged);

        if (m_toggleFishingButton != null)
        {
            m_toggleFishingButton.onClick.AddListener(OnToggleFishingClicked);
        }
    }

    public override void Unbind()
    {
        m_vm.StateText.Unbind(OnStateTextChanged);
        m_vm.BattleFishNameText.Unbind(OnFishNameTextChanged);
        m_vm.HpText.Unbind(OnHpTextChanged);
        m_vm.HpRatio.Unbind(OnHpRatioChanged);
        m_vm.ResultText.Unbind(OnResultTextChanged);
        m_vm.ToggleButtonText.Unbind(OnToggleButtonTextChanged);
        m_vm.DebugWaitTimeText.Unbind(OnDebugWaitTimeTextChanged);
        m_vm.DebugBattleTimeText.Unbind(OnDebugBattleTimeTextChanged);

        if (m_toggleFishingButton != null)
        {
            m_toggleFishingButton.onClick.RemoveListener(OnToggleFishingClicked);
        }

        m_vm.Unbind();
    }

    private void Update()
    {
        // Debug HUD
        m_vm.RefreshDebugTime();
    }

    private void OnStateTextChanged(string value)
    {
        if (m_stateText != null)
        {
            m_stateText.text = value;
        }
    }

    private void OnFishNameTextChanged(string value)
    {
        if (m_fishNameText != null)
        {
            m_fishNameText.text = value;
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

    private void OnResultTextChanged(string value)
    {
        if (m_resultText != null)
        {
            m_resultText.text = value;
        }
    }

    private void OnDebugWaitTimeTextChanged(string value)
    {
        if (m_debugWaitTimeText != null)
        {
            m_debugWaitTimeText.text = value;
        }
    }

    private void OnDebugBattleTimeTextChanged(string value)
    {
        if (m_debugBattleTimeText != null)
        {
            m_debugBattleTimeText.text = value;
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


}

