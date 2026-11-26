using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DesktopCompanion.Core;
using DesktopCompanion.Data;
using DesktopCompanion.Systems;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DesktopCompanion.EditorTools
{
    /// <summary>장비 시트 검증. JSON 파일을 덮어쓰기 전에 실행한다.</summary>
    public static class EquipmentSheetValidator
    {
        private static readonly HashSet<string> RuleSheets = new()
        {
            "EquipmentValueRule", "EquipmentValueWeight", "EquipmentRarityRange", "EquipmentRarityWeight",
            "EquipmentOptionPool", "EquipmentFixedProfile", "EquipmentFixedOption"
        };
        private static readonly HashSet<string> DefinitionFields = new()
        {
            "m_id", "m_tier", "m_mountingArea", "m_mainStat", "m_attackMainStat",
            "m_mainValueRuleId", "m_optionGroupId", "m_rarityGroupId"
        };
        private static readonly HashSet<string> ProductFields = new() { "m_id", "m_baseId", "m_itemType", "m_equipmentProfileId" };

        // BuildExternalShape의 ID 키 병합/빈 ID 행 생략 전에 검사해야 오류가 유실되지 않는다.
        public static IReadOnlyList<string> ValidateRows(Dictionary<string, SortedDictionary<int, Dictionary<int, object>>> sheets)
        {
            var issues = new List<string>();
            foreach (var sheet in sheets)
            {
                if (!RuleSheets.Contains(sheet.Key) && sheet.Key != "ItemData_Equipment_New" && sheet.Key != "ShopProducts_New") continue;
                if (!sheet.Value.TryGetValue(1, out var header)) continue;
                var columns = header.Where(x => x.Value is string s && s.StartsWith("m_")).ToList();
                int idColumn = columns.FirstOrDefault(x => Equals(x.Value, "m_id")).Key;
                var ids = new HashSet<int>();
                foreach (var row in sheet.Value.Where(x => x.Key >= 3))
                {
                    if (!columns.Any(x => row.Value.TryGetValue(x.Key, out var v) && !string.IsNullOrWhiteSpace(v?.ToString()))) continue;
                    if (!row.Value.TryGetValue(idColumn, out var raw) || !int.TryParse(raw?.ToString(), out int id) || id <= 0)
                        issues.Add($"{sheet.Key} 행={row.Key} m_id: 양의 정수 ID가 필요합니다.");
                    else if (!ids.Add(id)) issues.Add($"{sheet.Key} 행={row.Key} ID={id} m_id: 중복 ID입니다.");
                }
            }
            return issues.AsReadOnly();
        }

        public static IReadOnlyList<string> ValidateItems(JArray items)
        {
            var issues = new List<string>();
            var instances = new List<GameData>();
            var types = GameDataTypes.BuildTypeMap();
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new SerializeFieldContractResolver(),
                Converters = { new StringEnumConverter { AllowIntegerValues = false } }
            };
            try
            {
                foreach (var item in items.OfType<JObject>())
                {
                    string typeName = item.Value<string>("type");
                    if (typeName == null || (!RuleSheets.Contains(typeName) && typeName != "ItemData_Equipment_New" && typeName != "ShopProducts_New")) continue;
                    if (!types.TryGetValue(typeName, out var type))
                    {
                        issues.Add($"{typeName}: GameData 타입을 찾을 수 없습니다.");
                        continue;
                    }
                    var instance = (GameData)ScriptableObject.CreateInstance(type);
                    instances.Add(instance);
                    var selected = new JObject();
                    foreach (var property in item.Properties())
                        if (RuleSheets.Contains(typeName) || (typeName == "ItemData_Equipment_New" ? DefinitionFields : ProductFields).Contains(property.Name))
                            selected[property.Name] = property.Value.DeepClone();
                    if (RuleSheets.Contains(typeName))
                    {
                        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                        {
                            bool optional = field.Name == "m_mainValue" ||
                                (typeName == "EquipmentValueRule" && selected.Value<string>("m_distribution") == "WeightedValues"
                                 && (field.Name == "m_min" || field.Name == "m_max" || field.Name == "m_step"));
                            if (!optional && (!selected.TryGetValue(field.Name, out var token) || token.Type == JTokenType.Null || token.ToString() == ""))
                                issues.Add($"{typeName} ID={item["m_id"]} {field.Name}: 필수 값이 없습니다.");
                            if (field.FieldType == typeof(int) && selected.TryGetValue(field.Name, out var integerToken)
                                && !int.TryParse(integerToken.ToString(), out _))
                                issues.Add($"{typeName} ID={item["m_id"]} {field.Name}: 소수 없이 int 범위의 정수가 필요합니다.");
                        }
                    }
                    try { JsonConvert.PopulateObject(selected.ToString(), instance, settings); }
                    catch (Exception e) { issues.Add($"{typeName} ID={item["m_id"]}: {e.Message}"); }
                }
                foreach (var issue in EquipmentRule.ValidateData(instances)) issues.Add(issue.ToString());
            }
            finally
            {
                foreach (var instance in instances) UnityEngine.Object.DestroyImmediate(instance);
            }
            return issues.AsReadOnly();
        }
    }
}
