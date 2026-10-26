using System.Collections.Generic;
using DesktopCompanion.Data;

namespace DesktopCompanion.Entities
{
    /// <summary>신규 플레이어 상태. 기본값은 PlayerData_New, 최종 세부스탯은 PlayerSystem_New이 보관한다.</summary>
    public class Entity_Player_New : Entity
    {
        private readonly PlayerData_New m_playerData;
        private float m_str;
        private float m_tec;
        private float m_luc;
        private readonly Dictionary<EquipmentMountingArea, EntityHandle> m_equipped = new();

        public int CurrentLicense { get; private set; }
        public int Gold { get; private set; }

        public float Str => m_str;
        public float Tec => m_tec;
        public float Luk => m_luc;

        public PlayerData_New BaseData => m_playerData;
        public IReadOnlyDictionary<EquipmentMountingArea, EntityHandle> Equipped => m_equipped;

        public Entity_Player_New(EntityHandle id, PlayerData_New data) : base(id, data)
        {
            m_playerData = data;
            m_str = data.Str;
            m_tec = data.Tec;
            m_luc = data.Luk;
            CurrentLicense = data.StartingLicense;
            Gold = data.StartingGold;
        }

        // 고유 값 변경·계산 요청·변경 알림은 PlayerSystem_New에서 처리한다.
        internal void SetMainStats(float str, float tec, float luk)
        {
            m_str = str;
            m_tec = tec;
            m_luc = luk;
        }

        public void SetLicense(int license) => CurrentLicense = license;
        public void SetGold(int gold) => Gold = gold;
        public void AddGold(int delta) => Gold += delta;

        public void Equip(EquipmentMountingArea area, EntityHandle handle) => m_equipped[area] = handle;
        public void Unequip(EquipmentMountingArea area) => m_equipped.Remove(area);
        public bool TryGetEquipped(EquipmentMountingArea area, out EntityHandle handle)
            => m_equipped.TryGetValue(area, out handle);
    }
}
