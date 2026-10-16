// Ant Garden core — the Path Recognize network (schemaVersion 2.x) as the canyon generator reads it,
// plus src/pathfield.js validateNetwork().
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AntGarden.Core
{
    /// <summary>Thrown when JSON is not a usable Path Recognize network (same messages as the web prototype).</summary>
    public sealed class InvalidNetworkException : Exception
    {
        /// <summary>Creates the exception with a readable (Chinese, as in the prototype) message.</summary>
        public InvalidNetworkException(string message) : base(message)
        {
        }
    }

    /// <summary>A recognized node in image pixel coordinates (x right, y down).</summary>
    public sealed class NetworkNode
    {
        /// <summary>Node id ("n0", …); null when missing.</summary>
        public string Id;
        /// <summary>"junction" | "endpoint" | …; null when missing.</summary>
        public string Type;
        /// <summary>Pixel x.</summary>
        public double X = double.NaN;
        /// <summary>Pixel y (down).</summary>
        public double Y = double.NaN;
        /// <summary>True when the node touches the image border.</summary>
        public bool Boundary;
    }

    /// <summary>A recognized path (polyline in image pixels) between two nodes.</summary>
    public sealed class NetworkEdge
    {
        /// <summary>Edge id ("e0", …); null when missing.</summary>
        public string Id;
        /// <summary>Source node id.</summary>
        public string Source;
        /// <summary>Target node id.</summary>
        public string Target;
        /// <summary>Polyline points as [x, y] in pixels; null when the JSON has no points array.</summary>
        public List<double[]> Points;
        /// <summary>Mean recognized width in pixels (JS number coercion; NaN when missing).</summary>
        public double MeanWidthPx = double.NaN;
        /// <summary>True when meanWidthPx is a JSON number (only those count toward the median width).</summary>
        public bool MeanWidthIsNumber;
        /// <summary>Fraction of the path that was inferred (NaN when missing).</summary>
        public double InferredFraction = double.NaN;
    }

    /// <summary>
    /// A Path Recognize network JSON (schemaVersion 2.x), reduced to what the canyon generator reads:
    /// image size and name, nodes, and edges with points and meanWidthPx.
    /// </summary>
    public sealed class NetworkData
    {
        /// <summary>image.name ("path-013.png"); "" when missing.</summary>
        public string Name = "";
        /// <summary>schemaVersion as JavaScript String() would print it; null when missing.</summary>
        public string SchemaVersion;
        /// <summary>image.width in pixels (NaN when missing).</summary>
        public double ImageWidth = double.NaN;
        /// <summary>image.height in pixels (NaN when missing).</summary>
        public double ImageHeight = double.NaN;
        /// <summary>Nodes; null when the JSON has no nodes array.</summary>
        public List<NetworkNode> Nodes;
        /// <summary>Edges; null when the JSON has no edges array.</summary>
        public List<NetworkEdge> Edges;

        private bool _rootNotObject;

        /// <summary>Parses network JSON text. Structural problems are reported by <see cref="Validate"/>.</summary>
        public static NetworkData Parse(string json)
        {
            return FromJson(MiniJson.Parse(json));
        }

        /// <summary>Builds the network from an already parsed <see cref="MiniJson"/> tree.</summary>
        public static NetworkData FromJson(object root)
        {
            var data = new NetworkData();
            var o = root as Dictionary<string, object>;
            if (o == null)
            {
                // JS: typeof [] === 'object', so an array root passes the first check and fails on schemaVersion.
                data._rootNotObject = !(root is List<object>);
                return data;
            }
            object v;
            if (o.TryGetValue("schemaVersion", out v) && v != null) data.SchemaVersion = JsString(v);
            if (o.TryGetValue("image", out v) && v is Dictionary<string, object> image)
            {
                object w;
                data.ImageWidth = image.TryGetValue("width", out w) ? JsNumber(w) : double.NaN;
                data.ImageHeight = image.TryGetValue("height", out w) ? JsNumber(w) : double.NaN;
                data.Name = image.TryGetValue("name", out w) && w != null ? JsString(w) : "";
            }
            if (o.TryGetValue("nodes", out v) && v is List<object> nodes)
            {
                data.Nodes = new List<NetworkNode>(nodes.Count);
                foreach (object item in nodes) data.Nodes.Add(ReadNode(item));
            }
            if (o.TryGetValue("edges", out v) && v is List<object> edges)
            {
                data.Edges = new List<NetworkEdge>(edges.Count);
                foreach (object item in edges) data.Edges.Add(ReadEdge(item));
            }
            return data;
        }

        /// <summary>
        /// Throws <see cref="InvalidNetworkException"/> when this is not a usable network
        /// (pathfield.js validateNetwork; the PathField constructor calls it).
        /// </summary>
        public void Validate()
        {
            if (_rootNotObject) throw new InvalidNetworkException("不是 JSON 对象");
            string v = SchemaVersion ?? "";
            if (!v.StartsWith("2.", StringComparison.Ordinal))
                throw new InvalidNetworkException("不支持的 schemaVersion「" + (v.Length > 0 ? v : "缺失") + "」，需要 Path Recognize 导出的 2.x network");
            if (!(ImageWidth > 0 && ImageHeight > 0)) throw new InvalidNetworkException("缺少 image.width / image.height");
            if (Nodes == null || Edges == null) throw new InvalidNetworkException("缺少 nodes 或 edges 数组");
            if (Edges.Count == 0) throw new InvalidNetworkException("这张图没有识别出路径（edges 为空）");
        }

        private static NetworkNode ReadNode(object item)
        {
            var o = item as Dictionary<string, object>;
            if (o == null) throw new InvalidNetworkException("nodes 里有不是对象的条目");
            var n = new NetworkNode();
            object v;
            n.Id = o.TryGetValue("id", out v) ? JsStringOrNull(v) : null;
            n.Type = o.TryGetValue("type", out v) ? JsStringOrNull(v) : null;
            n.X = o.TryGetValue("x", out v) ? JsNumber(v) : double.NaN;
            n.Y = o.TryGetValue("y", out v) ? JsNumber(v) : double.NaN;
            n.Boundary = o.TryGetValue("boundary", out v) && JsTruthy(v);
            return n;
        }

        private static NetworkEdge ReadEdge(object item)
        {
            var o = item as Dictionary<string, object>;
            if (o == null) throw new InvalidNetworkException("edges 里有不是对象的条目");
            var e = new NetworkEdge();
            object v;
            e.Id = o.TryGetValue("id", out v) ? JsStringOrNull(v) : null;
            e.Source = o.TryGetValue("source", out v) ? JsStringOrNull(v) : null;
            e.Target = o.TryGetValue("target", out v) ? JsStringOrNull(v) : null;
            if (o.TryGetValue("meanWidthPx", out v))
            {
                e.MeanWidthPx = JsNumber(v);
                e.MeanWidthIsNumber = v is double;
            }
            if (o.TryGetValue("inferredFraction", out v)) e.InferredFraction = v == null ? 0 : JsNumber(v); // (x ?? 0)
            if (o.TryGetValue("points", out v) && v != null)
            {
                var list = v as List<object>;
                if (list == null) throw new InvalidNetworkException("edge.points 不是数组");
                e.Points = new List<double[]>(list.Count);
                foreach (object p in list)
                {
                    var xy = p as List<object>;
                    if (xy == null) throw new InvalidNetworkException("edge.points 的点不是 [x, y] 数组");
                    e.Points.Add(new[] { xy.Count > 0 ? JsNumber(xy[0]) : double.NaN, xy.Count > 1 ? JsNumber(xy[1]) : double.NaN });
                }
            }
            return e;
        }

        // ---- JavaScript conversions for values read from JSON -------------------------------------

        /// <summary>JS ToNumber for a JSON value: null → 0, bool → 0/1, numeric strings parsed, otherwise NaN.</summary>
        internal static double JsNumber(object v)
        {
            if (v == null) return 0;
            if (v is double d) return d;
            if (v is bool b) return b ? 1 : 0;
            if (v is string s)
            {
                s = s.Trim();
                if (s.Length == 0) return 0;
                double r;
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out r)) return r;
                if (s == "Infinity" || s == "+Infinity") return double.PositiveInfinity;
                if (s == "-Infinity") return double.NegativeInfinity;
                return double.NaN;
            }
            return double.NaN;
        }

        /// <summary>JS truthiness of a JSON value.</summary>
        internal static bool JsTruthy(object v)
        {
            if (v == null) return false;
            if (v is bool b) return b;
            if (v is double d) return JsMath.Truthy(d);
            if (v is string s) return s.Length > 0;
            return true;
        }

        private static string JsStringOrNull(object v)
        {
            return v == null ? null : JsString(v);
        }

        /// <summary>JS String(value) for a JSON value.</summary>
        internal static string JsString(object v)
        {
            if (v == null) return "null";
            if (v is string s) return s;
            if (v is double d) return JsMath.NumberToString(d);
            if (v is bool b) return b ? "true" : "false";
            if (v is List<object> list)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < list.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    if (list[i] != null) sb.Append(JsString(list[i]));
                }
                return sb.ToString();
            }
            return "[object Object]";
        }
    }
}
