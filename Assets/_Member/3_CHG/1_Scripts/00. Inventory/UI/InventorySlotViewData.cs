using DesktopCompanion.Data;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Views
{
    public class InventorySlotViewData
    {
        public int SlotIndex;
        public bool IsEmpty;

        public ItemType SlotType;
        public ItemType ItemType;

        public EntityHandle Handle;

        public string ItemName;
        public string IconKey;
        public int DataId;

        public float Size;
        public ItemQuality Quality;
        public int UpgradeLevel;
        public int Quantity;

        public bool IsSelected;

        // TEMP: Drag-Drop 도입 시 수정
        public bool IsMoveSource;
        public bool IsMoveMode;

        public static InventorySlotViewData Empty(
            int slotIndex,
            ItemType slotType,
            bool isSelected,
            bool isMoveMode)
        {
            return new InventorySlotViewData
            {
                SlotIndex = slotIndex,
                IsEmpty = true,

                SlotType = slotType,
                ItemType = slotType,

                Handle = default,

                ItemName = string.Empty,
                IconKey = string.Empty,
                DataId = 0,

                Size = 0f,
                Quality = ItemQuality.OneStar,
                UpgradeLevel = 0,
                Quantity = 0,

                IsSelected = isSelected,

                // TEMP: Drag-Drop 도입 시 수정
                IsMoveSource = false,
                IsMoveMode = isMoveMode
            };
        }
    }
}