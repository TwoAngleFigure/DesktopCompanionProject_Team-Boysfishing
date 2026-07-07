using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;
using DesktopCompanion.Systems;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 플레이어의 능력치를 관리하고, 장비 장착에 따른 스탯 변화를 실시간으로 계산하는 시스템입니다.
    /// </summary>
    public class PlayerSystem : SystemBase
    {
        EntityHandle playerHandle;

        #region Stats

        // 능력치 분류별 헤더 설정
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

        // 외부에서 계산된 최종 능력치를 읽기 위한 프로퍼티들
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

        /// <summary>
        /// 시스템 초기화 시 플레이어 데이터를 생성합니다.
        /// </summary>
        public override void Initialize()
        {
            playerHandle = EntityManager.Create<PlayerData>(1);
        }

        /// <summary>
        /// 장착된 모든 장비의 모디파이어를 합산하여 최종 스탯을 갱신합니다.
        /// </summary>
        public void CaculatedStat()
        {
            Entity_Player entity_Player = EntityManager.Get<Entity_Player>(playerHandle);

            // 1. 초기화: 모든 스탯을 캐릭터 고유의 기본값(BaseData)으로 리셋합니다.
            // 이렇게 함으로써 이전 장비의 효과가 남지 않도록 보장합니다.
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

            // 2. 장착된 장비들의 스탯 모디파이어를 순회하며 추가 합산합니다.
            foreach (EntityHandle equitmentHandle in entity_Player.Equipped.Values)
            {
                Entity_Equipment equipment = EntityManager.Get<Entity_Equipment>(equitmentHandle);

                foreach (StatModifier stat in equipment.CurrentModifiers)
                {
                    switch (stat.Stat)
                    {
                        // 정수형 스탯은 int로 형변환하여 더함
                        case PlayerStat.DamagePerClick:
                            m_baseDamagePerClick += (int)stat.Value;
                            break;

                        // 실수형 스탯은 데이터 정밀도 유지를 위해 형변환 없이 더함
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
                        case PlayerStat.MapMovementSpeedPerTime:
                            m_baseMapMovementSpeedPerTime += stat.Value;
                            break;
                        case PlayerStat.InventorySize:
                            m_baseInventorySize += (int)stat.Value;
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
                    }
                }
            }
        }

        /// <summary>
        /// 특정 부위에 장비를 장착하고 능력치를 재계산합니다.
        /// </summary>
        /// <param name="area">장착할 슬롯 부위</param>
        /// <param name="handle">장비의 엔티티 핸들</param>
        private bool TryFindAndRemoveFromInventory(InventorySystem inventory, EntityHandle handle)
        {
            // (현재 InventorySystem 구조상 Fish, Equipment, Materials 세 종류로 관리됨)
            ItemType[] types = { ItemType.Fish, ItemType.Equipment, ItemType.Materials };

            foreach (var type in types)
            {
                EntityHandle[] slots = inventory.GetSlots(type);
                for (int i = 0; i < slots.Length; i++)
                {
                    // 슬롯의 핸들과 찾으려는 핸들이 같다면
                    if (slots[i].Equals(handle))
                    {
                        // 해당 인덱스의 아이템을 이동 요청 (destroyEntity: false)
                        return inventory.TryRemoveAt(type, i, false);
                    }
                }
            }
            return false;
        }

        //  수정된 Equip 메서드
        public void Equip(EquipmentMountingArea area, EntityHandle afterEquipHandle)
        {
            Entity_Player player = EntityManager.Get<Entity_Player>(playerHandle);
            var inventory = SystemManager.GetSystem<InventorySystem>();

            //  기존 장비 교체 로직
            if (player.Equipped.TryGetValue(area, out EntityHandle beforeEquipHandle))
            {
                player.Unequip(area);
                inventory?.AddItem(beforeEquipHandle); // 인벤토리로 되돌림
            }

           
            if (inventory != null)
            {
                bool isRemoved = TryFindAndRemoveFromInventory(inventory, afterEquipHandle);
                if (!isRemoved)
                {
                    Debug.LogWarning("인벤토리에서 해당 아이템을 찾을 수 없거나 삭제에 실패했습니다.");
                }
            }

            //  새로운 장비 장착
            player.Equip(area, afterEquipHandle);
            CaculatedStat();

            Debug.Log($"{area} 부위에 새로운 장비가 장착되었습니다.");
        }
    }
}
