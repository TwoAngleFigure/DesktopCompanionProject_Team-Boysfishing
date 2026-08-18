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
                Dictionary<string, System.Type> typeMap = GameDataTypes.BuildTypeMap();

                if (HasStaleFormulaCells(sheets, typeMap))
                {
                    return;
                }

                JObject externalShape = BuildExternalShape(sheets);
                JArray items = ExternalSheetNormalizer.Normalize(externalShape, typeMap);

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

        /// <summary>
        /// 변환에 실리는 열에 캐시값 없는 수식 셀이 있으면 변환을 막는다.
        ///
        /// 그런 셀은 빈 셀과 구분되지 않아 해당 필드가 조용히 비어버린다. ShopProducts의 m_name·m_price처럼
        /// 수식으로 채우는 변환 대상 열이 실제로 있으므로, 눈치채지 못한 채 데이터가 통째로 날아갈 수 있다.
        /// Excel/LibreOffice로 한 번 열고 저장하면 캐시가 복구된다.
        ///
        /// 변환 대상이 아닌 시트(_Lookup 등)와 표시 전용(#) 열은 무시한다 — 어차피 JSON에 나가지 않으므로
        /// 캐시가 없어도 잃을 값이 없다. 대부분의 수식이 표시 전용 열에 몰려 있어, 걸러내지 않으면
        /// 실제 피해가 없는 상황에서 변환이 통째로 막힌다.
        /// </summary>
        private static bool HasStaleFormulaCells(
            Dictionary<string, SortedDictionary<int, Dictionary<int, object>>> sheets,
            Dictionary<string, System.Type> typeMap)
        {
            var affected = XlsxSheetReader.StaleFormulaCells
                .Where(x => typeMap.ContainsKey(x.sheet) && IsConvertedColumn(sheets, x.sheet, x.column))
                .ToList();

            if (affected.Count == 0)
            {
                return false;
            }

            string sample = string.Join(", ", affected.Take(10).Select(x => $"{x.sheet}!{x.cell}"));
            string message =
                $"수식 결과가 저장돼 있지 않은 셀 {affected.Count}개를 발견했습니다.\n" +
                $"이 상태로 변환하면 해당 열이 빈 값으로 들어갑니다.\n\n" +
                $"xlsx를 Excel에서 한 번 열고 저장한 뒤 다시 시도하세요.\n\n예: {sample}";

            Debug.LogError($"[GameDataConverter] {message}");
            EditorUtility.DisplayDialog("GameData 변환 중단", message, "확인");
            return true;
        }

        /// <summary>
        /// 그 열이 실제로 JSON에 실리는 열인지. 헤더가 #로 시작하거나 비어 있으면 표시 전용이라
        /// 값이 없어도 산출물에 영향이 없다(<see cref="BuildExternalShape"/>가 같은 규칙으로 스킵한다).
        /// 헤더를 읽지 못하면 판단을 보류하고 변환 대상으로 본다 — 막는 쪽이 안전하다.
        /// </summary>
        private static bool IsConvertedColumn(
            Dictionary<string, SortedDictionary<int, Dictionary<int, object>>> sheets,
            string sheetName, int column)
        {
            if (!sheets.TryGetValue(sheetName, out var rows) ||
                !rows.TryGetValue(1, out Dictionary<int, object> headerCells))
            {
                return true;
            }
            return headerCells.TryGetValue(column, out object header) && IsConvertedHeader(header);
        }

        /// <summary>헤더 값이 변환 대상인지(#·빈 헤더 = 표시 전용, D17).</summary>
        private static bool IsConvertedHeader(object header)
            => header is string name && !string.IsNullOrWhiteSpace(name) && !name.StartsWith("#");

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
                    if (IsConvertedHeader(value))
                    {
                        headers[col] = (string)value;
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
