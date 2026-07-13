using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DesktopCompanion.Views
{
    public class EquipmentWindowView : UIWindowBase
    {
        private readonly EquipmentViewModel m_vm = new();

        [Header("UI 연결")]
        [SerializeField] private TextMeshProUGUI m_allStatsText;
        [SerializeField] private EquipmentSlotWidget[] m_slots;

        // [추가됨] 배 장비창 텍스트 2개를 연결할 빈칸!
        [Header("Ship Equip UI")]
        [SerializeField] private TextMeshProUGUI m_engineSpeedText;
        [SerializeField] private TextMeshProUGUI m_storageSizeText;
        [SerializeField] private Button m_storageUpgradeBtn;//테스트용

        [Header("Inventory Link")]
        [SerializeField] private ItemPickupController m_itemPickupController;

        [Header("Tab Buttons")]
        [SerializeField] private Button m_tabPlayerEquipBtn;
        [SerializeField] private Button m_tabShipEquipBtn;
        [SerializeField] private Button m_tabStatsBtn;

        [Header("Tab Panels")]
        [SerializeField] private GameObject m_playerEquipPanel;
        [SerializeField] private GameObject m_shipEquipPanel;
        [SerializeField] private GameObject m_statsPanel;

        public override void Bind()
        {
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            m_vm.OnEquipmentChanged += RefreshUI;

            foreach (var slot in m_slots)
            {
                slot.Bind(
                    onClickAction: clickedArea =>
                    {
                        m_vm.EquipCommand.Execute((clickedArea, default(EntityHandle)));
                    },
                    onDropAction: dropArea =>
                    {
                        if (m_itemPickupController != null && m_itemPickupController.HasItem)
                        {
                            EntityHandle droppedItem = m_itemPickupController.PickedHandle;
                            m_vm.EquipCommand.Execute((dropArea, droppedItem));
                            m_itemPickupController.ClearPickup();
                        }
                    },
                    onBeginDragAction: dragArea =>
                    {
                        EntityHandle equippedItem = m_vm.GetEquippedHandleForArea(dragArea);

                        if (!equippedItem.Equals(default(EntityHandle)) && m_itemPickupController != null)
                        {
                            m_itemPickupController.BeginPickup(ItemType.Equipment, -1, equippedItem, null);
                            m_vm.EquipCommand.Execute((dragArea, default(EntityHandle)));
                        }
                    }
                );
            }

            if (m_tabPlayerEquipBtn != null) m_tabPlayerEquipBtn.onClick.AddListener(() => SwitchTab(0));
            if (m_tabShipEquipBtn != null) m_tabShipEquipBtn.onClick.AddListener(() => SwitchTab(1));
            if (m_tabStatsBtn != null) m_tabStatsBtn.onClick.AddListener(() => SwitchTab(2));

            RefreshUI();
            SwitchTab(0);
        }

        public override void Unbind()
        {
            m_vm.OnEquipmentChanged -= RefreshUI;
            m_vm.Unbind();

            foreach (var slot in m_slots)
            {
                slot.Unbind();
            }

            if (m_tabPlayerEquipBtn != null) m_tabPlayerEquipBtn.onClick.RemoveAllListeners();
            if (m_tabShipEquipBtn != null) m_tabShipEquipBtn.onClick.RemoveAllListeners();
            if (m_tabStatsBtn != null) m_tabStatsBtn.onClick.RemoveAllListeners();
        }

        private void RefreshUI()
        {
            foreach (var slot in m_slots)
            {
                string itemName = m_vm.GetItemNameForArea(slot.Area);
                slot.RefreshSlotUI(itemName);
            }

            if (m_allStatsText != null)
            {
                m_allStatsText.text = m_vm.GetAllStatsFormattedText();
            }

            // [추가됨] 스탯창이 갱신될 때, 배 장비창의 텍스트도 자동으로 최신 스탯을 받아옵니다!
            if (m_engineSpeedText != null)
            {
                m_engineSpeedText.text = m_vm.GetEngineSpeedText();
            }
            if (m_storageSizeText != null)
            {
                m_storageSizeText.text = m_vm.GetStorageSizeText();
            }
        }

        private void SwitchTab(int tabIndex)
        {
            if (m_playerEquipPanel != null) m_playerEquipPanel.SetActive(tabIndex == 0);
            if (m_shipEquipPanel != null) m_shipEquipPanel.SetActive(tabIndex == 1);
            if (m_statsPanel != null) m_statsPanel.SetActive(tabIndex == 2);
        }
    }
}