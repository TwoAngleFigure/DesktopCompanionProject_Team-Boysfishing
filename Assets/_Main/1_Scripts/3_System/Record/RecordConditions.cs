using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesktopCompanion.Systems
{
    /// <summary>조건의 절 하나: 집계키 >= 목표값.</summary>
    public readonly struct RecordClause
    {
        public readonly RecordKey Key;
        public readonly long Target;

        public RecordClause(RecordKey key, long target)
        {
            Key = key;
            Target = target;
        }
    }

    /// <summary>
    /// 파싱된 조건. 절 전부를 만족해야 충족이다(AND).
    /// 빈 조건은 항상 충족, 무효 조건(형식 오류)은 항상 미충족이다.
    /// </summary>
    public sealed class RecordCondition
    {
        public static readonly RecordCondition Empty = new(Array.Empty<RecordClause>(), false);
        public static readonly RecordCondition Invalid = new(Array.Empty<RecordClause>(), true);

        public readonly RecordClause[] Clauses;
        public readonly bool IsInvalid;

        public bool IsEmpty => IsInvalid == false && Clauses.Length == 0;

        public RecordCondition(RecordClause[] clauses, bool isInvalid)
        {
            Clauses = clauses;
            IsInvalid = isInvalid;
        }
    }

    /// <summary>
    /// 시트 조건 칸(m_condition)의 해석 규칙. 필드가 없다 — 값 조회 함수는 호출자가 넘긴다.
    /// 모든 소비자와 에디터 검증 툴이 같은 함수를 쓴다.
    ///
    /// 표기: "집계키>=값"을 ;로 연결한다. 집계키는 "출처" 또는 "출처:대상ID". 빈칸은 조건 없음.
    /// 예: FishCaught>=100 / FishCaught:100001>=5;FishCaught>=50
    /// 비교는 >=만 지원한다. 기록은 증가만 하므로 "이상"만 한 번 달성되면 계속 유지된다.
    /// </summary>
    public static class RecordConditions
    {
        private const string CompareToken = ">=";

        /// <summary>
        /// 파싱한다. 형식 오류면 context(데이터 종류·ID)를 붙여 에러 로그를 남기고 무효 조건을 반환한다.
        /// 무효 조건은 항상 미충족이므로 오류 때문에 의도치 않은 사건이 일어나지 않는다.
        /// </summary>
        public static RecordCondition Parse(string text, string context)
        {
            if (TryParse(text, out RecordCondition condition, out string error))
            {
                return condition;
            }

            Debug.LogError($"[RecordConditions] {context}: {error}");
            return RecordCondition.Invalid;
        }

        /// <summary>로그 없이 파싱한다. 검증 툴용.</summary>
        public static bool TryParse(string text, out RecordCondition condition, out string error)
        {
            condition = RecordCondition.Empty;
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            var clauses = new List<RecordClause>();
            foreach (string raw in text.Split(';'))
            {
                string token = raw.Trim();
                if (token.Length == 0)
                {
                    continue;   // 빈 절(;;)은 무시한다
                }

                if (TryParseClause(token, out RecordClause clause, out error) == false)
                {
                    condition = RecordCondition.Invalid;
                    return false;
                }

                clauses.Add(clause);
            }

            condition = new RecordCondition(clauses.ToArray(), false);
            return true;
        }

        /// <summary>현재 값으로 충족 여부를 판정한다. read에는 보통 RecordSystem.GetValue를 넘긴다.</summary>
        public static bool IsMet(RecordCondition condition, Func<RecordKey, long> read)
        {
            if (condition == null || condition.IsInvalid)
            {
                return false;
            }

            foreach (RecordClause clause in condition.Clauses)
            {
                if (read(clause.Key) < clause.Target)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 이번 변경으로 미충족 → 충족이 되었는지 판정한다. 변경된 키에 이전 값을 대입해 이전 판정을 계산한다.
        /// 기록은 증가만 하므로 별도 저장 없이 달성 순간을 감지한다.
        /// </summary>
        public static bool IsAchievedBy(RecordCondition condition, IReadOnlyList<RecordChange> changes, Func<RecordKey, long> read)
        {
            if (condition == null || condition.IsInvalid || condition.IsEmpty || IsMet(condition, read) == false)
            {
                return false;
            }

            bool wasMet = IsMet(condition, key =>
            {
                for (int i = 0; i < changes.Count; i++)
                {
                    if (changes[i].Key.Equals(key))
                    {
                        return changes[i].OldValue;
                    }
                }

                return read(key);
            });

            return wasMet == false;
        }

        /// <summary>절 하나의 현재 값과 목표값. 진행도 표시용.</summary>
        public static (long current, long target) GetProgress(RecordClause clause, Func<RecordKey, long> read)
            => (read(clause.Key), clause.Target);

        // "FishCaught>=100" / "FishCaught:100001>=5"
        private static bool TryParseClause(string token, out RecordClause clause, out string error)
        {
            clause = default;
            error = null;

            int compareIndex = token.IndexOf(CompareToken, StringComparison.Ordinal);
            if (compareIndex < 0)
            {
                error = $"'{token}' — '집계키>=값' 형식이 아니다";
                return false;
            }

            string keyText = token.Substring(0, compareIndex).Trim();
            string valueText = token.Substring(compareIndex + CompareToken.Length).Trim();

            string[] keyParts = keyText.Split(':');
            string sourceText = keyParts[0].Trim();
            if (IsSourceName(sourceText, out RecordSource source) == false)
            {
                error = $"'{token}' — 알 수 없는 출처 '{sourceText}'";
                return false;
            }

            int targetId = 0;
            if (keyParts.Length > 2
                || (keyParts.Length == 2 && (int.TryParse(keyParts[1].Trim(), out targetId) == false || targetId <= 0)))
            {
                error = $"'{token}' — 대상 ID는 1 이상의 정수여야 한다";
                return false;
            }

            if (long.TryParse(valueText, out long target) == false || target <= 0)
            {
                error = $"'{token}' — 값은 1 이상의 정수여야 한다";
                return false;
            }

            clause = new RecordClause(new RecordKey(source, targetId), target);
            return true;
        }

        // Enum.TryParse는 "1" 같은 숫자 문자열도 받아들이므로 정의된 이름인지 따로 확인한다.
        private static bool IsSourceName(string text, out RecordSource source)
        {
            source = RecordSource.None;
            return text.Length > 0
                && char.IsLetter(text[0])
                && Enum.TryParse(text, out source)
                && Enum.IsDefined(typeof(RecordSource), source)
                && source != RecordSource.None;
        }
    }
}
