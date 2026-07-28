using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    internal sealed class InventorySizeController
    {
        internal const int ExpandableInventoryInitialSize = 25;

        private const int ExpandableInventoryExpandSize = 5;
        private const int ExpandableInventoryRemainingSlots = 5;

        private InventorySlotStorage m_slotStorage;

        public InventorySizeController(InventorySlotStorage slotStorage)
        {
            m_slotStorage = slotStorage;
        }

        public void ExpandInventoryIfNeeded(ItemType itemType)
        {
            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);

            if (!ExpandIfNeeded(slotType, out int previousSlotSize, out int nextSlotSize, out int remainingSlotCount))
            {
                return;
            }
        }

        private bool ExpandIfNeeded(ItemType itemType, out int previousSlotSize, out int nextSlotSize, out int remainingSlotCount)
        {
            previousSlotSize = nextSlotSize = remainingSlotCount = default;
            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);
            if(!IsExpandableInventoryType(slotType))
            {
                return false;
            }

            previousSlotSize = m_slotStorage.GetMaxSlotCount(slotType);
            remainingSlotCount = previousSlotSize - m_slotStorage.GetUsedSlotCount(slotType);
            if(remainingSlotCount > ExpandableInventoryRemainingSlots)
            {
                return false;
            }

            nextSlotSize = previousSlotSize + ExpandableInventoryExpandSize;
            m_slotStorage.ResizeExpandableInventory(slotType, nextSlotSize);

            return true;
        }

        /// <summary>
        /// 저장된 최고 슬롯 위치와 아이템 수를 기준으로 복원에 필요한 초기 크기 계산
        /// </summary>
        public static int CalculateInitialExpandableSize(int highestSlotIndex, int savedSlotCount)
        {
            int requiredUsedSlotCount = Mathf.Max(highestSlotIndex + 1, savedSlotCount);

            // 복원 직후 최소 여섯 개의 빈 슬롯이 남도록 계산
            int requiredSlotCount = requiredUsedSlotCount + ExpandableInventoryRemainingSlots + 1;
            int roundedSlotCount = RoundUpToMultiple(requiredSlotCount, ExpandableInventoryExpandSize);

            return Mathf.Max(ExpandableInventoryInitialSize, roundedSlotCount);
        }

        private static bool IsExpandableInventoryType(ItemType itemType)
        {
            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);
            return slotType == ItemType.Equipment || slotType == ItemType.Materials;
        }

        private static int RoundUpToMultiple(int value, int multiple)
        {
            if (value <= 0)
            {
                return multiple;
            }

            return ((value + multiple - 1) / multiple) * multiple;
        }

        public bool ResizeFishSlots(int slotSize)
        {
            if (slotSize <= 0)
            {
                return false;
            }

            int currentCount = m_slotStorage.GetMaxSlotCount(ItemType.Fish);

            if (currentCount == slotSize)
            {
                return false;
            }

            return m_slotStorage.ResizeFishSlots(slotSize);
        }
    }
}
