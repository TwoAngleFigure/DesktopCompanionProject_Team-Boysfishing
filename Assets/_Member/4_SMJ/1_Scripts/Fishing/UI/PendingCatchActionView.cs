using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    /// <summary>
    /// Pending 물고기의 판매 또는 인벤토리 정리 선택을 표시하는 일반 UI View입니다.
    /// Root는 항상 활성화하고 PendingPanel만 표시/숨김 처리합니다.
    /// </summary>
    public class PendingCatchActionView : UIViewBase
    {
        [Header("Pending UI")]
        [SerializeField] private GameObject m_pendingPanel;
        [SerializeField] private Button m_sellCaughtFishButton;
        [SerializeField] private TMP_Text m_sellText;
        [SerializeField] private Button m_openInventoryButton;

        [Header("Temporary Navigation")]
        [SerializeField] private InventoryWindowView m_inventoryWindow;

        private readonly PendingCatchActionVM m_vm = new();

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();
            m_vm.IsPendingCatchVisible.Bind(HandlePendingCatchVisibilityChanged);
            m_vm.SellPriceText.Bind(HandleSellPriceTextChanged);

            if (m_sellCaughtFishButton != null)
            {
                m_sellCaughtFishButton.onClick.AddListener(HandleSellCaughtFishClicked);
            }

            if (m_openInventoryButton != null)
            {
                m_openInventoryButton.onClick.AddListener(HandleOpenInventoryClicked);
            }
        }

        public override void Unbind()
        {
            m_vm.IsPendingCatchVisible.Unbind(HandlePendingCatchVisibilityChanged);
            m_vm.SellPriceText.Unbind(HandleSellPriceTextChanged);

            if (m_sellCaughtFishButton != null)
            {
                m_sellCaughtFishButton.onClick.RemoveListener(HandleSellCaughtFishClicked);
            }

            if (m_openInventoryButton != null)
            {
                m_openInventoryButton.onClick.RemoveListener(HandleOpenInventoryClicked);
            }

            m_vm.Unbind();
        }

        private void HandlePendingCatchVisibilityChanged(bool isVisible)
        {
            if (m_pendingPanel != null)
            {
                m_pendingPanel.SetActive(isVisible);
            }
        }

        private void HandleSellCaughtFishClicked()
        {
            m_vm.SellPendingCatch?.Execute();
        }

        private void HandleSellPriceTextChanged(string text)
        {
            if (m_sellText != null)
            {
                m_sellText.text = text;
            }
        }

        private void HandleOpenInventoryClicked()
        {
            if (m_inventoryWindow == null)
            {
                Debug.LogWarning(
                    "[PendingCatchActionView] InventoryWindowView가 연결되지 않았습니다.");
                return;
            }

            m_inventoryWindow.Show();
        }
    }
}
