using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>
    /// 플레이어의 기본값 정의. 현재 주스탯은 Entity_Player_New에, 최종 계산 결과는 PlayerSystem_New에 보관한다.
    /// 시트·JSON은 아래 직렬화 필드 이름에 맞춰 기본값을 제공한다.
    /// </summary>
    [CreateAssetMenu(menuName = "DesktopCompanion/Player New")]
    public class PlayerData_New : GameData
    {
        [Header("Main Stats")]
        [SerializeField] private float m_str;
        [SerializeField] private float m_tec;
        [SerializeField] private float m_luc;

        [Header("Detail Stats")]
        [SerializeField] private float m_healthPoint;
        [SerializeField] private float m_attackPoint;
        [SerializeField] private float m_flatAttackPoint;
        [SerializeField] private float m_armorPenetration;
        [SerializeField] private float m_multiHitChance;
        [SerializeField] private float m_criticalChance;
        [SerializeField] private float m_criticalDamage;
        [SerializeField] private float m_ccDuration;
        [SerializeField] private float m_biteTime;
        [SerializeField] private float m_hookingTiming;
        [SerializeField] private float m_sizeModifier;
        [SerializeField] private float m_rarityProbability;

        [Header("Main Stat Formulas")]
        [SerializeField] private StatFormula_New[] m_statFormulas;

        [Header("Auto Fishing Settings")]
        [Tooltip("자동 릴링 공격 간격(초). 수동·자동 공격력은 동일하다.")]
        [SerializeField] private float m_autoAttackInterval = 1f;

        [Header("Existing Gameplay Settings")]
        [SerializeField] private float m_baseGoldGettingMultiply;
        [SerializeField] private float m_baseMapMovementSpeedPerTime;
        [SerializeField] private int m_baseInventorySize;
        [SerializeField] private int m_startingLicense;
        [SerializeField] private int m_startingGold;

        public float Str => m_str;
        public float Tec => m_tec;
        public float Luk => m_luc;

        public float HealthPoint => m_healthPoint;
        public float AttackPoint => m_attackPoint;
        public float FlatAttackPoint => m_flatAttackPoint;
        public float ArmorPenetration => m_armorPenetration;
        public float MultiHitChance => m_multiHitChance;
        public float CriticalChance => m_criticalChance;
        public float CriticalDamage => m_criticalDamage;
        public float CCDuration => m_ccDuration;
        public float BiteTime => m_biteTime;
        public float HookingTiming => m_hookingTiming;
        public float SizeModifier => m_sizeModifier;
        public float RarityProbability => m_rarityProbability;

        public IReadOnlyList<StatFormula_New> StatFormulas => m_statFormulas ?? System.Array.Empty<StatFormula_New>();

        public float AutoAttackInterval => m_autoAttackInterval;
        public float BaseGoldGettingMultiply => m_baseGoldGettingMultiply;
        public float BaseMapMovementSpeedPerTime => m_baseMapMovementSpeedPerTime;
        public int BaseInventorySize => m_baseInventorySize;
        public int StartingLicense => m_startingLicense;
        public int StartingGold => m_startingGold;
    }
}
