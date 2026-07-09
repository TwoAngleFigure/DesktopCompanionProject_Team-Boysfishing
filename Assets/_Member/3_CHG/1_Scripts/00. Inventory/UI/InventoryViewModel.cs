using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using System.Collections.Generic;

namespace DesktopCompanion.Views
{
    public class InventoryViewModel : UIViewModelBase
    {
        private InventorySystem m_inventorySystem;

        private EntityManager m_entityManager;
        private DataManager m_dataManager;

        private ItemType m_currentTab = ItemType.Fish;

        private int m_selectedSlotIndex = -1;

        // TEMP: Drag-Drop 도입 시 수정
        private bool m_isMoveMode;

        // ViewModel -> View
        public readonly BindableProperty<List<InventorySlotViewData>> Slots = new(new List<InventorySlotViewData>());
        public readonly BindableProperty<InventorySlotViewData> SelectedSlot = new(null);
        public readonly BindableProperty<bool> IsMoveMode = new(false);
        public readonly BindableProperty<ItemType> CurrentTab = new(ItemType.Fish);

        // View -> ViewModel
        public RelayCommand<ItemType> SelectTabCommand { get; private set; }
        public RelayCommand<int> SelectSlotCommand { get; private set; }

        // TEMP: Drag-Drop 도입 시 수정
        public RelayCommand MoveButtonCommand { get; private set; }

        public void InjectManagers(EntityManager entityManager, DataManager dataManager)
        {
            m_entityManager = entityManager;
            m_dataManager = dataManager;
        }

        public override void Bind()
        {
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();

            SelectTabCommand = new RelayCommand<ItemType>(SelectTab);
            SelectSlotCommand = new RelayCommand<int>(SelectSlot);

            // TEMP: Drag-Drop 도입 시 수정
            MoveButtonCommand = new RelayCommand(ToggleMoveMode);

            if (m_inventorySystem != null)
            {
                m_inventorySystem.OnInventoryChanged += HandleInventoryChanged;
            }

            RefreshSlots();
            RefreshSelectedSlot();
        }

        public override void Unbind()
        {
            if (m_inventorySystem != null)
            {
                m_inventorySystem.OnInventoryChanged -= HandleInventoryChanged;
            }

            m_inventorySystem = null;
            m_entityManager = null;
            m_dataManager = null;
        }

        private void HandleInventoryChanged()
        {
            RefreshSlots();
            RefreshSelectedSlot();
        }

        private void SelectTab(ItemType tab)
        {
            m_currentTab = NormalizeTab(tab);
            CurrentTab.Value = m_currentTab;

            m_selectedSlotIndex = -1;
            m_isMoveMode = false;
            IsMoveMode.Value = false;

            RefreshSlots();
            RefreshSelectedSlot();
        }

        private void SelectSlot(int slotIndex)
        {
            // TEMP: Drag-Drop 도입 시 수정
            if (m_isMoveMode)
            {
                TrySwapSelectedSlot(slotIndex);
                return;
            }

            m_selectedSlotIndex = slotIndex;

            RefreshSlots();
            RefreshSelectedSlot();
        }

        private void ToggleMoveMode()
        {
            if (m_selectedSlotIndex < 0)
            {
                return;
            }

            InventorySlotViewData selected = ResolveSlotData(m_selectedSlotIndex);

            if (selected == null || selected.IsEmpty)
            {
                return;
            }

            // TEMP: Drag-Drop 도입 시 수정
            m_isMoveMode = !m_isMoveMode;
            IsMoveMode.Value = m_isMoveMode;

            RefreshSlots();
        }

        private void TrySwapSelectedSlot(int targetSlotIndex)
        {
            int fromIndex = m_selectedSlotIndex;
            int toIndex = targetSlotIndex;

            // TEMP: Drag-Drop 도입 시 수정
            if (fromIndex == toIndex)
            {
                m_isMoveMode = false;
                IsMoveMode.Value = false;

                RefreshSlots();
                return;
            }

            bool success = false;

            if (m_inventorySystem != null)
            {
                success = m_inventorySystem.SwapSlots(m_currentTab, fromIndex, toIndex);
            }

            m_isMoveMode = false;
            IsMoveMode.Value = false;

            m_selectedSlotIndex = success ? toIndex : -1;

            RefreshSlots();
            RefreshSelectedSlot();
        }

        private void RefreshSlots()
        {
            List<InventorySlotViewData> result = new();

            int slotCount = 0;

            if (m_inventorySystem != null)
            {
                slotCount = m_inventorySystem.GetMaxSlotCount(m_currentTab);
            }

            for (int i = 0; i < slotCount; i++)
            {
                result.Add(ResolveSlotData(i));
            }

            Slots.Value = result;
        }

        private void RefreshSelectedSlot()
        {
            if (m_selectedSlotIndex < 0)
            {
                SelectedSlot.Value = null;
                return;
            }

            SelectedSlot.Value = ResolveSlotData(m_selectedSlotIndex);
        }

        private InventorySlotViewData ResolveSlotData(int slotIndex)
        {
            bool isSelected = slotIndex == m_selectedSlotIndex;

            if (m_inventorySystem == null)
            {
                return InventorySlotViewData.Empty(slotIndex, m_currentTab, isSelected, m_isMoveMode);
            }

            bool hasHandle = m_inventorySystem.GetHandleAt(m_currentTab, slotIndex, out EntityHandle handle);

            if (!hasHandle)
            {
                return InventorySlotViewData.Empty(slotIndex, m_currentTab, isSelected, m_isMoveMode);
            }

            if (m_entityManager == null)
            {
                return InventorySlotViewData.Empty(slotIndex, m_currentTab, isSelected, m_isMoveMode);
            }

            Entity entity = m_entityManager.Get(handle);

            if (entity == null)
            {
                return InventorySlotViewData.Empty(slotIndex, m_currentTab, isSelected, m_isMoveMode);
            }

            bool success = BuildSlotViewData( slotIndex, handle, entity, isSelected, out InventorySlotViewData viewData);

            if (!success)
            {
                return InventorySlotViewData.Empty(slotIndex, m_currentTab, isSelected, m_isMoveMode);
            }

            return viewData;
        }

        private bool BuildSlotViewData(int slotIndex, EntityHandle handle, Entity entity, bool isSelected, out InventorySlotViewData viewData)
        {
            viewData = null;

            if (entity is Entity_Fish fish)
            {
                ItemData_Fish itemData = m_dataManager != null ? m_dataManager.GetData<ItemData_Fish>(entity.DataId) : null;

                viewData = CreateBaseSlotData(slotIndex, handle, ItemType.Fish, itemData, entity, isSelected);

                viewData.Size = fish.Size;
                viewData.Quality = fish.Quality;
                return true;
            }

            if (entity is Entity_Equipment equipment)
            {
                ItemData_Equipment itemData = m_dataManager != null ? m_dataManager.GetData<ItemData_Equipment>(entity.DataId) : null;

                viewData = CreateBaseSlotData(slotIndex, handle, ItemType.Equipment, itemData, entity, isSelected);

                viewData.UpgradeLevel = equipment.UpgradeLevel;
                return true;
            }

            if (entity is Entity_Materials materials)
            {
                ItemData_Materials itemData = m_dataManager != null ? m_dataManager.GetData<ItemData_Materials>(entity.DataId) : null;

                viewData = CreateBaseSlotData(slotIndex, handle, ItemType.Materials, itemData, entity, isSelected);

                viewData.Quantity = materials.Quantity;
                return true;
            }

            if (entity is Entity_Consumables consumables)
            {
                ItemData_Consumables itemData = m_dataManager != null ? m_dataManager.GetData<ItemData_Consumables>(entity.DataId) : null;

                viewData = CreateBaseSlotData(slotIndex, handle, ItemType.Consumables, itemData, entity, isSelected);

                viewData.Quantity = consumables.Quantity;
                return true;
            }

            return false;
        }

        private InventorySlotViewData CreateBaseSlotData(int slotIndex, EntityHandle handle, 
            ItemType actualItemType, ItemData itemData, Entity entity, bool isSelected)
        {
            string itemName = string.Empty;

            if (itemData != null)
            {
                itemName = itemData.Name;
            }
            else if (entity != null)
            {
                itemName = entity.Name;
            }

            return new InventorySlotViewData
            {
                SlotIndex = slotIndex,
                IsEmpty = false,
                SlotType = m_currentTab,
                ItemType = actualItemType,
                Handle = handle,
                ItemData = itemData,
                ItemName = itemName,
                DataId = entity != null ? entity.DataId : 0,
                Size = 0f,
                Quality = ItemQuality.OneStar,
                UpgradeLevel = 0,
                Quantity = 0,
                IsSelected = isSelected,
                // TEMP: Drag - Drop 도입 시 수정
                IsMoveSource = m_isMoveMode && isSelected,
                IsMoveMode = m_isMoveMode
            };
        }

        private ItemType NormalizeTab(ItemType tab)
        {
            if (tab == ItemType.Consumables)
            {
                return ItemType.Materials;
            }

            return tab;
        }
    }
}