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

        // 아이템 공통
        public int Tier;
        public int UnitSellPrice;

        // 물고기
        public float Size;
        public ItemRarity Rarity;
        public ItemQuality Quality;

        // 장비
        public EquipmentMountingArea MountingArea;
        public int UpgradeLevel;

        // 재료 및 소모품
        public int Quantity;

        public string GradeText;
        public string EffectText;
        public string SellPriceText;

        public bool IsSelected;

        public static InventorySlotViewData Empty(int slotIndex, ItemType slotType, bool isSelected)
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

                Tier = 0,
                UnitSellPrice = 0,

                Size = 0f,
                Rarity = ItemRarity.Normal,
                Quality = ItemQuality.OneStar,

                MountingArea = default,
                UpgradeLevel = 0,

                Quantity = 0,

                GradeText = string.Empty,
                EffectText = string.Empty,
                SellPriceText = string.Empty,

                IsSelected = isSelected
            };
        }
    }
}