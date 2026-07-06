using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DesktopCompanion.Data;

namespace DesktopCompanion.Core
{
    /// <summary>
    /// JSON 콘텐츠 테이블에서 GameData(SO 인스턴스)를 로드한다(D14·§15.2).
    ///
    /// 파일 형식: 디렉토리 내 *.json, 각 파일은 항목 배열 —
    /// [ { "type": "ItemData_Fish", "m_id": 1, "m_name": "...", ... }, ... ]
    /// Data 간 참조 필드는 { "type": "ItemData_Materials", "id": 5 } 로 표기.
    ///
    /// 2-pass: ① 전 항목의 타입/ID만 읽어 빈 SO 인스턴스 생성·등록
    ///          ② 전체 필드 Populate(참조는 GameDataRefConverter가 레지스트리에서 즉시 해소)
    /// </summary>
    public static class JsonDataLoader
    {
        /// <summary>디렉토리에 JSON 콘텐츠가 있으면 로드. 없으면 false(호출부가 SO 폴백).</summary>
        public static bool TryLoadAll(string directory, out List<GameData> result)
        {
            result = null;
            if (!Directory.Exists(directory))
            {
                return false;
            }
            string[] files = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                return false;
            }

            Dictionary<string, Type> typeMap = GameDataTypes.BuildTypeMap();
            var registry = new Dictionary<(Type, int), GameData>();
            var entries = new List<(JObject json, GameData instance)>();

            // ── Pass 1: 인스턴스 생성 + 레지스트리 등록 ──
            foreach (string file in files)
            {
                JArray array;
                try
                {
                    // 루트로 포맷 자동 판별(D16): JArray=내부 표준, JObject=외부 시트 포맷(정규화 후 동일 파이프)
                    JToken parsed = JToken.Parse(File.ReadAllText(file));
                    if (parsed is JObject sheetRoot)
                    {
                        array = ExternalSheetNormalizer.Normalize(sheetRoot, typeMap);
                    }
                    else if (parsed is JArray internalArray)
                    {
                        array = internalArray;
                    }
                    else
                    {
                        Debug.LogError($"[JsonDataLoader] 지원하지 않는 루트 토큰 {parsed.Type} ({file})");
                        continue;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[JsonDataLoader] 파싱 실패 {file}: {e.Message}");
                    continue;
                }

                foreach (JToken token in array)
                {
                    if (token is not JObject obj)
                    {
                        continue;
                    }
                    string typeName = obj.Value<string>("type");
                    if (typeName == null || !typeMap.TryGetValue(typeName, out Type type))
                    {
                        Debug.LogError($"[JsonDataLoader] 알 수 없는 type '{typeName}' ({file})");
                        continue;
                    }
                    JToken idToken = obj["m_id"];
                    if (idToken == null)
                    {
                        Debug.LogError($"[JsonDataLoader] m_id 누락: {typeName} ({file})");
                        continue;
                    }
                    int id = idToken.Value<int>();

                    if (registry.ContainsKey((type, id)))
                    {
                        Debug.LogError($"[JsonDataLoader] 중복 {typeName} ID={id} ({file})");
                        continue;
                    }

                    var instance = (GameData)ScriptableObject.CreateInstance(type);
                    registry.Add((type, id), instance);
                    entries.Add((obj, instance));
                }
            }

            // ── Pass 2: 전체 필드 채우기 (참조는 레지스트리에서 즉시 해소) ──
            JsonSerializerSettings settings = CreateSettings((typeName, id) =>
            {
                if (typeMap.TryGetValue(typeName, out Type t) && registry.TryGetValue((t, id), out GameData found))
                {
                    return found;
                }
                return null;
            });

            foreach ((JObject json, GameData instance) in entries)
            {
                try
                {
                    JsonConvert.PopulateObject(json.ToString(), instance, settings);
                    instance.name = string.IsNullOrEmpty(instance.Name)
                        ? $"{instance.GetType().Name}_{instance.ID}"
                        : instance.Name;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[JsonDataLoader] Populate 실패 {instance.GetType().Name}#{instance.ID}: {e.Message}");
                }
            }

            Debug.Log($"[JsonDataLoader] JSON 콘텐츠 로드: 파일 {files.Length}개, 항목 {entries.Count}건");
            result = new List<GameData>(entries.Count);
            foreach ((_, GameData instance) in entries)
            {
                result.Add(instance);
            }
            return true;
        }

        private static JsonSerializerSettings CreateSettings(Func<string, int, GameData> resolver)
            => new JsonSerializerSettings
            {
                ContractResolver = new SerializeFieldContractResolver(),
                Converters = { new GameDataRefConverter(resolver), new StringEnumConverter() },
            };

    }
}
