using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
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

        // 모디파이어 스탯 컬럼(§04): 헤더=PlayerStat 이름, 셀="Op:Value". 스탯 이름을 행마다 타이핑하지 않아 오타 불가.
        private static readonly HashSet<string> s_statColumns =
            new(Enum.GetNames(typeof(DesktopCompanion.Data.PlayerStat)));

        // 타입 시트 → 조립된 모디파이어를 담을 내부 필드명(구 단일 컬럼명이 하던 역할 대체).
        private static readonly Dictionary<string, string> s_modifierField = new()
        {
            { "ItemData_Equipment", "m_baseModifiers" },
            { "ItemData_Consumables", "m_modifiers" },
        };

        // 압축 문자열 미파싱 경고의 중복 억제(타입·필드 단위). 100행이 같은 열을 쓰면 경고도 100번 나므로
        // 최초 1건만 남긴다. Normalize 진입 시 초기화 — 에디터 단일 스레드 실행 전제.
        private static readonly HashSet<string> s_warnedCompactFields = new();

        public static JArray Normalize(JObject root, IReadOnlyDictionary<string, Type> typeMap)
        {
            var items = new JArray();
            var index = new Dictionary<(string type, int id), JObject>();
            s_warnedCompactFields.Clear();

            // ── 타입 시트 → 항목 ──
            foreach (JProperty sheet in root.Properties())
            {
                string sheetName = sheet.Name;
                if (sheetName == SheetEquipmentUpgrades || sheetName == SheetStageTierPools)
                {
                    continue;   // 서브시트는 아래에서 병합
                }
                if (!typeMap.TryGetValue(sheetName, out Type sheetType))
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

                    JObject item = NormalizeItem(sheetName, sheetType, id, row);
                    items.Add(item);
                    index[(sheetName, id)] = item;
                }
            }

            MergeEquipmentUpgrades(root, index);
            MergeStageTierPools(root, index);
            return items;
        }

        // ── 타입 시트 행 하나 → 내부 표준 항목 ──
        private static JObject NormalizeItem(string typeName, Type type, int id, JObject row)
        {
            var item = new JObject { ["type"] = typeName, ["m_id"] = id };
            JToken posX = null, posY = null;

            foreach (JProperty field in row.Properties())
            {
                string name = field.Name;
                JToken value = field.Value;

                if (name.StartsWith("#") || s_statColumns.Contains(name))
                {
                    continue;   // 표시 전용 열(D17) / 스탯 컬럼(아래에서 일괄 조립) — 개별 변환 제외
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
                    case "m_drops":
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
                        WarnIfUnparsedCompact(type, name, value, typeName, id);
                        // 단순 스칼라. 정수형 실수(예: 50.0)는 정수로 복원한다(int 필드 로드 안전).
                        // XlsxSheetReader의 셀 단위 복원과 동일 규칙을 외부 시트맵(Google Sheets 등) 경로에도 적용.
                        item[name] = NormalizeScalar(value);
                        break;
                }
            }

            if (posX != null || posY != null)
            {
                item["m_mapPosition"] = new JObject { ["x"] = posX ?? 0, ["y"] = posY ?? 0 };
            }

            // 스탯 컬럼(헤더=스탯 이름) → 모디파이어 배열(§04). 스탯 컬럼이 있으면 이 값으로 설정하며,
            // 레거시 단일 컬럼(m_baseModifiers/m_modifiers)이 있었다면 위 switch가 설정한 값을 대체한다(전환기 병행 지원).
            if (s_modifierField.TryGetValue(typeName, out string modField))
            {
                JArray mods = BuildModifiersFromColumns(row, typeName, id);
                if (mods != null)
                {
                    item[modField] = mods;
                }
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
                // 스탯 컬럼 우선(§04), 없으면 레거시 단일 컬럼(m_modifiers)로 폴백(전환기 병행 지원).
                JArray upgradeMods = BuildModifiersFromColumns(row, SheetEquipmentUpgrades, equipmentId.Value)
                                     ?? ParseModifiers(row["m_modifiers"], SheetEquipmentUpgrades, equipmentId.Value);
                SetIfNotNull(step, "m_modifiers", upgradeMods);
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

        // 스탯 컬럼(헤더=스탯 이름, 셀="Op:Value")들을 모아 StatModifier[]로 조립한다(§04).
        // 헤더가 스탯 이름을 고정하므로 스탯 이름 오타가 불가능하다. 스탯 컬럼이 하나도 없으면 null.
        private static JArray BuildModifiersFromColumns(JObject row, string context, int id)
        {
            JArray array = null;
            foreach (JProperty field in row.Properties())
            {
                if (!s_statColumns.Contains(field.Name))
                {
                    continue;
                }
                string cell = AsString(field.Value);
                if (string.IsNullOrWhiteSpace(cell))
                {
                    continue;   // 빈 셀 = 해당 스탯 수식자 없음
                }

                string[] parts = cell.Split(':');
                if (parts.Length != 2)
                {
                    Debug.LogError($"[ExternalSheetNormalizer] {context}#{id} 스탯 '{field.Name}' 형식 오류 '{cell}' — 'Op:Value' 필요");
                    continue;
                }

                string op = parts[0].Trim();
                if (!Enum.IsDefined(typeof(DesktopCompanion.Data.ModifierOperation), op))
                {
                    Debug.LogError($"[ExternalSheetNormalizer] {context}#{id} 스탯 '{field.Name}' 연산 오타 '{op}' — Add/Multiply 중 하나여야 함");
                    continue;
                }

                float value;
                try
                {
                    value = ParseFloat(parts[1]);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ExternalSheetNormalizer] {context}#{id} 스탯 '{field.Name}' 값 파싱 실패 '{parts[1]}': {e.Message}");
                    continue;
                }

                (array ??= new JArray()).Add(new JObject
                {
                    ["m_stat"] = field.Name,        // 헤더가 곧 스탯 이름(오타 불가)
                    ["m_operation"] = op,
                    ["m_value"] = value,
                });
            }
            return array;
        }

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

        /// <summary>
        /// 압축 문자열로 보이는데 전용 파서(위 switch)가 없는 열을 변환 시점에 잡는다.
        /// 시트 컬럼명을 바꾸면 case 라벨과 조용히 어긋나 값이 문자열째로 실리고, 로드 시점
        /// PopulateObject에서야 형 변환 실패로 터진다 — 그 사고를 여기서 앞당겨 알린다.
        /// 대상 C# 필드가 string이면 소비자가 직접 파싱하는 원본 문자열 설계이므로 제외한다.
        /// </summary>
        private static void WarnIfUnparsedCompact(Type type, string field, JToken value, string context, int id)
        {
            if (value == null || value.Type != JTokenType.String)
            {
                return;
            }
            string text = value.Value<string>();
            if (string.IsNullOrEmpty(text) || (text.IndexOf(':') < 0 && text.IndexOf(';') < 0))
            {
                return;
            }

            FieldInfo target = FindField(type, field);
            if (target != null && target.FieldType == typeof(string))
            {
                return;   // 예: RecipeData_Mixture.m_ingredients — 압축 문자열을 그대로 보관하는 필드
            }
            if (!s_warnedCompactFields.Add($"{context}.{field}"))
            {
                return;   // 같은 열은 최초 1건만
            }

            string reason = target == null
                ? $"{type.Name}에 '{field}' 필드가 없음"
                : $"{type.Name}.{field}는 {target.FieldType.Name} 형";
            Debug.LogWarning($"[ExternalSheetNormalizer] {context}#{id} 열 '{field}'가 압축 문자열로 보이는데 전용 파서가 없습니다 " +
                             $"({reason}, 값 '{text}'). 시트 컬럼명과 NormalizeItem의 case 라벨이 어긋났는지 확인하세요.");
        }

        // 선언 타입부터 상위로 올라가며 필드를 찾는다(GetField는 상위 클래스의 private을 반환하지 않음).
        private static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }
            return null;
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

        // 정수형 실수(소수부 0, 예: 50.0)를 정수 토큰으로 복원한다. 그 외(진짜 소수·문자열 등)는 그대로.
        // 목적: int C# 필드에 float 리터럴이 들어가 JsonReaderException이 나는 것을 방지(§15.7).
        // 진짜 float 필드에 정수 토큰이 들어가도 로드에는 문제없다(Newtonsoft가 int→float 허용).
        private static JToken NormalizeScalar(JToken value)
        {
            if (value != null && value.Type == JTokenType.Float)
            {
                double d = value.Value<double>();
                if (!double.IsInfinity(d) && d == Math.Floor(d))
                {
                    return new JValue((long)d);
                }
            }
            return value;
        }

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
