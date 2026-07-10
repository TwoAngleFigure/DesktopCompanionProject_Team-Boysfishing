using DesktopCompanion.Entities;
using DesktopCompanion.Views;
using UnityEngine;
using UnityEngine.UI; // 버튼 컴포넌트용
using TMPro;

namespace DesktopCompanion.Views
{
    public class EquipmentWindowView : UIWindowBase
    {
        private readonly EquipmentViewModel m_vm = new();

        [Header("UI 연결")]
        [SerializeField] private TextMeshProUGUI m_damageText;
        [SerializeField] private EquipmentSlotWidget[] m_slots;

        // [탭 기능] 추가된 변수들
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
            // Inject 에러 안 나도록 사용자님 원본 그대로 2개 다 넣음
            m_vm.Inject(SystemManager, EntityManager);
            m_vm.Bind();

            // 1. 공격력 텍스트 바인딩
            m_vm.Damage.Bind(damageValue => m_damageText.text = $"공격력: {damageValue}");

            // 2. 뷰모델의 '장비 변경 이벤트' 구독 (UI 자동 갱신)
            m_vm.OnEquipmentChanged += RefreshAllSlots;

            // 3. 슬롯 클릭 이벤트 연결
            foreach (var slot in m_slots)
            {
                slot.Bind(clickedArea =>
                {
                    m_vm.EquipCommand.Execute((clickedArea, default(EntityHandle)));
                });
            }

            // 4. 탭 버튼 클릭 이벤트 연결
            m_tabPlayerEquipBtn.onClick.AddListener(() => SwitchTab(0));
            m_tabShipEquipBtn.onClick.AddListener(() => SwitchTab(1));
            m_tabStatsBtn.onClick.AddListener(() => SwitchTab(2));

            // 초기화: 슬롯 이름들 불러오고, 1번 탭(플레이어 장비) 강제로 켜기
            RefreshAllSlots();
            SwitchTab(0);
        }

        public override void Unbind()
        {
            m_vm.Damage.Unbind(damageValue => m_damageText.text = $"공격력: {damageValue}");
            m_vm.OnEquipmentChanged -= RefreshAllSlots;
            m_vm.Unbind();

            foreach (var slot in m_slots)
            {
                slot.Unbind();
            }

            m_tabPlayerEquipBtn.onClick.RemoveAllListeners();
            m_tabShipEquipBtn.onClick.RemoveAllListeners();
            m_tabStatsBtn.onClick.RemoveAllListeners();
        }

        // 모든 슬롯의 텍스트(이름)를 새로고침 하는 함수
        private void RefreshAllSlots()
        {
            foreach (var slot in m_slots)
            {
                string itemName = m_vm.GetItemNameForArea(slot.Area);
                slot.RefreshSlotUI(itemName);
            }
        }

        // 탭 화면 전환 함수
        private void SwitchTab(int tabIndex)
        {
            if (m_playerEquipPanel != null) m_playerEquipPanel.SetActive(tabIndex == 0);
            if (m_shipEquipPanel != null) m_shipEquipPanel.SetActive(tabIndex == 1);
            if (m_statsPanel != null) m_statsPanel.SetActive(tabIndex == 2);
        }
    }
}