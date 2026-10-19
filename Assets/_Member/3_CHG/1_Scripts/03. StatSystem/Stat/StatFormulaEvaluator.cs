using System;
using System.Collections.Generic;
using System.Globalization;

namespace DesktopCompanion.Systems
{
    /// <summary>스탯 수식 문자열을 한 번 해석하여 재사용한다. 변수는 Str, Tec, Luk, BaseValue다.</summary>
    internal sealed class StatFormulaEvaluator
    {
        private readonly Func<Variables, double> m_expression;

        private readonly struct Variables
        {
            public readonly float Str;
            public readonly float Tec;
            public readonly float Luk;
            public readonly float BaseValue;

            public Variables(float str, float tec, float luk, float baseValue)
            {
                Str = str;
                Tec = tec;
                Luk = luk;
                BaseValue = baseValue;
            }
        }

        private StatFormulaEvaluator(Func<Variables, double> expression) => m_expression = expression;

        internal static bool TryCompile(string expression, out StatFormulaEvaluator evaluator, out string error)
        {
            evaluator = null;
            error = null;
            try
            {
                evaluator = new StatFormulaEvaluator(new Parser(expression).Parse());
                return true;
            }
            catch (FormatException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        internal bool TryEvaluate(float str, float tec, float luk, float baseValue, out float value, out string error)
        {
            value = 0f;
            error = null;
            try
            {
                double calculated = RequireFinite(m_expression(new Variables(str, tec, luk, baseValue)));
                if (calculated > float.MaxValue || calculated < -float.MaxValue)
                    throw new ArithmeticException("결과가 float 표현 범위를 벗어났습니다.");
                value = (float)calculated;
                return true;
            }
            catch (ArithmeticException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static double RequireFinite(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArithmeticException("계산 결과가 NaN 또는 Infinity입니다.");
            return value;
        }

        private sealed class Parser
        {
            private readonly string m_text;
            private int m_position;
            private int m_depth;

            internal Parser(string expression)
            {
                if (string.IsNullOrWhiteSpace(expression)) throw new FormatException("수식이 비어 있습니다.");
                if (expression.Length > 2048) throw new FormatException("수식이 최대 길이(2048자)를 초과했습니다.");
                m_text = expression;
            }

            internal Func<Variables, double> Parse()
            {
                var expression = ParseSum();
                SkipWhitespace();
                if (m_position != m_text.Length) throw InvalidToken();
                return expression;
            }

            private Func<Variables, double> ParseSum()
            {
                var expression = ParseProduct();
                while (true)
                {
                    SkipWhitespace();
                    if (!Peek('+') && !Peek('-')) return expression;
                    char operation = m_text[m_position++];
                    var left = expression;
                    var right = ParseProduct();
                    expression = input => RequireFinite(operation == '+' ? left(input) + right(input) : left(input) - right(input));
                }
            }

            private Func<Variables, double> ParseProduct()
            {
                var expression = ParseUnary();
                while (true)
                {
                    SkipWhitespace();
                    if (!Peek('*') && !Peek('/') && !Peek('%')) return expression;
                    char operation = m_text[m_position++];
                    var left = expression;
                    var right = ParseUnary();
                    expression = input =>
                    {
                        double a = left(input);
                        double b = right(input);
                        if (operation != '*' && b == 0d) throw new DivideByZeroException("0으로 나눌 수 없습니다.");
                        return RequireFinite(operation == '*' ? a * b : operation == '/' ? a / b : a % b);
                    };
                }
            }

            private Func<Variables, double> ParseUnary()
            {
                if (++m_depth > 64) throw new FormatException("수식의 중첩이 너무 깊습니다.");
                try
                {
                    SkipWhitespace();
                    if (Take('+')) return ParseUnary();
                    if (Take('-'))
                    {
                        var operand = ParseUnary();
                        return input => -operand(input);
                    }
                    return ParsePower();
                }
                finally { m_depth--; }
            }

            private Func<Variables, double> ParsePower()
            {
                var left = ParsePrimary();
                SkipWhitespace();
                if (!Take('^')) return left;
                var right = ParseUnary();
                return input => RequireFinite(Math.Pow(left(input), right(input)));
            }

            private Func<Variables, double> ParsePrimary()
            {
                SkipWhitespace();
                if (Take('('))
                {
                    var expression = ParseSum();
                    SkipWhitespace();
                    if (!Take(')')) throw new FormatException("닫는 괄호가 필요합니다.");
                    return expression;
                }
                if (m_position < m_text.Length && (IsDigit(m_text[m_position]) || Peek('.'))) return ParseNumber();

                string name = ReadIdentifier();
                SkipWhitespace();
                if (Take('(')) return ParseFunction(name);
                switch (name.ToLowerInvariant())
                {
                    case "str": return input => input.Str;
                    case "tec": return input => input.Tec;
                    case "luk": return input => input.Luk;
                    case "basevalue": return input => input.BaseValue;
                    default: throw new FormatException($"알 수 없는 변수: {name}");
                }
            }

            private Func<Variables, double> ParseNumber()
            {
                int start = m_position;
                while (m_position < m_text.Length && IsDigit(m_text[m_position])) m_position++;
                if (Take('.')) while (m_position < m_text.Length && IsDigit(m_text[m_position])) m_position++;
                if (Take('e') || Take('E'))
                {
                    if (Peek('+') || Peek('-')) m_position++;
                    while (m_position < m_text.Length && IsDigit(m_text[m_position])) m_position++;
                }
                string number = m_text.Substring(start, m_position - start);
                if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                    double.IsNaN(value) || double.IsInfinity(value)) throw new FormatException($"잘못된 숫자: {number}");
                return _ => value;
            }

            private Func<Variables, double> ParseFunction(string name)
            {
                var arguments = new List<Func<Variables, double>>();
                SkipWhitespace();
                if (!Take(')'))
                {
                    do { arguments.Add(ParseSum()); SkipWhitespace(); } while (Take(','));
                    if (!Take(')')) throw new FormatException("함수의 닫는 괄호가 필요합니다.");
                }

                int count;
                switch (name.ToLowerInvariant())
                {
                    case "abs": case "sqrt": count = 1; break;
                    case "min": case "max": case "pow": count = 2; break;
                    case "clamp": count = 3; break;
                    default: throw new FormatException($"지원하지 않는 함수: {name}");
                }
                if (arguments.Count != count) throw new FormatException($"{name} 함수는 인수가 {count}개 필요합니다.");
                switch (name.ToLowerInvariant())
                {
                    case "abs": return input => RequireFinite(Math.Abs(arguments[0](input)));
                    case "sqrt": return input => RequireFinite(Math.Sqrt(arguments[0](input)));
                    case "min": return input => RequireFinite(Math.Min(arguments[0](input), arguments[1](input)));
                    case "max": return input => RequireFinite(Math.Max(arguments[0](input), arguments[1](input)));
                    case "pow": return input => RequireFinite(Math.Pow(arguments[0](input), arguments[1](input)));
                    default: return input =>
                    {
                        double value = RequireFinite(arguments[0](input));
                        double min = RequireFinite(arguments[1](input));
                        double max = RequireFinite(arguments[2](input));
                        if (min > max) throw new ArithmeticException("Clamp의 최소값이 최대값보다 큽니다.");
                        return Math.Max(min, Math.Min(max, value));
                    };
                }
            }

            private string ReadIdentifier()
            {
                int start = m_position;
                while (m_position < m_text.Length && (char.IsLetterOrDigit(m_text[m_position]) || m_text[m_position] == '_')) m_position++;
                if (m_position == start) throw InvalidToken();
                return m_text.Substring(start, m_position - start);
            }

            private void SkipWhitespace()
            {
                while (m_position < m_text.Length && char.IsWhiteSpace(m_text[m_position])) m_position++;
            }
            private bool Peek(char token) => m_position < m_text.Length && m_text[m_position] == token;
            private bool Take(char token)
            {
                if (!Peek(token)) return false;
                m_position++;
                return true;
            }
            private static bool IsDigit(char value) => value >= '0' && value <= '9';
            private FormatException InvalidToken() => new FormatException($"수식의 {m_position + 1}번째 문자를 해석할 수 없습니다.");
        }
    }
}
