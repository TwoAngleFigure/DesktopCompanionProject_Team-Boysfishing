using System;
using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using DesktopCompanion.Save;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// �÷��̾��� �ɷ�ġ�� �����ϰ�, ��� ������ ���� ���� ��ȭ�� �ǽð����� ����ϴ� �ý����Դϴ�.
    /// </summary>
    public class PlayerSystem : SystemBase, ISaveable
    {
        private EntityHandle playerHandle;
        private Entity_Player entity_Player;
        private InventorySystem m_inventorySystem;

        public EntityHandle PlayerHandle => playerHandle;

        public event Action<EntityHandle> OnStatChanged;

        #region Stats

        [Header("Battle")]
        [SerializeField] private int m_baseDamagePerClick;
        [SerializeField] private float m_baseManualDamagePerHitMultiply;
        [SerializeField] private int m_baseBattleTimeVariable;
        [SerializeField] private float m_baseCriticalChance;
        [SerializeField] private float m_baseCriticalMultiply;

        [Header("Auto Battle")]
        [SerializeField] private float m_baseAutoBattleCooltime;
        [SerializeField] private float m_baseAutoSpeedPerTime;
        [SerializeField] private float m_baseAutoDamagePerHitMultiply;

        [Header("Battle Reward")]
        [SerializeField] private float m_baseProbabilityAtFishSize;
        [SerializeField] private float m_baseProbabilityAtFishRarity;
        [SerializeField] private float m_baseGoldGettingMultiply;

        [Header("Other")]
        [SerializeField] private float m_baseMapMovementSpeedPerTime;
        [SerializeField] private int m_baseInventorySize;
        [SerializeField] private int m_startingLicense;
        [SerializeField] private int m_startingGold;

        public int BaseDamagePerClick => m_baseDamagePerClick;
        public float BaseManualDamagePerHitMultiply => m_baseManualDamagePerHitMultiply;
        public int BaseBattleTimeVariable => m_baseBattleTimeVariable;
        public float BaseCriticalChance => m_baseCriticalChance;
        public float BaseCriticalMultiply => m_baseCriticalMultiply;

        public float BaseAutoBattleCooltime => m_baseAutoBattleCooltime;
        public float BaseAutoSpeedPerTime => m_baseAutoSpeedPerTime;
        public float BaseAutoDamagePerHitMultiply => m_baseAutoDamagePerHitMultiply;

        public float BaseProbabilityAtFishSize => m_baseProbabilityAtFishSize;
        public float BaseProbabilityAtFishRarity => m_baseProbabilityAtFishRarity;
        public float BaseGoldGettingMultiply => m_baseGoldGettingMultiply;

        public float BaseMapMovementSpeedPerTime => m_baseMapMovementSpeedPerTime;
        public int BaseInventorySize => m_baseInventorySize;
        public int StartingLicense => m_startingLicense;
        public int StartingGold => m_startingGold;

        #endregion

        #region Save

        public string SaveId => "player_system_stats";
        public Type StateType => typeof(string);

        public object CaptureState()
        {
            return "stats_calculated_dynamically";
        }

        public void RestoreState(object state)
        {
            // R1: 복원 단계에서는 원시 상태만 다룬다. 최종 스탯 계산(CaculatedStat)은
            // 복원 이후 Phase 2(PostInitialize)에서 수행되어 저장값을 반영하므로 여기서 호출하지 않는다.
            // [������] ������ Ȯ�ο� �α״� �ѱ۷�
            Debug.Log("[PlayerSystem] ���̺� �ε� �Ϸ�! ��� ���� ���� �Ϸ�.");
        }

        #endregion

        public override void Initialize()
        {
            playerHandle = EntityManager.Create<PlayerData>(1);
            entity_Player = EntityManager.Get<Entity_Player>(playerHandle);
        }

        public override void PostInitialize()
        {
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();
            Entity_Player entity_Player = EntityManager.Get<Entity_Player>(playerHandle);
            m_startingLicense = entity_Player.CurrentLicense;
            m_startingGold = entity_Player.Gold;

            CaculatedStat();
        }

        public void InitializeStat()
        {
            m_baseDamagePerClick = entity_Player.BaseData.BaseDamagePerClick;
            m_baseManualDamagePerHitMultiply = entity_Player.BaseData.BaseManualDamagePerHitMultiply;
            m_baseBattleTimeVariable = entity_Player.BaseData.BaseBattleTimeVariable;
            m_baseCriticalChance = entity_Player.BaseData.BaseCriticalChance;
            m_baseCriticalMultiply = entity_Player.BaseData.BaseCriticalMultiply;

            m_baseAutoBattleCooltime = entity_Player.BaseData.BaseAutoBattleCooltime;
            m_baseAutoSpeedPerTime = entity_Player.BaseData.BaseAutoSpeedPerTime;
            m_baseAutoDamagePerHitMultiply = entity_Player.BaseData.BaseAutoDamagePerHitMultiply;

            m_baseProbabilityAtFishSize = entity_Player.BaseData.BaseProbabilityAtFishSize;
            m_baseProbabilityAtFishRarity = entity_Player.BaseData.BaseProbabilityAtFishRarity;
            m_baseGoldGettingMultiply = entity_Player.BaseData.BaseGoldGettingMultiply;

            m_baseMapMovementSpeedPerTime = entity_Player.BaseData.BaseMapMovementSpeedPerTime;
            m_baseInventorySize = entity_Player.BaseData.BaseInventorySize;
        }

        public void CaculatedStat()
        {
            InitializeStat();

            foreach (EntityHandle equitmentHandle in entity_Player.Equipped.Values)
            {
                Entity_Equipment equipment = EntityManager.Get<Entity_Equipment>(equitmentHandle);

                foreach (StatModifier stat in equipment.CurrentModifiers)
                {
                    switch (stat.Stat)
                    {
                        case PlayerStat.DamagePerClick:
                            m_baseDamagePerClick += (int)stat.Value;
                            break;
                        case PlayerStat.ManualDamagePerHitMultiply:
                            m_baseManualDamagePerHitMultiply += stat.Value;
                            break;
                        case PlayerStat.BattleTimeVariable:
                            m_baseBattleTimeVariable += (int)stat.Value;
                            break;
                        case PlayerStat.CriticalChance:
                            m_baseCriticalChance += stat.Value;
                            break;
                        case PlayerStat.CriticalMultiply:
                            m_baseCriticalMultiply += stat.Value;
                            break;
                        case PlayerStat.AutoBattleCooltime:
                            m_baseAutoBattleCooltime += stat.Value;
                            break;
                        case PlayerStat.AutoSpeedPerTime:
                            m_baseAutoSpeedPerTime += stat.Value;
                            break;
                        case PlayerStat.AutoDamagePerHitMultiply:
                            m_baseAutoDamagePerHitMultiply += stat.Value;
                            break;
                        case PlayerStat.ProbabilityAtFishSize:
                            m_baseProbabilityAtFishSize += stat.Value;
                            break;
                        case PlayerStat.ProbabilityAtFishRarity:
                            m_baseProbabilityAtFishRarity += stat.Value;
                            break;
                        case PlayerStat.GoldGettingMultiply:
                            m_baseGoldGettingMultiply += stat.Value;
                            break;
                        case PlayerStat.MapMovementSpeedPerTime:
                            m_baseMapMovementSpeedPerTime += stat.Value;
                            break;
                        case PlayerStat.InventorySize:
                            m_baseInventorySize += (int)stat.Value;
                            break;
                    }
                }
            }

            OnStatChanged?.Invoke(playerHandle);
        }

        private bool TryFindAndRemoveFromInventory(InventorySystem inventory, EntityHandle handle)
        {
            ItemType[] types = { ItemType.Fish, ItemType.Equipment, ItemType.Materials };

            foreach (var type in types)
            {
                EntityHandle[] slots = inventory.GetSlots(type);
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].Equals(handle))
                    {
                        return inventory.RemoveAt(type, i, false);
                    }
                }
            }
            return false;
        }

        public void Equip(EquipmentMountingArea area, EntityHandle afterEquipHandle)
        {
            Entity_Player player = EntityManager.Get<Entity_Player>(playerHandle);

            if (player.Equipped.TryGetValue(area, out EntityHandle beforeEquipHandle))
            {
                player.Unequip(area);
                m_inventorySystem?.AddItem(beforeEquipHandle);
            }

            if (m_inventorySystem != null)
            {
                bool isRemoved = TryFindAndRemoveFromInventory(m_inventorySystem, afterEquipHandle);
                if (!isRemoved)
                {
                    // [������] ������ Ȯ�ο� �α״� �ѱ۷�
                    Debug.LogWarning("�κ��丮���� �ش� �������� ã�� �� ���ų� ������ �����߽��ϴ�.");
                }
            }

            player.Equip(area, afterEquipHandle);
            CaculatedStat();

            // [������] ������ Ȯ�ο� �α״� �ѱ۷�
            Debug.Log($"{area} ������ ���ο� ��� �����Ǿ����ϴ�.");
        }

        /// <summary>
        /// Ư�� ������ ������ ����� �̸��� ��ȯ�մϴ�. (UI ���� ǥ�ÿ�)
        /// </summary>
        public string GetEquippedItemName(EquipmentMountingArea area)
        {
            // [����] UI�� �ٷ� �Ѿ�� �ؽ�Ʈ�� ��� �����Ͽ� ��Ʈ ���� ����
            if (playerHandle.Value == Guid.Empty) return "Empty Slot";

            Entity_Player player = EntityManager.Get<Entity_Player>(playerHandle);
            if (player != null && player.Equipped.TryGetValue(area, out EntityHandle handle))
            {
                Entity_Equipment equipment = EntityManager.Get<Entity_Equipment>(handle);
                // ����� ���� �̸�(������)�� ������ ����
                return equipment != null ? equipment.Name : "Empty Slot";
            }

            return "Empty Slot";
        }
    }
}