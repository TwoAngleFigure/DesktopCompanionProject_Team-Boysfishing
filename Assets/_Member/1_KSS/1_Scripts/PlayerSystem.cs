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

        // [팀장님 프레임워크 규칙 적용] UI 등 외부에 알릴 때 '누구의' 스탯이 변했는지 핸들을 넘겨줍니다.
        public event Action<EntityHandle> OnStatChanged;

        #region Stats

        // ==========================================
        // 1. 능력치 변수 선언부 (기준점)
        // ==========================================
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

        // ==========================================
        // 2. 외부 읽기용 프로퍼티 (선언부와 순서 동일)
        // ==========================================
        // Battle
        public int BaseDamagePerClick => m_baseDamagePerClick;
        public float BaseManualDamagePerHitMultiply => m_baseManualDamagePerHitMultiply;
        public int BaseBattleTimeVariable => m_baseBattleTimeVariable;
        public float BaseCriticalChance => m_baseCriticalChance;
        public float BaseCriticalMultiply => m_baseCriticalMultiply;

        // Auto Battle
        public float BaseAutoBattleCooltime => m_baseAutoBattleCooltime;
        public float BaseAutoSpeedPerTime => m_baseAutoSpeedPerTime;
        public float BaseAutoDamagePerHitMultiply => m_baseAutoDamagePerHitMultiply;

        // Battle Reward
        public float BaseProbabilityAtFishSize => m_baseProbabilityAtFishSize;
        public float BaseProbabilityAtFishRarity => m_baseProbabilityAtFishRarity;
        public float BaseGoldGettingMultiply => m_baseGoldGettingMultiply;

        // Other
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
            // 스탯은 저장할 필요가 없으므로 더미(Dummy) 데이터를 넘깁니다.
            return "stats_calculated_dynamically";
        }

        public void RestoreState(object state)
        {
            // GameManager가 세이브 파일 로드를 끝낸 직후에 자동으로 실행
            CaculatedStat();
            Debug.Log("[PlayerSystem] 세이브 로드 완료! 장비 스탯 재계산 완료.");
        }
        // ==========================================

        /// <summary>
        /// Phase 1: 시스템 초기화 시 플레이어 데이터를 생성합니다.
        /// </summary>
        public override void Initialize()
        {
            playerHandle = EntityManager.Create<PlayerData>(1);
        }

        /// <summary>
        /// Phase 2: 다른 시스템이 모두 준비된 후 인벤토리 시스템을 찾아 캐싱하고, 기본 스탯을 계산합니다.
        /// </summary>
        public override void PostInitialize()
        {
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();

            // 스탯이 0으로 출력되는 버그를 막기 위해 최초 1회 무조건 계산 실행
            CaculatedStat();
        }

        /// <summary>
        /// 장착된 모든 장비의 모디파이어를 합산하여 최종 스탯을 갱신합니다.
        /// </summary>
        public void CaculatedStat()
        {
            Entity_Player entity_Player = EntityManager.Get<Entity_Player>(playerHandle);

            // ==========================================
            // 3. 스탯 초기화 로직 (선언부와 순서 동일)
            // ==========================================
            // Battle
            m_baseDamagePerClick = entity_Player.BaseData.BaseDamagePerClick;
            m_baseManualDamagePerHitMultiply = entity_Player.BaseData.BaseManualDamagePerHitMultiply;
            m_baseBattleTimeVariable = entity_Player.BaseData.BaseBattleTimeVariable;
            m_baseCriticalChance = entity_Player.BaseData.BaseCriticalChance;
            m_baseCriticalMultiply = entity_Player.BaseData.BaseCriticalMultiply;

            // Auto Battle
            m_baseAutoBattleCooltime = entity_Player.BaseData.BaseAutoBattleCooltime;
            m_baseAutoSpeedPerTime = entity_Player.BaseData.BaseAutoSpeedPerTime;
            m_baseAutoDamagePerHitMultiply = entity_Player.BaseData.BaseAutoDamagePerHitMultiply;

            // Battle Reward
            m_baseProbabilityAtFishSize = entity_Player.BaseData.BaseProbabilityAtFishSize;
            m_baseProbabilityAtFishRarity = entity_Player.BaseData.BaseProbabilityAtFishRarity;
            m_baseGoldGettingMultiply = entity_Player.BaseData.BaseGoldGettingMultiply;

            // Other
            m_baseMapMovementSpeedPerTime = entity_Player.BaseData.BaseMapMovementSpeedPerTime;
            m_baseInventorySize = entity_Player.BaseData.BaseInventorySize;

            // ==========================================
            // 4. 장비 모디파이어 합산 (선언부와 순서 동일하게 재배치)
            // ==========================================
            foreach (EntityHandle equitmentHandle in entity_Player.Equipped.Values)
            {
                Entity_Equipment equipment = EntityManager.Get<Entity_Equipment>(equitmentHandle);

                foreach (StatModifier stat in equipment.CurrentModifiers)
                {
                    switch (stat.Stat)
                    {
                        // [Battle]
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

                        // [Auto Battle]
                        case PlayerStat.AutoBattleCooltime:
                            m_baseAutoBattleCooltime += stat.Value;
                            break;
                        case PlayerStat.AutoSpeedPerTime:
                            m_baseAutoSpeedPerTime += stat.Value;
                            break;
                        case PlayerStat.AutoDamagePerHitMultiply:
                            m_baseAutoDamagePerHitMultiply += stat.Value;
                            break;

                        // [Battle Reward]
                        case PlayerStat.ProbabilityAtFishSize:
                            m_baseProbabilityAtFishSize += stat.Value;
                            break;
                        case PlayerStat.ProbabilityAtFishRarity:
                            m_baseProbabilityAtFishRarity += stat.Value;
                            break;
                        case PlayerStat.GoldGettingMultiply:
                            m_baseGoldGettingMultiply += stat.Value;
                            break;

                        // [Other]
                        case PlayerStat.MapMovementSpeedPerTime:
                            m_baseMapMovementSpeedPerTime += stat.Value;
                            break;
                        case PlayerStat.InventorySize:
                            m_baseInventorySize += (int)stat.Value;
                            break;
                    }
                }
            }

            // 스탯 계산이 끝난 후, 내 핸들(playerHandle)을 함께 넘겨줍니다.
            OnStatChanged?.Invoke(playerHandle);
        }

        /// <summary>
        /// PlayerSystem 내부에 아이템을 찾아 위치를 이동(Remove)시키는 헬퍼 메서드
        /// </summary>
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

        /// <summary>
        /// 특정 부위에 장비를 장착하고 능력치를 재계산합니다.
        /// </summary>
        public void Equip(EquipmentMountingArea area, EntityHandle afterEquipHandle)
        {
            Entity_Player player = EntityManager.Get<Entity_Player>(playerHandle);

            // 1. 기존 장비 교체 로직 (장착 해제 후 인벤토리로 되돌림)
            if (player.Equipped.TryGetValue(area, out EntityHandle beforeEquipHandle))
            {
                player.Unequip(area);
                m_inventorySystem?.AddItem(beforeEquipHandle);
            }

            // 2. 인벤토리에서 새 장비 꺼내기
            if (m_inventorySystem != null)
            {
                bool isRemoved = TryFindAndRemoveFromInventory(m_inventorySystem, afterEquipHandle);
                if (!isRemoved)
                {
                    Debug.LogWarning("인벤토리에서 해당 아이템을 찾을 수 없거나 삭제에 실패했습니다.");
                }
            }

            // 3. 새로운 장비 장착 및 스탯 갱신
            player.Equip(area, afterEquipHandle);
            CaculatedStat();

            Debug.Log($"{area} 부위에 새로운 장비가 장착되었습니다.");
        }
    }
}