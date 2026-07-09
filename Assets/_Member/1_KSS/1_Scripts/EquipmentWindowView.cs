using DesktopCompanion.Entities;
using DesktopCompanion.Views;
using UnityEngine;
using TMPro;

namespace DesktopCompanion.Views
{
    public class EquipmentWindowView : UIWindowBase
    {
        private readonly EquipmentViewModel m_vm = new();

        [Header("UI 연결")]
        [SerializeField] private TextMeshProUGUI m_damageText;

        // [핵심 변경점] 버튼 수십 개 대신, 위에서 만든 슬롯 위젯을 '배열'로 한 번에 관리합니다!
        [SerializeField] private EquipmentSlotWidget[] m_slots;

        public override void Bind()
        {
            m_vm.Inject(SystemManager);
            m_vm.Bind();

            // 1. 공격력 텍스트 바인딩
            m_vm.Damage.Bind(damageValue => m_damageText.text = $"공격력: {damageValue}");

            // 2. [핵심] 배열에 있는 모든 슬롯을 한 바퀴 돌면서 클릭 이벤트를 연결해줍니다. (for문 활용)
            foreach (var slot in m_slots)
            {
                // 슬롯이 클릭되면 -> 뷰모델의 장착/해제 명령(Command)을 실행해라!
                slot.Bind(clickedArea =>
                {
                    // ※ 실제로는 클릭했을 때 인벤토리에 띄워둔 선택된 아이템 핸들을 가져와서 넘겨야 합니다.
                    // 지금은 UI 작동 테스트를 위해 빈 핸들(default)을 넘기도록 세팅했습니다.
                    m_vm.EquipCommand.Execute((clickedArea, default(EntityHandle)));
                });
            }
        }

        public override void Unbind()
        {
            m_vm.Damage.Unbind(damageValue => m_damageText.text = $"공격력: {damageValue}");
            m_vm.Unbind();

            // 모든 슬롯의 연결도 깔끔하게 해제
            foreach (var slot in m_slots)
            {
                slot.Unbind();
            }
        }
    }
}