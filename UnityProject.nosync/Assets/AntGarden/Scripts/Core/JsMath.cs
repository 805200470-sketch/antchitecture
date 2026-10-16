// Ant Garden core — port of the web prototype (Ant_Garden_Demo/src) to plain C#.
// JavaScript number semantics that the rest of the port relies on to match the prototype bit for bit.
// Pure C#: no UnityEngine, C# 9, double precision throughout.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace AntGarden.Core
{
    /// <summary>
    /// Math helpers with JavaScript (V8) semantics. <see cref="Clamp"/>, <see cref="Lerp"/> and
    /// <see cref="Smoothstep"/> are the helpers exported by noise.js; the rest reproduce the exact
    /// behaviour of <c>Math.hypot</c>, <c>Math.round</c>, <c>Math.max</c>, <c>x | 0</c> and
    /// <c>x &gt;&gt;&gt; 0</c>, which differ from their obvious C# counterparts.
    /// </summary>
    public static class JsMath
    {
        /// <summary>noise.js <c>clamp</c>: <c>v &lt; a ? a : v &gt; b ? b : v</c> (NaN passes through).</summary>
        public static double Clamp(double v, double a, double b)
        {
            return v < a ? a : v > b ? b : v;
        }

        /// <summary>noise.js <c>lerp</c>: <c>a + (b - a) * t</c>.</summary>
        public static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        /// <summary>noise.js <c>smoothstep</c>: cubic Hermite step of <paramref name="v"/> between a and b.</summary>
        public static double Smoothstep(double a, double b, double v)
        {
            double t = Clamp((v - a) / (b - a), 0, 1);
            return t * t * (3 - 2 * t);
        }

        /// <summary>
        /// V8's <c>Math.hypot(a, b)</c>. V8 normalises by the larger magnitude before summing
        /// (<c>sqrt((a/m)² + (b/m)²) · m</c>), which differs from <c>sqrt(a*a + b*b)</c> in the last bit
        /// for roughly a third of all inputs, so every hypot in the port goes through here.
        /// </summary>
        public static double Hypot(double a, double b)
        {
            double x = Math.Abs(a);
            double y = Math.Abs(b);
            bool anyNaN = double.IsNaN(x) || double.IsNaN(y);
            double max = 0;
            if (!double.IsNaN(x) && x > max) max = x;
            if (!double.IsNaN(y) && y > max) max = y;
            if (double.IsPositiveInfinity(max)) return double.PositiveInfinity;
            if (anyNaN) return double.NaN;
            if (max == 0) return 0;
            double nx = x / max;
            double ny = y / max;
            // V8 runs a Kahan-compensated sum over the normalised squares; with two terms the
            // compensation is never applied, which leaves exactly this expression.
            return Math.Sqrt(nx * nx + ny * ny) * max;
        }

        /// <summary>
        /// JavaScript <c>Math.sin</c> (V8's fdlibm, see Fdlibm.cs): the same bits on every platform.
        /// Arguments beyond ±2^19·π (≈ 1.6e6) fall back to <see cref="Math.Sin"/>.
        /// </summary>
        public static double Sin(double x)
        {
            return Fdlibm.Sin(x);
        }

        /// <summary>JavaScript <c>Math.cos</c> (V8's fdlibm). Arguments beyond ±2^19·π (≈ 1.6e6) fall back to <see cref="Math.Cos"/>.</summary>
        public static double Cos(double x)
        {
            return Fdlibm.Cos(x);
        }

        /// <summary>JavaScript <c>Math.atan</c> (V8's fdlibm).</summary>
        public static double Atan(double x)
        {
            return Fdlibm.Atan(x);
        }

        /// <summary>JavaScript <c>Math.atan2</c> (V8's fdlibm).</summary>
        public static double Atan2(double y, double x)
        {
            return Fdlibm.Atan2(y, x);
        }

        /// <summary>JavaScript <c>Math.log</c> (V8's fdlibm).</summary>
        public static double Log(double x)
        {
            return Fdlibm.Log(x);
        }

        /// <summary>JavaScript <c>Math.round</c>: halves round toward +∞ (C# rounds them to even).</summary>
        public static double Round(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return x;
            double f = Math.Floor(x);
            return x - f >= 0.5 ? f + 1 : f; // x - floor(x) is exact for doubles
        }

        /// <summary>JavaScript <c>Math.max(a, b)</c>: NaN if either is NaN, +0 beats -0.</summary>
        public static double Max(double a, double b)
        {
            if (double.IsNaN(a) || double.IsNaN(b)) return double.NaN;
            if (a > b) return a;
            if (b > a) return b;
            return IsNegativeZero(a) ? b : a;
        }

        /// <summary>JavaScript <c>Math.min(a, b)</c>: NaN if either is NaN, -0 beats +0.</summary>
        public static double Min(double a, double b)
        {
            if (double.IsNaN(a) || double.IsNaN(b)) return double.NaN;
            if (a < b) return a;
            if (b < a) return b;
            return IsNegativeZero(a) ? a : b;
        }

        /// <summary>JavaScript <c>Math.max(...list)</c>: -∞ for an empty list, NaN if any element is NaN.</summary>
        public static double MaxOf(IList<double> list)
        {
            double m = double.NegativeInfinity;
            bool nan = false;
            for (int i = 0; i < list.Count; i++)
            {
                double v = list[i];
                if (double.IsNaN(v)) nan = true;
                else m = Max(m, v);
            }
            return nan ? double.NaN : m;
        }

        /// <summary>JavaScript <c>Math.min(...list)</c>: +∞ for an empty list, NaN if any element is NaN.</summary>
        public static double MinOf(IList<double> list)
        {
            double m = double.PositiveInfinity;
            bool nan = false;
            for (int i = 0; i < list.Count; i++)
            {
                double v = list[i];
                if (double.IsNaN(v)) nan = true;
                else m = Min(m, v);
            }
            return nan ? double.NaN : m;
        }

        /// <summary>JavaScript <c>ToInt32</c> (<c>x | 0</c>): truncates, wraps modulo 2³², NaN/±∞ → 0.</summary>
        public static int ToInt32(double v)
        {
            if (v >= -2147483648.0 && v <= 2147483647.0) return (int)v; // false for NaN
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
            double m = Math.Truncate(v) % 4294967296.0; // exact; keeps the dividend's sign
            if (m < 0) m += 4294967296.0;
            return unchecked((int)(uint)m);
        }

        /// <summary>JavaScript <c>ToUint32</c> (<c>x &gt;&gt;&gt; 0</c>).</summary>
        public static uint ToUint32(double v)
        {
            return unchecked((uint)ToInt32(v));
        }

        /// <summary>JavaScript <c>Math.imul</c>: low 32 bits of the product, as a signed int.</summary>
        public static int Imul(int a, int b)
        {
            return unchecked((int)((uint)a * (uint)b));
        }

        /// <summary>True for numbers that are neither NaN nor ±∞ (<c>Number.isFinite</c> on a number).</summary>
        public static bool IsFinite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }

        /// <summary>JavaScript truthiness of a number: false for 0, -0 and NaN.</summary>
        public static bool Truthy(double v)
        {
            return v != 0 && !double.IsNaN(v);
        }

        /// <summary>JavaScript <c>x || 1</c> for a number: 1 when x is 0, -0 or NaN.</summary>
        public static double OrOne(double v)
        {
            return Truthy(v) ? v : 1;
        }

        /// <summary>True for -0.0.</summary>
        public static bool IsNegativeZero(double v)
        {
            return v == 0 && BitConverter.DoubleToInt64Bits(v) < 0;
        }

        /// <summary>
        /// JavaScript <c>Number.prototype.toString()</c> for the common cases (integers, plain decimals,
        /// exponent form). Used where the prototype turns numbers into strings (ids, schemaVersion).
        /// </summary>
        public static string NumberToString(double v)
        {
            if (double.IsNaN(v)) return "NaN";
            if (double.IsPositiveInfinity(v)) return "Infinity";
            if (double.IsNegativeInfinity(v)) return "-Infinity";
            if (v == 0) return "0";
            if (Math.Floor(v) == v && Math.Abs(v) < 1e21) return v.ToString("F0", CultureInfo.InvariantCulture);
            string r = v.ToString("R", CultureInfo.InvariantCulture);
            int e = r.IndexOf('E');
            if (e < 0) return r;
            string mant = r.Substring(0, e);
            string exp = r.Substring(e + 1);
            char sign = '+';
            if (exp.Length > 0 && (exp[0] == '+' || exp[0] == '-'))
            {
                sign = exp[0];
                exp = exp.Substring(1);
            }
            exp = exp.TrimStart('0');
            if (exp.Length == 0) exp = "0";
            return mant + "e" + sign + exp;
        }

        /// <summary>JavaScript <c>Array.prototype.slice(start)</c> (negative start counts from the end).</summary>
        public static List<T> Slice<T>(List<T> list, int start)
        {
            return Slice(list, start, list.Count);
        }

        /// <summary>JavaScript <c>Array.prototype.slice(start, end)</c> with its clamping rules.</summary>
        public static List<T> Slice<T>(List<T> list, int start, int end)
        {
            int len = list.Count;
            int from = start < 0 ? Math.Max(len + start, 0) : Math.Min(start, len);
            int to = end < 0 ? Math.Max(len + end, 0) : Math.Min(end, len);
            int n = Math.Max(to - from, 0);
            return list.GetRange(from, n);
        }

        /// <summary>A reversed copy (<c>[...list].reverse()</c>).</summary>
        public static List<T> Reversed<T>(List<T> list)
        {
            var copy = new List<T>(list);
            copy.Reverse();
            return copy;
        }
    }
}
