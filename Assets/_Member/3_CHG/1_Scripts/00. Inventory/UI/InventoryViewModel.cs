using System.Collections.Generic;
using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    public class InventoryViewModel : UIViewModelBase
    {
        private InventorySystem m_inventorySystem;

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

            Entity entity = EntityManager.Get(handle);

            if (entity == null)
            {
                return InventorySlotViewData.Empty(slotIndex, m_currentTab, isSelected, m_isMoveMode);
            }

            return CreateSlotViewData(slotIndex, handle, entity, isSelected);
        }

        private InventorySlotViewData CreateSlotViewData(int slotIndex, EntityHandle handle, Entity entity, bool isSelected)
        {
            ItemType actualItemType = GetItemType(entity);
            string iconKey = BuildIconKey(entity, actualItemType);

            InventorySlotViewData viewData = new InventorySlotViewData
            {
                SlotIndex = slotIndex,
                IsEmpty = false,

                SlotType = m_currentTab,

                ItemType = actualItemType,

                Handle = handle,

                ItemName = entity.Name,
                IconKey = iconKey,
                DataId = entity.DataId,

                Size = 0f,
                Quality = ItemQuality.OneStar,
                UpgradeLevel = 0,
                Quantity = 0,

                IsSelected = isSelected,

                // TEMP: Drag-Drop 도입 시 수정
                IsMoveSource = m_isMoveMode && isSelected,
                IsMoveMode = m_isMoveMode
            };

            ApplyRuntimeValues(viewData, entity);

            return viewData;
        }

        private void ApplyRuntimeValues(InventorySlotViewData viewData, Entity entity)
        {
            if (entity is Entity_Fish fish)
            {
                viewData.Size = fish.Size;
                viewData.Quality = fish.Quality;
                return;
            }

            if (entity is Entity_Equipment equipment)
            {
                viewData.UpgradeLevel = equipment.UpgradeLevel;
                return;
            }

            if (entity is Entity_Materials materials)
            {
                viewData.Quantity = materials.Quantity;
                return;
            }

            if (entity is Entity_Consumables consumables)
            {
                viewData.Quantity = consumables.Quantity;
            }
        }

        private ItemType GetItemType(Entity entity)
        {
            if (entity is Entity_Fish)
            {
                return ItemType.Fish;
            }

            if (entity is Entity_Equipment)
            {
                return ItemType.Equipment;
            }

            if (entity is Entity_Materials)
            {
                return ItemType.Materials;
            }

            if (entity is Entity_Consumables)
            {
                return ItemType.Consumables;
            }

            return m_currentTab;
        }

        private string BuildIconKey(Entity entity, ItemType itemType)
        {
            if (entity == null)
            {
                return string.Empty;
            }

            string dataClassName = GetDataClassName(itemType);

            if (string.IsNullOrEmpty(dataClassName))
            {
                return string.Empty;
            }

            // 현재 프로젝트의 AssetKey 규칙:
            // {데이터클래스명}_{DataId}_{용도}
            return $"{dataClassName}_{entity.DataId}_Icon";
        }

        private string GetDataClassName(ItemType itemType)
        {
            switch (itemType)
            {
                case ItemType.Fish:
                    return nameof(ItemData_Fish);

                case ItemType.Equipment:
                    return nameof(ItemData_Equipment);

                case ItemType.Materials:
                    return nameof(ItemData_Materials);

                case ItemType.Consumables:
                    return nameof(ItemData_Consumables);

                default:
                    return string.Empty;
            }
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