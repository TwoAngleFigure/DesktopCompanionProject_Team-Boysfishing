using DesktopCompanion.Views; // 팀장님 프레임워크 네임스페이스
using UnityEngine;
using UnityEngine.UI;
using TMPro; // 텍스트를 위한 네임스페이스

public class EquipmentWindowView : UIWindowBase
{
    // 뷰모델 생성
    private readonly EquipmentViewModel m_vm = new();

    [Header("UI 연결")]
    [SerializeField] private TextMeshProUGUI m_damageText; // 공격력 텍스트
    [SerializeField] private Button m_equipTestButton; // 임시 장착 테스트 버튼

    public override void Bind()
    {
        // 1. 뷰모델에 매니저 주입 및 바인딩 시작
        m_vm.Inject(SystemManager);
        m_vm.Bind();

        // 2. 뷰모델의 데이터(Damage)가 변할 때마다 텍스트를 바꾸도록 연결(Bind)
        m_vm.Damage.Bind(damageValue => m_damageText.text = $"공격력: {damageValue}");

        // 3. 버튼을 누르면 뷰모델의 EquipCommand를 실행하도록 연결
        // (실제 게임에서는 인벤토리의 특정 아이템 핸들을 가져와서 넘겨야 합니다)
        m_equipTestButton.onClick.AddListener(() =>
        {
            // 임시 테스트용 데이터 (실제로는 선택된 아이템의 데이터를 넣어야 함)
            // m_vm.EquipCommand.Execute((EquipmentMountingArea.Weapon, 특정아이템핸들));
        });
    }

    public override void Unbind()
    {
        // 연결했던 것들을 모두 해제
        m_equipTestButton.onClick.RemoveAllListeners();

        m_vm.Damage.Unbind(damageValue => m_damageText.text = $"공격력: {damageValue}");
        m_vm.Unbind();
    }
}