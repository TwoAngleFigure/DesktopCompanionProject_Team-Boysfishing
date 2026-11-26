using System;
using System.Collections.Generic;
using DesktopCompanion.Data;

namespace DesktopCompanion.Entities
{
    public readonly struct MainStatValue
    {
        public MainStatKind Kind { get; }
        public float Value { get; }
        internal MainStatValue(MainStatKind kind, float value) => (Kind, Value) = (kind, value);
    }

    public readonly struct EquipmentOption
    {
        public PlayerStat_New Stat { get; }
        public ModifierOperation_New Operation { get; }
        public float Value { get; }
        internal EquipmentOption(PlayerStat_New stat, ModifierOperation_New operation, float value)
            => (Stat, Operation, Value) = (stat, operation, value);
    }

    /// <summary>생산/복원 검증을 통과한 장비 한 개의 확정값. 강화 단계와 소유자는 포함하지 않는다.</summary>
    public sealed class EquipmentState
    {
        public const int CurrentVersion = 1;
        public int Version => CurrentVersion;
        public int DataId { get; }
        public ItemRarity Rarity { get; }
        public MainStatValue? Main { get; }
        public MainStatKind? AttackMainStat { get; }
        public IReadOnlyList<EquipmentOption> Options { get; }

        internal EquipmentState(int dataId, ItemRarity rarity, MainStatValue? main,
            MainStatKind? attackMainStat, IEnumerable<EquipmentOption> options)
        {
            DataId = dataId;
            Rarity = rarity;
            Main = main;
            AttackMainStat = attackMainStat;
            Options = new List<EquipmentOption>(options).AsReadOnly();
        }
    }

    // 저장용 DTO만 가변이다. enum은 이름 문자열로 저장하며 빈 값과 실제 0을 구분한다.
    [Serializable]
    public sealed class EquipmentStateSave
    {
        public int Version;
        public int DataId;
        public string Rarity;
        public MainStatValueSave Main;
        public string AttackMainStat;
        public List<EquipmentOptionSave> Options;
    }

    [Serializable]
    public sealed class MainStatValueSave
    {
        public string Kind;
        public float? Value;
    }

    [Serializable]
    public sealed class EquipmentOptionSave
    {
        public string Stat;
        public string Operation;
        public float? Value;
    }

    [Serializable]
    public sealed class EquipmentSaveEntry
    {
        public string Handle;
        public EquipmentStateSave State;
    }
}
