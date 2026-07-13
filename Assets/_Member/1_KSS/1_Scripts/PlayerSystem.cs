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
    /// 플레이어의 기본 스탯을 관리하고, 장비 장착에 따른 스탯 변화를 실시간으로 계산하는 시스템입니다.
    /// </summary>
    public class PlayerSystem : SystemBase, ISaveable
    {
        private EntityHandle playerHandle;
        private Entity_Player entity_Player;
        private InventorySystem m_inventorySystem;

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

            // [디버그] 저장 데이터 복원 확인용 로그
            Debug.Log("[PlayerSystem] 세이브 데이터 복원 완료! 원시 상태 로드 성공.");
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
                    // [경고] 장착하려는 아이템이 인벤토리에 없을 경우 경고 로그 출력
                    Debug.LogWarning("[PlayerSystem] 인벤토리에서 장착할 아이템을 찾을 수 없거나 제거에 실패했습니다.");
                }
            }

            player.Equip(area, afterEquipHandle);
            CaculatedStat();

            // [디버그] 정상적으로 장착되었음을 알리는 로그 출력
            Debug.Log($"[PlayerSystem] {area} 슬롯에 새로운 장비가 성공적으로 장착되었습니다.");
        }

        /// <summary>
        /// 특정 장비 슬롯에 장착된 아이템의 이름을 반환합니다. (UI 표시용)
        /// </summary>
        public string GetEquippedItemName(EquipmentMountingArea area)
        {
            // [예외 처리] 플레이어 엔티티가 없거나 핸들이 비어있을 경우 텍스트 에러 방지용 "Empty Slot" 반환
            if (playerHandle.Value == Guid.Empty) return "Empty Slot";

            Entity_Player player = EntityManager.Get<Entity_Player>(playerHandle);
            if (player != null && player.Equipped.TryGetValue(area, out EntityHandle handle))
            {
                Entity_Equipment equipment = EntityManager.Get<Entity_Equipment>(handle);
                // 장비 데이터가 유효하면 해당 장비의 이름을 반환하고, 없으면 "Empty Slot" 반환
                return equipment != null ? equipment.Name : "Empty Slot";
            }

            return "Empty Slot";
        }

        /// <summary>
        /// 특정 구역에 장착된 장비의 실제 데이터(EntityHandle)를 반환합니다. (드래그 탈착용)
        /// </summary>
        public EntityHandle GetEquippedItemHandle(EquipmentMountingArea area)
        {
            if (playerHandle.Value == Guid.Empty) return default;

            Entity_Player player = EntityManager.Get<Entity_Player>(playerHandle);
            if (player != null && player.Equipped.TryGetValue(area, out EntityHandle handle))
            {
                return handle;
            }

            return default;
        }
    }
}