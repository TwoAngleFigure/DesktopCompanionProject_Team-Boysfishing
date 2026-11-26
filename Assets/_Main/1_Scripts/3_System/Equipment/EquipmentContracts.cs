using System.Collections.Generic;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Systems
{
    public enum EquipmentSource { Shop, Craft, Reward }
    public enum EquipmentError { None, InvalidRequest, InvalidDataId, InvalidRule, UnsupportedStat, InvalidSave }

    public readonly struct EquipmentCreateRequest
    {
        public int DataId { get; }
        public EquipmentSource Source { get; }
        public int FixedProfileId { get; }
        public EquipmentCreateRequest(int dataId, EquipmentSource source, int fixedProfileId = 0)
            => (DataId, Source, FixedProfileId) = (dataId, source, fixedProfileId);
    }

    /// <summary>장비 한 개의 효과. 합산/공식 계산/런타임 반영은 Player·Stat 담당자가 수행한다.</summary>
    public sealed class EquipmentEffects
    {
        public MainStatValue? Main { get; }
        public MainStatKind? AttackMainStat { get; }
        public IReadOnlyList<StatModifier_New> Modifiers { get; }
        internal EquipmentEffects(EquipmentState state)
        {
            Main = state.Main;
            AttackMainStat = state.AttackMainStat;
            var modifiers = new List<StatModifier_New>();
            foreach (var option in state.Options)
                modifiers.Add(new StatModifier_New(option.Stat, option.Operation, option.Value));
            Modifiers = modifiers.AsReadOnly();
        }
    }

    public readonly struct EquipmentDataIssue
    {
        public string Sheet { get; }
        public int RowId { get; }
        public string Column { get; }
        public string Message { get; }
        public EquipmentDataIssue(string sheet, int rowId, string column, string message)
            => (Sheet, RowId, Column, Message) = (sheet, rowId, column, message);
        public override string ToString() => $"{Sheet} ID={RowId} {Column}: {Message}";
    }
}
