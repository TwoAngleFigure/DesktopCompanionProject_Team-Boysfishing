using DesktopCompanion.Data;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// InventorySlotView가 표시하기 쉬운 UI 전용 데이터.
    /// ViewModel -> EntityHandle을 해석해서 ViewData 생성
    /// </summary>
    public class InventorySlotViewData
    {
        public int SlotIndex;
        public bool IsEmpty;

        public ItemType SlotType;
        public ItemType ItemType;

        public EntityHandle Handle;
        public ItemData ItemData;

        public string ItemName;
        public int DataId;

        public float Size;
        public ItemQuality Quality;
        public int UpgradeLevel;
        public int Quantity;

        public bool IsSelected;

        // TEMP:
        // Drag & Drop 도입 전까지 사용하는 임시 이동 모드 표시용 값.
        // 최종 Drag & Drop 구현 시 제거 또는 교체 예정.
        public bool IsMoveSource;
        public bool IsMoveMode;

        public static InventorySlotViewData Empty(int slotIndex, ItemType slotType, bool isSelected, bool isMoveMode)
        {
            return new InventorySlotViewData
            {
                SlotIndex = slotIndex,
                IsEmpty = true,

                SlotType = slotType,
                ItemType = slotType,

                Handle = default,
                ItemData = null,

                ItemName = string.Empty,
                DataId = 0,

                Size = 0f,
                Quality = ItemQuality.OneStar,
                UpgradeLevel = 0,
                Quantity = 0,

                IsSelected = isSelected,

                // TEMP:
                // 이동 모드 상태 표현용. Drag & Drop 도입 시 제거 예정.
                IsMoveSource = false,
                IsMoveMode = isMoveMode
            };
        }
    }
}