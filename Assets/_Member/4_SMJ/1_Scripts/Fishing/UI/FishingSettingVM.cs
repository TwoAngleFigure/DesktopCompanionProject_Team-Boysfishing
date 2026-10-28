using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    public class FishingSettingVM : UIViewModelBase
    {
        private FishingSettingSystem m_fishingSettingSystem;

        public readonly BindableProperty<InventoryFullPolicy>
            CurrentInventoryFullPolicy = new(
                InventoryFullPolicy.StopAndAsk);

        public readonly BindableProperty<RecordStopCriterion>
            CurrentRecordStopCriterion = new(
                RecordStopCriterion.Quality);

        public readonly BindableProperty<bool>
            IsRecordStopCriterionEnabled = new(false);

        public RelayCommand<InventoryFullPolicy>
            ChangeInventoryFullPolicy
        { get; private set; }

        public RelayCommand<RecordStopCriterion>
            ChangeRecordStopCriterion
        { get; private set; }

        public override void Bind()
        {
            m_fishingSettingSystem = SystemManager.GetSystem<FishingSettingSystem>();

            ChangeInventoryFullPolicy = new RelayCommand<InventoryFullPolicy>(ExecuteChangeInventoryFullPolicy);

            ChangeRecordStopCriterion = new RelayCommand<RecordStopCriterion>(ExecuteChangeRecordStopCriterion);

            if (m_fishingSettingSystem == null)
            {
                RefreshSettingState();
                return;
            }

            m_fishingSettingSystem.OnInventoryFullPolicyChanged += HandleInventoryFullPolicyChanged;

            m_fishingSettingSystem.OnRecordStopCriterionChanged += HandleRecordStopCriterionChanged;

            RefreshSettingState();
        }

        public override void Unbind()
        {
            if (m_fishingSettingSystem != null)
            {
                m_fishingSettingSystem.OnInventoryFullPolicyChanged -= HandleInventoryFullPolicyChanged;

                m_fishingSettingSystem.OnRecordStopCriterionChanged -= HandleRecordStopCriterionChanged;
            }

            m_fishingSettingSystem = null;
        }

        private void RefreshSettingState()
        {
            if (m_fishingSettingSystem == null)
            {
                CurrentInventoryFullPolicy.Value = InventoryFullPolicy.StopAndAsk;

                CurrentRecordStopCriterion.Value = RecordStopCriterion.Quality;

                IsRecordStopCriterionEnabled.Value = false;
                return;
            }

            InventoryFullPolicy policy = m_fishingSettingSystem.CurrentInventoryFullPolicy;

            CurrentInventoryFullPolicy.Value = policy;

            CurrentRecordStopCriterion.Value = m_fishingSettingSystem.CurrentRecordStopCriterion;

            IsRecordStopCriterionEnabled.Value = policy == InventoryFullPolicy.StopOnRecordUpdate;
        }

        private void ExecuteChangeInventoryFullPolicy(InventoryFullPolicy policy)
        {
            m_fishingSettingSystem?.SetInventoryFullPolicy(policy);
        }

        private void ExecuteChangeRecordStopCriterion(RecordStopCriterion criterion)
        {
            if (m_fishingSettingSystem == null ||
                m_fishingSettingSystem.CurrentInventoryFullPolicy
                    != InventoryFullPolicy.StopOnRecordUpdate)
            {
                return;
            }

            m_fishingSettingSystem.SetRecordStopCriterion(criterion);
        }

        private void HandleInventoryFullPolicyChanged(InventoryFullPolicy policy)
        {
            RefreshSettingState();
        }

        private void HandleRecordStopCriterionChanged(RecordStopCriterion criterion)
        {
            RefreshSettingState();
        }
    }
}