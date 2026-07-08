using System; // [수정됨] Type 에러 해결을 위해 추가
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

        public Action<PlayerStat> OnStatChanged;

        // [ISaveable 인터페이스 구현부]26-07-08 추가
        public string SaveId => "player_system_stats";
        public Type StateType => typeof(string);

        public object CaptureState()
        {
            // 스탯은 저장할 필요가 없으므로 더미(Dummy) 데이터를 넘깁니다.
            return "stats_calculated_dynamically";
        }

        public void RestoreState(object state)
        {
            // GameManager가 세이브 파일 로드를 끝낸 직후에 자동으로 이 함수를 실행해 줍니다.
            CaculatedStat();
            Debug.Log("[PlayerSystem] 세이브 로드 완료! 장비 스탯 재계산 완료.");
        }
        // ==========================================

        /// <summary>
        /// Phase 1: 시스템 초기화 시 플레이어 데이터를 생성합니다.
        /// </summary>
        public override void Initialize()
        {
            // [수정됨] 중복되었던 Initialize 메서드를 하나로 정리했습니다.
            playerHandle = EntityManager.Create<PlayerData>(1);
        }

        /// <summary>
        /// Phase 2: 다른 시스템이 모두 준비된 후 인벤토리 시스템을 찾아 캐싱합니다.
        /// </summary>
        public override void PostInitialize()
        {
            m_inventorySystem = SystemManager.GetSystem<InventorySystem>();
        }

        /// <summary>
        /// 장착된 모든 장비의 모디파이어를 합산하여 최종 스탯을 갱신합니다.
        /// </summary>
        public void CaculatedStat()
        {
            Entity_Player entity_Player = EntityManager.Get<Entity_Player>(playerHandle);

            // 1. 초기화: 모든 스탯을 캐릭터 고유의 기본값(BaseData)으로 리셋합니다.
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

            bool m_baseDamagePerClick_Tri = false;
            bool m_baseManualDamagePerHitMultiply_Tri = false;
            bool m_baseBattleTimeVariable_Tri = false;
            bool m_baseCriticalChance_Tri = false;
            bool m_baseCriticalMultiply_Tri = false;
            bool m_baseAutoBattleCooltime_Tri = false;
            bool m_baseAutoSpeedPerTime_Tri = false;
            bool m_baseAutoDamagePerHitMultiply_Tri = false;
            bool m_baseProbabilityAtFishSize_Tri = false;
            bool m_baseProbabilityAtFishRarity_Tri = false;
            bool m_baseGoldGettingMultiply_Tri = false;
            bool m_baseMapMovementSpeedPerTime_Tri = false;
            bool m_baseInventorySize_Tri = false;

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
                            m_baseDamagePerClick_Tri = true;
                            break;
                        case PlayerStat.BattleTimeVariable:
                            m_baseBattleTimeVariable += (int)stat.Value;
                            m_baseBattleTimeVariable_Tri = true;
                            break;
                        case PlayerStat.InventorySize:
                            m_baseInventorySize += (int)stat.Value;
                            m_baseInventorySize_Tri = true;
                            break;

                        // 실수형 스탯은 데이터 정밀도 유지를 위해 형변환 없이 더함
                        case PlayerStat.ManualDamagePerHitMultiply:
                            m_baseManualDamagePerHitMultiply += stat.Value;
                            m_baseManualDamagePerHitMultiply_Tri = true;
                            break;
                        case PlayerStat.CriticalChance:
                            m_baseCriticalChance += stat.Value;
                            m_baseCriticalChance_Tri = true;
                            break;
                        case PlayerStat.CriticalMultiply:
                            m_baseCriticalMultiply += stat.Value;
                            m_baseCriticalMultiply_Tri = true;
                            break;
                        case PlayerStat.AutoBattleCooltime:
                            m_baseAutoBattleCooltime += stat.Value;
                            m_baseAutoBattleCooltime_Tri = true;
                            break;
                        case PlayerStat.AutoSpeedPerTime:
                            m_baseAutoSpeedPerTime += stat.Value;
                            m_baseAutoSpeedPerTime_Tri = true;
                            break;
                        case PlayerStat.AutoDamagePerHitMultiply:
                            m_baseAutoDamagePerHitMultiply += stat.Value;
                            m_baseAutoDamagePerHitMultiply_Tri = true;
                            break;
                        case PlayerStat.MapMovementSpeedPerTime:
                            m_baseMapMovementSpeedPerTime += stat.Value;
                            m_baseMapMovementSpeedPerTime_Tri = true;
                            break;
                        case PlayerStat.ProbabilityAtFishSize:
                            m_baseProbabilityAtFishSize += stat.Value;
                            m_baseProbabilityAtFishSize_Tri = true;
                            break;
                        case PlayerStat.ProbabilityAtFishRarity:
                            m_baseProbabilityAtFishRarity += stat.Value;
                            m_baseProbabilityAtFishRarity_Tri = true;
                            break;
                        case PlayerStat.GoldGettingMultiply:
                            m_baseGoldGettingMultiply += stat.Value;
                            m_baseGoldGettingMultiply_Tri = true;
                            break;
                    }
                }
            }

            if (m_baseDamagePerClick_Tri) OnStatChanged.Invoke(PlayerStat.DamagePerClick);
            if (m_baseManualDamagePerHitMultiply_Tri) OnStatChanged.Invoke(PlayerStat.ManualDamagePerHitMultiply);
            if (m_baseBattleTimeVariable_Tri) OnStatChanged.Invoke(PlayerStat.BattleTimeVariable);
            if (m_baseCriticalChance_Tri) OnStatChanged.Invoke(PlayerStat.CriticalChance);
            if (m_baseCriticalMultiply_Tri) OnStatChanged.Invoke(PlayerStat.CriticalMultiply);
            if (m_baseAutoBattleCooltime_Tri) OnStatChanged.Invoke(PlayerStat.AutoBattleCooltime);
            if (m_baseAutoSpeedPerTime_Tri) OnStatChanged.Invoke(PlayerStat.AutoSpeedPerTime);
            if (m_baseAutoDamagePerHitMultiply_Tri) OnStatChanged.Invoke(PlayerStat.AutoDamagePerHitMultiply);
            if (m_baseProbabilityAtFishSize_Tri) OnStatChanged.Invoke(PlayerStat.ProbabilityAtFishSize);
            if (m_baseProbabilityAtFishRarity_Tri) OnStatChanged.Invoke(PlayerStat.ProbabilityAtFishRarity);
            if (m_baseGoldGettingMultiply_Tri) OnStatChanged.Invoke(PlayerStat.GoldGettingMultiply);
            if (m_baseMapMovementSpeedPerTime_Tri) OnStatChanged.Invoke(PlayerStat.MapMovementSpeedPerTime);
            if (m_baseInventorySize_Tri) OnStatChanged.Invoke(PlayerStat.InventorySize);
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

            // [수정됨] 매번 GetSystem을 부르지 않고 PostInitialize에서 찾아둔 변수(m_inventorySystem)를 사용합니다.

            // 기존 장비 교체 로직
            if (player.Equipped.TryGetValue(area, out EntityHandle beforeEquipHandle))
            {
                player.Unequip(area);
                m_inventorySystem?.AddItem(beforeEquipHandle); // 인벤토리로 되돌림
            }

            if (m_inventorySystem != null)
            {
                bool isRemoved = TryFindAndRemoveFromInventory(m_inventorySystem, afterEquipHandle);
                if (!isRemoved)
                {
                    Debug.LogWarning("인벤토리에서 해당 아이템을 찾을 수 없거나 삭제에 실패했습니다.");
                }
            }

            // 새로운 장비 장착
            player.Equip(area, afterEquipHandle);
            CaculatedStat();

            Debug.Log($"{area} 부위에 새로운 장비가 장착되었습니다.");
        }
    }
}