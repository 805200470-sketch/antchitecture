using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AntGarden.EditorTools
{
    /// <summary>Tiny JSON reader for the asset manifests: objects → Dictionary, arrays → List, numbers → double.</summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            int i = 0;
            object v = Value(json, ref i);
            return v;
        }

        static void Ws(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end of JSON");
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>();
                i++;
                Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i);
                    string key = Str(s, ref i);
                    Ws(s, ref i);
                    i++; // ':'
                    d[key] = Value(s, ref i);
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; // '}'
                    return d;
                }
            }
            if (c == '[')
            {
                var l = new List<object>();
                i++;
                Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i));
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; // ']'
                    return l;
                }
            }
            if (c == '"') return Str(s, ref i);
            if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
            if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
            if (s.Substring(i).StartsWith("null")) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    char e = s[i];
                    if (e == 'n') sb.Append('\n');
                    else if (e == 't') sb.Append('\t');
                    else if (e == 'u') { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    else sb.Append(e);
                }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }

        // ---- convenience accessors
        public static Dictionary<string, object> Obj(object o, string key) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;
        public static List<object> Arr(object o, string key) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(key, out var v) ? v as List<object> : null;
        public static double Num(object o, string key, double def = 0) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(key, out var v) && v is double d ? d : def;
        public static string Txt(object o, string key, string def = null) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(key, out var v) && v is string s ? s : def;
        public static bool Has(object o, string key) => o is Dictionary<string, object> d && d.ContainsKey(key);
    }
}
