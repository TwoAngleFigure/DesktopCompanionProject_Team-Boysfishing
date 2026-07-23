using System;
using System.Collections.Generic;
using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;

namespace DesktopCompanion.Views
{
    public class InventorySellViewModel : UIViewModelBase
    {
        private InventorySystem m_inventorySystem;
        private ShopSystem m_shopSystem;

        private readonly Dictionary<EntityHandle, int> m_selectedSellAmounts = new();
        private EntityHandle m_pendingSellHandle;

        public readonly BindableProperty<bool> IsSellMode = new(false);

        public readonly BindableProperty<bool> IsQuantityPopupOpen = new(false);
        public readonly BindableProperty<string> QuantityItemName = new(string.Empty);
        public readonly BindableProperty<int> QuantityAmount = new(1);
        public readonly BindableProperty<int> QuantityMaxAmount = new(1);

        public readonly BindableProperty<bool> IsConfirmPopupOpen = new(false);
        public readonly BindableProperty<int> SelectedItemCount = new(0);
        public readonly BindableProperty<int> SelectedTotalAmount = new(0);
        public readonly BindableProperty<int> ExpectedGold = new(0);

        public readonly BindableProperty<bool> IsFilterPopupOpen = new(false);
        public readonly BindableProperty<bool> AutoSellEnabled = new(false);
        public readonly BindableProperty<ItemQuality> AutoSellMaxQuality = new(ItemQuality.OneStar);
        public readonly BindableProperty<ItemRarity> AutoSellMaxRarity = new(ItemRarity.Normal);

        public RelayCommand SellButtonCommand { get; private set; }
        public RelayCommand<InventorySlotViewData> SelectSellItemCommand { get; private set; }

        public RelayCommand<int> SetQuantityCommand { get; private set; }
        public RelayCommand ApplyQuantityCommand { get; private set; }
        public RelayCommand CancelQuantityCommand { get; private set; }

        public RelayCommand ConfirmSellCommand { get; private set; }
        public RelayCommand CancelSellCommand { get; private set; }
        public RelayCommand ResetSellStateCommand { get; private set; }

        public RelayCommand OpenFilterCommand { get; private set; }
        public RelayCommand CloseFilterCommand { get; private set; }
        public RelayCommand<ItemQuality> SetAutoSellQualityCommand { get; private set; }
        public RelayCommand<ItemRarity> SetAutoSellRarityCommand { get; private set; }
        public RelayCommand ApplyAutoSellFilterCommand { get; private set; }
        public RelayCommand DisableAutoSellCommand { get; private set; }

        public event Action OnSellSelectionChanged;

        public override void Bind()
        {
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();
            m_shopSystem = SystemManager.GetSystem<ShopSystem>();

            SellButtonCommand = new RelayCommand(HandleSellButton);
            SelectSellItemCommand = new RelayCommand<InventorySlotViewData>(SelectSellItem);

            SetQuantityCommand = new RelayCommand<int>(SetQuantity);
            ApplyQuantityCommand = new RelayCommand(ApplyQuantity);
            CancelQuantityCommand = new RelayCommand(CloseQuantityPopup);

            ConfirmSellCommand = new RelayCommand(ConfirmSell);
            CancelSellCommand = new RelayCommand(CancelSell);
            ResetSellStateCommand = new RelayCommand(ResetSellState);

            OpenFilterCommand = new RelayCommand(OpenFilter);
            CloseFilterCommand = new RelayCommand(CloseFilter);
            SetAutoSellQualityCommand = new RelayCommand<ItemQuality>(SetAutoSellQuality);
            SetAutoSellRarityCommand = new RelayCommand<ItemRarity>(SetAutoSellRarity);
            ApplyAutoSellFilterCommand = new RelayCommand(ApplyAutoSellFilter);
            DisableAutoSellCommand = new RelayCommand(DisableAutoSell);

            if (m_inventorySystem != null)
            {
                m_inventorySystem.OnInventoryChanged += HandleInventoryChanged;
                m_inventorySystem.OnAutoSellFilterChanged += HandleAutoSellFilterChanged;
                RefreshAutoSellFilter();
            }

            RefreshSellSummary();
        }

        public override void Unbind()
        {
            if (m_inventorySystem != null)
            {
                m_inventorySystem.OnInventoryChanged -= HandleInventoryChanged;
                m_inventorySystem.OnAutoSellFilterChanged -= HandleAutoSellFilterChanged;
            }

            m_selectedSellAmounts.Clear();
            m_pendingSellHandle = default;

            IsSellMode.Value = false;
            IsQuantityPopupOpen.Value = false;
            IsConfirmPopupOpen.Value = false;
            IsFilterPopupOpen.Value = false;

            QuantityItemName.Value = string.Empty;
            QuantityAmount.Value = 1;
            QuantityMaxAmount.Value = 1;

            SelectedItemCount.Value = 0;
            SelectedTotalAmount.Value = 0;
            ExpectedGold.Value = 0;

            m_inventorySystem = null;
            m_shopSystem = null;
            OnSellSelectionChanged = null;
        }

        public int GetSelectedAmount(EntityHandle handle)
        {
            return m_selectedSellAmounts.TryGetValue(handle, out int amount)
                ? amount
                : 0;
        }

        private void HandleSellButton()
        {
            if (IsQuantityPopupOpen.Value || IsConfirmPopupOpen.Value || IsFilterPopupOpen.Value)
            {
                return;
            }

            if (!IsSellMode.Value)
            {
                EnterSellMode();
                return;
            }

            CleanupInvalidSelections();

            if (m_selectedSellAmounts.Count == 0)
            {
                ResetSellState();
                return;
            }

            RefreshSellSummary();
            IsConfirmPopupOpen.Value = true;
        }

        private void EnterSellMode()
        {
            m_selectedSellAmounts.Clear();
            m_pendingSellHandle = default;

            IsSellMode.Value = true;
            IsQuantityPopupOpen.Value = false;
            IsConfirmPopupOpen.Value = false;
            IsFilterPopupOpen.Value = false;

            RefreshSellSelection();
        }

        private void SelectSellItem(InventorySlotViewData slotData)
        {
            if (!IsSellMode.Value
                || IsQuantityPopupOpen.Value
                || IsConfirmPopupOpen.Value
                || slotData == null
                || slotData.IsEmpty)
            {
                return;
            }

            if (m_selectedSellAmounts.Remove(slotData.Handle))
            {
                RefreshSellSelection();
                return;
            }

            if (m_shopSystem == null || m_shopSystem.CalculateSellGold(slotData.Handle, 1) <= 0)
            {
                return;
            }

            switch (slotData.ItemType)
            {
                case ItemType.Fish:
                case ItemType.Equipment:
                    m_selectedSellAmounts.Add(slotData.Handle, 1);
                    RefreshSellSelection();
                    break;

                case ItemType.Materials:
                case ItemType.Consumables:
                    OpenQuantityPopup(slotData);
                    break;
            }
        }

        private void OpenQuantityPopup(InventorySlotViewData slotData)
        {
            if (slotData.Quantity <= 0)
            {
                return;
            }

            m_pendingSellHandle = slotData.Handle;

            QuantityItemName.Value = slotData.ItemName;
            QuantityMaxAmount.Value = slotData.Quantity;
            QuantityAmount.Value = 1;

            IsQuantityPopupOpen.Value = true;
        }

        private void SetQuantity(int amount)
        {
            int maxAmount = Math.Max(1, QuantityMaxAmount.Value);
            QuantityAmount.Value = Math.Max(1, Math.Min(amount, maxAmount));
        }

        private void ApplyQuantity()
        {
            if (!IsQuantityPopupOpen.Value || m_inventorySystem == null)
            {
                return;
            }

            int amount = QuantityAmount.Value;

            if (!m_inventorySystem.CanRemoveByHandle(m_pendingSellHandle, amount))
            {
                CloseQuantityPopup();
                return;
            }

            m_selectedSellAmounts[m_pendingSellHandle] = amount;

            CloseQuantityPopup();
            RefreshSellSelection();
        }

        private void CloseQuantityPopup()
        {
            m_pendingSellHandle = default;

            QuantityItemName.Value = string.Empty;
            QuantityAmount.Value = 1;
            QuantityMaxAmount.Value = 1;

            IsQuantityPopupOpen.Value = false;
        }

        private void ConfirmSell()
        {
            if (!IsSellMode.Value || m_shopSystem == null)
            {
                return;
            }

            CleanupInvalidSelections();

            List<SellRequest> requests = BuildSellRequests();

            if (requests.Count == 0)
            {
                ResetSellState();
                return;
            }

            if (!m_shopSystem.SellItems(requests, out _))
            {
                IsConfirmPopupOpen.Value = false;
                CleanupInvalidSelections();
                RefreshSellSelection();
                return;
            }

            ResetSellState();
        }

        private void CancelSell()
        {
            ResetSellState();
        }

        private void ResetSellState()
        {
            m_selectedSellAmounts.Clear();
            m_pendingSellHandle = default;

            IsSellMode.Value = false;
            IsQuantityPopupOpen.Value = false;
            IsConfirmPopupOpen.Value = false;

            QuantityItemName.Value = string.Empty;
            QuantityAmount.Value = 1;
            QuantityMaxAmount.Value = 1;

            RefreshSellSelection();
        }

        private void HandleInventoryChanged()
        {
            CleanupInvalidSelections();
            RefreshSellSelection();
        }

        private void CleanupInvalidSelections()
        {
            if (m_inventorySystem == null)
            {
                m_selectedSellAmounts.Clear();
                return;
            }

            List<EntityHandle> handles = new(m_selectedSellAmounts.Keys);

            for (int i = 0; i < handles.Count; i++)
            {
                EntityHandle handle = handles[i];
                int amount = m_selectedSellAmounts[handle];

                if (!m_inventorySystem.CanRemoveByHandle(handle, amount))
                {
                    m_selectedSellAmounts.Remove(handle);
                }
            }
        }

        private List<SellRequest> BuildSellRequests()
        {
            List<SellRequest> requests = new();

            foreach (KeyValuePair<EntityHandle, int> pair in m_selectedSellAmounts)
            {
                requests.Add(new SellRequest(pair.Key, pair.Value));
            }

            return requests;
        }

        private void RefreshSellSelection()
        {
            RefreshSellSummary();
            OnSellSelectionChanged?.Invoke();
        }

        private void RefreshSellSummary()
        {
            int totalAmount = 0;

            foreach (int amount in m_selectedSellAmounts.Values)
            {
                totalAmount += amount;
            }

            List<SellRequest> requests = BuildSellRequests();

            SelectedItemCount.Value = m_selectedSellAmounts.Count;
            SelectedTotalAmount.Value = totalAmount;
            ExpectedGold.Value = m_shopSystem != null
                ? m_shopSystem.CalculateSellGold(requests)
                : 0;
        }

        private void OpenFilter()
        {
            if (IsSellMode.Value || IsQuantityPopupOpen.Value || IsConfirmPopupOpen.Value)
            {
                return;
            }

            RefreshAutoSellFilter();
            IsFilterPopupOpen.Value = true;
        }

        private void CloseFilter()
        {
            IsFilterPopupOpen.Value = false;
        }

        private void SetAutoSellQuality(ItemQuality quality)
        {
            AutoSellMaxQuality.Value = quality;
        }

        private void SetAutoSellRarity(ItemRarity rarity)
        {
            AutoSellMaxRarity.Value = rarity;
        }

        private void ApplyAutoSellFilter()
        {
            if (m_inventorySystem == null)
            {
                return;
            }

            m_inventorySystem.SetAutoSellFilter(true, AutoSellMaxQuality.Value, AutoSellMaxRarity.Value);
            IsFilterPopupOpen.Value = false;
        }

        private void DisableAutoSell()
        {
            if (m_inventorySystem == null)
            {
                return;
            }

            m_inventorySystem.SetAutoSellFilter(false, AutoSellMaxQuality.Value, AutoSellMaxRarity.Value);
            IsFilterPopupOpen.Value = false;
        }

        private void HandleAutoSellFilterChanged()
        {
            RefreshAutoSellFilter();
        }

        private void RefreshAutoSellFilter()
        {
            if (m_inventorySystem == null)
            {
                return;
            }

            AutoSellEnabled.Value = m_inventorySystem.AutoSellEnabled;
            AutoSellMaxQuality.Value = m_inventorySystem.MaxAutoSellQuality;
            AutoSellMaxRarity.Value = m_inventorySystem.MaxAutoSellRarity;
        }
    }
}
