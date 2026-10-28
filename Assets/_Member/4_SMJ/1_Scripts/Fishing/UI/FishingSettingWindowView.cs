using DesktopCompanion.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class FishingSettingWindowView : UIWindowBase
    {
        [Header("Window")]
        [SerializeField] private Button m_closeButton;

        [Header("Inventory Full Policy Buttons")]
        [SerializeField] private Button m_stopAndAskButton;
        [SerializeField] private Button m_alwaysSellButton;
        [SerializeField] private Button m_stopOnRecordUpdateButton;

        [Header("Record Stop Criterion Buttons")]
        [SerializeField] private CanvasGroup m_recordCriterionCanvasGroup;
        [SerializeField] private Button m_qualityCriterionButton;
        [SerializeField] private Button m_sizeCriterionButton;

        [Header("Button Background Images")]
        [SerializeField] private Image m_stopAndAskBackground;
        [SerializeField] private Image m_alwaysSellBackground;
        [SerializeField] private Image m_stopOnRecordUpdateBackground;
        [SerializeField] private Image m_qualityCriterionBackground;
        [SerializeField] private Image m_sizeCriterionBackground;

        [Header("Visual Settings")]
        [SerializeField] private Color m_selectedButtonColor;
        [SerializeField] private Color m_unselectedButtonColor;

        [SerializeField, Range(0f, 1f)]
        private float m_disabledCriterionAlpha = 0.45f;

        private readonly FishingSettingVM m_vm = new();

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            m_vm.CurrentInventoryFullPolicy.Bind(HandleInventoryFullPolicyChanged);
            m_vm.CurrentRecordStopCriterion.Bind(HandleRecordStopCriterionChanged);
            m_vm.IsRecordStopCriterionEnabled.Bind(HandleRecordStopCriterionEnabledChanged);

            m_closeButton?.onClick.AddListener(Close);
            m_stopAndAskButton?.onClick.AddListener(HandleStopAndAskClicked);
            m_alwaysSellButton?.onClick.AddListener(HandleAlwaysSellClicked);
            m_stopOnRecordUpdateButton?.onClick.AddListener(HandleStopOnRecordUpdateClicked);
            m_qualityCriterionButton?.onClick.AddListener(HandleQualityCriterionClicked);
            m_sizeCriterionButton?.onClick.AddListener(HandleSizeCriterionClicked);
        }

        public override void Unbind()
        {
            m_vm.CurrentInventoryFullPolicy.Unbind(HandleInventoryFullPolicyChanged);
            m_vm.CurrentRecordStopCriterion.Unbind(HandleRecordStopCriterionChanged);
            m_vm.IsRecordStopCriterionEnabled.Unbind(HandleRecordStopCriterionEnabledChanged);

            m_closeButton?.onClick.RemoveListener(Close);
            m_stopAndAskButton?.onClick.RemoveListener(HandleStopAndAskClicked);
            m_alwaysSellButton?.onClick.RemoveListener(HandleAlwaysSellClicked);
            m_stopOnRecordUpdateButton?.onClick.RemoveListener(HandleStopOnRecordUpdateClicked);
            m_qualityCriterionButton?.onClick.RemoveListener(HandleQualityCriterionClicked);
            m_sizeCriterionButton?.onClick.RemoveListener(HandleSizeCriterionClicked);

            m_vm.Unbind();
        }

        private void HandleStopAndAskClicked()
        {
            m_vm.ChangeInventoryFullPolicy?.Execute(InventoryFullPolicy.StopAndAsk);
        }

        private void HandleAlwaysSellClicked()
        {
            m_vm.ChangeInventoryFullPolicy?.Execute(InventoryFullPolicy.AlwaysSell);
        }

        private void HandleStopOnRecordUpdateClicked()
        {
            m_vm.ChangeInventoryFullPolicy?.Execute(InventoryFullPolicy.StopOnRecordUpdate);
        }

        private void HandleQualityCriterionClicked()
        {
            m_vm.ChangeRecordStopCriterion?.Execute(RecordStopCriterion.Quality);
        }

        private void HandleSizeCriterionClicked()
        {
            m_vm.ChangeRecordStopCriterion?.Execute(RecordStopCriterion.Size);
        }

        private void HandleInventoryFullPolicyChanged(InventoryFullPolicy policy)
        {
            SetButtonSelected(m_stopAndAskBackground, policy == InventoryFullPolicy.StopAndAsk);
            SetButtonSelected(m_alwaysSellBackground, policy == InventoryFullPolicy.AlwaysSell);
            SetButtonSelected(m_stopOnRecordUpdateBackground, policy == InventoryFullPolicy.StopOnRecordUpdate);
        }

        private void HandleRecordStopCriterionChanged(RecordStopCriterion criterion)
        {
            SetButtonSelected(m_qualityCriterionBackground, criterion == RecordStopCriterion.Quality);
            SetButtonSelected(m_sizeCriterionBackground, criterion == RecordStopCriterion.Size);
        }

        private void HandleRecordStopCriterionEnabledChanged(bool isEnabled)
        {
            if (m_recordCriterionCanvasGroup != null)
            {
                m_recordCriterionCanvasGroup.alpha = isEnabled ? 1f : m_disabledCriterionAlpha;
                m_recordCriterionCanvasGroup.interactable = isEnabled;
                m_recordCriterionCanvasGroup.blocksRaycasts = isEnabled;
            }

            if (m_qualityCriterionButton != null)
            {
                m_qualityCriterionButton.interactable = isEnabled;
            }

            if (m_sizeCriterionButton != null)
            {
                m_sizeCriterionButton.interactable = isEnabled;
            }
        }

        private void SetButtonSelected(Image background, bool isSelected)
        {
            if (background == null)
            {
                return;
            }

            background.color = isSelected ? m_selectedButtonColor : m_unselectedButtonColor;
        }
    }
}