using DesktopCompanion.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FishingView : UIViewBase
{
    [SerializeField] private TMP_Text m_stateText;

    [Header("Battle Gauge")]
    [SerializeField] private TMP_Text m_hpText;
    [SerializeField] private Slider m_hpSlider;
    [SerializeField] private Slider m_timeLimitSlider;

    [Header("State Toggle")]
    [SerializeField] private Button m_toggleFishingButton;
    [SerializeField] private TMP_Text m_toggleFishingButtonText;

    [Tooltip("버튼의 상태 연출(선택). 낚시가 도는 동안 켜짐 상태로 둔다")]
    [SerializeField] private ButtonStateOwner m_toggleFishingStateOwner;

    [Header("Manual Attack")]
    [SerializeField] private Button m_manualAttackButton;

    private readonly FishingVM m_vm = new();

    public override void Bind()
    {
        m_vm.Inject(SystemManager, EntityManager);
        m_vm.Bind();

        m_vm.StateText.Bind(OnStateTextChanged);
        m_vm.HpText.Bind(OnHpTextChanged);
        m_vm.HpRatio.Bind(OnHpRatioChanged);
        m_vm.ToggleButtonText.Bind(OnToggleButtonTextChanged);
        m_vm.IsFishingActive.Bind(OnFishingActiveChanged);
        m_vm.BattleTimeRemainingRatio.Bind(OnBattleTimeRemainingRatioChanged);
        m_vm.IsManualAttackEnabled.Bind(OnManualAttackEnabledChanged);

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
        m_vm.IsFishingActive.Unbind(OnFishingActiveChanged);
        m_vm.BattleTimeRemainingRatio.Unbind(OnBattleTimeRemainingRatioChanged);
        m_vm.IsManualAttackEnabled.Unbind(OnManualAttackEnabledChanged);

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


    private void OnToggleButtonTextChanged(string value)
    {
        if (m_toggleFishingButtonText != null)
        {
            m_toggleFishingButtonText.text = value;
        }
    }

    /// <summary>
    /// 낚시 진행 여부를 버튼 연출에 넘긴다. 클릭이 아니라 시스템이 상태를 바꾼 결과만 따르므로,
    /// 시작이 거부된 경우에 표시만 켜지지 않는다.
    /// </summary>
    private void OnFishingActiveChanged(bool isActive)
    {
        if (m_toggleFishingStateOwner != null)
        {
            m_toggleFishingStateOwner.SetActive(isActive);
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

    private void OnManualAttackEnabledChanged(bool isEnabled)
    {
        if (m_manualAttackButton != null)
        {
            m_manualAttackButton.interactable = isEnabled;
        }
    }

}

