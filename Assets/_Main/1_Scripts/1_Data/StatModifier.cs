using UnityEngine;

namespace DesktopCompanion.Data
{
    /// <summary>장비/소모품이 변경할 수 있는 플레이어 스탯 식별자 (기획서 장비 표 기준).</summary>
    public enum PlayerStat
    {
        // 공통 전투
        DamagePerClick,             // 낚싯대: 기초 공격력
        ManualDamagePerHitMultiply, // 낚싯대: 수동 데미지 배율
        BattleTimeVariable,         // 낚싯줄: 전투 시간 변수
        // 수동 전투
        CriticalChance,             // 릴: 크리티컬 확률
        CriticalMultiply,           // 루어: 크리티컬 배율
        // 자동 전투
        AutoBattleCooltime,         // 모자: 자동 시 다음 낚시 간격
        AutoSpeedPerTime,           // 한벌옷: 자동 시 초당 공격 횟수
        AutoDamagePerHitMultiply,   // 장갑: 자동 시 1타당 기초 공격력 배율
        // 전투 외
        MapMovementSpeedPerTime,    // 엔진: 이동속도
        InventorySize,              // 물고기 창고: 인벤토리
        // 전투 보상
        ProbabilityAtFishSize,      // 미끼: 높은 성급 등장 확률
        ProbabilityAtFishRarity,    // 떡밥: 높은 등급(종) 등장 확률
        GoldGettingMultiply,        // GPS: 판매 시 골드 배율
    }

    public enum ModifierOperation
    {
        Add,        // 합연산
        Multiply,   // 곱연산
    }

    /// <summary>
    /// 플레이어 스탯 변경 정의. "어떤 스탯을, 어떻게, 얼마나".
    /// 적용 순서(Add→Multiply 등)는 PlayerSystem 구현 시 확정.
    /// </summary>
    [System.Serializable]
    public class StatModifier
    {
        [SerializeField] private PlayerStat m_stat;
        [SerializeField] private ModifierOperation m_operation;
        [SerializeField] private float m_value;

        public PlayerStat Stat => m_stat;
        public ModifierOperation Operation => m_operation;
        public float Value => m_value;
    }
}
