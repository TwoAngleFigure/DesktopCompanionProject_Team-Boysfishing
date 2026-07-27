using System;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 인벤토리 슬롯 배열과 슬롯 위치 변경만 담당하는 내부 저장소
    /// </summary>
    internal sealed class InventorySlotStorage
    {
        private EntityHandle[] m_fishSlots;
        private EntityHandle[] m_equipmentSlots;
        private EntityHandle[] m_materialSlots;

        public InventorySlotStorage(int fishSlotCount, int equipmentSlotCount, int materialSlotCount)
        {
            m_fishSlots = new EntityHandle[fishSlotCount];
            m_equipmentSlots = new EntityHandle[equipmentSlotCount];
            m_materialSlots = new EntityHandle[materialSlotCount];
        }

        /// <summary>
        /// 외부에서 원본 배열을 수정하지 못하도록 슬롯 복사본 반환
        /// </summary>
        public EntityHandle[] GetSlotsCopy(ItemType itemType)
        {
            EntityHandle[] sourceSlots = GetSlotArray(itemType);
            EntityHandle[] copiedSlots = new EntityHandle[sourceSlots.Length];

            Array.Copy(sourceSlots, copiedSlots, sourceSlots.Length);
            return copiedSlots;
        }

        /// <summary>
        /// InventorySystem 내부에서만 사용하는 원본 슬롯 배열
        /// </summary>
        private EntityHandle[] GetSlotArray(ItemType itemType)
        {
            ItemType slotType = NormalizeSlotType(itemType);

            switch (slotType)
            {
                case ItemType.Fish:
                    return m_fishSlots;

                case ItemType.Equipment:
                    return m_equipmentSlots;

                case ItemType.Materials:
                    return m_materialSlots;

                default:
                    return Array.Empty<EntityHandle>();
            }
        }

        public int GetMaxSlotCount(ItemType itemType)
        {
            return GetSlotArray(itemType).Length;
        }

        public int GetUsedSlotCount(ItemType itemType)
        {
            EntityHandle[] slots = GetSlotArray(itemType);
            int usedCount = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                if (!IsEmptyHandle(slots[i]))
                {
                    usedCount++;
                }
            }

            return usedCount;
        }

        public bool IsValidSlotIndex(ItemType itemType, int slotIndex)
        {
            EntityHandle[] slots = GetSlotArray(itemType);
            return slotIndex >= 0 && slotIndex < slots.Length;
        }

        public bool GetHandle(ItemType itemType, int slotIndex, out EntityHandle handle)
        {
            handle = default;

            if (!IsValidSlotIndex(itemType, slotIndex))
            {
                return false;
            }

            EntityHandle[] slots = GetSlotArray(itemType);

            if (IsEmptyHandle(slots[slotIndex]))
            {
                return false;
            }

            handle = slots[slotIndex];
            return true;
        }

        public bool SetHandle(ItemType itemType, int slotIndex, EntityHandle handle)
        {
            if (!IsValidSlotIndex(itemType, slotIndex))
            {
                return false;
            }

            GetSlotArray(itemType)[slotIndex] = handle;
            return true;
        }

        public bool ClearHandle(ItemType itemType, int slotIndex, out EntityHandle removedHandle)
        {
            removedHandle = default;

            if (!GetHandle(itemType, slotIndex, out removedHandle))
            {
                return false;
            }

            GetSlotArray(itemType)[slotIndex] = default;
            return true;
        }

        public bool Swap(ItemType itemType, int fromIndex, int toIndex)
        {
            if (!IsValidSlotIndex(itemType, fromIndex) || !IsValidSlotIndex(itemType, toIndex) || fromIndex == toIndex)
            {
                return false;
            }

            EntityHandle[] slots = GetSlotArray(itemType);
            EntityHandle tempHandle = slots[toIndex];

            slots[toIndex] = slots[fromIndex];
            slots[fromIndex] = tempHandle;

            return true;
        }

        public bool ContainsHandle(EntityHandle handle)
        {
            return ContainsHandle(m_fishSlots, handle)
                || ContainsHandle(m_equipmentSlots, handle)
                || ContainsHandle(m_materialSlots, handle);
        }

        public bool FindSlot(EntityHandle handle, out ItemType slotType, out int slotIndex)
        {
            if (FindHandleIndex(m_fishSlots, handle, out slotIndex))
            {
                slotType = ItemType.Fish;
                return true;
            }

            if (FindHandleIndex(m_equipmentSlots, handle, out slotIndex))
            {
                slotType = ItemType.Equipment;
                return true;
            }

            if (FindHandleIndex(m_materialSlots, handle, out slotIndex))
            {
                slotType = ItemType.Materials;
                return true;
            }

            slotType = default;
            slotIndex = -1;
            return false;
        }

        public int FindEmptySlotIndex(ItemType itemType)
        {
            EntityHandle[] slots = GetSlotArray(itemType);

            for (int i = 0; i < slots.Length; i++)
            {
                if (IsEmptyHandle(slots[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        public bool ResizeFishSlots(int slotSize)
        {
            if (slotSize <= 0 || !CanResizeSlotArray(m_fishSlots, slotSize))
            {
                return false;
            }

            Array.Resize(ref m_fishSlots, slotSize);
            return true;
        }

        public static ItemType NormalizeSlotType(ItemType itemType)
        {
            return itemType == ItemType.Consumables
                ? ItemType.Materials
                : itemType;
        }

        public void ResizeExpandableInventory(ItemType itemType, int slotSize)
        {
            ItemType slotType = NormalizeSlotType(itemType);

            switch (slotType)
            {
                case ItemType.Equipment:
                    Array.Resize(ref m_equipmentSlots, slotSize);
                    break;

                case ItemType.Materials:
                    Array.Resize(ref m_materialSlots, slotSize);
                    break;
            }
        }

        private static bool CanResizeSlotArray(EntityHandle[] slots, int nextSlotSize)
        {
            if (nextSlotSize >= slots.Length)
            {
                return true;
            }

            // 현재 기획 - 인벤토리 크기는 감소하지 않음
            // 그래도 일단 방지는 해둠
            for (int i = nextSlotSize; i < slots.Length; i++)
            {
                if (!IsEmptyHandle(slots[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsHandle(EntityHandle[] slots, EntityHandle handle)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Equals(handle))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool FindHandleIndex(EntityHandle[] slots, EntityHandle handle, out int slotIndex)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Equals(handle))
                {
                    slotIndex = i;
                    return true;
                }
            }

            slotIndex = -1;
            return false;
        }

        private static bool IsEmptyHandle(EntityHandle handle)
        {
            return handle.Value == Guid.Empty;
        }
    }
}
