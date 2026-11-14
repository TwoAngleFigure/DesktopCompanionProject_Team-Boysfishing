using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>
    /// 파싱된 시간 조건. 절 전부를 만족해야 참이다(AND). 지정하지 않은 항목은 제한하지 않는다.
    /// 빈 조건은 항상 참, 무효 조건(형식 오류)은 항상 거짓이다.
    /// 생성은 TimeConditionRule의 파싱 함수만 한다.
    /// </summary>
    public sealed class TimeCondition
    {
        public static readonly TimeCondition Empty = new();
        public static readonly TimeCondition Invalid = new() { IsInvalid = true };

        public bool IsInvalid { get; private set; }

        public bool HasSince { get; internal set; }       // Date>= 절
        public GameDate Since { get; internal set; }
        public int Year { get; internal set; }            // 0 = 제한 없음
        public bool HasSeason { get; internal set; }
        public Season Season { get; internal set; }
        public int DayFrom { get; internal set; }         // 0 = 제한 없음
        public int DayTo { get; internal set; }
        public string Phase { get; internal set; }        // null = 제한 없음

        public bool IsEmpty => IsInvalid == false && HasSince == false && HasWindowClause == false;

        /// <summary>Date>= 절만으로 된 조건. 한 번 참이 되면 거짓으로 돌아가지 않는다.</summary>
        public bool IsPermanent => IsInvalid == false && HasSince && HasWindowClause == false;

        // 진입과 이탈을 반복하게 만드는 절(Year·Season·Day·Time)이 하나라도 있는지.
        private bool HasWindowClause => Year != 0 || HasSeason || DayFrom != 0 || Phase != null;

        internal TimeCondition() { }
    }

    /// <summary>
    /// 시트 시간 조건 칸(m_timeCondition)의 해석 규칙. 필드가 없다.
    /// 런타임 소비자와 변환 검증기가 같은 함수를 쓴다.
    ///
    /// 표기: 절을 ;로 연결한다(모두 만족). 빈칸은 조건 없음.
    ///   Date>=년차.계절.날짜   그 날짜가 되었거나 지났다
    ///   Year=n                 그 년차다
    ///   Season=이름            Spring / Summer / Autumn / Winter
    ///   Day=n 또는 Day=a~b     계절 안의 날짜(범위)
    ///   Time=시간대            DayPhaseData.m_key
    /// 예: Date>=1.Summer.10 / Season=Summer;Day=10~12 / Season=Summer;Time=Night
    /// </summary>
    public static class TimeConditionRule
    {
        private const string SinceToken = ">=";
        private const char EqualToken = '=';
        private const char RangeToken = '~';

        /// <summary>
        /// 파싱한다. 형식 오류면 context(데이터 종류·ID)를 붙여 에러 로그를 남기고 무효 조건을 반환한다.
        /// 무효 조건은 항상 거짓이므로 오류 때문에 의도치 않은 컨텐츠가 열리지 않는다.
        /// </summary>
        public static TimeCondition Parse(string text, TimeConverter converter, string context)
        {
            if (TryParse(text, converter, out TimeCondition condition, out string error))
            {
                return condition;
            }

            Debug.LogError($"[TimeConditionRule] {context}: {error}");
            return TimeCondition.Invalid;
        }

        /// <summary>로그 없이 파싱한다. 검증기용. 날짜 범위와 시간대 이름 검증에 converter의 설정값을 쓴다.</summary>
        public static bool TryParse(string text, TimeConverter converter, out TimeCondition condition, out string error)
        {
            condition = TimeCondition.Empty;
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            var parsed = new TimeCondition();
            var seenNames = new HashSet<string>();
            foreach (string raw in text.Split(';'))
            {
                string token = raw.Trim();
                if (token.Length == 0)
                {
                    continue;   // 빈 절(;;)은 무시한다
                }

                if (TryParseClause(token, converter, parsed, seenNames, out error) == false)
                {
                    condition = TimeCondition.Invalid;
                    return false;
                }
            }

            condition = parsed;
            return true;
        }

        /// <summary>그 시각에 조건이 참인지 판정한다. 무효 조건은 false, 빈 조건은 true다.</summary>
        public static bool IsActive(TimeCondition condition, double elapsedDays, TimeConverter converter)
        {
            if (IsActiveOnDay(condition, TimeConverter.DayIndexOf(elapsedDays), converter) == false)
            {
                return false;
            }

            return condition.Phase == null
                || condition.Phase == converter.PhaseAt(TimeConverter.HourOf(elapsedDays));
        }

        /// <summary>Time= 절을 제외하고 그 날짜에 조건이 참인지 판정한다.</summary>
        public static bool IsActiveOnDay(TimeCondition condition, long dayIndex, TimeConverter converter)
        {
            if (condition == null || condition.IsInvalid)
            {
                return false;
            }

            if (condition.HasSince && dayIndex < converter.ToDayIndex(condition.Since))
            {
                return false;
            }

            GameDate date = converter.ToDate(dayIndex);
            if (condition.Year != 0 && date.Year != condition.Year)
            {
                return false;
            }

            if (condition.HasSeason && date.Season != condition.Season)
            {
                return false;
            }

            return condition.DayFrom == 0 || (date.Day >= condition.DayFrom && date.Day <= condition.DayTo);
        }

        private static bool TryParseClause(string token, TimeConverter converter, TimeCondition target,
            HashSet<string> seenNames, out string error)
        {
            error = null;

            // ">="를 먼저 찾는다. "Date>=…"가 일반 절의 '='에서 잘리지 않게 한다.
            int sinceIndex = token.IndexOf(SinceToken, StringComparison.Ordinal);
            int equalIndex = token.IndexOf(EqualToken);
            bool isSince = sinceIndex >= 0;
            if (isSince == false && equalIndex < 0)
            {
                error = $"'{token}' — '이름=값' 또는 'Date>=년차.계절.날짜' 형식이 아니다";
                return false;
            }

            string name = token.Substring(0, isSince ? sinceIndex : equalIndex).Trim();
            string value = token.Substring(isSince ? sinceIndex + SinceToken.Length : equalIndex + 1).Trim();

            if (seenNames.Add(name) == false)
            {
                error = $"'{token}' — '{name}' 절이 두 번 이상 있다";
                return false;
            }

            switch (name)
            {
                case "Date" when isSince:
                    return TryParseSince(token, value, converter, target, out error);

                case "Year" when isSince == false:
                    if (int.TryParse(value, out int year) == false || year < 1)
                    {
                        error = $"'{token}' — 년차는 1 이상의 정수여야 한다";
                        return false;
                    }

                    target.Year = year;
                    return true;

                case "Season" when isSince == false:
                    if (TryParseSeason(value, out Season season) == false)
                    {
                        error = $"'{token}' — 알 수 없는 계절 '{value}'";
                        return false;
                    }

                    target.HasSeason = true;
                    target.Season = season;
                    return true;

                case "Day" when isSince == false:
                    return TryParseDayRange(token, value, converter, target, out error);

                case "Time" when isSince == false:
                    if (converter.HasPhase(value) == false)
                    {
                        error = $"'{token}' — 정의되지 않은 시간대 '{value}'";
                        return false;
                    }

                    target.Phase = value;
                    return true;

                default:
                    error = $"'{token}' — 알 수 없는 절 '{name}'";
                    return false;
            }
        }

        // "1.Summer.10"
        private static bool TryParseSince(string token, string value, TimeConverter converter, TimeCondition target,
            out string error)
        {
            error = null;
            string[] parts = value.Split('.');
            if (parts.Length != 3
                || int.TryParse(parts[0].Trim(), out int year) == false || year < 1
                || TryParseSeason(parts[1].Trim(), out Season season) == false
                || int.TryParse(parts[2].Trim(), out int day) == false || day < 1 || day > converter.DaysPerSeason)
            {
                error = $"'{token}' — 'Date>=년차.계절.날짜' 형식이 아니거나 범위를 벗어났다(날짜 1~{converter.DaysPerSeason})";
                return false;
            }

            target.HasSince = true;
            target.Since = new GameDate(year, season, day);
            return true;
        }

        // "10" / "10~12"
        private static bool TryParseDayRange(string token, string value, TimeConverter converter, TimeCondition target,
            out string error)
        {
            error = null;
            string[] parts = value.Split(RangeToken);
            int from = 0;
            int to = 0;

            bool isParsed = parts.Length == 1
                ? int.TryParse(parts[0].Trim(), out from)
                : parts.Length == 2 && int.TryParse(parts[0].Trim(), out from) && int.TryParse(parts[1].Trim(), out to);
            if (isParsed && parts.Length == 1)
            {
                to = from;
            }

            if (isParsed == false || from < 1 || to < from || to > converter.DaysPerSeason)
            {
                error = $"'{token}' — 날짜는 1~{converter.DaysPerSeason} 범위의 정수 또는 '시작~끝'이어야 한다";
                return false;
            }

            target.DayFrom = from;
            target.DayTo = to;
            return true;
        }

        // Enum.TryParse는 "1" 같은 숫자 문자열도 받아들이므로 정의된 이름인지 따로 확인한다.
        private static bool TryParseSeason(string text, out Season season)
        {
            season = default;
            return text.Length > 0
                && char.IsLetter(text[0])
                && Enum.TryParse(text, out season)
                && Enum.IsDefined(typeof(Season), season);
        }
    }
}
