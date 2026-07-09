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
    /// 플레이어의 능력치를 관리하고, 장비 장착에 따른 스탯 변화를 실시간으로 계산하는 시스템입니다.
    /// </summary>
    public class PlayerSystem : SystemBase, ISaveable
    {
        EntityHandle playerHandle;
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

        // ==========================================
        // [ISaveable 인터페이스 구현부]
        // ==========================================
        public string SaveId => "player_system_stats";
        public Type StateType => typeof(string);

        public object CaptureState()
        {
            return "stats_calculated_dynamically";
        }

        public void RestoreState(object state)
        {
            CaculatedStat();
            // [복구됨] 개발자 확인용 로그는 한글로
            Debug.Log("[PlayerSystem] 세이브 로드 완료! 장비 스탯 재계산 완료.");
        }
        // ==========================================

        public override void Initialize()
        {
            playerHandle = EntityManager.Create<PlayerData>(1);
        }

        public override void PostInitialize()
        {
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();
            Entity_Player entity_Player = EntityManager.Get<Entity_Player>(playerHandle);
            m_startingLicense = entity_Player.CurrentLicense;
            m_startingGold = entity_Player.Gold;

            
            CaculatedStat();
        }

        public void CaculatedStat()
        {
            Entity_Player entity_Player = EntityManager.Get<Entity_Player>(playerHandle);

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
                        return inventory.TryRemoveAt(type, i, false);
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
                    // [복구됨] 개발자 확인용 로그는 한글로
                    Debug.LogWarning("인벤토리에서 해당 아이템을 찾을 수 없거나 삭제에 실패했습니다.");
                }
            }

            player.Equip(area, afterEquipHandle);
            CaculatedStat();

            // [복구됨] 개발자 확인용 로그는 한글로
            Debug.Log($"{area} 부위에 새로운 장비가 장착되었습니다.");
        }

        /// <summary>
        /// 특정 부위에 장착된 장비의 이름을 반환합니다. (UI 슬롯 표시용)
        /// </summary>
        public string GetEquippedItemName(EquipmentMountingArea area)
        {
            // [유지] UI로 바로 넘어가는 텍스트는 영어를 유지하여 폰트 깨짐 방지
            if (playerHandle.Value == Guid.Empty) return "Empty Slot";

            Entity_Player player = EntityManager.Get<Entity_Player>(playerHandle);
            if (player != null && player.Equipped.TryGetValue(area, out EntityHandle handle))
            {
                Entity_Equipment equipment = EntityManager.Get<Entity_Equipment>(handle);
                // 장비의 원래 이름(데이터)이 영어라고 가정
                return equipment != null ? equipment.Name : "Empty Slot";
            }

            return "Empty Slot";
        }
    }
}