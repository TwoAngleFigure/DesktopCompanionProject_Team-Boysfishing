using DesktopCompanion.Systems;
using DesktopCompanion.Entities;
using DesktopCompanion.Data;
using DesktopCompanion.Views;

public class EquipmentViewModel : UIViewModelBase
{
    private PlayerSystem m_playerSystem;

    // 1. 화면에 바인딩할 데이터 (예: 공격력)
    public readonly BindableProperty<int> Damage = new(0);

    // 2. 화면에서 누를 버튼의 명령 (장비 장착 명령)
    public RelayCommand<(EquipmentMountingArea area, EntityHandle handle)> EquipCommand { get; private set; }

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
    }

    // 최신 스탯을 가져와서 BindableProperty에 넣어줌 -> UI가 알아서 바뀜!
    private void RefreshStats()
    {
        if (m_playerSystem != null)
        {
            Damage.Value = m_playerSystem.BaseDamagePerClick;
        }
    }
}