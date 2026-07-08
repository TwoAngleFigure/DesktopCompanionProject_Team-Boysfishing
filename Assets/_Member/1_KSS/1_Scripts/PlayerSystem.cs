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
        public void Equip(EquipmentMountingArea area, EntityHandle afterEquipHandle)
        {
            // 1. 플레이어 데이터 가져오기
            Entity_Player player = EntityManager.Get<Entity_Player>(playerHandle);

            // (가정) 인벤토리 시스템에 접근 (실제 환경에 맞게 EntityManager.Get 등으로 수정하세요)
            // var inventory = EntityManager.Get<Entity_Inventory>(playerHandle); 

            // 2. [교체 로직] 이미 해당 슬롯에 장비가 있는지 확인
            if (player.Equipped.TryGetValue(area, out EntityHandle beforeEquipHandle))
            {
                // A. 기존 장비 해제 (내부 데이터 삭제)
                player.Unequip(area);

                // B. 기존 장비를 인벤토리에 다시 추가
                // inventory.AddItem(beforeEquipHandle);
                Debug.Log($"기존 장비가 인벤토리로 이동되었습니다: {beforeEquipHandle}");
            }

            // 3. 새로운 장비 처리
            // A. 새 장비를 인벤토리에서 제거
            // inventory.RemoveItem(afterEquipHandle);

            // B. 새로운 장비 장착
            player.Equip(area, afterEquipHandle);

            // 4. 장비 장착 완료 후 전체 능력치 다시 계산
            CaculatedStat();

            Debug.Log($"{area} 부위에 새로운 장비가 장착되었습니다.");
        }
    }
}