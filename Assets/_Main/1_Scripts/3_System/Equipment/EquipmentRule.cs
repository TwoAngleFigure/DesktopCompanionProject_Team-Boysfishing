using System;
using System.Collections.Generic;
using System.Linq;
using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Entities;

namespace DesktopCompanion.Systems
{
    /// <summary>장비 한 개의 값을 확정한다. 개체 생성·지급·장착·파일 저장은 호출자의 책임이다.</summary>
    public static class EquipmentRule
    {
        public static bool TryCreateState(DataManager data, EquipmentCreateRequest request, Random random,
            out EquipmentState state, out EquipmentError error)
        {
            state = null;
            error = EquipmentError.InvalidRequest;
            if (data == null || request.DataId <= 0 || !Enum.IsDefined(typeof(EquipmentSource), request.Source))
                return false;
            if (request.Source == EquipmentSource.Shop)
                return TryGetFixedProfile(data, request.DataId, request.FixedProfileId, out state, out error);
            if (request.FixedProfileId != 0 || random == null)
                return false;
            var definition = data.GetData<ItemData_Equipment_New>(request.DataId);
            error = EquipmentError.InvalidDataId;
            if (definition == null)
                return false;
            error = EquipmentError.InvalidRule;
            // 먼저 모든 도달 가능한 결과를 검증한다. 잘못된 설정에서 난수/부분 상태를 소비하지 않는다.
            if (EquipmentDataValidator.ValidateRandom(data, definition).Count != 0)
                return false;

            MainStatValue? main = null;
            ItemRarity rarity;
            if (HasMain(definition.MountingArea))
            {
                TryGetValueDomain(data, definition.MainValueRuleId, out var domain);
                float value = domain.Roll(random);
                main = new MainStatValue(definition.MainStat, value);
                rarity = data.GetAll<EquipmentRarityRange>().Single(x => x.Tier == definition.Tier
                    && value >= x.MinInclusive && value < x.MaxExclusive).Rarity;
            }
            else
            {
                var rows = data.GetAll<EquipmentRarityWeight>()
                    .Where(x => x.GroupId == definition.RarityGroupId && x.Tier == definition.Tier)
                    .OrderBy(x => x.ID).ToList();
                rarity = Pick(rows, x => x.Weight, random).Rarity;
            }

            var options = new List<EquipmentOption>();
            var candidates = data.GetAll<EquipmentOptionPool>()
                .Where(x => x.GroupId == definition.OptionGroupId && x.Tier == definition.Tier && x.Rarity == rarity)
                .OrderBy(x => x.ID).ToList();
            for (int i = 0; i < OptionCount(rarity); i++)
            {
                // 복원 추출: 같은 종류를 다시 선택할 수 있고 각 줄의 값도 따로 추첨한다.
                var option = Pick(candidates, x => x.Weight, random);
                TryGetValueDomain(data, option.ValueRuleId, out var domain);
                options.Add(new EquipmentOption(option.Stat, option.Operation, domain.Roll(random)));
            }
            state = new EquipmentState(definition.ID, rarity, main, AttackKind(definition), options);
            error = EquipmentError.None;
            return true;
        }

        /// <summary>상점 미리보기와 생산이 같은 고정값을 사용한다. 난수/핸들을 만들지 않는다.</summary>
        public static bool TryGetFixedProfile(DataManager data, int dataId, int profileId,
            out EquipmentState state, out EquipmentError error)
        {
            state = null;
            error = EquipmentError.InvalidRequest;
            if (data == null || dataId <= 0 || profileId <= 0)
                return false;
            var definition = data.GetData<ItemData_Equipment_New>(dataId);
            error = EquipmentError.InvalidDataId;
            if (definition == null)
                return false;
            error = EquipmentError.InvalidRule;
            var profile = data.GetData<EquipmentFixedProfile>(profileId);
            if (!ValidDefinition(definition) || profile == null || profile.EquipmentId != dataId
                || !ValidRarity(profile.Rarity) || profile.MainValue.HasValue != HasMain(definition.MountingArea)
                || (profile.MainValue.HasValue && !Finite(profile.MainValue.Value)))
                return false;
            var rows = data.GetAll<EquipmentFixedOption>().Where(x => x.ProfileId == profileId)
                .OrderBy(x => x.Index).ToList();
            if (rows.Count != OptionCount(profile.Rarity))
                return false;
            var options = new List<EquipmentOption>();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.ID <= 0 || row.Index != i || !Finite(row.Value))
                    return false;
                if (!SupportedOption(row.Stat, row.Operation))
                {
                    error = EquipmentError.UnsupportedStat;
                    return false;
                }
                options.Add(new EquipmentOption(row.Stat, row.Operation, row.Value));
            }
            MainStatValue? main = profile.MainValue.HasValue
                ? new MainStatValue(definition.MainStat, profile.MainValue.Value) : null;
            state = new EquipmentState(dataId, profile.Rarity, main, AttackKind(definition), options);
            error = EquipmentError.None;
            return true;
        }

        /// <summary>확정값만 저장 DTO로 복사한다. 반환값을 수정해도 원본 장비는 바뀌지 않는다.</summary>
        public static bool TryCaptureState(EquipmentState state, out EquipmentStateSave save, out EquipmentError error)
        {
            save = null;
            error = EquipmentError.InvalidRequest;
            if (state == null)
                return false;
            save = new EquipmentStateSave
            {
                Version = state.Version, DataId = state.DataId, Rarity = state.Rarity.ToString(),
                Main = state.Main.HasValue ? new MainStatValueSave
                { Kind = state.Main.Value.Kind.ToString(), Value = state.Main.Value.Value } : null,
                AttackMainStat = state.AttackMainStat?.ToString(),
                Options = state.Options.Select(x => new EquipmentOptionSave
                { Stat = x.Stat.ToString(), Operation = x.Operation.ToString(), Value = x.Value }).ToList()
            };
            error = EquipmentError.None;
            return true;
        }

        /// <summary>생산 표를 조회하지 않는다. 저장 당시의 수치·레어도·옵션 순서/중복을 유지한다.</summary>
        public static bool TryRestoreState(ItemData_Equipment_New definition, EquipmentStateSave save,
            out EquipmentState state, out EquipmentError error)
        {
            state = null;
            error = EquipmentError.InvalidSave;
            if (definition == null || save == null || save.Version != EquipmentState.CurrentVersion
                || save.DataId <= 0 || save.DataId != definition.ID
                || !Enum.IsDefined(typeof(EquipmentMountingArea), definition.MountingArea)
                || !TryEnumName(save.Rarity, out ItemRarity rarity) || !ValidRarity(rarity)
                || save.Options == null || save.Options.Count != OptionCount(rarity)
                || (save.Main != null) != HasMain(definition.MountingArea))
                return false;
            MainStatValue? main = null;
            MainStatKind? attack = null;
            if (save.Main != null)
            {
                if (!TryEnumName(save.Main.Kind, out MainStatKind kind) || kind == MainStatKind.None
                    || !save.Main.Value.HasValue || !Finite(save.Main.Value.Value))
                    return false;
                main = new MainStatValue(kind, save.Main.Value.Value);
            }
            if (definition.MountingArea == EquipmentMountingArea.FishingRod)
            {
                if (!TryEnumName(save.AttackMainStat, out MainStatKind kind) || kind == MainStatKind.None)
                    return false;
                attack = kind;
            }
            else if (save.AttackMainStat != null)
                return false;
            var options = new List<EquipmentOption>();
            foreach (var option in save.Options)
            {
                if (option == null || !option.Value.HasValue || !Finite(option.Value.Value)
                    || !TryEnumName(option.Stat, out PlayerStat_New stat)
                    || !TryEnumName(option.Operation, out ModifierOperation_New operation)
                    || !SupportedOption(stat, operation))
                    return false;
                options.Add(new EquipmentOption(stat, operation, option.Value.Value));
            }
            state = new EquipmentState(save.DataId, rarity, main, attack, options);
            error = EquipmentError.None;
            return true;
        }

        public static bool TryGetEffects(EquipmentState state, out EquipmentEffects effects, out EquipmentError error)
        {
            effects = null;
            error = EquipmentError.InvalidRequest;
            if (state == null)
                return false;
            effects = new EquipmentEffects(state);
            error = EquipmentError.None;
            return true;
        }

        public static IReadOnlyList<EquipmentDataIssue> ValidateData(IEnumerable<GameData> source)
            => EquipmentDataValidator.Validate(source);

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool ValidRarity(ItemRarity rarity) => rarity >= ItemRarity.Normal && rarity <= ItemRarity.Legendary;
        internal static int OptionCount(ItemRarity rarity) => (int)rarity;
        internal static bool SupportedOption(PlayerStat_New stat, ModifierOperation_New operation)
            => Enum.IsDefined(typeof(PlayerStat_New), stat) && operation == ModifierOperation_New.Add;
        internal static bool HasMain(EquipmentMountingArea area) => area == EquipmentMountingArea.FishingRod
            || area == EquipmentMountingArea.FishingLine || area == EquipmentMountingArea.Reel || area == EquipmentMountingArea.Lure;
        internal static bool ValidDefinition(ItemData_Equipment_New definition)
            => definition.ID > 0 && definition.Tier > 0
            && Enum.IsDefined(typeof(EquipmentMountingArea), definition.MountingArea)
            && Enum.IsDefined(typeof(MainStatKind), definition.MainStat)
            && HasMain(definition.MountingArea) == (definition.MainStat != MainStatKind.None)
            && Enum.IsDefined(typeof(MainStatKind), definition.AttackMainStat)
            && (definition.MountingArea == EquipmentMountingArea.FishingRod) == (definition.AttackMainStat != MainStatKind.None)
            && definition.MainValueRuleId >= 0 && definition.OptionGroupId >= 0 && definition.RarityGroupId >= 0;
        private static MainStatKind? AttackKind(ItemData_Equipment_New definition)
            => definition.AttackMainStat == MainStatKind.None ? null : definition.AttackMainStat;
        private static bool TryEnumName<T>(string text, out T value) where T : struct, Enum
            => Enum.TryParse(text, false, out value) && Enum.IsDefined(typeof(T), value) && value.ToString() == text;

        internal static T Pick<T>(IReadOnlyList<T> rows, Func<T, int> weight, Random random)
        {
            long total = 0;
            foreach (var row in rows) total += weight(row);
            double roll = random.NextDouble() * total;
            long cumulative = 0;
            foreach (var row in rows)
            {
                cumulative += weight(row);
                if (roll < cumulative) return row;
            }
            throw new InvalidOperationException("검증되지 않은 빈 장비 추첨 표입니다.");
        }

        internal sealed class ValueDomain
        {
            internal EquipmentValueRule Rule;
            internal List<EquipmentValueWeight> Weights;
            internal int Count;
            internal float At(int index) => (float)((double)Rule.Min + (double)Rule.Step * index);
            internal float Roll(Random random) => Weights != null ? Pick(Weights, x => x.Weight, random).Value : At(random.Next(Count));
            internal bool Reaches(double lower, double upper)
            {
                if (Weights != null) return Weights.Any(x => x.Weight > 0 && x.Value >= lower && x.Value < upper);
                // 실제 float 결과의 lower_bound. 간격이 매우 작아도 전체 격자를 열거하지 않는다.
                int left = 0, right = Count;
                while (left < right)
                {
                    int mid = left + (right - left) / 2;
                    if (At(mid) < lower) left = mid + 1;
                    else right = mid;
                }
                return left < Count && At(left) < upper;
            }
        }

        internal static bool TryGetValueDomain(DataManager data, int id, out ValueDomain domain)
        {
            domain = null;
            var rule = data.GetData<EquipmentValueRule>(id);
            if (rule == null || rule.ID <= 0) return false;
            if (rule.Distribution == EquipmentValueDistribution.WeightedValues)
            {
                var weights = data.GetAll<EquipmentValueWeight>().Where(x => x.RuleId == id).OrderBy(x => x.ID).ToList();
                if (weights.Any(x => x.ID <= 0 || x.Weight < 0 || !Finite(x.Value)) || !weights.Any(x => x.Weight > 0))
                    return false;
                domain = new ValueDomain { Rule = rule, Weights = weights };
                return true;
            }
            if (rule.Distribution != EquipmentValueDistribution.UniformStep || !Finite(rule.Min) || !Finite(rule.Max)
                || !Finite(rule.Step) || rule.Step <= 0 || rule.Min > rule.Max)
                return false;
            double intervals = ((double)rule.Max - rule.Min) / rule.Step;
            double rounded = Math.Round(intervals);
            if (rounded < 0 || rounded >= int.MaxValue || Math.Abs(intervals - rounded) > 0.00001)
                return false;
            domain = new ValueDomain { Rule = rule, Count = (int)rounded + 1 };
            // 끝값도 정의된 max와 같은 float여야 한다.
            if (domain.At(domain.Count - 1) != rule.Max)
            {
                domain = null;
                return false;
            }
            return true;
        }
    }
}
