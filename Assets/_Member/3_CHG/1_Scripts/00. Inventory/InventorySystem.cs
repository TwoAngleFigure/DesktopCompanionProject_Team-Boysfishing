using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Save;
using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

namespace DesktopCompanion.Systems
{
    public class InventorySystem : SystemBase, ISaveable
    {
        private const int FallbackInventorySize = 20;
        private const bool EnableInventoryDebugLog = true;

        private InventorySlotStorage m_slotStorage;
        private InventorySaveLoad m_saveLoad;
        private InventoryAutoSellFilter m_autoSellFilter;
        private InventoryItemController m_itemController;

        private PlayerSystem m_playerSystem;
        private ShopSystem m_shopSystem;

        public event Action OnInventoryChanged;

        public event Action OnInventorySaveRequested;
        public event Action OnAutoSellFilterChanged;

        private InventorySave m_loadedSave;

        public string SaveId => "inventory";
        public Type StateType => typeof(InventorySave);

        public bool AutoSellEnabled => m_autoSellFilter.AutoSellEnabled;
        public ItemQuality MaxAutoSellQuality => m_autoSellFilter.MaxAutoSellQuality;
        public ItemRarity MaxAutoSellRarity => m_autoSellFilter.MaxAutoSellRarity;

        public override void Initialize()
        {
        }

        public override void PostInitialize()
        {
            m_playerSystem = SystemManager.GetSystem<PlayerSystem>();
            m_shopSystem = SystemManager.GetSystem<ShopSystem>();

            int fishInventorySize = GetCurrentInventorySize();
            int equipmentInventorySize = InventorySaveLoad.GetInitialExpandableInventorySize(m_loadedSave, ItemType.Equipment);
            int materialInventorySize = InventorySaveLoad.GetInitialExpandableInventorySize(m_loadedSave, ItemType.Materials);

            m_slotStorage = new InventorySlotStorage(fishInventorySize, equipmentInventorySize, materialInventorySize);
            m_saveLoad = new InventorySaveLoad(m_slotStorage, EntityManager, DataManager, LogDebug, LogWarning);
            m_autoSellFilter = new InventoryAutoSellFilter();
            m_itemController = new InventoryItemController(m_slotStorage, EntityManager);

            if (m_loadedSave != null)
            {
                RestoreLoadedSave();
                m_loadedSave = null;
            }

            if (m_playerSystem != null)
            {
                m_playerSystem.OnStatChanged += HandlePlayerStatChanged;
            }

            LogDebug($"PostInitialize complete. fishSlots: {fishInventorySize}, equipmentSlots: {equipmentInventorySize}, materialSlots: {materialInventorySize}");
        }

        /// <summary>
        /// itemType 슬롯 Read용 함수
        /// </summary>
        public EntityHandle[] GetSlots(ItemType itemType)
        {
            return m_slotStorage.GetSlotsCopy(itemType);
        }

        /// <summary> 슬롯 갯수를 세는 함수 </summary>
        public int GetMaxSlotCount(ItemType itemType)
        {
            return m_slotStorage.GetMaxSlotCount(itemType);
        }

        /// <summary> 비어있지 않은 슬롯 갯수를 세는 함수 </summary>
        public int GetUsedSlotCount(ItemType itemType)
        {
            return m_slotStorage.GetUsedSlotCount(itemType);
        }

        /// <summary> 특정 슬롯을 참조해서 핸들을 반환하는 함수 </summary>
        public bool GetHandleAt(ItemType itemType, int slotIndex, out EntityHandle handle)
        {
            return m_slotStorage.GetHandle(itemType, slotIndex, out handle);
        }

        /// <summary> 자동 필터 세팅 </summary>
        public bool SetAutoSellFilter(bool enabled, ItemQuality maxQuality, ItemRarity maxRarity)
        {
            switch(m_autoSellFilter.Set(enabled, maxQuality, maxRarity))
            {
                case SetFilterResult.Changed:
                    OnAutoSellFilterChanged?.Invoke();
                    RequestSave();
                    return true;
                case SetFilterResult.UnChanged:
                    return true;
                case SetFilterResult.Invalid:
                default:
                    return false;
            }
        }

        /// <summary> 인벤토리에 빈 슬롯이 있는 지 확인하는 함수 </summary>
        public bool HasEmptySlot(ItemType itemType)
        {
            return m_slotStorage.FindEmptySlotIndex(itemType) >= 0;
        }

        public bool AddItem(EntityHandle itemHandle)
        {
            if(m_itemController.IsValidToAddItem(itemHandle, out AddItemContext context) == AddItemResult.Invalid)
            {
                LogWarning("[InventorySystem] 추가하려는 아이템의 검증이 실패하였습니다.");
                return false;
            }

            if (context.ItemEntity is Entity_Fish fish && m_autoSellFilter.IsAutoSellTarget(fish))
            {
                if (m_shopSystem == null)
                {
                    LogWarning("TryAddItem failed. ShopSystem not found.");
                    return false;
                }

                bool sold = m_shopSystem.SellAcquiredItem(itemHandle, out int earnedGold);

                if (sold)
                {
                    LogDebug($"Fish sold before inventory add. dataId: {fish.DataId}, name: {fish.Name}, earnedGold: {earnedGold}");
                    RequestSave();
                }
                else
                {
                    LogWarning($"TryAddItem failed. Fish sale failed. dataId: {fish.DataId}, name: {fish.Name}");
                }

                return sold;
            }

            AddItemResult addResult = m_itemController.AddItem(context);
            if (addResult == AddItemResult.Added || addResult == AddItemResult.Merged)
            {
                ExpandInventoryIfNeeded(context.Type);
                NotifyInventoryChanged("[InventorySystem] AddItem is Succeeded", true);
                return true;
            }
            else if(addResult == AddItemResult.NoSpace)
            {
                ExpandInventoryIfNeeded(context.Type);
                addResult = m_itemController.AddItem(context);
                if(addResult == AddItemResult.Added || addResult == AddItemResult.Merged)
                {
                    ExpandInventoryIfNeeded(context.Type);
                    NotifyInventoryChanged("[InventorySystem] AddItem is Succeeded", true);
                    return true;
                }
            }

                return false;
        }

        public bool RemoveAt(ItemType itemType, int slotIndex, bool destroyEntity = false, bool requestSave = true)
        {
            if(!m_itemController.RemoveAt(itemType,slotIndex, destroyEntity))
            {
                LogWarning("[InventorySystem] 아이템 제거에 실패했습니다.");
                return false;
            }

            NotifyInventoryChanged($"Remove item / itemType: {itemType}, slotIndex: {slotIndex}", requestSave);
            return true;
        }

        public bool SwapSlots(ItemType itemType, int fromIndex, int toIndex)
        {
            if(!m_itemController.SwapSlots(itemType, fromIndex, toIndex))
            {
                LogWarning("[InventorySystem] 슬롯 교환에 실패했습니다.");
                return false;
            }

            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);
            NotifyInventoryChanged($"Swap slots / slotType: {slotType}, from: {fromIndex}, to: {toIndex}", true);

            return true;
        }

        public bool RemoveQuantityAt(ItemType itemType, int slotIndex, int amount, bool destroyEntityWhenZero = true, bool requestSave = true)
        {
            if(!m_itemController.RemoveQuantityAt(itemType, slotIndex, amount, out Entity entity, destroyEntityWhenZero))
            {
                LogWarning("[InventorySystem] 아이템 제거를 실패했습니다.");
                return false;
            }

            NotifyInventoryChanged($"Decrease quantity / dataId: {entity.DataId}, amount: {amount}", requestSave);
            return true;
        }

        /// <summary>
        /// 아이템 복수 개를 삭제해야 할 때 사용하는 함수
        /// </summary>
        public bool RemoveByHandles(IReadOnlyCollection<ItemQuantity> items, bool requestSave = true)
        {
            if (!m_itemController.RemoveByHandles(items))
            {
                LogWarning("[InventorySystem] 아이템 복수 개 삭제에 실패했습니다");
                return false;
            }
            
            NotifyInventoryChanged($"Remove item batch / requestCount: {items.Count}", requestSave);
            return true;
        }

        //item이 인벤토리에 몇 개 있는지 반환
        public int GetTotalQuantityByDataId(ItemType itemType, int dataId)
        {
            return m_itemController.GetTotalQuantityByDataId(itemType,dataId);
        }

        public bool ConsumeItemByDataId(ItemType itemType, int dataId, int amount)
        {
            bool result = m_itemController.ConsumeItemByDataId(itemType, dataId, amount);
            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);

            if (result)
            {
                LogDebug($"ConsumeItemByDataId success. slotType: {slotType}, itemType: {itemType}, dataId: {dataId}, amount: {amount}");
                NotifyInventoryChanged($"Consume item / slotType: {slotType}, itemType: {itemType}, dataId: {dataId}, amount: {amount}", true);
            }
            else
            {
                LogWarning($"ConsumeItemByDataId failed. slotType: {slotType}, itemType: {itemType}, dataId: {dataId}, amount: {amount}");
            }

            return result;
        }

        public object CaptureState()
        {
            return m_saveLoad.Capture(m_autoSellFilter.AutoSellEnabled, m_autoSellFilter.MaxAutoSellQuality, m_autoSellFilter.MaxAutoSellRarity);
        }

        public void RestoreState(object state)
        {
            LogDebug("RestoreState called.");

            if (state is not InventorySave save)
            {
                LogWarning($"RestoreState failed. Invalid state type: {state?.GetType().Name ?? "null"}");
                return;
            }

            m_loadedSave = save;

            LogDebug($"RestoreState pending. savedSlotCount: {save.slots.Count}");
        }

        private void NotifyInventoryChanged(string reason, bool requestSave)
        {
            LogDebug($"Inventory changed. Reason: {reason}");

            OnInventoryChanged?.Invoke();

            if (!requestSave)
            {
                return;
            }

            RequestSave();
        }

        public void RequestSave()
        {
            LogDebug("Save requested by inventory change.");
            OnInventorySaveRequested?.Invoke();
        }

        private void LogDebug(string message)
        {
            if (EnableInventoryDebugLog)
            {
                Debug.Log($"[InventorySystem] {message}");
            }
        }

        private void LogWarning(string message)
        {
            if (EnableInventoryDebugLog)
            {
                Debug.LogWarning($"[InventorySystem] {message}");
            }
        }

        
        
        private void RestoreLoadedSave()
        {
            InventoryAutoSellFilterState filterState = m_saveLoad.Restore(m_loadedSave, out int restoredCount, out int failedCount);
            m_autoSellFilter.Set(filterState.Enabled, filterState.MaxQuality, filterState.MaxRarity);

            NotifyInventoryChanged($"Restore inventory / restored: {restoredCount}, failed: {failedCount}", false);
        }

        private void ExpandInventoryIfNeeded(ItemType itemType)
        {
            ItemType slotType = InventorySlotStorage.NormalizeSlotType(itemType);

            if (!m_slotStorage.ExpandIfNeeded(slotType, out int previousSlotSize, out int nextSlotSize, out int remainingSlotCount))
            {
                return;
            }
        }

        public bool CanRemoveByHandle(EntityHandle handle, int amount)
        {
            return m_itemController.CanRemoveByHandle(handle, amount);
        }

        private int GetCurrentInventorySize()
        {
            if (m_playerSystem == null)
            {
                LogWarning($"PlayerSystem not found. fallback slot size: {FallbackInventorySize}");
                return FallbackInventorySize;
            }

            int size = m_playerSystem.BaseInventorySize;

            if (size <= 0)
            {
                LogWarning($"PlayerSystem.BaseInventorySize is invalid. value: {size}, fallback slot size: {FallbackInventorySize}");
                return FallbackInventorySize;
            }

            return size;
        }

        private bool ResizeFishSlots(int slotSize)
        {
            if (slotSize <= 0)
            {
                LogWarning($"ResizeFishSlots failed. Invalid slot size: {slotSize}");
                return false;
            }

            if (!m_slotStorage.ResizeFishSlots(slotSize))
            {
                LogWarning($"ResizeFishSlots failed. Fish exist outside next slot size. nextSize: {slotSize}");
                return false;
            }

            NotifyInventoryChanged($"Resize fish inventory / slotSize: {slotSize}", false);

            return true;
        }

        private void HandlePlayerStatChanged(EntityHandle playerHandle)
        {
            int nextInventorySize = GetCurrentInventorySize();

            bool result = ResizeFishSlots(nextInventorySize);

            LogDebug($"Fish inventory size stat changed. nextSize: {nextInventorySize}, resizeResult: {result}");
        }
    }
}