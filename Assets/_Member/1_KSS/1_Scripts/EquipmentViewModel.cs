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

    public RelayCommand UpgradeStorageCommand { get; private set; }

    public override void Bind()
    {
        m_playerSystem = SystemManager.GetSystem<PlayerSystem>();

        EquipCommand = new RelayCommand<(EquipmentMountingArea, EntityHandle)>(
            args => m_playerSystem?.Equip(args.Item1, args.Item2)
        );

        UpgradeStorageCommand = new RelayCommand(
            () => m_playerSystem?.UpgradeFishStorage()
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

    public string GetItemNameForArea(EquipmentMountingArea area)
    {
        string itemName = m_playerSystem != null ? m_playerSystem.GetEquippedItemName(area) : null;
        if (!string.IsNullOrEmpty(itemName))
        {
            return itemName;
        }

        // 장착된 장비가 없을 때 기본 부위 이름 반환
        return area switch
        {
            EquipmentMountingArea.FishingRod => "낚시대",
            EquipmentMountingArea.FishingLine => "낚시줄",
            EquipmentMountingArea.Reel => "릴",
            EquipmentMountingArea.Lure => "루어",
            EquipmentMountingArea.Hat => "모자",
            EquipmentMountingArea.Uniform => "옷",
            EquipmentMountingArea.Gloves => "장갑",
            EquipmentMountingArea.Engine => "배 엔진",
            EquipmentMountingArea.Storage => "물고기창고",
            EquipmentMountingArea.GPS => "GPS",
            EquipmentMountingArea.Bait => "미끼",
            EquipmentMountingArea.Groundbait => "떡밥",
            _ => "빈 슬롯"
        };
    }
    public EntityHandle GetEquippedHandleForArea(EquipmentMountingArea area)
    {
        return m_playerSystem != null ? m_playerSystem.GetEquippedItemHandle(area) : default;
    }

    // [추가] View가 스탯 창을 켜거나 탭을 바꿀 때, 전체 스탯을 가공해서 넘겨줍니다.
    public string GetAllStatsFormattedText()
    {
        if (m_playerSystem == null) return "스탯 정보를 불러오는 중...";

        // C#의 문자열 보간($)과 줄바꿈(\n)을 활용해 하나의 거대한 텍스트로 묶습니다.
        string stats =
            $"<b>[ 전투 스탯 ]</b>\n" +
            $"클릭 데미지 :  {m_playerSystem.BaseDamagePerClick}\n" +
            $"수동 타격 배율 :  {m_playerSystem.BaseManualDamagePerHitMultiply}\n" +
            $"크리티컬 확률 :  {m_playerSystem.BaseCriticalChance}%\n" +
            $"크리티컬 배율 :  {m_playerSystem.BaseCriticalMultiply}배\n" +
            $"전투 시간 변수 :  {m_playerSystem.BaseBattleTimeVariable}\n\n" +

            $"<b>[ 자동 전투 ]</b>\n" +
            $"자동 공격 쿨타임 :  {m_playerSystem.BaseAutoBattleCooltime}초\n" +
            $"자동 공격 속도 :  {m_playerSystem.BaseAutoSpeedPerTime}\n" +
            $"자동 타격 배율 :  {m_playerSystem.BaseAutoDamagePerHitMultiply}\n\n" +

            $"<b>[ 보상 및 기타 ]</b>\n" +
            $"대어 낚시 확률 :  {m_playerSystem.BaseProbabilityAtFishSize}\n" +
            $"희귀어 낚시 확률 :  {m_playerSystem.BaseProbabilityAtFishRarity}\n" +
            $"골드 획득 배율 :  {m_playerSystem.BaseGoldGettingMultiply}배\n" +
            $"이동 속도 :  {m_playerSystem.BaseMapMovementSpeedPerTime}\n" +
            $"인벤토리 크기 :  {m_playerSystem.BaseInventorySize}칸";

        return stats;
    }
    public string GetEngineSpeedText()
    {
        return m_playerSystem != null ? $"이동 속도: {m_playerSystem.BaseMapMovementSpeedPerTime}" : "이동 속도: 0";
    }

    public string GetStorageSizeText()
    {
        return m_playerSystem != null ? $"물고기 창고: {m_playerSystem.BaseInventorySize}칸" : "물고기 창고: 0칸";
    }
}
