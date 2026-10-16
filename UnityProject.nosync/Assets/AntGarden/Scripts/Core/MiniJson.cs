// Ant Garden core — a small dependency-free JSON reader (Unity has no System.Text.Json).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AntGarden.Core
{
    /// <summary>
    /// Minimal JSON parser. Objects become <c>Dictionary&lt;string, object&gt;</c> (a repeated key keeps
    /// the last value, as in <c>JSON.parse</c>), arrays <c>List&lt;object&gt;</c>, numbers <c>double</c>,
    /// plus <c>string</c>, <c>bool</c> and <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Numbers with at most ~15 significant digits and a decimal exponent within ±22 (every number in
    /// the Path Recognize networks) are converted exactly with one correctly rounded operation, so the
    /// result never depends on the runtime's <c>double.Parse</c>. Longer numbers fall back to
    /// <c>double.Parse</c> (correctly rounded on .NET Core 3+, Mono may differ in the last bit).
    /// Stateless and thread-safe.
    /// </remarks>
    public static class MiniJson
    {
        private static readonly double[] Pow10 =
        {
            1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11,
            1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22,
        };

        /// <summary>Parses a JSON document. Throws <see cref="FormatException"/> on malformed input.</summary>
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var p = new Parser(json);
            p.SkipWhitespace();
            object v = p.ParseValue();
            p.SkipWhitespace();
            if (!p.AtEnd) throw p.Error("unexpected trailing characters");
            return v;
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _pos;
            private readonly StringBuilder _sb = new StringBuilder();

            public Parser(string s)
            {
                _s = s;
                _pos = s.Length > 0 && s[0] == '﻿' ? 1 : 0; // tolerate a UTF-8 BOM decoded as text
            }

            public bool AtEnd => _pos >= _s.Length;

            public FormatException Error(string message)
            {
                return new FormatException("JSON: " + message + " at position " + _pos.ToString(CultureInfo.InvariantCulture));
            }

            public void SkipWhitespace()
            {
                while (_pos < _s.Length)
                {
                    char c = _s[_pos];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') _pos++;
                    else break;
                }
            }

            public object ParseValue()
            {
                if (_pos >= _s.Length) throw Error("unexpected end of input");
                char c = _s[_pos];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
                        throw Error("unexpected character '" + c + "'");
                }
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _pos, word, 0, word.Length) != 0) throw Error("invalid literal");
                _pos += word.Length;
            }

            private Dictionary<string, object> ParseObject()
            {
                var o = new Dictionary<string, object>(StringComparer.Ordinal);
                _pos++; // {
                SkipWhitespace();
                if (_pos < _s.Length && _s[_pos] == '}')
                {
                    _pos++;
                    return o;
                }
                while (true)
                {
                    SkipWhitespace();
                    if (_pos >= _s.Length || _s[_pos] != '"') throw Error("expected a string key");
                    string key = ParseString();
                    SkipWhitespace();
                    if (_pos >= _s.Length || _s[_pos] != ':') throw Error("expected ':'");
                    _pos++;
                    SkipWhitespace();
                    o[key] = ParseValue();
                    SkipWhitespace();
                    if (_pos >= _s.Length) throw Error("unterminated object");
                    char c = _s[_pos++];
                    if (c == ',') continue;
                    if (c == '}') return o;
                    throw Error("expected ',' or '}'");
                }
            }

            private List<object> ParseArray()
            {
                var a = new List<object>();
                _pos++; // [
                SkipWhitespace();
                if (_pos < _s.Length && _s[_pos] == ']')
                {
                    _pos++;
                    return a;
                }
                while (true)
                {
                    SkipWhitespace();
                    a.Add(ParseValue());
                    SkipWhitespace();
                    if (_pos >= _s.Length) throw Error("unterminated array");
                    char c = _s[_pos++];
                    if (c == ',') continue;
                    if (c == ']') return a;
                    throw Error("expected ',' or ']'");
                }
            }

            private string ParseString()
            {
                _pos++; // opening quote
                int start = _pos;
                // fast path: no escapes
                while (_pos < _s.Length)
                {
                    char c = _s[_pos];
                    if (c == '"')
                    {
                        string plain = _s.Substring(start, _pos - start);
                        _pos++;
                        return plain;
                    }
                    if (c == '\\') break;
                    _pos++;
                }
                _sb.Clear();
                _sb.Append(_s, start, _pos - start);
                while (_pos < _s.Length)
                {
                    char c = _s[_pos++];
                    if (c == '"') return _sb.ToString();
                    if (c != '\\')
                    {
                        _sb.Append(c);
                        continue;
                    }
                    if (_pos >= _s.Length) break;
                    char e = _s[_pos++];
                    switch (e)
                    {
                        case '"': _sb.Append('"'); break;
                        case '\\': _sb.Append('\\'); break;
                        case '/': _sb.Append('/'); break;
                        case 'b': _sb.Append('\b'); break;
                        case 'f': _sb.Append('\f'); break;
                        case 'n': _sb.Append('\n'); break;
                        case 'r': _sb.Append('\r'); break;
                        case 't': _sb.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _s.Length) throw Error("bad \\u escape");
                            int code = 0;
                            for (int k = 0; k < 4; k++)
                            {
                                int h = HexValue(_s[_pos++]);
                                if (h < 0) throw Error("bad \\u escape");
                                code = code * 16 + h;
                            }
                            _sb.Append((char)code);
                            break;
                        default: throw Error("bad escape");
                    }
                }
                throw Error("unterminated string");
            }

            private static int HexValue(char c)
            {
                if (c >= '0' && c <= '9') return c - '0';
                if (c >= 'a' && c <= 'f') return c - 'a' + 10;
                if (c >= 'A' && c <= 'F') return c - 'A' + 10;
                return -1;
            }

            private object ParseNumber()
            {
                int start = _pos;
                bool neg = false;
                if (_s[_pos] == '-')
                {
                    neg = true;
                    _pos++;
                }
                ulong mant = 0;
                int exp10 = 0;
                bool exact = true; // false once the mantissa no longer fits the fast path
                if (_pos < _s.Length && _s[_pos] == '0')
                {
                    _pos++;
                }
                else if (_pos < _s.Length && _s[_pos] >= '1' && _s[_pos] <= '9')
                {
                    while (_pos < _s.Length && _s[_pos] >= '0' && _s[_pos] <= '9')
                    {
                        Accumulate(ref mant, ref exp10, ref exact, _s[_pos] - '0', false);
                        _pos++;
                    }
                }
                else
                {
                    throw Error("invalid number");
                }
                if (_pos < _s.Length && _s[_pos] == '.')
                {
                    _pos++;
                    if (_pos >= _s.Length || _s[_pos] < '0' || _s[_pos] > '9') throw Error("invalid number");
                    while (_pos < _s.Length && _s[_pos] >= '0' && _s[_pos] <= '9')
                    {
                        Accumulate(ref mant, ref exp10, ref exact, _s[_pos] - '0', true);
                        _pos++;
                    }
                }
                if (_pos < _s.Length && (_s[_pos] == 'e' || _s[_pos] == 'E'))
                {
                    _pos++;
                    int sign = 1;
                    if (_pos < _s.Length && (_s[_pos] == '+' || _s[_pos] == '-'))
                    {
                        if (_s[_pos] == '-') sign = -1;
                        _pos++;
                    }
                    if (_pos >= _s.Length || _s[_pos] < '0' || _s[_pos] > '9') throw Error("invalid number");
                    int e = 0;
                    while (_pos < _s.Length && _s[_pos] >= '0' && _s[_pos] <= '9')
                    {
                        if (e < 100000) e = e * 10 + (_s[_pos] - '0');
                        _pos++;
                    }
                    exp10 += sign * e;
                }
                if (exact && mant <= (1UL << 53) && exp10 >= -22 && exp10 <= 22)
                {
                    // Clinger's fast path: both operands are exact doubles, so one rounding step.
                    double v = mant;
                    if (exp10 < 0) v /= Pow10[-exp10];
                    else if (exp10 > 0) v *= Pow10[exp10];
                    return neg ? -v : v;
                }
                return double.Parse(_s.Substring(start, _pos - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            private static void Accumulate(ref ulong mant, ref int exp10, ref bool exact, int digit, bool fraction)
            {
                if (fraction) exp10--;
                if (!exact)
                {
                    if (!fraction) exp10++; // integer digit dropped from the mantissa
                    return;
                }
                if (mant > 100000000000000000UL) // 1e17: another digit could overflow 2^64
                {
                    exact = false;
                    if (!fraction) exp10++;
                    return;
                }
                mant = mant * 10 + (ulong)digit;
            }
        }
    }
}
