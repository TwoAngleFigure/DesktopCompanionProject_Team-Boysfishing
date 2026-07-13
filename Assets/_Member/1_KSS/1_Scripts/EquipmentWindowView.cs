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
        [SerializeField] private TextMeshProUGUI m_damageText;
        [SerializeField] private EquipmentSlotWidget[] m_slots;

        // 인벤토리 팀원분의 마우스 드래그 컨트롤러를 연결할 빈칸
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

            m_vm.Damage.Bind(damageValue =>
            {
                if (m_damageText != null) m_damageText.text = $"공격력: {damageValue}";
            });

            m_vm.OnEquipmentChanged += RefreshAllSlots;

            foreach (var slot in m_slots)
            {
                slot.Bind(
                    onClickAction: clickedArea =>
                    {
                        m_vm.EquipCommand.Execute((clickedArea, default(EntityHandle)));
                    },
                    onDropAction: dropArea =>
                    {
                        // 1. 인벤토리에서 끌고 온 아이템을 내 위에 떨어뜨렸을 때
                        if (m_itemPickupController != null && m_itemPickupController.HasItem)
                        {
                            EntityHandle droppedItem = m_itemPickupController.PickedHandle;
                            m_vm.EquipCommand.Execute((dropArea, droppedItem));
                            m_itemPickupController.ClearPickup();
                        }
                    },
                    onBeginDragAction: dragArea =>
                    {
                        // 2. 장비창에 껴있는 장비를 바깥으로 끌어낼 때
                        EntityHandle equippedItem = m_vm.GetEquippedHandleForArea(dragArea);

                        // [에러 해결] '!=' 기호 대신 .Equals() 함수를 사용하여 안전하게 비교합니다!
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

            RefreshAllSlots();
            SwitchTab(0);
        }

        public override void Unbind()
        {
            m_vm.Damage.Unbind(damageValue =>
            {
                if (m_damageText != null) m_damageText.text = $"공격력: {damageValue}";
            });

            m_vm.OnEquipmentChanged -= RefreshAllSlots;
            m_vm.Unbind();

            foreach (var slot in m_slots)
            {
                slot.Unbind();
            }

            if (m_tabPlayerEquipBtn != null) m_tabPlayerEquipBtn.onClick.RemoveAllListeners();
            if (m_tabShipEquipBtn != null) m_tabShipEquipBtn.onClick.RemoveAllListeners();
            if (m_tabStatsBtn != null) m_tabStatsBtn.onClick.RemoveAllListeners();
        }

        private void RefreshAllSlots()
        {
            foreach (var slot in m_slots)
            {
                string itemName = m_vm.GetItemNameForArea(slot.Area);
                slot.RefreshSlotUI(itemName);
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