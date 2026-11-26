using DesktopCompanion.Data;

namespace DesktopCompanion.Entities
{
    /// <summary>신규 장비 상태. 기존 Entity_Equipment와 독립된 타입이며 지급 전에 확정값을 주입한다.</summary>
    public sealed class Entity_Equipment_New : Entity
    {
        private readonly ItemData_Equipment_New m_equipment;

        public ItemData_Equipment_New ItemData => m_equipment;
        public int UpgradeLevel { get; private set; }
        public EquipmentState State { get; private set; }

        public Entity_Equipment_New(EntityHandle id, ItemData_Equipment_New data) : base(id, data)
            => m_equipment = data;

        /// <summary>null은 미초기화 상태다. 구형 장비로 간주하거나 지급하지 않는다.</summary>
        public bool TryInitializeState(EquipmentState state)
        {
            if (State != null || state == null || state.DataId != DataId)
                return false;
            State = state;
            return true;
        }

        // 효과는 EquipmentRule.TryGetEffects(State)에서만 얻는다. 구형 modifier와 합치지 않는다.
        public void SetUpgradeLevel(int level) => UpgradeLevel = level;
    }
}
