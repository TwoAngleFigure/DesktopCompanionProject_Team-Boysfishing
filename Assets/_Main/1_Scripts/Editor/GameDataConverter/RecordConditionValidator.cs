using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using DesktopCompanion.Core;
using DesktopCompanion.Systems;

namespace DesktopCompanion.EditorTools
{
    /// <summary>
    /// GameData의 시트 조건 칸(m_condition)을 런타임과 같은 규칙(RecordConditions.TryParse)으로 검증한다.
    /// GameData 변환 완료 시 자동 실행되며, 메뉴에서 현재 GameData.json을 대상으로 다시 실행할 수 있다.
    /// </summary>
    public static class RecordConditionValidator
    {
        private const string ConditionField = "m_condition";
        private const string OutputFileName = "GameData.json";

        [MenuItem("Tools/DesktopCompanion/기록 조건 검증")]
        public static void ValidateGameDataFile()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Data", OutputFileName);
            if (File.Exists(path) == false)
            {
                EditorUtility.DisplayDialog("기록 조건 검증", $"GameData.json이 없습니다.\n{path}", "확인");
                return;
            }

            try
            {
                JToken root = JToken.Parse(File.ReadAllText(path));
                JArray items = root is JObject sheetRoot
                    ? ExternalSheetNormalizer.Normalize(sheetRoot, GameDataTypes.BuildTypeMap())
                    : root as JArray;

                if (items == null)
                {
                    EditorUtility.DisplayDialog("기록 조건 검증", "지원하지 않는 JSON 형식입니다.", "확인");
                    return;
                }

                int errorCount = Validate(items, out int checkedCount);
                EditorUtility.DisplayDialog("기록 조건 검증",
                    $"조건 {checkedCount}건 검사, 오류 {errorCount}건\n\n오류 내용은 Console 창을 확인하세요.", "확인");
            }
            catch (Exception e)
            {
                Debug.LogError($"[RecordConditionValidator] 검증 실패: {e}");
                EditorUtility.DisplayDialog("기록 조건 검증 실패", e.Message, "확인");
            }
        }

        /// <summary>
        /// 내부 표준 JSON 항목 배열의 m_condition 값을 검증하고 오류 수를 반환한다.
        /// 빈 조건은 검사 수에 넣지 않는다. 오류는 데이터 종류·ID와 함께 Console에 남긴다.
        /// </summary>
        public static int Validate(JArray items, out int checkedCount)
        {
            checkedCount = 0;
            int errorCount = 0;
            Dictionary<string, Type> typeMap = GameDataTypes.BuildTypeMap();
            var warnedTypes = new HashSet<string>();

            foreach (JToken token in items)
            {
                if (token is not JObject item || item[ConditionField] is not JToken conditionToken
                    || conditionToken.Type == JTokenType.Null)
                {
                    continue;
                }

                string text = conditionToken.Type == JTokenType.String
                    ? conditionToken.Value<string>()
                    : conditionToken.ToString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                string typeName = item.Value<string>("type") ?? "?";
                string id = item["m_id"]?.ToString() ?? "?";

                // 시트에 열은 있는데 클래스에 필드가 없으면 값이 로드 시 무시된다. 종류당 한 번만 알린다.
                if (typeMap.TryGetValue(typeName, out Type type) && HasConditionField(type) == false
                    && warnedTypes.Add(typeName))
                {
                    Debug.LogWarning($"[RecordConditionValidator] {typeName}: 시트에 {ConditionField} 열이 있지만 클래스에 필드가 없어 로드되지 않는다");
                }

                checkedCount++;
                if (RecordConditions.TryParse(text, out _, out string error) == false)
                {
                    errorCount++;
                    Debug.LogError($"[RecordConditionValidator] {typeName} {id}: {error}");
                }
            }

            if (checkedCount > 0)
            {
                Debug.Log($"[RecordConditionValidator] 조건 {checkedCount}건 검사, 오류 {errorCount}건");
            }

            return errorCount;
        }

        private static bool HasConditionField(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(ConditionField,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field.FieldType == typeof(string);
                }
            }

            return false;
        }
    }
}
