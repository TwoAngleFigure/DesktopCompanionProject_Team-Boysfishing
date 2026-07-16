using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using System.Buffers.Text;
using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Entities
{
    /// <summary>
    /// Data를 원본으로 생성된 런타임 인스턴스. 얇은(anemic) 상태 컨테이너로,
    /// 정적값은 Data 참조에서 읽고 가변 상태만 보유한다. 행위/로직은 System이 담당한다.
    /// 생명주기(생성·소멸·핸들 발급)는 EntityManager가 소유한다.
    /// </summary>
    public abstract class Entity
    {
        public EntityHandle Id { get; }
        protected readonly GameData m_data;   // 정적값은 Data 참조에서 읽음(복사 X)

        public int DataId => m_data.ID;
        public string Name => m_data.Name;

        protected Entity(EntityHandle id, GameData data)
        {
            Id = id;
            m_data = data;
        }
    }

    // ── 아이템 계열 ──

    /// <summary>인벤토리의 물고기 개체. 크기/성급은 포획 시 롤 결과가 주입된다(G3·G6).</summary>
    public class Entity_Fish : Entity
    {
        private readonly ItemData_Fish m_fish;

        public float Size { get; private set; }
        public ItemQuality Quality { get; private set; }

        public ItemRarity Rarity => m_fish.Rarity;   // 종별 고정(G1) — Data에서 읽음

        public ItemData_Fish ItemData => m_fish;

        public Entity_Fish(EntityHandle id, ItemData_Fish data) : base(id, data) => m_fish = data;

        /// <summary>포획 시 Entity_BattleFish의 롤 결과를 복사 주입.</summary>
        public void SetRollResult(float size, ItemQuality quality)
        {
            Size = size;
            Quality = quality;
        }
    }

    /// <summary>장비 개체. 강화 단계는 개체별 가변 상태.</summary>
    public class Entity_Equipment : Entity
    {
        private readonly ItemData_Equipment m_equipment;

        public int UpgradeLevel { get; private set; }   // 0 = 무강화

        public Entity_Equipment(EntityHandle id, ItemData_Equipment data) : base(id, data)
            => m_equipment = data;

        public ItemData_Equipment ItemData => m_equipment;

        /// <summary>현재 강화 단계의 효과(정의는 Data에서 조회).</summary>
        public StatModifier[] CurrentModifiers => m_equipment.GetModifiers(UpgradeLevel);

        public void SetUpgradeLevel(int level) => UpgradeLevel = level;
    }

    /// <summary>재료 개체 — 스택 수량 보유(D10).</summary>
    public class Entity_Materials : Entity
    {
        private readonly ItemData_Materials m_materials;

        public int Quantity { get; private set; }

        public Entity_Materials(EntityHandle id, ItemData_Materials data, int quantity = 1) : base(id, data)
            => (Quantity, m_materials) = (quantity, data);

        public ItemData_Materials ItemData => m_materials;

        public void SetQuantity(int quantity) => Quantity = quantity;
        public void Add(int delta) => Quantity += delta;
    }

    /// <summary>소모품 개체 — 스택 수량 보유(D10). 사용 시 감소.</summary>
    public class Entity_Consumables : Entity
    {
        private readonly ItemData_Consumables m_consumables;

        public int Quantity { get; private set; }

        public Entity_Consumables(EntityHandle id, ItemData_Consumables data, int quantity = 1) : base(id, data)
            => (Quantity, m_consumables) = (quantity, data);

        public ItemData_Consumables ItemData => m_consumables;

        public void SetQuantity(int quantity) => Quantity = quantity;
        public void Add(int delta) => Quantity += delta;
    }

    // ── 전투 계열 ──

    /// <summary>
    /// 전투(낚시) 중인 물고기 개체 — 일시 상태(세이브 대상 아님).
    /// 입질 시 System이 생성하며 크기 롤+성급 산출(G6). 포획 성공 시 Entity_Fish로 복사.
    /// </summary>
    public class Entity_BattleFish : Entity
    {
        private readonly BattleFishData m_battleFish;

        public float Size { get; private set; }
        public ItemQuality Quality { get; private set; }
        public int CurrentHp { get; private set; }

        public BattleFishData BattleData => m_battleFish;

        public Entity_BattleFish(EntityHandle id, BattleFishData data) : base(id, data)
        {
            m_battleFish = data;
            CurrentHp = data.MaxHp;
        }

        /// <summary>입질 시 System이 롤한 크기/성급 주입.</summary>
        public void SetRollResult(float size, ItemQuality quality)
        {
            Size = size;
            Quality = quality;
        }

        public void ApplyDamage(int damage) => CurrentHp = System.Math.Max(0, CurrentHp - damage);
    }

    // ── 플레이어 ──

    /// <summary>
    /// 플레이어 개체 — 세이브 핵심 대상(G2). 라이센스·골드·장착 목록 등 가변 상태 보유.
    /// 최종 스탯은 저장하지 않고 PlayerSystem이 (PlayerData 기본값 + 장착 modifier)로 재계산.
    /// </summary>
    public class Entity_Player : Entity
    {
        private readonly PlayerData m_playerData;
        private readonly Dictionary<EquipmentMountingArea, EntityHandle> m_equipped = new();

        public int CurrentLicense { get; private set; }
        public int Gold { get; private set; }

        public PlayerData BaseData => m_playerData;
        public IReadOnlyDictionary<EquipmentMountingArea, EntityHandle> Equipped => m_equipped;

        public Entity_Player(EntityHandle id, PlayerData data) : base(id, data)
        {
            m_playerData = data;
            CurrentLicense = data.StartingLicense;
            Gold = data.StartingGold;
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
