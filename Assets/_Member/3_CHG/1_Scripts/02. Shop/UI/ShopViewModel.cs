using DesktopCompanion.Data;
using DesktopCompanion.Systems;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private void SelectProduct(int productId)
        {
            foreach(ShopProductViewData data in Products.Value)
            {
                if (data.ProductId == productId)
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
                if (m_shopSystem.BuyItem(SelectedProduct.Value.ProductId, amount))
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
            IReadOnlyList<ShopProducts> data = m_shopSystem.Products;

            foreach(ShopProducts product in data)
            {
                if(!m_shopSystem.FindProductItemData(product, out ItemData item) || item == null)
                    continue;

                ShopProductViewData productData;
                productData = CreateProductViewData(product, item , m_shopSystem.IsBuyableProduct(product));
                if(productData == null)
                    continue;

                products.Add(productData);
            }

            products.Sort((a, b) =>
            {
                int buyableCompare = b.IsBuyable.CompareTo(a.IsBuyable);

                if (buyableCompare != 0)
                {
                    return buyableCompare;
                }

                int tierCompare = a.Tier.CompareTo(b.Tier);

                if (tierCompare != 0)
                {
                    return tierCompare;
                }

                return a.ProductId.CompareTo(b.ProductId);
            });


            Products.Value = products;
        }

        private ShopProductViewData CreateProductViewData(ShopProducts product, ItemData item, bool isBuyable)
        {
            if (m_shopSystem == null || product == null || product.ItemType == ItemType.Fish)
                return null;

            string iconkey = BuildIconKey(item);

            ShopProductViewData result = new ShopProductViewData(
                product.ID,
                product.BaseId,
                item.Name,
                product.ItemType,
                iconkey,
                product.Tier,
                product.Price,
                GetTypeText(product),
                GetDescription(item),
                isBuyable,
                product.IsSummon
                );

            if (product.ItemType == ItemType.Equipment)
                result.IsStackable = false;
            else 
                result.IsStackable = true;

            return result;
        }

        #region TextBuild
        private string GetTypeText(ShopProducts product)
        {
            switch(product.ItemType)
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

        private string BuildConsumableDetailText(ItemData_Consumables itemData)
        {
            StringBuilder builder = new();

            StringBuilder effectBuilder = new();

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
                builder.Append("장착 효과 없음");
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

        /// <summary>
        /// 표시용 데이터가 가리키는 아이템 정의를 찾는다.
        ///
        /// 조회에 필요한 것은 종류와 baseId뿐이라 ShopProducts를 새로 만들지 않는다.
        /// ShopProducts는 GameData(ScriptableObject)라 new로 만들면 Unity가 런타임에 거부한다
        /// — 정의는 JsonDataLoader가 CreateInstance로만 만든다.
        /// </summary>
        public ItemData FindData(ShopProductViewData productViewData)
        {
            if (productViewData == null)
                return null;

            return m_shopSystem.FindItemDataById(productViewData.ItemType, productViewData.BaseId);
        }

        public ItemData FindDataById(ItemType type, int id)
        {
            return m_shopSystem.FindItemDataById(type, id);
        }
    }
}