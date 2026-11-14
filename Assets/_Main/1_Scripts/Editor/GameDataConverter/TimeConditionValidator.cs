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
    /// GameData의 시간 설정(TimeSettingData·DayPhaseData)과 시트 시간 조건 칸(m_timeCondition)을
    /// 런타임과 같은 규칙(TimeConditionRule.TryParse)으로 검증한다.
    /// GameData 변환 완료 시 자동 실행되며, 메뉴에서 현재 GameData.json을 대상으로 다시 실행할 수 있다.
    /// </summary>
    public static class TimeConditionValidator
    {
        private const string ConditionField = "m_timeCondition";
        private const string OutputFileName = "GameData.json";
        private const string SettingType = "TimeSettingData";
        private const string PhaseType = "DayPhaseData";

        // 설정이 없거나 틀렸을 때 조건 검증에 쓰는 값. TimeSystem의 기본값과 같다.
        private const double DefaultDayLengthMinutes = 24.0;
        private const int DefaultDaysPerSeason = 28;

        [MenuItem("Tools/DesktopCompanion/시간 조건 검증")]
        public static void ValidateGameDataFile()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Data", OutputFileName);
            if (File.Exists(path) == false)
            {
                EditorUtility.DisplayDialog("시간 조건 검증", $"GameData.json이 없습니다.\n{path}", "확인");
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
                    EditorUtility.DisplayDialog("시간 조건 검증", "지원하지 않는 JSON 형식입니다.", "확인");
                    return;
                }

                int errorCount = Validate(items, out int checkedCount);
                EditorUtility.DisplayDialog("시간 조건 검증",
                    $"조건 {checkedCount}건 검사, 오류 {errorCount}건(설정 오류 포함)\n\n오류 내용은 Console 창을 확인하세요.", "확인");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TimeConditionValidator] 검증 실패: {e}");
                EditorUtility.DisplayDialog("시간 조건 검증 실패", e.Message, "확인");
            }
        }

        /// <summary>
        /// 내부 표준 JSON 항목 배열의 시간 설정과 m_timeCondition 값을 검증하고 오류 수를 반환한다.
        /// 빈 조건은 검사 수에 넣지 않는다. 오류는 데이터 종류·ID와 함께 Console에 남긴다.
        /// </summary>
        public static int Validate(JArray items, out int checkedCount)
        {
            checkedCount = 0;
            int errorCount = 0;

            TimeConverter converter = BuildConverter(items, ref errorCount);

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
                    Debug.LogWarning($"[TimeConditionValidator] {typeName}: 시트에 {ConditionField} 열이 있지만 클래스에 필드가 없어 로드되지 않는다");
                }

                checkedCount++;
                if (TimeConditionRule.TryParse(text, converter, out _, out string error) == false)
                {
                    errorCount++;
                    Debug.LogError($"[TimeConditionValidator] {typeName} {id}: {error}");
                }
            }

            if (checkedCount > 0 || errorCount > 0)
            {
                Debug.Log($"[TimeConditionValidator] 조건 {checkedCount}건 검사, 오류 {errorCount}건(설정 오류 포함)");
            }

            return errorCount;
        }

        // 변환 결과의 설정 항목으로 런타임과 같은 변환 규칙을 만든다. 설정 자체의 오류도 여기서 센다.
        private static TimeConverter BuildConverter(JArray items, ref int errorCount)
        {
            double dayLengthMinutes = DefaultDayLengthMinutes;
            int daysPerSeason = DefaultDaysPerSeason;
            int settingCount = 0;
            var phases = new List<(string key, double startHour)>();
            var seenKeys = new HashSet<string>();
            var seenStartHours = new HashSet<double>();

            foreach (JToken token in items)
            {
                if (token is not JObject item)
                {
                    continue;
                }

                string typeName = item.Value<string>("type");
                string id = item["m_id"]?.ToString() ?? "?";

                if (typeName == SettingType)
                {
                    settingCount++;
                    ValidateSetting(item, id, ref dayLengthMinutes, ref daysPerSeason, ref errorCount);
                }
                else if (typeName == PhaseType)
                {
                    ValidatePhase(item, id, phases, seenKeys, seenStartHours, ref errorCount);
                }
            }

            if (settingCount == 0)
            {
                Debug.LogWarning($"[TimeConditionValidator] {SettingType}가 없다. 게임은 기본값(하루 {DefaultDayLengthMinutes}분, 계절당 {DefaultDaysPerSeason}일)으로 동작한다");
            }
            else if (settingCount > 1)
            {
                errorCount++;
                Debug.LogError($"[TimeConditionValidator] {SettingType}는 1행(ID=1)이어야 한다. 현재 {settingCount}행");
            }

            return new TimeConverter(dayLengthMinutes * 60.0, daysPerSeason, phases);
        }

        private static void ValidateSetting(JObject item, string id, ref double dayLengthMinutes, ref int daysPerSeason,
            ref int errorCount)
        {
            if (id != "1")
            {
                errorCount++;
                Debug.LogError($"[TimeConditionValidator] {SettingType} {id}: ID는 1이어야 한다(TimeSystem이 ID=1만 읽는다)");
            }

            double? dayLength = ReadNumber(item, "m_dayLengthMinutes");
            if (dayLength == null || dayLength.Value <= 0.0)
            {
                errorCount++;
                Debug.LogError($"[TimeConditionValidator] {SettingType} {id}: m_dayLengthMinutes는 0보다 큰 수여야 한다");
            }
            else
            {
                dayLengthMinutes = dayLength.Value;
            }

            double? days = ReadNumber(item, "m_daysPerSeason");
            if (days == null || days.Value < 1.0 || days.Value != Math.Floor(days.Value))
            {
                errorCount++;
                Debug.LogError($"[TimeConditionValidator] {SettingType} {id}: m_daysPerSeason은 1 이상의 정수여야 한다");
            }
            else
            {
                daysPerSeason = (int)days.Value;
            }

            double? startHour = ReadNumber(item, "m_startHour");
            if (startHour == null || startHour.Value < 0.0 || startHour.Value >= TimeConverter.HoursPerDay)
            {
                errorCount++;
                Debug.LogError($"[TimeConditionValidator] {SettingType} {id}: m_startHour는 0 이상 24 미만의 수여야 한다");
            }

            string flowMode = item.Value<string>("m_flowMode");
            if (IsEnumName<TimeFlowMode>(flowMode) == false)
            {
                errorCount++;
                Debug.LogError($"[TimeConditionValidator] {SettingType} {id}: m_flowMode '{flowMode}'는 흐름 방식 이름이 아니다({string.Join(" / ", Enum.GetNames(typeof(TimeFlowMode)))})");
            }
        }

        private static void ValidatePhase(JObject item, string id, List<(string key, double startHour)> phases,
            HashSet<string> seenKeys, HashSet<double> seenStartHours, ref int errorCount)
        {
            string key = item.Value<string>("m_key")?.Trim();
            double? startHour = ReadNumber(item, "m_startHour");
            bool isValid = true;

            if (string.IsNullOrEmpty(key) || char.IsLetter(key[0]) == false || key.IndexOf(';') >= 0 || key.IndexOf('=') >= 0)
            {
                isValid = false;
                errorCount++;
                Debug.LogError($"[TimeConditionValidator] {PhaseType} {id}: m_key '{key}'는 영문자로 시작해야 하고 ';'·'='를 쓸 수 없다");
            }
            else if (seenKeys.Add(key) == false)
            {
                isValid = false;
                errorCount++;
                Debug.LogError($"[TimeConditionValidator] {PhaseType} {id}: m_key '{key}'가 중복된다");
            }

            if (startHour == null || startHour.Value < 0.0 || startHour.Value >= TimeConverter.HoursPerDay)
            {
                isValid = false;
                errorCount++;
                Debug.LogError($"[TimeConditionValidator] {PhaseType} {id}: m_startHour는 0 이상 24 미만의 수여야 한다");
            }
            else if (seenStartHours.Add(startHour.Value) == false)
            {
                isValid = false;
                errorCount++;
                Debug.LogError($"[TimeConditionValidator] {PhaseType} {id}: m_startHour {startHour.Value}가 다른 시간대와 중복된다");
            }

            if (isValid)
            {
                phases.Add((key, startHour.Value));
            }
        }

        private static double? ReadNumber(JObject item, string field)
        {
            JToken token = item[field];
            if (token == null)
            {
                return null;
            }

            return token.Type == JTokenType.Integer || token.Type == JTokenType.Float
                ? token.Value<double>()
                : (double?)null;
        }

        // Enum.TryParse는 숫자 문자열도 받아들이므로 정의된 이름인지 따로 확인한다.
        private static bool IsEnumName<T>(string text) where T : struct, Enum
            => string.IsNullOrEmpty(text) == false
                && char.IsLetter(text[0])
                && Enum.TryParse(text, out T value)
                && Enum.IsDefined(typeof(T), value);

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
