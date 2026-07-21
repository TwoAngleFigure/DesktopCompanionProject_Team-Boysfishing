using DesktopCompanion.Data;
using DesktopCompanion.Systems;
using System;
using System.Collections.Generic;


namespace DesktopCompanion.Views
{
    public class ShopViewModel : UIViewModelBase
    {
        private ShopSystem m_shopSystem;
        private CurrencySystem m_currencySystem;

        public readonly BindableProperty<int> Gold = new(0);
        public readonly BindableProperty<List<ShopProductViewData>> Products = new(new List<ShopProductViewData>());
        public readonly BindableProperty<ShopProductViewData> SelectedProduct = new(null);

        public RelayCommand<int> SelectProductCommand;
        public RelayCommand<int> BuyCommand;

        public event Action OnBuySucceeded;

        public override void Bind()
        {
            m_shopSystem = SystemManager.GetSystem<ShopSystem>();
            m_currencySystem = SystemManager.GetSystem<CurrencySystem>();

            SelectProductCommand = new RelayCommand<int>(SelectProduct);
            BuyCommand = new RelayCommand<int>(BuyItem);

            if (m_currencySystem != null)
            {
                m_currencySystem.OnGoldChanged += HandleGoldChanged;
                Gold.Value = m_currencySystem.CurrentGold;
            }

            RefreshProducts();
            SelectedProduct.Value = null;
        }

        public override void Unbind()
        {
            if(m_currencySystem != null)
                m_currencySystem.OnGoldChanged -= HandleGoldChanged;

            SelectedProduct.Value = null;
            m_shopSystem = null;
            m_currencySystem = null;
        }

        private void SelectProduct(int dataId)
        {
            foreach(ShopProductViewData data in Products.Value)
            {
                if (data.DataId == dataId)
                {
                    SelectedProduct.Value = data;
                    return;
                }
            }

            SelectedProduct.Value = null;
        }

        private void BuyItem(int amount)
        {
            if (SelectedProduct.Value == null)
                return;

            if (amount <= 0)
                return;

            if(m_shopSystem != null)
            {
                if (m_shopSystem.BuyItem(SelectedProduct.Value.ItemType, SelectedProduct.Value.DataId, amount))
                    OnBuySucceeded?.Invoke();

            }
        }

        private void HandleGoldChanged(int currentGold) 
        {
            Gold.Value = currentGold;
        }

        private void RefreshProducts()
        {
            List<ShopProductViewData> products = new();
            IReadOnlyList<ItemData> data = new List<ItemData>();
            if(m_shopSystem != null)
            {
                data = m_shopSystem.GetBuyableItems();
            }

            for(int i = 0; i < data.Count; i++)
            {
                ItemData item = data[i];
                ShopProductViewData product = CreateProductViewData(item);
                
                if(product != null) 
                    products.Add(product);
            }

            Products.Value = products;
        }

        private ShopProductViewData CreateProductViewData(ItemData item)
        {
            if (m_shopSystem == null || item == null || item.Type == ItemType.Fish)
                return null;

            string iconkey = BuildIconKey(item);

            ShopProductViewData result = new ShopProductViewData(
                item.ID,
                item.Name,
                item.Type,
                iconkey,
                item.Tier,
                m_shopSystem.CalculateItemPrice(item.BasePrice)
                );

            if (item.Type == ItemType.Equipment)
                result.IsStackable = false;
            else 
                result.IsStackable = true;

            return result;
        }

        private string BuildIconKey(ItemData item)
        {
            if (item == null)
                return null;
            return AssetKeys.Of(item, AssetUsage.Icon);
        }
    }
}

