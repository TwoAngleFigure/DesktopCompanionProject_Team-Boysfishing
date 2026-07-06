using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using DesktopCompanion.Core;

namespace DesktopCompanion.EditorTools
{
    /// <summary>
    /// 내장 변환 툴(D17·§15.8.2): GameData.xlsx → StreamingAssets/Data/GameData.json (내부 표준 포맷).
    ///
    /// 파이프: xlsx 파싱(XlsxSheetReader) → 외부 포맷 모양 JObject 조립
    ///        → ExternalSheetNormalizer.Normalize (§15.7 규칙 재사용, 단일 소스)
    ///        → 내부 표준 JArray 저장.
    ///
    /// 규칙: 헤더가 #로 시작하거나 빈 열은 스킵(표시 전용). 2행(설명)은 스킵.
    ///       타입 시트는 첫 열 값이 항목 키(m_id), 서브시트는 행 번호가 키(중복 원천 불가 → row_key 무관).
    /// </summary>
    public static class GameDataConverter
    {
        private const string PrefKey = "DesktopCompanion.GameDataXlsxPath";
        private const string OutputFileName = "GameData.json";

        private static readonly HashSet<string> s_subSheets = new() { "EquipmentUpgrades", "StageTierPools" };

        [MenuItem("Tools/DesktopCompanion/GameData 변환 (xlsx → JSON)")]
        public static void Convert()
        {
            string defaultPath = EditorPrefs.GetString(PrefKey,
                Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/GameData.xlsx")));
            string directory = File.Exists(defaultPath) ? Path.GetDirectoryName(defaultPath) : Application.dataPath;

            string xlsxPath = EditorUtility.OpenFilePanel("GameData xlsx 선택", directory, "xlsx");
            if (string.IsNullOrEmpty(xlsxPath))
            {
                return;
            }
            EditorPrefs.SetString(PrefKey, xlsxPath);

            try
            {
                var sheets = XlsxSheetReader.Read(xlsxPath);
                JObject externalShape = BuildExternalShape(sheets);
                JArray items = ExternalSheetNormalizer.Normalize(externalShape, GameDataTypes.BuildTypeMap());

                string json = items.ToString(Formatting.Indented);

                // sanity: 표시 전용(#) 필드 잔존 확인(§15.8.5)
                if (json.Contains("\"#"))
                {
                    Debug.LogWarning("[GameDataConverter] 산출 JSON에 '#' 필드가 남아 있습니다 — 규약 위반 열 확인 필요");
                }

                string outputDir = Path.Combine(Application.streamingAssetsPath, "Data");
                Directory.CreateDirectory(outputDir);
                string outputPath = Path.Combine(outputDir, OutputFileName);
                File.WriteAllText(outputPath, json);
                AssetDatabase.Refresh();

                string summary = BuildSummary(items);
                Debug.Log($"[GameDataConverter] 변환 완료 → {outputPath}\n{summary}");
                EditorUtility.DisplayDialog("GameData 변환 완료",
                    $"{Path.GetFileName(xlsxPath)} → StreamingAssets/Data/{OutputFileName}\n\n{summary}\n\n" +
                    "에러/경고는 Console 창을 확인하세요.", "확인");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameDataConverter] 변환 실패: {e}");
                EditorUtility.DisplayDialog("GameData 변환 실패", e.Message, "확인");
            }
        }

        // 시트 데이터 → 외부 포맷 모양 JObject (Normalizer 입력 규약에 맞춤)
        private static JObject BuildExternalShape(
            Dictionary<string, SortedDictionary<int, Dictionary<int, object>>> sheets)
        {
            var root = new JObject();
            foreach ((string sheetName, var rows) in sheets)
            {
                if (!rows.TryGetValue(1, out Dictionary<int, object> headerCells))
                {
                    continue;   // 헤더 없는 시트(README 등) — Normalize 화이트리스트가 어차피 거름
                }

                // 헤더 수집: #·빈 헤더 스킵(표시 전용, D17)
                var headers = new SortedDictionary<int, string>();
                foreach ((int col, object value) in headerCells)
                {
                    if (value is string name && !string.IsNullOrWhiteSpace(name) && !name.StartsWith("#"))
                    {
                        headers[col] = name;
                    }
                }
                if (headers.Count == 0)
                {
                    continue;
                }

                int keyColumn = headers.Keys.First();   // 첫 유효 열 = 항목 키(m_id/row_key)
                bool isSubSheet = s_subSheets.Contains(sheetName);
                var sheetObj = new JObject();

                foreach ((int rowNum, Dictionary<int, object> cells) in rows)
                {
                    if (rowNum < 3)
                    {
                        continue;   // 1행=헤더, 2행=설명
                    }

                    var fields = new JObject();
                    foreach ((int col, string fieldName) in headers)
                    {
                        if (col == keyColumn)
                        {
                            continue;   // 키 열은 필드에서 제외(외부 툴 규약과 동일)
                        }
                        if (cells.TryGetValue(col, out object value))
                        {
                            fields[fieldName] = JToken.FromObject(value);
                        }
                    }
                    if (fields.Count == 0)
                    {
                        continue;   // 빈 행(#열 수식 프리필만 있는 행 포함)
                    }

                    // 서브시트는 행 번호가 키(중복 불가) — row_key 값은 무시. 타입 시트는 첫 열 값(m_id).
                    string key = isSubSheet
                        ? rowNum.ToString()
                        : cells.TryGetValue(keyColumn, out object keyValue) ? keyValue.ToString() : null;
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }
                    sheetObj[key] = fields;
                }
                root[sheetName] = sheetObj;
            }
            return root;
        }

        private static string BuildSummary(JArray items)
        {
            var counts = new SortedDictionary<string, int>();
            foreach (JToken item in items)
            {
                string type = item.Value<string>("type") ?? "?";
                counts[type] = counts.TryGetValue(type, out int n) ? n + 1 : 1;
            }
            return $"총 {items.Count}건\n" + string.Join("\n", counts.Select(kv => $"  {kv.Key}: {kv.Value}"));
        }
    }
}
