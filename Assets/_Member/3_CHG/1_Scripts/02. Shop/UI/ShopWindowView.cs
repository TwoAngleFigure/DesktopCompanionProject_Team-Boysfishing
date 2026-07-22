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
        [SerializeField] private Button m_clostButton;

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

        private readonly ShopViewModel m_vm;
        private readonly List<ShopProductView> m_products;

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            m_vm.Gold.Bind(RefreshGoldText);
            m_vm.Products.Bind(RefreshProductView);
            m_vm.SelectedProduct.Bind();
            m_vm.OnBuySucceeded += OnBuySucceeded;

            if(m_closeButton != null)
            {
                m_closeButton.onClick.AddListener(OnClickCloseButton);
            }
        }

        public override void Unbind()
        {
            if(m_closeButton != null)
                m_closeButton.onClick.RemoveListener(OnClickCloseButton);

            m_vm.OnBuySucceeded -= OnBuySucceeded;
            m_vm.SelectedProduct.Unbind();
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
                return;


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
                int productCount = m_products.Count;

                //TODO : productView.Initialize()

                m_products.Add(productView);
            }

            for(int i = 0; i < count; i++)
            {
                m_products[i].gameObject.SetActive(true);
            }

            return true;
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
    }
}
