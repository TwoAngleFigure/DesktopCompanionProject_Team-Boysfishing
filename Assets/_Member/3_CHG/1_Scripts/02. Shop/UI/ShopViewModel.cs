using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using System;
using System.Collections.Generic;
using System.Text;


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
                
                if(product != null && product.Price > 0) 
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
                m_shopSystem.CalculateItemPrice(item.BasePrice),
                GetTypeText(item),
                GetDescription(item)
                );

            if (item.Type == ItemType.Equipment)
                result.IsStackable = false;
            else 
                result.IsStackable = true;

            return result;
        }

        private string GetTypeText(ItemData item)
        {
            switch(item.Type)
            {
                case ItemType.Equipment:
                    return "장비";
                case ItemType.Materials:
                    return "재료";
                case ItemType.Consumables:
                    return "소모품";
                default:
                    return "";
            }
        }

        private string GetDescription(ItemData item)
        {
            switch (item.Type)
            {
                case ItemType.Consumables:
                    return BuildConsumableDetailText((ItemData_Consumables)item);
                default:
                    return String.Empty;
                    
            }
        }

        #region TextBuild
        private string BuildConsumableDetailText(ItemData_Consumables itemData)
        {
            StringBuilder builder = new();

            StringBuilder effectBuilder = new();

            builder.Append($"{itemData.Tier} 티어");
            builder.AppendLine();

            AppendModifiers(effectBuilder, itemData.Modifiers);

            if (itemData.SummonTarget != null)
            {
                if (effectBuilder.Length > 0)
                {
                    effectBuilder.AppendLine();
                }

                effectBuilder.Append(itemData.SummonTarget.Name);
                effectBuilder.Append(" 소환");
            }

            if (effectBuilder.Length > 0)
            {
                builder.Append(effectBuilder);
            }
            else
            {
                builder.Append("효과 정보 없음");
            }

            return builder.ToString();
        }

        private void AppendModifiers(StringBuilder builder, StatModifier[] modifiers)
        {
            if (modifiers == null || modifiers.Length == 0)
            {
                return;
            }

            for (int i = 0; i < modifiers.Length; i++)
            {
                StatModifier modifier = modifiers[i];

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(GetPlayerStatText(modifier.Stat));
                builder.Append(' ');
                builder.Append(GetModifierValueText(modifier.Stat, modifier.Value));
            }
        }

        private string GetPlayerStatText(PlayerStat stat)
        {
            switch (stat)
            {
                case PlayerStat.DamagePerClick:
                    return "클릭 공격력";

                case PlayerStat.ManualDamagePerHitMultiply:
                    return "수동 공격 배율";

                case PlayerStat.BattleTimeVariable:
                    return "전투 시간";

                case PlayerStat.CriticalChance:
                    return "크리티컬 확률";

                case PlayerStat.CriticalMultiply:
                    return "크리티컬 배율";

                case PlayerStat.AutoBattleCooltime:
                    return "자동 낚시 간격";

                case PlayerStat.AutoSpeedPerTime:
                    return "자동 공격 속도";

                case PlayerStat.AutoDamagePerHitMultiply:
                    return "자동 공격 배율";

                case PlayerStat.MapMovementSpeedPerTime:
                    return "이동 속도";

                case PlayerStat.InventorySize:
                    return "인벤토리 크기";

                case PlayerStat.ProbabilityAtFishSize:
                    return "높은 성급 등장 확률";

                case PlayerStat.ProbabilityAtFishRarity:
                    return "높은 등급 등장 확률";

                case PlayerStat.GoldGettingMultiply:
                    return "골드 획득 배율";

                default:
                    return stat.ToString();
            }
        }

        private string GetModifierValueText(PlayerStat stat, float value)
        {
            if (IsPercentStat(stat))
            {
                return $"{GetSignedNumber(value * 100f)}%";
            }

            return GetSignedNumber(value);
        }

        private bool IsPercentStat(PlayerStat stat)
        {
            return stat == PlayerStat.CriticalChance
                || stat == PlayerStat.ProbabilityAtFishSize
                || stat == PlayerStat.ProbabilityAtFishRarity;
        }

        private string GetSignedNumber(float value)
        {
            return value >= 0f
                ? $"+{value:0.##}"
                : $"{value:0.##}";
        }
        #endregion

        private string BuildIconKey(ItemData item)
        {
            if (item == null)
                return null;
            return AssetKeys.Of(item, AssetUsage.Icon);
        }
    }
}