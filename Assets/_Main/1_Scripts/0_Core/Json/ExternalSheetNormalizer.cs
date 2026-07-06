using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DesktopCompanion.Core
{
    /// <summary>
    /// 외부 변환 툴(스프레드시트→JSON)의 시트맵 포맷을 내부 표준 포맷(JArray)으로 정규화한다(D16·§15.7).
    ///
    /// 외부 포맷: { "시트명": { "첫열값": { 필드: 값 } } }
    ///  - 타입 시트: 첫 열 = m_id (정수 아닌 키 = 설명 행 → 스킵)
    ///  - 서브시트(EquipmentUpgrades/StageTierPools): 첫 열 = row_key(고유, 값 자체는 무시)
    ///  - README/Enums 등 화이트리스트 밖 시트는 스킵
    ///
    /// 변환: _id 참조→{type,id}, 압축 문자열→배열, mapPosition_x/y→Vector2, 서브시트 병합.
    /// 결과는 기존 JsonDataLoader 2-pass 파이프에 그대로 투입된다.
    /// </summary>
    public static class ExternalSheetNormalizer
    {
        private const string SheetEquipmentUpgrades = "EquipmentUpgrades";
        private const string SheetStageTierPools = "StageTierPools";

        // 참조 필드맵(§15.7.4): 외부 "_id" 필드 → (내부 필드명, 대상 타입명). 새 참조 열은 여기 한 줄 추가.
        private static readonly Dictionary<string, (string field, string type)> s_refMap = new()
        {
            { "m_aquariumMaterial_id", ("m_aquariumMaterial", "ItemData_Materials") },
            { "m_itemFish_id", ("m_itemFish", "ItemData_Fish") },
            { "m_summonTarget_id", ("m_summonTarget", "BattleFishData") },
            { "m_boss_id", ("m_boss", "BattleFishData") },
            { "m_requiredBoss_id", ("m_requiredBoss", "BattleFishData") },   // LicenseData 해금 조건①
        };

        public static JArray Normalize(JObject root, IReadOnlyDictionary<string, Type> typeMap)
        {
            var items = new JArray();
            var index = new Dictionary<(string type, int id), JObject>();

            // ── 타입 시트 → 항목 ──
            foreach (JProperty sheet in root.Properties())
            {
                string sheetName = sheet.Name;
                if (sheetName == SheetEquipmentUpgrades || sheetName == SheetStageTierPools)
                {
                    continue;   // 서브시트는 아래에서 병합
                }
                if (!typeMap.ContainsKey(sheetName))
                {
                    continue;   // README/Enums 등 비데이터 시트
                }
                if (sheet.Value is not JObject rows)
                {
                    continue;
                }

                foreach (JProperty rowProp in rows.Properties())
                {
                    if (!int.TryParse(rowProp.Name, out int id))
                    {
                        continue;   // 설명 행("고유 ID (int)" 등) 필터
                    }
                    if (rowProp.Value is not JObject row)
                    {
                        continue;
                    }

                    JObject item = NormalizeItem(sheetName, id, row);
                    items.Add(item);
                    index[(sheetName, id)] = item;
                }
            }

            MergeEquipmentUpgrades(root, index);
            MergeStageTierPools(root, index);
            return items;
        }

        // ── 타입 시트 행 하나 → 내부 표준 항목 ──
        private static JObject NormalizeItem(string typeName, int id, JObject row)
        {
            var item = new JObject { ["type"] = typeName, ["m_id"] = id };
            JToken posX = null, posY = null;

            foreach (JProperty field in row.Properties())
            {
                string name = field.Name;
                JToken value = field.Value;

                if (name.StartsWith("#"))
                {
                    continue;   // 표시 전용 열(D17) — 변환 제외
                }

                if (s_refMap.TryGetValue(name, out var mapped))
                {
                    int? refId = GetInt(value);
                    if (refId.HasValue)
                    {
                        item[mapped.field] = MakeRef(mapped.type, refId.Value);
                    }
                    continue;   // null 참조는 미기록(=null)
                }

                switch (name)
                {
                    case "m_baseModifiers":
                    case "m_modifiers":
                        SetIfNotNull(item, name, ParseModifiers(value, typeName, id));
                        break;
                    case "m_materialCosts":
                    case "m_craftMaterials":
                        SetIfNotNull(item, name, ParseMaterialCosts(value, typeName, id));
                        break;
                    case "m_bossDrops":
                        SetIfNotNull(item, name, ParseItemDrops(value, typeName, id));
                        break;
                    case "m_qualityThresholds":
                        SetIfNotNull(item, name, ParseFloats(value, typeName, id));
                        break;
                    case "m_mapPosition_x":
                        posX = value;
                        break;
                    case "m_mapPosition_y":
                        posY = value;
                        break;
                    default:
                        item[name] = value;   // 단순 값 그대로
                        break;
                }
            }

            if (posX != null || posY != null)
            {
                item["m_mapPosition"] = new JObject { ["x"] = posX ?? 0, ["y"] = posY ?? 0 };
            }
            return item;
        }

        // ── 서브시트 병합: EquipmentUpgrades → m_upgradeSteps ──
        private static void MergeEquipmentUpgrades(JObject root, Dictionary<(string, int), JObject> index)
        {
            if (root[SheetEquipmentUpgrades] is not JObject rows)
            {
                return;
            }

            var grouped = new Dictionary<int, List<(int level, JObject step)>>();
            foreach (JProperty rowProp in rows.Properties())
            {
                if (rowProp.Value is not JObject row)
                {
                    continue;
                }
                int? equipmentId = GetInt(row["equipment_id"]);
                int? level = GetInt(row["level"]);
                if (!equipmentId.HasValue || !level.HasValue)
                {
                    continue;   // 설명 행
                }

                var step = new JObject { ["m_goldCost"] = GetInt(row["m_goldCost"]) ?? 0 };
                SetIfNotNull(step, "m_modifiers", ParseModifiers(row["m_modifiers"], SheetEquipmentUpgrades, equipmentId.Value));
                SetIfNotNull(step, "m_materialCosts", ParseMaterialCosts(row["m_materialCosts"], SheetEquipmentUpgrades, equipmentId.Value));

                if (!grouped.TryGetValue(equipmentId.Value, out var list))
                {
                    grouped[equipmentId.Value] = list = new List<(int, JObject)>();
                }
                list.Add((level.Value, step));
            }

            foreach ((int equipmentId, var steps) in grouped)
            {
                steps.Sort((a, b) => a.level.CompareTo(b.level));

                // 행 소실 조기 감지(§15.7.7): level이 1..N 연속이 아니면 경고
                for (int i = 0; i < steps.Count; i++)
                {
                    if (steps[i].level != i + 1)
                    {
                        Debug.LogWarning($"[ExternalSheetNormalizer] 장비 {equipmentId} 강화 level 불연속 " +
                                         $"(기대 {i + 1}, 실제 {steps[i].level}) — 외부 툴 행 소실(row_key 중복?) 의심");
                        break;
                    }
                }

                if (!index.TryGetValue(("ItemData_Equipment", equipmentId), out JObject item))
                {
                    Debug.LogError($"[ExternalSheetNormalizer] EquipmentUpgrades: 대상 장비 없음 id={equipmentId}");
                    continue;
                }
                var array = new JArray();
                foreach ((_, JObject step) in steps)
                {
                    array.Add(step);
                }
                item["m_upgradeSteps"] = array;
            }
        }

        // ── 서브시트 병합: StageTierPools → m_tierPools ──
        private static void MergeStageTierPools(JObject root, Dictionary<(string, int), JObject> index)
        {
            if (root[SheetStageTierPools] is not JObject rows)
            {
                return;
            }

            var grouped = new Dictionary<int, JArray>();
            foreach (JProperty rowProp in rows.Properties())
            {
                if (rowProp.Value is not JObject row)
                {
                    continue;
                }
                int? stageId = GetInt(row["stage_id"]);
                int? tier = GetInt(row["m_tier"]);
                if (!stageId.HasValue || !tier.HasValue)
                {
                    continue;   // 설명 행
                }

                var pool = new JObject
                {
                    ["m_tier"] = tier.Value,
                    ["m_requiredLicense"] = GetInt(row["m_requiredLicense"]) ?? 0,
                };
                SetIfNotNull(pool, "m_entries", ParsePoolEntries(row["entries"], stageId.Value));

                if (!grouped.TryGetValue(stageId.Value, out JArray list))
                {
                    grouped[stageId.Value] = list = new JArray();
                }
                list.Add(pool);
            }

            foreach ((int stageId, JArray pools) in grouped)
            {
                if (!index.TryGetValue(("StageData", stageId), out JObject item))
                {
                    Debug.LogError($"[ExternalSheetNormalizer] StageTierPools: 대상 지역 없음 id={stageId}");
                    continue;
                }
                item["m_tierPools"] = pools;
            }
        }

        // ── 압축 문자열 파서(§15.7.5) ──

        // "Stat:Op:Value;..." → StatModifier[]
        private static JArray ParseModifiers(JToken token, string context, int id)
            => ParseCompact(token, 3, context, id, parts => new JObject
            {
                ["m_stat"] = parts[0],
                ["m_operation"] = parts[1],
                ["m_value"] = ParseFloat(parts[2]),
            });

        // "materialId:count;..." → MaterialCost[]
        private static JArray ParseMaterialCosts(JToken token, string context, int id)
            => ParseCompact(token, 2, context, id, parts => new JObject
            {
                ["m_material"] = MakeRef("ItemData_Materials", int.Parse(parts[0])),
                ["m_count"] = int.Parse(parts[1]),
            });

        // "타입:id:count:확률;..." → ItemDrop[]
        private static JArray ParseItemDrops(JToken token, string context, int id)
            => ParseCompact(token, 4, context, id, parts => new JObject
            {
                ["m_item"] = MakeRef(parts[0], int.Parse(parts[1])),
                ["m_count"] = int.Parse(parts[2]),
                ["m_probability"] = ParseFloat(parts[3]),
            });

        // "battleFishId:weight;..." → FishPoolEntry[]
        private static JArray ParsePoolEntries(JToken token, int stageId)
            => ParseCompact(token, 2, SheetStageTierPools, stageId, parts => new JObject
            {
                ["m_fish"] = MakeRef("BattleFishData", int.Parse(parts[0])),
                ["m_weight"] = ParseFloat(parts[1]),
            });

        // "v1;v2;v3;v4" → float[]
        private static JArray ParseFloats(JToken token, string context, int id)
        {
            string text = AsString(token);
            if (text == null)
            {
                return null;
            }
            try
            {
                var array = new JArray();
                foreach (string part in SplitItems(text))
                {
                    array.Add(ParseFloat(part));
                }
                return array;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ExternalSheetNormalizer] {context}#{id} 숫자 목록 파싱 실패 '{text}': {e.Message}");
                return null;
            }
        }

        private static JArray ParseCompact(JToken token, int expectedParts, string context, int id, Func<string[], JObject> build)
        {
            string text = AsString(token);
            if (text == null)
            {
                return null;
            }
            try
            {
                var array = new JArray();
                foreach (string entry in SplitItems(text))
                {
                    string[] parts = entry.Split(':');
                    if (parts.Length != expectedParts)
                    {
                        throw new FormatException($"'{entry}' — 토큰 {expectedParts}개 필요");
                    }
                    for (int i = 0; i < parts.Length; i++)
                    {
                        parts[i] = parts[i].Trim();
                    }
                    array.Add(build(parts));
                }
                return array;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ExternalSheetNormalizer] {context}#{id} 압축 문자열 파싱 실패 '{text}': {e.Message}");
                return null;
            }
        }

        // ── 공용 헬퍼 ──

        private static JObject MakeRef(string typeName, int id)
            => new JObject { ["type"] = typeName, ["id"] = id };

        private static void SetIfNotNull(JObject target, string field, JArray value)
        {
            if (value != null)
            {
                target[field] = value;
            }
        }

        private static string[] SplitItems(string text)
            => text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

        private static float ParseFloat(string text)
            => float.Parse(text.Trim(), CultureInfo.InvariantCulture);

        private static string AsString(JToken token)
            => token == null || token.Type == JTokenType.Null ? null
             : token.Type == JTokenType.String ? token.Value<string>()
             : token.ToString();

        private static int? GetInt(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                return token.Value<int>();
            }
            return int.TryParse(token.ToString(), out int parsed) ? parsed : null;
        }
    }
}
