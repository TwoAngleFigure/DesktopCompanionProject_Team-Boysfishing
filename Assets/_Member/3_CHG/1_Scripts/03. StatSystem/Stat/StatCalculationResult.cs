namespace DesktopCompanion.Systems
{
    /// <summary>전체 계산 결과의 전달용 객체. 최종값은 PlayerSystem_New만 보관한다.</summary>
    public sealed class StatCalculationResult
    {
        public float HealthPoint { get; internal set; }
        public float AttackPoint { get; internal set; }
        public float FlatAttackPoint { get; internal set; }
        public float ArmorPenetration { get; internal set; }
        public float MultiHitChance { get; internal set; }
        public float CriticalChance { get; internal set; }
        public float CriticalDamage { get; internal set; }
        public float CCDuration { get; internal set; }
        public float BiteTime { get; internal set; }
        public float HookingTiming { get; internal set; }
        public float SizeModifier { get; internal set; }
        public float RarityProbability { get; internal set; }

        public float GoldGettingMultiply { get; internal set; }
        public float MapMovementSpeedPerTime { get; internal set; }
        public int InventorySize { get; internal set; }
    }
}
