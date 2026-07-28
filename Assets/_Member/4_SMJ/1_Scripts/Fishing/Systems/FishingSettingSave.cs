using System;

namespace DesktopCompanion.Systems
{
    [Serializable]
    public class FishingSettingSave
    {
        public InventoryFullPolicy inventoryFullPolicy;
        public RecordStopCriterion recordStopCriterion;
    }
}