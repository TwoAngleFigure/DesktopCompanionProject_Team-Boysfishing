using System;
using System.Collections.Generic;
using System.Linq;
using DesktopCompanion.Core;
using DesktopCompanion.Data;

namespace DesktopCompanion.Systems
{
    internal static class EquipmentDataValidator
    {
        internal static IReadOnlyList<EquipmentDataIssue> Validate(IEnumerable<GameData> source)
        {
            var issues = new List<EquipmentDataIssue>();
            if (source == null)
            {
                issues.Add(new EquipmentDataIssue("Equipment", 0, "data", "데이터가 없습니다."));
                return issues.AsReadOnly();
            }
            var rows = source.Where(x => x != null).ToList();
            foreach (var duplicate in rows.GroupBy(x => (x.GetType(), x.ID)).Where(x => x.Count() > 1))
                Add(issues, duplicate.First(), "m_id", "중복 ID입니다.");
            // DataManager가 중복을 버리기 전에 위에서 보고한다.
            var data = new DataManager();
            data.Load(rows.GroupBy(x => (x.GetType(), x.ID)).Select(x => x.First()));
            foreach (var row in rows.Where(x => IsRuleType(x.GetType())))
                if (row.ID <= 0) Add(issues, row, "m_id", "양의 고유 ID가 필요합니다.");

            foreach (var rule in data.GetAll<EquipmentValueRule>())
                if (!EquipmentRule.TryGetValueDomain(data, rule.ID, out _))
                    Add(issues, rule, "m_distribution/m_min/m_max/m_step", "격자 또는 가중치 값 표가 유효하지 않습니다.");
            foreach (var row in data.GetAll<EquipmentValueWeight>())
            {
                if (data.GetData<EquipmentValueRule>(row.RuleId)?.Distribution != EquipmentValueDistribution.WeightedValues)
                    Add(issues, row, "m_ruleId", "WeightedValues 규칙을 참조해야 합니다.");
                if (!EquipmentRule.Finite(row.Value) || row.Weight < 0)
                    Add(issues, row, "m_value/m_weight", "유한 수치와 0 이상 가중치가 필요합니다.");
            }
            foreach (var group in data.GetAll<EquipmentRarityRange>().GroupBy(x => x.Tier))
                ValidateRanges(group.OrderBy(x => x.MinInclusive).ToList(), issues);
            foreach (var group in data.GetAll<EquipmentRarityWeight>().GroupBy(x => (x.GroupId, x.Tier)))
            {
                foreach (var row in group)
                    if (row.GroupId <= 0 || row.Tier <= 0 || row.Weight < 0 || !EquipmentRule.ValidRarity(row.Rarity))
                        Add(issues, row, "m_groupId/m_tier/m_rarity/m_weight", "그룹·티어·레어도·가중치를 확인하세요.");
                if (!group.Any(x => x.Weight > 0)) Add(issues, group.First(), "m_weight", "양수 가중치가 한 개 이상 필요합니다.");
            }
            foreach (var row in data.GetAll<EquipmentOptionPool>()) ValidateOption(data, row, issues);
            foreach (var group in data.GetAll<EquipmentOptionPool>().GroupBy(x => (x.GroupId, x.Tier, x.Rarity)))
                if (!group.Any(x => x.Weight > 0)) Add(issues, group.First(), "m_weight", "양수 가중치가 한 개 이상 필요합니다.");
            foreach (var row in data.GetAll<EquipmentFixedOption>())
            {
                if (data.GetData<EquipmentFixedProfile>(row.ProfileId) == null)
                    Add(issues, row, "m_profileId", "고정 프로필을 찾을 수 없습니다.");
                if (row.Index < 0 || !EquipmentRule.Finite(row.Value) || !EquipmentRule.SupportedOption(row.Stat, row.Operation))
                    Add(issues, row, "m_index/m_stat/m_operation/m_value", "0 이상 순번과 지원하는 Add 스탯/유한 수치가 필요합니다.");
            }
            foreach (var profile in data.GetAll<EquipmentFixedProfile>())
                if (!EquipmentRule.TryGetFixedProfile(data, profile.EquipmentId, profile.ID, out _, out var error))
                    Add(issues, profile, "m_equipmentId/m_rarity/m_mainValue", $"프로필 또는 연결된 옵션이 유효하지 않습니다: {error}");
            foreach (var definition in data.GetAll<ItemData_Equipment_New>())
            {
                bool randomConfigured = definition.MainValueRuleId != 0 || definition.RarityGroupId != 0 || definition.OptionGroupId != 0;
                if (randomConfigured) issues.AddRange(ValidateRandom(data, definition));
                else if ((definition.MainStat != MainStatKind.None || definition.AttackMainStat != MainStatKind.None)
                    && !EquipmentRule.ValidDefinition(definition))
                    Add(issues, definition, "m_mainStat/m_attackMainStat", "부위에 맞는 주스탯과 낚싯대 공격 기준이 필요합니다.");
            }
            foreach (var product in data.GetAll<ShopProducts_New>())
            {
                if (product.EquipmentProfileId == 0) continue; // 아직 연결하지 않은 기존 상품을 허용한다.
                var profile = data.GetData<EquipmentFixedProfile>(product.EquipmentProfileId);
                if (product.ItemType != ItemType.Equipment || profile == null || profile.EquipmentId != product.BaseId)
                    Add(issues, product, "m_equipmentProfileId", "장비 상품의 BaseId와 같은 장비를 참조하는 프로필이 필요합니다.");
            }
            return issues.AsReadOnly();
        }

        internal static List<EquipmentDataIssue> ValidateRandom(DataManager data, ItemData_Equipment_New definition)
        {
            var issues = new List<EquipmentDataIssue>();
            if (!EquipmentRule.ValidDefinition(definition))
            {
                Add(issues, definition, "m_tier/m_mountingArea/m_mainStat/m_attackMainStat", "장비 정의가 부위 규칙에 맞지 않습니다.");
                return issues;
            }
            var rarities = new HashSet<ItemRarity>();
            if (EquipmentRule.HasMain(definition.MountingArea))
            {
                if (definition.RarityGroupId != 0 || !EquipmentRule.TryGetValueDomain(data, definition.MainValueRuleId, out var domain))
                {
                    Add(issues, definition, "m_mainValueRuleId/m_rarityGroupId", "주스탯 값 규칙이 필요하며 별도 레어도 그룹은 0이어야 합니다.");
                    return issues;
                }
                var ranges = data.GetAll<EquipmentRarityRange>().Where(x => x.Tier == definition.Tier)
                    .OrderBy(x => x.MinInclusive).ToList();
                ValidateRanges(ranges, issues);
                if (issues.Count != 0) return issues;
                double coveredUntil = double.NegativeInfinity;
                foreach (var range in ranges)
                {
                    if (domain.Reaches(coveredUntil, range.MinInclusive))
                        Add(issues, definition, "m_mainValueRuleId", "생산 가능한 주스탯 중 레어도 구간에 속하지 않는 값이 있습니다.");
                    if (domain.Reaches(range.MinInclusive, range.MaxExclusive)) rarities.Add(range.Rarity);
                    coveredUntil = range.MaxExclusive;
                }
                if (domain.Reaches(coveredUntil, double.PositiveInfinity))
                    Add(issues, definition, "m_mainValueRuleId", "생산 가능한 주스탯의 상단을 레어도 구간이 덮지 못합니다.");
            }
            else
            {
                var weights = data.GetAll<EquipmentRarityWeight>().Where(x => x.GroupId == definition.RarityGroupId && x.Tier == definition.Tier).ToList();
                if (definition.MainValueRuleId != 0 || definition.RarityGroupId <= 0 || !weights.Any(x => x.Weight > 0)
                    || weights.Any(x => x.ID <= 0 || x.Weight < 0 || !EquipmentRule.ValidRarity(x.Rarity)))
                    Add(issues, definition, "m_mainValueRuleId/m_rarityGroupId", "주스탯 없는 부위는 별도의 유효한 레어도 가중치 표가 필요합니다.");
                foreach (var row in weights.Where(x => x.Weight > 0)) rarities.Add(row.Rarity);
            }
            foreach (var rarity in rarities.Where(x => EquipmentRule.OptionCount(x) > 0))
            {
                var options = data.GetAll<EquipmentOptionPool>().Where(x => x.GroupId == definition.OptionGroupId
                    && x.Tier == definition.Tier && x.Rarity == rarity).ToList();
                if (definition.OptionGroupId <= 0 || !options.Any(x => x.Weight > 0))
                    Add(issues, definition, "m_optionGroupId", $"티어 {definition.Tier}, {rarity}의 보조스탯 후보가 없습니다.");
                foreach (var option in options) ValidateOption(data, option, issues);
            }
            return issues;
        }

        internal static bool IsRuleType(Type type) => type == typeof(EquipmentValueRule) || type == typeof(EquipmentValueWeight)
            || type == typeof(EquipmentRarityRange) || type == typeof(EquipmentRarityWeight) || type == typeof(EquipmentOptionPool)
            || type == typeof(EquipmentFixedProfile) || type == typeof(EquipmentFixedOption);

        private static void ValidateOption(DataManager data, EquipmentOptionPool row, List<EquipmentDataIssue> issues)
        {
            if (row.ID <= 0 || row.GroupId <= 0 || row.Tier <= 0 || row.Weight < 0 || !EquipmentRule.ValidRarity(row.Rarity))
                Add(issues, row, "m_groupId/m_tier/m_rarity/m_weight", "그룹·티어·레어도·가중치를 확인하세요.");
            if (!EquipmentRule.SupportedOption(row.Stat, row.Operation))
                Add(issues, row, "m_stat/m_operation", "지원하는 PlayerStat_New와 Add만 사용할 수 있습니다.");
            if (!EquipmentRule.TryGetValueDomain(data, row.ValueRuleId, out _))
                Add(issues, row, "m_valueRuleId", "유효한 수치 규칙을 참조해야 합니다.");
        }

        private static void ValidateRanges(List<EquipmentRarityRange> rows, List<EquipmentDataIssue> issues)
        {
            double previousMax = double.NegativeInfinity;
            ItemRarity previousRarity = ItemRarity.Normal;
            foreach (var row in rows)
            {
                if (row.ID <= 0 || row.Tier <= 0 || !EquipmentRule.ValidRarity(row.Rarity)
                    || !EquipmentRule.Finite(row.MinInclusive) || !EquipmentRule.Finite(row.MaxExclusive)
                    || row.MinInclusive >= row.MaxExclusive)
                    Add(issues, row, "m_tier/m_rarity/m_minInclusive/m_maxExclusive", "유한한 하한 < 상한 구간이 필요합니다.");
                if (row.MinInclusive < previousMax)
                    Add(issues, row, "m_minInclusive", "같은 티어의 레어도 구간이 겹칩니다.");
                if (row.Rarity < previousRarity)
                    Add(issues, row, "m_rarity", "주스탯 구간이 높아질 때 레어도가 낮아질 수 없습니다.");
                previousMax = Math.Max(previousMax, row.MaxExclusive);
                previousRarity = row.Rarity;
            }
        }

        private static void Add(List<EquipmentDataIssue> issues, GameData row, string column, string message)
            => issues.Add(new EquipmentDataIssue(row.GetType().Name, row.ID, column, message));
    }
}
