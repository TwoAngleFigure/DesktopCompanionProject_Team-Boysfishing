using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>장비/소모품이 변경할 수 있는 플레이어 스탯 식별자 (기획서 장비 표 기준).</summary>
    public enum PlayerStat_New
    {
        // 신규 정의에서 사용하는 ID는 유지하되, 옛 이름은 변환 경계에서만 처리한다.
        CriticalChance = 3,
        MapMovementSpeedPerTime = 8,
        InventorySize = 9,
        GoldGettingMultiply = 12,

        HealthPoint = 13,
        AttackPoint = 14,
        FlatAttackPoint = 15,
        ArmorPenetration = 16,
        MultiHitChance = 17,
        CriticalDamage = 18,
        CCDuration = 19,
        BiteTime = 20,
        HookingTiming = 21,
        SizeModifier = 22,
        RarityProbability = 23,
    }

    public enum ModifierOperation_New
    {
        Add,        // 합연산
        Multiply,   // 규칙 미정: 현재 계산에서는 경고 후 제외
    }

    /// <summary>
    /// 플레이어 스탯 변경 정의. "어떤 스탯을, 어떻게, 얼마나".
    /// 연산 규칙은 StatSystem에서 처리한다. Multiply 규칙 연결은 후속 작업이다.
    /// </summary>
    [System.Serializable]
    public class StatModifier_New
    {
        [SerializeField] private PlayerStat_New m_stat = PlayerStat_New.HealthPoint;
        [SerializeField] private ModifierOperation_New m_operation;
        [SerializeField] private float m_value;

        public StatModifier_New() { }

        public StatModifier_New(PlayerStat_New stat, ModifierOperation_New operation, float value)
        {
            m_stat = stat;
            m_operation = operation;
            m_value = value;
        }

        public PlayerStat_New Stat => m_stat;
        public ModifierOperation_New Operation => m_operation;
        public float Value => m_value;
    }
}
