using DesktopCompanion.Data;
using DesktopCompanion.Systems;
using System.Collections.Generic;
using System.Diagnostics;


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
        public RelayCommand BuyCommand;

        public override void Bind()
        {
            m_shopSystem = SystemManager.GetSystem<ShopSystem>();
            m_currencySystem = SystemManager.GetSystem<CurrencySystem>();

            SelectProductCommand = new RelayCommand<int>(SelectProduct);
            BuyCommand = new RelayCommand(BuyItem);

            if (m_currencySystem != null)
            {
                m_currencySystem.OnGoldChanged += HandleGoldChanged;
                Gold.Value = m_currencySystem.CurrentGold;
            }

            RefreshProducts();
        }

        public override void Unbind()
        {
            if(m_currencySystem != null)
                m_currencySystem.OnGoldChanged -= HandleGoldChanged;

            m_shopSystem = null;
            m_currencySystem = null;
        }

        private void SelectProduct(int dataId)
        {

        }

        private void BuyItem()
        {

        }

        private void HandleGoldChanged(int currentGold) 
        {
            Gold.Value = currentGold;
        }

        private void RefreshProducts()
        {
            List<ShopProductViewData> products = new();
            List<ItemData> data = m_shopSystem.GetBuyableItems();

            for(int i = 0; i < data.Count; i++)
            {
                ItemData item = data[i];
                products.Add(CreateProductViewData(item));
            }

            Products.Value = products;
        }

        private ShopProductViewData CreateProductViewData(ItemData item)
        {
            if (m_shopSystem == null || item.Type == ItemType.Fish)
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

