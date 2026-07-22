using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class ShopWindowView : UIWindowBase
    {
        [Header("UI")]
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TMP_Text m_goldText;

        [Header("Product")]
        [SerializeField] private Transform m_productRoot;
        [SerializeField] private ShopProductView m_productViewPrefab;

        [Header("Tooltip")]
        [SerializeField] private InventoryItemTooltipView m_tooltopView;

        [Header("Buy Popup")]
        [SerializeField] private GameObject m_buyPopup;
        [SerializeField] private InputField m_buyAmountInput;
        [SerializeField] private Slider m_buyAmountSlider;
        [SerializeField] private Button m_buyButton;
        [SerializeField] private Button m_cancelButton;
        [SerializeField] private TMP_Text m_buyText;

        private readonly ShopViewModel m_vm = new();
        private readonly List<ShopProductView> m_products = new();

        private readonly Dictionary<string, Sprite> m_runtimeIconCache = new();

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            m_vm.Gold.Bind(RefreshGoldText);
            m_vm.Products.Bind(RefreshProductView);
            m_vm.OnBuySucceeded += OnBuySucceeded;

            if(m_buyButton != null)

            if(m_cancelButton != null)

            if(m_closeButton != null)
                m_closeButton.onClick.AddListener(OnClickCloseButton);
            
        }

        public override void Unbind()
        {
            if(m_closeButton != null)
                m_closeButton.onClick.RemoveListener(OnClickCloseButton);

            m_vm.OnBuySucceeded -= OnBuySucceeded;
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

                m_products[i].Set(productData, icon);
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
                int productsCount = m_products.Count;

                productView.Initialize(productsCount, OnProductClick, OnProductPointEnter, OnProductPointExit);

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
            if(m_buyPopup != null)
                m_buyPopup.SetActive(false);
        }

        private void OnClickCloseButton()
        {
            Close();
        }

        private void OnProductClick(int index)
        {

        }

        private void OnProductPointEnter(int index)
        {

        }

        private void OnProductPointExit(int index)
        {

        }
    }
}
