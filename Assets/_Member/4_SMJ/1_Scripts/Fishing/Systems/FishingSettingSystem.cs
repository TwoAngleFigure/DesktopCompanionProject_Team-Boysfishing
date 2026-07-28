using DesktopCompanion.Save;
using System;


namespace DesktopCompanion.Systems
{
    public class FishingSettingSystem : SystemBase, ISaveable
    {
        private InventoryFullPolicy m_inventoryFullPolicy = InventoryFullPolicy.StopAndAsk;
        private RecordStopCriterion m_recordStopCriterion = RecordStopCriterion.Quality;

        public InventoryFullPolicy CurrentInventoryFullPolicy => m_inventoryFullPolicy;
        public RecordStopCriterion CurrentRecordStopCriterion => m_recordStopCriterion;

        public event Action<InventoryFullPolicy> OnInventoryFullPolicyChanged;
        public event Action<RecordStopCriterion> OnRecordStopCriterionChanged;


        public void SetInventoryFullPolicy(InventoryFullPolicy policy)
        {
            if (m_inventoryFullPolicy == policy)
            {
                return;
            }

            m_inventoryFullPolicy = policy;
            OnInventoryFullPolicyChanged?.Invoke(m_inventoryFullPolicy);
        }

        public void SetRecordStopCriterion(RecordStopCriterion criterion)
        {
            if (m_recordStopCriterion == criterion)
            {
                return;
            }

            m_recordStopCriterion = criterion;
            OnRecordStopCriterionChanged?.Invoke(m_recordStopCriterion);
        }

        public string SaveId => "fishing_settings";

        public Type StateType => typeof(FishingSettingSave);

        public object CaptureState()
        {
            return new FishingSettingSave
            {
                inventoryFullPolicy = m_inventoryFullPolicy,
                recordStopCriterion = m_recordStopCriterion
            };
        }

        public void RestoreState(object state)
        {
            if (state is not FishingSettingSave save)
            {
                return;
            }

            if (Enum.IsDefined(typeof(InventoryFullPolicy), save.inventoryFullPolicy))
            {
                SetInventoryFullPolicy(save.inventoryFullPolicy);
            }

            if (Enum.IsDefined(typeof(RecordStopCriterion), save.recordStopCriterion))
            {
                SetRecordStopCriterion(save.recordStopCriterion);
            }
        }
    }
}

