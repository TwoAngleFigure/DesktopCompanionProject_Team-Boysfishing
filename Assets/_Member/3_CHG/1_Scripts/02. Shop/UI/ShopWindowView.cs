using DesktopCompanion.Data;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class ShopWindowView : UIWindowBase, IItemTooltipSource
    {
        [Header("UI")]
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TMP_Text m_goldText;
        [SerializeField] private Sprite m_EquipmentSlotBackground;
        [SerializeField] private Sprite m_MaterialsSlotBackground;

        [Header("Product")]
        [SerializeField] private Transform m_productRoot;
        [SerializeField] private ShopProductView m_productViewPrefab;

        [Header("Buy Popup")]
        [SerializeField] private GameObject m_buyPopup;
        [SerializeField] private TMP_Text m_buyItemName;
        [SerializeField] private TMP_Text m_requireGoldText;
        [SerializeField] private TMP_InputField m_buyAmountInput;
        [SerializeField] private Slider m_buyAmountSlider;
        [SerializeField] private Button m_maxAmountButton;
        [SerializeField] private Button m_buyButton;
        [SerializeField] private Button m_cancelButton;

        private int m_buyAmount = 1;

        private readonly ShopViewModel m_vm = new();
        private readonly List<ShopProductView> m_products = new();

        private readonly Dictionary<string, Sprite> m_runtimeIconCache = new();

        public ItemTooltipData BuildTooltip(ItemSlotVD vd)
        {
            ItemTooltipData data = null;
            if (vd != null)
            {
                ItemData itemData = null;
                switch (vd.Kind)
                {
                    case TooltipItemKind.Equipment:
                        itemData = m_vm.FindDataById(ItemType.Equipment, vd.DataId);
                        break;
                    case TooltipItemKind.Materials:
                        itemData = m_vm.FindDataById(ItemType.Materials, vd.DataId);
                        break;
                    case TooltipItemKind.Consumables:
                        itemData = m_vm.FindDataById(ItemType.Consumables, vd.DataId);
                        break;
                }

                data = ItemTooltipBuilder.FromData(itemData);
            }
            return data;
        }

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            m_vm.Gold.Bind(RefreshGoldText);
            m_vm.Products.Bind(RefreshProductView);
            m_vm.SelectedProduct.Bind(OnSelectedProductChanged);
            m_vm.OnBuySucceeded += OnBuySucceeded;

            m_buyAmountSlider?.onValueChanged.AddListener(OnBuyAmountSliderChanged);
            m_buyAmountInput?.onEndEdit.AddListener(OnBuyAmountInputEdit);
            m_maxAmountButton?.onClick.AddListener(OnClickMaxAmountButton);
            m_buyButton?.onClick.AddListener(OnClickBuyButton);
            m_cancelButton?.onClick.AddListener(OnClickCancelButton);
            m_closeButton?.onClick.AddListener(OnClickCloseButton);

            m_buyAmountSlider.wholeNumbers = true;
        }

        public override void Unbind()
        {
            m_closeButton?.onClick.RemoveListener(OnClickCloseButton);
            m_cancelButton?.onClick.RemoveListener(OnClickCancelButton);
            m_buyButton?.onClick.RemoveListener(OnClickBuyButton);
            m_maxAmountButton?.onClick.RemoveListener(OnClickMaxAmountButton);
            m_buyAmountInput?.onEndEdit.RemoveListener(OnBuyAmountInputEdit);
            m_buyAmountSlider?.onValueChanged.RemoveListener(OnBuyAmountSliderChanged);

            m_vm.OnBuySucceeded -= OnBuySucceeded;
            m_vm.SelectedProduct.Unbind(OnSelectedProductChanged);
            m_vm.Products.Unbind(RefreshProductView);
            m_vm.Gold.Unbind(RefreshGoldText);

            m_vm.Unbind();
            
        }

        private void RefreshGoldText(int gold)
        {
            if(m_goldText != null)
            {
                m_goldText.text = gold.ToString("N0");
            }
        }

        private void RefreshProductView(List<ShopProductViewData> products)
        {
            if (products == null)
            {
                for(int i = 0; i < m_products.Count; i++)
                    m_products[i].gameObject.SetActive(false);
                return;
            }

            if (!EnsureProductView(products.Count))
                return;

            for(int i = 0; i < products.Count; i++)
            {
                ShopProductViewData productData = products[i];
                Sprite icon = GetIcon(productData);
                ItemSlotVD vd = ItemSlotVD.FromData(m_vm.FindData(productData));

                switch (productData.ItemType)
                {
                    case ItemType.Equipment:
                    case ItemType.Consumables:
                        m_products[i].Set(m_EquipmentSlotBackground, productData, icon, vd, this);
                        break;
                    case ItemType.Materials:
                        m_products[i].Set(m_MaterialsSlotBackground, productData, icon, vd, this);
                        break;
                }
            }

            for(int i = products.Count; i < m_products.Count; i++)
            {
                m_products[i].gameObject.SetActive(false);
            }
        }

        private bool EnsureProductView(int count)
        {
            if (m_productViewPrefab == null || m_productRoot == null)
            {
                Debug.LogWarning("[ShopWindowView] m_productViewPrefab 혹은 생성 위치가 지정되지 않았습니다.");
                return false;
            }

            while(m_products.Count < count)
            {
                ShopProductView productView = Instantiate(m_productViewPrefab, m_productRoot);

                productView.Initialize(OnClickButton);

                m_products.Add(productView);
            }

            for(int i = 0; i < count; i++)
            {
                m_products[i].gameObject.SetActive(true);
            }

            return true;
        }

        private Sprite GetIcon(ShopProductViewData productData)
        {
            if (productData == null || string.IsNullOrEmpty(productData.IconKey))
                return null;

            string iconKey = productData.IconKey;

            // 원래 Sprite 타입으로 정상 캐시된 경우
            if (AssetProvider.TryGet<Sprite>(iconKey, out Sprite sprite))
            {
                return sprite;
            }

            // 이전에 Texture2D에서 변환해둔 Sprite가 있는 경우
            if (m_runtimeIconCache.TryGetValue(iconKey, out Sprite cachedSprite)
                && cachedSprite != null)
            {
                return cachedSprite;
            }

            // 빌드에서 Texture2D로 캐시된 경우
            if (AssetProvider.TryGet<Texture2D>(iconKey, out Texture2D texture)
                && texture != null)
            {
                Sprite convertedSprite = CreateSpriteFromTexture(iconKey, texture);

                if (convertedSprite != null)
                {
                    m_runtimeIconCache[iconKey] = convertedSprite;
                    return convertedSprite;
                }
            }

            // 실제 캐시 타입 확인용
            if (AssetProvider.TryGet<UnityEngine.Object>(iconKey, out UnityEngine.Object cachedAsset))
            {
                return null;
            }

            Debug.LogWarning(
                $"[ShopWindowView] Icon asset not found. key: {iconKey}");

            return null;
        }

        private Sprite CreateSpriteFromTexture(string iconKey, Texture2D texture)
        {
            if (texture == null)
                return null;

            Rect textureRect = new(
               0f,
               0f,
               texture.width,
               texture.height);

            Vector2 pivot = new(0.5f, 0.5f);

            Sprite sprite = Sprite.Create(
                texture,
                textureRect,
                pivot,
                100f,
                0,
                SpriteMeshType.FullRect);

            if (sprite == null)
            {
                return null;
            }

            sprite.name = $"{texture.name}_RuntimeSprite";

            return sprite;
        }

        private void OnBuySucceeded()
        {
            m_vm.SelectedProduct.Value = null;
        }

        private void OnClickCloseButton()
        {
            m_vm.SelectedProduct.Value = null;
            Close();
        }

        private void OnSelectedProductChanged(ShopProductViewData data)
        {
            if(data == null)
            {
                m_buyPopup.gameObject.SetActive(false);
                return;
            }

            if (m_buyItemName != null)
                m_buyItemName.text = data.Name;

            if(m_buyAmountSlider != null)
            {
                if (data.IsStackable)
                    m_buyAmountSlider.maxValue = Mathf.Max(m_vm.Gold.Value / data.Price, 1);
                else
                    m_buyAmountSlider.maxValue = 1;
                m_buyAmountSlider.minValue = 1;
            }

            ApplyBuyAmount(1);

            if (m_vm.Gold.Value < data.Price)
                m_buyButton.interactable = false;
            else
                m_buyButton.interactable = true;

            m_buyPopup.gameObject.SetActive(true);
        }

        private void OnClickButton(ShopProductViewData data)
        {
            m_vm.SelectProductCommand.Execute(data.ProductId);
        }

        private void OnBuyAmountSliderChanged(float value)
        {
            ApplyBuyAmount((int) value);
        }

        private void OnBuyAmountInputEdit(string text)
        {
            if(string.IsNullOrEmpty(text) || !int.TryParse(text, out int value))
            {
                ApplyBuyAmount(m_buyAmount);
                return;
            }

            if(m_vm.SelectedProduct.Value.Price * value > m_vm.Gold.Value)
            {
                ApplyBuyAmount(m_buyAmount);
                return;
            }
            
            ApplyBuyAmount(value);
        }

        private void ApplyBuyAmount(int amount)
        {
            if (amount <= 0 || amount > m_buyAmountSlider.maxValue)
            {
                m_buyAmountSlider.SetValueWithoutNotify(m_buyAmount);
                m_buyAmountInput.SetTextWithoutNotify(m_buyAmount.ToString());

                m_requireGoldText.text = "필요 골드 : " + (m_vm.SelectedProduct.Value.Price * m_buyAmount).ToString() + "G";
                return;
            }

            m_buyAmount = amount;

            m_buyAmountSlider.SetValueWithoutNotify(amount);
            m_buyAmountInput.SetTextWithoutNotify(amount.ToString());

            m_requireGoldText.text = "필요 골드 : " + (m_vm.SelectedProduct.Value.Price * m_buyAmount).ToString() + "G";
        }

        private void OnClickBuyButton()
        {
            m_vm.BuyCommand.Execute(m_buyAmount);
        }

        private void OnClickCancelButton()
        {
            m_vm.SelectedProduct.Value = null;
        }
        
        private void OnClickMaxAmountButton()
        {
            int maxAmount = (int)m_buyAmountSlider.maxValue;
            ApplyBuyAmount(maxAmount);
        }
    }
}
