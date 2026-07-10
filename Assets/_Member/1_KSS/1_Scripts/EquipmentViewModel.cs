using DesktopCompanion.Systems;
using DesktopCompanion.Entities;
using DesktopCompanion.Data;
using DesktopCompanion.Views;
using System; // Action을 사용하기 위해 추가

public class EquipmentViewModel : UIViewModelBase
{
    private PlayerSystem m_playerSystem;

    // 1. 화면에 바인딩할 데이터 (예: 공격력)
    public readonly BindableProperty<int> Damage = new(0);

    // 2. 화면에서 누를 버튼의 명령 (장비 장착 명령)
    public RelayCommand<(EquipmentMountingArea area, EntityHandle handle)> EquipCommand { get; private set; }

    // [추가됨] View에게 "장비 바뀌었으니 UI 새로고침해!" 라고 알려줄 이벤트
    public event Action OnEquipmentChanged;

    public override void Bind()
    {
        m_playerSystem = SystemManager.GetSystem<PlayerSystem>();

        EquipCommand = new RelayCommand<(EquipmentMountingArea, EntityHandle)>(
            args => m_playerSystem?.Equip(args.Item1, args.Item2)
        );

        // PlayerSystem의 스탯 변경 이벤트를 구독
        if (m_playerSystem != null)
        {
            m_playerSystem.OnStatChanged += OnStatChanged;
            RefreshStats(); // 처음 켰을 때 초기값 갱신
        }
    }

    public override void Unbind()
    {
        if (m_playerSystem != null) m_playerSystem.OnStatChanged -= OnStatChanged;
        m_playerSystem = null;
    }

    // 시스템에서 스탯 변경 이벤트가 날아오면 실행되는 함수
    private void OnStatChanged(EntityHandle handle)
    {
        RefreshStats();

        // [추가됨] 스탯이 변했다는 건 장비가 변했다는 뜻이므로, View에게 UI 갱신 신호를 보냅니다.
        OnEquipmentChanged?.Invoke();
    }

    // 최신 스탯을 가져와서 BindableProperty에 넣어줌 -> UI가 알아서 바뀜!
    private void RefreshStats()
    {
        if (m_playerSystem != null)
        {
            Damage.Value = m_playerSystem.BaseDamagePerClick;
        }
    }

    // [추가됨] View가 특정 슬롯의 장비 이름을 물어볼 때, PlayerSystem에서 꺼내서 대답해주는 함수
    public string GetItemNameForArea(EquipmentMountingArea area)
    {
        return m_playerSystem != null ? m_playerSystem.GetEquippedItemName(area) : "Empty Slot";
    }
}