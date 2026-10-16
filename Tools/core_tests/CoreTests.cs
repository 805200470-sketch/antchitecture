// Golden test for the C# port of the Ant Garden path → canyon core.
// Compiled together with UnityProject.nosync/Assets/AntGarden/Scripts/Core/*.cs by run_tests.sh and
// compared against golden.json (written by dump_golden.mjs from the JavaScript prototype).
//
//   CoreTests --golden golden.json --networks <dir> [--timing N] [--label ".NET 6"]
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using AntGarden.Core;

namespace AntGarden.CoreTests
{
    /// <summary>Running maximum deviation for one metric.</summary>
    internal sealed class Stat
    {
        public string Name;
        public double Tol;
        public double Max;
        public string Where = "";
        public long Count;
        public long Fails;
        public long NotBitExact;
    }

    /// <summary>Collects exact and tolerance checks for one test case.</summary>
    internal sealed class Checker
    {
        public readonly string Case;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Info = new List<string>();
        public readonly List<Stat> Stats = new List<Stat>();
        private readonly Dictionary<string, Stat> _byName = new Dictionary<string, Stat>();
        public int ExactChecks;
        public int ExactFails;

        public Checker(string name)
        {
            Case = name;
        }

        public bool Passed => ExactFails == 0 && Errors.Count == 0 && Stats.TrueForAll(s => s.Fails == 0);

        public void Fail(string message)
        {
            if (Errors.Count < 40) Errors.Add(message);
            else if (Errors.Count == 40) Errors.Add("… more failures suppressed");
        }

        public bool Exact<T>(string what, T expected, T actual)
        {
            ExactChecks++;
            if (EqualityComparer<T>.Default.Equals(expected, actual)) return true;
            ExactFails++;
            Fail($"{what}: expected {Show(expected)}, got {Show(actual)}");
            return false;
        }

        private static string Show(object o)
        {
            if (o == null) return "null";
            if (o is string s) return "\"" + s + "\"";
            if (o is double d) return d.ToString("R", CultureInfo.InvariantCulture);
            return o.ToString();
        }

        public void Near(string metric, double tol, double expected, double actual, string where)
        {
            if (!_byName.TryGetValue(metric, out Stat st))
            {
                st = new Stat { Name = metric, Tol = tol };
                _byName[metric] = st;
                Stats.Add(st);
            }
            st.Count++;
            double dev;
            bool same = expected == actual || (double.IsNaN(expected) && double.IsNaN(actual));
            if (same) dev = 0;
            else if (double.IsNaN(expected) || double.IsNaN(actual) || double.IsInfinity(expected) || double.IsInfinity(actual)) dev = double.PositiveInfinity;
            else dev = Math.Abs(expected - actual);
            if (!same) st.NotBitExact++;
            if (dev > st.Max || (st.Max == 0 && dev > 0))
            {
                st.Max = dev;
                st.Where = where + $" (js {G(expected)}, c# {G(actual)})";
            }
            if (!(dev <= tol))
            {
                st.Fails++;
                if (st.Fails <= 3) Fail($"{metric} @ {where}: js {G(expected)} vs c# {G(actual)} (|Δ| {dev:G3} > {tol:G3})");
            }
        }

        public static string G(double v)
        {
            return v.ToString("G17", CultureInfo.InvariantCulture);
        }
    }

    internal static class Program
    {
        // Tolerances. Doubles computed the same way should agree exactly; the margin only absorbs
        // last-bit differences between V8's and the platform's libm (sin/cos/atan2/pow).
        private const double TolDouble = 1e-6;
        // Values interpolated from the float32 grids (pathDist, dist, centerDist, halfWidth, height and
        // the normals/gradients derived from them). A different float32 rounding of one grid sample
        // moves them by up to one float32 ulp of the sample (≈ 1.5e-5 at |d| ≈ 200).
        private const double TolGrid = 1e-4;
        private const double TolNoise = 1e-12;

        private static string _networks;

        private static int Main(string[] args)
        {
            string golden = null;
            int timing = 3;
            string label = "C#";
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--golden": golden = args[++i]; break;
                    case "--networks": _networks = args[++i]; break;
                    case "--timing": timing = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--label": label = args[++i]; break;
                }
            }
            if (golden == null || _networks == null)
            {
                Console.Error.WriteLine("usage: CoreTests --golden golden.json --networks <dir> [--timing N] [--label name]");
                return 2;
            }
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.WriteLine($"=== Ant Garden core port — golden test ({label}, {Environment.Version}) ===");
            var sw = Stopwatch.StartNew();
            var g = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(golden));
            Console.WriteLine($"golden: {golden} ({sw.ElapsedMilliseconds} ms to parse), generated {g["generatedAt"]} by node {g["node"]}");

            var checkers = new List<Checker>();
            checkers.Add(CheckConfig((Dictionary<string, object>)g["config"]));
            checkers.Add(CheckNoise(g));
            checkers.Add(CheckLibm(g));
            checkers.Add(CheckValidation(g));
            foreach (object co in (List<object>)g["cases"])
            {
                var c = (Dictionary<string, object>)co;
                checkers.Add(CheckCase(c));
            }
            checkers.Add(CheckGridBuilders());

            Console.WriteLine();
            int passed = 0;
            foreach (Checker ck in checkers)
            {
                Report(ck);
                if (ck.Passed) passed++;
            }

            if (timing > 0) Timings(g, timing);

            Console.WriteLine();
            Console.WriteLine($"SUMMARY ({label}): {passed}/{checkers.Count} groups PASS — {(passed == checkers.Count ? "ALL PASS" : "FAILURES")}");
            return passed == checkers.Count ? 0 : 1;
        }

        // ---- reporting ----------------------------------------------------------------------------

        private static void Report(Checker ck)
        {
            Console.WriteLine($"[{(ck.Passed ? "PASS" : "FAIL")}] {ck.Case}  ({ck.ExactChecks} exact checks, {ck.ExactFails} failed)");
            foreach (Stat s in ck.Stats)
            {
                string exact = s.NotBitExact == 0 ? "bit-exact" : $"{s.NotBitExact}/{s.Count} not bit-exact";
                string worst = s.Max > 0 ? $"  worst at {s.Where}" : "";
                Console.WriteLine($"    {s.Name,-22} n={s.Count,6}  max|Δ|={s.Max,-10:G3} tol={s.Tol:G1}  {exact}{(s.Fails > 0 ? $"  FAILS={s.Fails}" : "")}{worst}");
            }
            foreach (string i in ck.Info) Console.WriteLine("    " + i);
            foreach (string e in ck.Errors) Console.WriteLine("    ! " + e);
        }

        // ---- helpers ------------------------------------------------------------------------------

        private static double Num(object o)
        {
            if (o is double d) return d;
            if (o is string s)
            {
                if (s == "NaN") return double.NaN;
                if (s == "Infinity") return double.PositiveInfinity;
                if (s == "-Infinity") return double.NegativeInfinity;
                if (s == "-0") return -0.0;
            }
            if (o == null) return double.NaN;
            throw new InvalidDataException("not a number: " + o);
        }

        private static List<object> L(object o) => (List<object>)o;
        private static Dictionary<string, object> O(object o) => (Dictionary<string, object>)o;
        private static string S(object o) => o as string;
        private static bool B(object o) => o is bool b && b;
        private static int I(object o) => (int)Num(o);

        private static NetworkData LoadNet(string id)
        {
            return NetworkData.Parse(File.ReadAllText(Path.Combine(_networks, id + ".json")));
        }

        private static uint Fnv(byte[] bytes)
        {
            uint h = 0x811c9dc5;
            unchecked
            {
                foreach (byte b in bytes)
                {
                    h ^= b;
                    h *= 0x01000193;
                }
            }
            return h;
        }

        private static uint HashF32(float[] a)
        {
            var bytes = new byte[a.Length * 4];
            Buffer.BlockCopy(a, 0, bytes, 0, bytes.Length);
            return Fnv(bytes);
        }

        private static uint HashF64(List<double> a)
        {
            double[] arr = a.ToArray();
            var bytes = new byte[arr.Length * 8];
            Buffer.BlockCopy(arr, 0, bytes, 0, bytes.Length);
            return Fnv(bytes);
        }

        private static double Sum(float[] a)
        {
            double s = 0;
            for (int i = 0; i < a.Length; i++) s += a[i];
            return s;
        }

        // ---- config -------------------------------------------------------------------------------

        private static Checker CheckConfig(Dictionary<string, object> js)
        {
            var ck = new Checker("PathConfig.Default() == config.js");
            PathConfig c = PathConfig.Default();
            var path = O(js["path"]);
            var frame = O(js["frame"]);
            var ground = O(js["ground"]);
            var stitch = O(js["stitch"]);
            ck.Exact("seed", Num(js["seed"]), (double)c.Seed);
            void N(string what, object v, double actual) => ck.Exact(what, Num(v), actual);
            N("path.worldSize", path["worldSize"], c.WorldSize);
            N("path.pxToWorld", path["pxToWorld"], c.PxToWorld);
            ck.Exact("path.widthMode", S(path["widthMode"]), c.WidthMode);
            N("path.lineDrawingMaxWidth", path["lineDrawingMaxWidth"], c.LineDrawingMaxWidth);
            N("path.measuredInfluence", path["measuredInfluence"], c.MeasuredInfluence);
            N("path.fixedWidth", path["fixedWidth"], c.FixedWidth);
            N("path.edgeWidthRange[0]", L(path["edgeWidthRange"])[0], c.EdgeWidthRange[0]);
            N("path.edgeWidthRange[1]", L(path["edgeWidthRange"])[1], c.EdgeWidthRange[1]);
            ck.Exact("path.edgeWidthRange.length", L(path["edgeWidthRange"]).Count, c.EdgeWidthRange.Length);
            N("path.widthVariation", path["widthVariation"], c.WidthVariation);
            N("path.widthFrequency", path["widthFrequency"], c.WidthFrequency);
            N("path.pinch", path["pinch"], c.Pinch);
            N("path.minHalfWidth", path["minHalfWidth"], c.MinHalfWidth);
            N("path.maxHalfWidth", path["maxHalfWidth"], c.MaxHalfWidth);
            N("path.chamberNoise", path["chamberNoise"], c.ChamberNoise);
            N("path.chamberMin", path["chamberMin"], c.ChamberMin);
            N("path.chamberMax", path["chamberMax"], c.ChamberMax);
            N("path.pondChance", path["pondChance"], c.PondChance);
            N("path.widthScale", path["widthScale"], c.WidthScale);
            N("path.minWidth", path["minWidth"], c.MinWidth);
            N("path.maxWidth", path["maxWidth"], c.MaxWidth);
            N("path.junctionClearing", path["junctionClearing"], c.JunctionClearing);
            N("path.endpointClearing", path["endpointClearing"], c.EndpointClearing);
            N("path.resampleStep", path["resampleStep"], c.ResampleStep);
            ck.Exact("path.defaultSample", S(path["defaultSample"]), c.DefaultSample);
            N("field.cell", O(js["field"])["cell"], c.FieldCell);
            N("frame.fill", frame["fill"], c.FrameFill);
            N("frame.borderMargin", frame["borderMargin"], c.FrameBorderMargin);
            N("frame.innerPad", frame["innerPad"], c.FrameInnerPad);
            N("frame.wallMargin", frame["wallMargin"], c.FrameWallMargin);
            N("frame.sideDepth", frame["sideDepth"], c.FrameSideDepth);
            N("frame.exitRadius", frame["exitRadius"], c.FrameExitRadius);
            ck.Exact("frame.forceR (unset)", frame["forceR"] == null, !c.FrameForceR.HasValue);
            N("ground.undulation", ground["undulation"], c.GroundUndulation);
            N("ground.bankHeight", ground["bankHeight"], c.GroundBankHeight);
            N("ground.bankWidth", ground["bankWidth"], c.GroundBankWidth);
            N("ground.bankLumps", ground["bankLumps"], c.GroundBankLumps);
            N("ground.plateauNoise", ground["plateauNoise"], c.GroundPlateauNoise);
            N("stitch.portInset", stitch["portInset"], c.StitchPortInset);
            N("stitch.linkBulge", stitch["linkBulge"], c.StitchLinkBulge);
            PathConfig k = c.Clone();
            k.EdgeWidthRange[0] = 99;
            ck.Exact("Clone() is deep", 0.8, c.EdgeWidthRange[0]);
            return ck;
        }

        // ---- RNG / noise / helpers ------------------------------------------------------------------

        private static Checker CheckNoise(Dictionary<string, object> g)
        {
            var ck = new Checker("Rng / Noise / ihash / cellular2 / JS math helpers");
            foreach (object ro in L(g["rng"]))
            {
                var r = O(ro);
                double seed = Num(r["seed"]);
                var rng = new Rng(seed);
                string at = "seed " + Checker.G(seed);
                var next = L(r["next"]);
                for (int k = 0; k < next.Count; k++) ck.Near("rng.next", 0, Num(next[k]), rng.Next(), at + $" #{k}");
                ck.Near("rng.float", 0, Num(r["float"]), rng.Float(-3, 7), at);
                ck.Near("rng.float", 0, Num(r["floatDefault"]), rng.Float(), at);
                ck.Exact("rng.int " + at, I(r["int"]), rng.Int(2, 9));
                ck.Exact("rng.pick " + at, I(r["pick"]), rng.Pick(new[] { 10, 20, 30, 40, 50 }));
                ck.Exact("rng.chance " + at, B(r["chance"]), rng.Chance(0.5));
                ck.Near("rng.gauss", TolNoise, Num(r["gauss"]), rng.Gauss(), at);
                ck.Near("rng.next", 0, Num(r["after"]), rng.Next(), at + " after");
            }
            var nzA = new Noise(20261002 + 11);
            var nzB = new Noise(7);
            var noise = O(g["noise"]);
            foreach (object po in L(noise["noise2"]))
            {
                var p = L(po);
                double x = Num(p[0]), y = Num(p[1]);
                ck.Near("noise2", TolNoise, Num(p[2]), nzA.Noise2(x, y), $"({x}, {y})");
                ck.Near("noise2", TolNoise, Num(p[3]), nzB.Noise2(x, y), $"seed 7 ({x}, {y})");
            }
            foreach (object po in L(noise["noise3"]))
            {
                var p = L(po);
                double x = Num(p[0]), y = Num(p[1]), z = Num(p[2]);
                ck.Near("noise3", TolNoise, Num(p[3]), nzA.Noise3(x, y, z), $"({x}, {y}, {z})");
                ck.Near("noise3", TolNoise, Num(p[4]), nzB.Noise3(x, y, z), $"seed 7 ({x}, {y}, {z})");
            }
            foreach (object po in L(noise["fbm2"]))
            {
                var p = L(po);
                double x = Num(p[0]), y = Num(p[1]);
                ck.Near("fbm2", TolNoise, Num(p[2]), nzA.Fbm2(x, y), $"({x}, {y})");
                ck.Near("fbm2", TolNoise, Num(p[3]), nzA.Fbm2(x * 0.06, y * 0.06, 3), $"({x}, {y}) 3 oct");
                ck.Near("fbm2", TolNoise, Num(p[4]), nzA.Fbm2(x, y, 5, 2.1, 0.45), $"({x}, {y}) 5 oct");
            }
            foreach (object po in L(noise["fbm3"]))
            {
                var p = L(po);
                double x = Num(p[0]), y = Num(p[1]), z = Num(p[2]);
                ck.Near("fbm3", TolNoise, Num(p[3]), nzA.Fbm3(x, y, z), $"({x}, {y}, {z})");
                ck.Near("fbm3", TolNoise, Num(p[4]), nzA.Fbm3(x, y, z, 2, 1.9, 0.6), $"({x}, {y}, {z}) custom");
            }
            foreach (object po in L(g["ihash"]))
            {
                var p = L(po);
                ck.Near("ihash", 0, Num(p[4]), Noise.IHash(Num(p[0]), Num(p[1]), Num(p[2]), Num(p[3])), $"({p[0]}, {p[1]}, {p[2]}, {p[3]})");
            }
            foreach (object po in L(g["cellular2"]))
            {
                var p = L(po);
                CellularResult r = Noise.Cellular2(Num(p[0]), Num(p[1]), Num(p[2]));
                string at = $"({p[0]}, {p[1]}) seed {p[2]}";
                ck.Near("cellular2", TolNoise, Num(p[3]), r.F1, at + " f1");
                ck.Near("cellular2", TolNoise, Num(p[4]), r.F2, at + " f2");
                ck.Near("cellular2", TolNoise, Num(p[5]), r.R1, at + " r1");
                ck.Near("cellular2", TolNoise, Num(p[6]), r.R2, at + " r2");
            }
            var h = O(g["helpers"]);
            foreach (object po in L(h["hypot"]))
            {
                var p = L(po);
                ck.Near("JsMath.Hypot", 0, Num(p[2]), JsMath.Hypot(Num(p[0]), Num(p[1])), $"({p[0]}, {p[1]})");
            }
            foreach (object po in L(h["round"]))
            {
                var p = L(po);
                ck.Near("JsMath.Round", 0, Num(p[1]), JsMath.Round(Num(p[0])), Checker.G(Num(p[0])));
            }
            foreach (object po in L(h["clamp"]))
            {
                var p = L(po);
                ck.Near("JsMath.Clamp", 0, Num(p[3]), JsMath.Clamp(Num(p[0]), Num(p[1]), Num(p[2])), "");
            }
            foreach (object po in L(h["lerp"]))
            {
                var p = L(po);
                ck.Near("JsMath.Lerp", 0, Num(p[3]), JsMath.Lerp(Num(p[0]), Num(p[1]), Num(p[2])), "");
            }
            foreach (object po in L(h["smoothstep"]))
            {
                var p = L(po);
                ck.Near("JsMath.Smoothstep", 0, Num(p[3]), JsMath.Smoothstep(Num(p[0]), Num(p[1]), Num(p[2])), "");
            }
            return ck;
        }

        private static bool Same(double a, double b)
        {
            return a == b || (double.IsNaN(a) && double.IsNaN(b));
        }

        /// <summary>
        /// The port's fdlibm must equal the fdlibm reference bit for bit; agreement with V8 itself
        /// (and, for comparison, with this runtime's System.Math) is reported alongside.
        /// </summary>
        private static Checker CheckLibm(Dictionary<string, object> g)
        {
            var ck = new Checker("JsMath sin/cos/atan2/log == fdlibm reference (V8's library), bit for bit");
            var libm = O(g["libm"]);
            void Group(string name, List<object> rows, int nArgs, Func<double[], double> port, Func<double[], double> sys)
            {
                int portV8 = 0, sysV8 = 0;
                foreach (object ro in rows)
                {
                    var r = L(ro);
                    var args = new double[nArgs];
                    for (int i = 0; i < nArgs; i++) args[i] = Num(r[i]);
                    double v8 = Num(r[nArgs]);
                    double reference = Num(r[nArgs + 1]);
                    double p = port(args);
                    ck.Near($"{name} == fdlibm", 0, reference, p, $"({string.Join(", ", Array.ConvertAll(args, Checker.G))})");
                    if (Same(p, v8)) portV8++;
                    if (Same(sys(args), v8)) sysV8++;
                }
                ck.Info.Add($"{name,-6}: port == V8 {portV8}/{rows.Count}   (this runtime's System.Math == V8: {sysV8}/{rows.Count})");
            }
            Group("sin", L(libm["sin"]), 1, a => JsMath.Sin(a[0]), a => Math.Sin(a[0]));
            Group("cos", L(libm["cos"]), 1, a => JsMath.Cos(a[0]), a => Math.Cos(a[0]));
            Group("atan2", L(libm["atan2"]), 2, a => JsMath.Atan2(a[0], a[1]), a => Math.Atan2(a[0], a[1]));
            Group("log", L(libm["log"]), 1, a => JsMath.Log(a[0]), a => Math.Log(a[0]));
            int powSame = 0;
            var pow = L(libm["pow"]);
            foreach (object ro in pow)
            {
                var r = L(ro);
                if (Same(Math.Pow(Num(r[0]), Num(r[1])), Num(r[2]))) powSame++;
            }
            ck.Info.Add($"pow   : System.Math.Pow(x, 1.5) == V8 {powSame}/{pow.Count} (the port keeps System.Math.Pow)");
            if (g.TryGetValue("libmStats", out object so) && so is Dictionary<string, object> stats)
            {
                foreach (var kv in stats)
                {
                    var s = O(kv.Value);
                    ck.Info.Add($"golden: V8 Math.{kv.Key} == plain fdlibm {I(s["v8EqualsFdlibm"])}/{I(s["n"])}, == fdlibm with fused multiply-add {I(s["v8EqualsFdlibmFma"])}/{I(s["n"])}");
                }
            }
            return ck;
        }

        // ---- validateNetwork -------------------------------------------------------------------------

        private static Checker CheckValidation(Dictionary<string, object> g)
        {
            var ck = new Checker("NetworkData.Validate == validateNetwork (same errors, same messages)");
            if (!g.TryGetValue("validation", out object vo)) return ck;
            foreach (object ro in L(vo))
            {
                var r = O(ro);
                string error = null;
                try
                {
                    NetworkData.Parse(S(r["json"])).Validate();
                }
                catch (InvalidNetworkException ex)
                {
                    error = ex.Message;
                }
                ck.Exact(S(r["name"]), S(r["error"]), error);
            }
            return ck;
        }

        // ---- one test case ----------------------------------------------------------------------------

        private static Checker CheckCase(Dictionary<string, object> c)
        {
            string name = S(c["name"]);
            var ck = new Checker(name);
            var ids = new List<string>();
            foreach (object o in L(c["ids"])) ids.Add(S(o));
            PathField f;
            StitchReport report = null;
            var sw = Stopwatch.StartNew();
            try
            {
                if (S(c["kind"]) == "single")
                {
                    f = new PathField(LoadNet(ids[0]), PathConfig.Default());
                }
                else
                {
                    var nets = new List<NetworkData>();
                    foreach (string id in ids) nets.Add(LoadNet(id));
                    var r = Stitcher.Stitch(nets, ids, PathConfig.Default(), S(c["layout"]));
                    f = r.field;
                    report = r.report;
                }
            }
            catch (Exception ex)
            {
                ck.Fail("construction threw: " + ex);
                return ck;
            }
            ck.Info.Add($"built in {sw.ElapsedMilliseconds} ms (JS: {Num(c["jsMs"]):F0} ms, cold)");
            var js = O(c["field"]);

            ck.Exact("name", S(js["name"]), f.Name);
            ck.Exact("widthMode", S(js["widthMode"]), f.WidthMode);
            ck.Near("R / apothem / scale", TolDouble, Num(js["R"]), f.R, "R");
            ck.Near("R / apothem / scale", TolDouble, Num(js["apothem"]), f.Apothem, "apothem");
            ck.Near("R / apothem / scale", TolDouble, Num(js["scale"]), f.Scale, "scale");
            var b = O(js["bounds"]);
            ck.Near("bounds", TolDouble, Num(b["minX"]), f.Bounds.MinX, "minX");
            ck.Near("bounds", TolDouble, Num(b["maxX"]), f.Bounds.MaxX, "maxX");
            ck.Near("bounds", TolDouble, Num(b["minZ"]), f.Bounds.MinZ, "minZ");
            ck.Near("bounds", TolDouble, Num(b["maxZ"]), f.Bounds.MaxZ, "maxZ");
            var hc = L(js["hexCenters"]);
            if (ck.Exact("hexCenters.count", hc.Count, f.HexCenters.Count))
            {
                for (int i = 0; i < hc.Count; i++)
                {
                    ck.Near("hexCenters", TolDouble, Num(L(hc[i])[0]), f.HexCenters[i].x, $"#{i}.x");
                    ck.Near("hexCenters", TolDouble, Num(L(hc[i])[1]), f.HexCenters[i].z, $"#{i}.z");
                }
            }
            ck.Near("totalLength", TolDouble, Num(js["totalLength"]), f.TotalLength, "");
            ck.Near("walkableArea", TolDouble, Num(js["walkableArea"]), f.WalkableArea, "");

            // grid
            var gr = O(js["grid"]);
            ck.Exact("grid.nx", I(gr["nx"]), f.Grid.Nx);
            ck.Exact("grid.nz", I(gr["nz"]), f.Grid.Nz);
            ck.Near("grid origin / cell", TolDouble, Num(gr["minX"]), f.Grid.MinX, "minX");
            ck.Near("grid origin / cell", TolDouble, Num(gr["minZ"]), f.Grid.MinZ, "minZ");
            ck.Near("grid origin / cell", TolDouble, Num(gr["cell"]), f.Grid.Cell, "cell");
            var hash = O(gr["hash"]);
            var sums = O(gr["sum"]);
            var exactGrids = new List<string>();
            var diffGrids = new List<string>();
            void GridCheck(string key, float[] arr)
            {
                bool same = (uint)Num(hash[key]) == HashF32(arr);
                (same ? exactGrids : diffGrids).Add(key);
                double tol = 1e-6 * arr.Length; // mean |Δ| per sample ≤ 1e-6
                ck.Near("grid sums (d,c,h,hw)", tol, Num(sums[key]), Sum(arr), key);
            }
            GridCheck("d", f.Grid.D);
            GridCheck("c", f.Grid.C);
            GridCheck("h", f.Grid.H);
            GridCheck("hw", f.Grid.Hw);
            ck.Info.Add($"float32 grids bit-identical to JS: [{string.Join(", ", exactGrids)}]{(diffGrids.Count > 0 ? $"; differing: [{string.Join(", ", diffGrids)}]" : "")}");

            // nodes
            var nodes = L(js["nodes"]);
            if (ck.Exact("nodes.count", nodes.Count, f.Nodes.Count))
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    var n = O(nodes[i]);
                    Node m = f.Nodes[i];
                    string at = $"node #{i} {S(n["id"])}";
                    ck.Exact(at + ".id", S(n["id"]), m.Id);
                    ck.Exact(at + ".type", S(n["type"]), m.Type);
                    ck.Exact(at + ".boundary", B(n["boundary"]), m.Boundary);
                    ck.Exact(at + ".degree", I(n["degree"]), m.Degree);
                    ck.Exact(at + ".inside", B(n["inside"]), m.Inside);
                    ck.Near("node x/z", TolDouble, Num(n["x"]), m.X, at + ".x");
                    ck.Near("node x/z", TolDouble, Num(n["z"]), m.Z, at + ".z");
                    ck.Near("node hw/clear", TolDouble, Num(n["hw"]), m.Hw, at + ".hw");
                    ck.Near("node hw/clear", TolDouble, Num(n["clear"]), m.Clear, at + ".clear");
                }
            }

            // edges
            var edges = L(js["edges"]);
            int geomExact = 0;
            if (ck.Exact("edges.count", edges.Count, f.Edges.Count))
            {
                for (int i = 0; i < edges.Count; i++)
                {
                    var e = O(edges[i]);
                    Edge m = f.Edges[i];
                    string at = $"edge #{i} {S(e["id"])}";
                    ck.Exact(at + ".id", S(e["id"]), m.Id);
                    ck.Exact(at + ".source", S(e["source"]), m.Source);
                    ck.Exact(at + ".target", S(e["target"]), m.Target);
                    ck.Exact(at + ".connector", B(e["connector"]), m.Connector);
                    ck.Exact(at + ".inferred", B(e["inferred"]), m.Inferred);
                    ck.Near("edge hw/length", TolDouble, Num(e["hw"]), m.Hw, at + ".hw");
                    ck.Near("edge hw/length", TolDouble, Num(e["length"]), m.Length, at + ".length");
                    if (!ck.Exact(at + ".pts.count", I(e["n"]), m.Pts.Count)) continue;
                    ck.Exact(at + ".hws.count", I(e["n"]), m.Hws.Count);
                    var pts = L(e["pts"]);
                    var hws = L(e["hws"]);
                    var flat = new List<double>(m.Pts.Count * 2);
                    for (int k = 0; k < m.Pts.Count; k++)
                    {
                        ck.Near("edge points", TolDouble, Num(pts[2 * k]), m.Pts[k][0], $"{at} pt {k}.x");
                        ck.Near("edge points", TolDouble, Num(pts[2 * k + 1]), m.Pts[k][1], $"{at} pt {k}.z");
                        ck.Near("edge half-widths", TolDouble, Num(hws[k]), m.Hws[k], $"{at} hw {k}");
                        flat.Add(m.Pts[k][0]);
                        flat.Add(m.Pts[k][1]);
                    }
                    if ((uint)Num(e["ptsHash"]) == HashF64(flat) && (uint)Num(e["hwsHash"]) == HashF64(m.Hws)) geomExact++;
                }
                ck.Info.Add($"edge geometry (points + half-widths) bit-identical to JS: {geomExact}/{edges.Count} edges");
            }

            // exits
            var exits = L(js["exits"]);
            if (ck.Exact("exits.count", exits.Count, f.Exits.Count))
            {
                for (int i = 0; i < exits.Count; i++)
                {
                    var e = O(exits[i]);
                    Exit m = f.Exits[i];
                    string at = $"exit #{i}";
                    ck.Exact(at + ".edgeId", S(e["edgeId"]), m.EdgeId);
                    ck.Near("exits", TolDouble, Num(e["x"]), m.X, at + ".x");
                    ck.Near("exits", TolDouble, Num(e["z"]), m.Z, at + ".z");
                    ck.Near("exits", TolDouble, Num(e["dirX"]), m.DirX, at + ".dirX");
                    ck.Near("exits", TolDouble, Num(e["dirZ"]), m.DirZ, at + ".dirZ");
                }
            }

            // spawn point
            var sp = O(js["spawn"]);
            var csp = f.SpawnPoint();
            ck.Near("spawnPoint", TolDouble, Num(sp["x"]), csp.x, "x");
            ck.Near("spawnPoint", TolDouble, Num(sp["z"]), csp.z, "z");
            ck.Near("spawnPoint", TolDouble, Num(sp["heading"]), csp.heading, "heading");

            // frame edges
            var fe = L(js["frameEdges"]);
            if (ck.Exact("frameEdges.count", fe.Count, f.FrameEdges.Count))
            {
                for (int i = 0; i < fe.Count; i++)
                {
                    var e = O(fe[i]);
                    FrameEdge m = f.FrameEdges[i];
                    string at = $"frameEdge #{i}";
                    ck.Exact(at + ".shared", B(e["shared"]), m.Shared);
                    ck.Near("frameEdges", TolDouble, Num(e["x0"]), m.X0, at + ".x0");
                    ck.Near("frameEdges", TolDouble, Num(e["z0"]), m.Z0, at + ".z0");
                    ck.Near("frameEdges", TolDouble, Num(e["x1"]), m.X1, at + ".x1");
                    ck.Near("frameEdges", TolDouble, Num(e["z1"]), m.Z1, at + ".z1");
                    ck.Near("frameEdges", TolDouble, Num(e["cx"]), m.Cx, at + ".cx");
                    ck.Near("frameEdges", TolDouble, Num(e["cz"]), m.Cz, at + ".cz");
                }
            }

            // modules + stitch report
            var mods = L(js["modules"]);
            if (ck.Exact("modules.count", mods.Count, f.Modules.Count))
            {
                for (int i = 0; i < mods.Count; i++)
                {
                    var m = O(mods[i]);
                    ck.Exact($"module #{i}.id", S(m["id"]), f.Modules[i].Id);
                    ck.Exact($"module #{i}.rot", I(m["rot"]), f.Modules[i].Rot);
                    ck.Near("modules", TolDouble, Num(m["x"]), f.Modules[i].X, $"#{i}.x");
                    ck.Near("modules", TolDouble, Num(m["z"]), f.Modules[i].Z, $"#{i}.z");
                }
            }
            if (c["report"] is Dictionary<string, object> rep)
            {
                if (report == null) ck.Fail("no stitch report");
                else
                {
                    var rm = L(rep["modules"]);
                    if (ck.Exact("report.modules.count", rm.Count, report.Modules.Count))
                        for (int i = 0; i < rm.Count; i++) ck.Exact($"report.modules[{i}]", S(rm[i]), report.Modules[i]);
                    ck.Exact("report.connectors", I(rep["connectors"]), report.Connectors);
                    ck.Exact("report.combos", I(rep["combos"]), report.Combos);
                    ck.Near("report.meanOffset", TolDouble, Num(rep["meanOffset"]), report.MeanOffset, "");
                    for (int i = 0; i < mods.Count && i < report.Rotations.Count; i++)
                        ck.Exact($"report.rotations[{i}]", I(O(mods[i])["rot"]), report.Rotations[i]);
                    ck.Info.Add($"stitch: {string.Join(", ", report.Modules)}; {report.Connectors} connectors; mean offset {report.MeanOffset:F3}");
                }
            }

            // samples
            var s = O(js["samples"]);
            var xs = L(s["x"]);
            var zs = L(s["z"]);
            var pd = L(s["pathDist"]);
            var di = L(s["dist"]);
            var cd = L(s["centerDist"]);
            var hwv = L(s["halfWidth"]);
            var hv = L(s["height"]);
            var hd = L(s["hexDist"]);
            var nv = L(s["normal"]);
            var gv = L(s["grad"]);
            for (int k = 0; k < xs.Count; k++)
            {
                double x = Num(xs[k]);
                double z = Num(zs[k]);
                string at = $"({x:F3}, {z:F3})";
                ck.Near("pathDist", TolGrid, Num(pd[k]), f.PathDist(x, z), at);
                ck.Near("dist", TolGrid, Num(di[k]), f.Dist(x, z), at);
                ck.Near("centerDist", TolGrid, Num(cd[k]), f.CenterDist(x, z), at);
                ck.Near("halfWidth", TolGrid, Num(hwv[k]), f.HalfWidth(x, z), at);
                ck.Near("height", TolGrid, Num(hv[k]), f.Height(x, z), at);
                ck.Near("hexDist", TolDouble, Num(hd[k]), f.HexDist(x, z), at);
                var n = f.Normal(x, z);
                ck.Near("normal", TolGrid, Num(nv[3 * k]), n.x, at + ".x");
                ck.Near("normal", TolGrid, Num(nv[3 * k + 1]), n.y, at + ".y");
                ck.Near("normal", TolGrid, Num(nv[3 * k + 2]), n.z, at + ".z");
                var gd = f.GradDist(x, z);
                ck.Near("gradDist", TolGrid, Num(gv[2 * k]), gd.x, at + ".x");
                ck.Near("gradDist", TolGrid, Num(gv[2 * k + 1]), gd.z, at + ".z");
            }
            ck.Info.Add($"{xs.Count} sample points");
            return ck;
        }

        // ---- the optimized grid builder must equal the reference one bit for bit -------------------

        private static int[] Bits(float[] a)
        {
            var bits = new int[a.Length];
            Buffer.BlockCopy(a, 0, bits, 0, a.Length * 4);
            return bits;
        }

        private static int CountDiff(float[] a, float[] b)
        {
            if (a.Length != b.Length) return int.MaxValue;
            int[] x = Bits(a), y = Bits(b);
            int n = 0;
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) n++;
            return n;
        }

        /// <summary>
        /// The tiled stamper (FieldStamper) must give the same float32 grids as the direct port of the
        /// JS stamping loops, bit for bit — for every module, more stitch layouts, and non-default configs.
        /// </summary>
        private static Checker CheckGridBuilders()
        {
            var ck = new Checker("grid builder: tiled stamper == direct port of the JS loops (bitwise)");
            var worlds = new List<(string layout, string[] ids)>();
            for (int i = 1; i <= 13; i++) worlds.Add(("single", new[] { "path-" + i.ToString("000", CultureInfo.InvariantCulture) }));
            worlds.Add(("row", new[] { "path-010", "path-013" }));
            worlds.Add(("row", new[] { "path-006", "path-010", "path-013" }));
            worlds.Add(("cluster", new[] { "path-001", "path-002", "path-003" }));
            worlds.Add(("row", new[] { "path-004", "path-005" }));
            worlds.Add(("cluster", new[] { "path-007", "path-008", "path-009" }));
            worlds.Add(("row", new[] { "path-011", "path-012", "path-002" }));
            var configs = new List<(string name, PathConfig cfg)> { ("default", PathConfig.Default()) };
            PathConfig odd = PathConfig.Default();
            odd.FieldCell = 0.37;
            odd.ChamberNoise = 0.8;
            odd.Seed = 1;
            configs.Add(("cell 0.37, chamberNoise 0.8, seed 1", odd));
            PathConfig measured = PathConfig.Default();
            measured.WidthMode = "measured";
            measured.FieldCell = 0.6;
            configs.Add(("measured widths, cell 0.6", measured));

            double tRef = 0, tFast = 0;
            int compared = 0;
            var sw = new Stopwatch();
            foreach (var (cname, cfg) in configs)
            {
                foreach (var (layout, ids) in worlds)
                {
                    // non-default configs: singles and the three game presets only (keeps the run short)
                    if (cname != "default" && layout != "single" && !(ids.Length == 3 && layout == "cluster" && ids[0] == "path-001") && !(ids.Length == 2 && ids[0] == "path-010")) continue;
                    var nets = new List<NetworkData>();
                    foreach (string id in ids) nets.Add(LoadNet(id));
                    PathField Build() => layout == "single" ? new PathField(nets[0], cfg) : Stitcher.Stitch(nets, ids, cfg, layout).field;
                    PathField a, b;
                    try
                    {
                        FieldStamper.UseReference = true;
                        sw.Restart();
                        a = Build();
                        tRef += sw.Elapsed.TotalMilliseconds;
                    }
                    finally
                    {
                        FieldStamper.UseReference = false;
                    }
                    sw.Restart();
                    b = Build();
                    tFast += sw.Elapsed.TotalMilliseconds;
                    string at = $"{cname} / {layout} {string.Join("+", ids)}";
                    ck.Exact(at + " grid size", a.Grid.Nx * 100000 + a.Grid.Nz, b.Grid.Nx * 100000 + b.Grid.Nz);
                    ck.Exact(at + " d cells differing", 0, CountDiff(a.Grid.D, b.Grid.D));
                    ck.Exact(at + " c cells differing", 0, CountDiff(a.Grid.C, b.Grid.C));
                    ck.Exact(at + " hw cells differing", 0, CountDiff(a.Grid.Hw, b.Grid.Hw));
                    ck.Exact(at + " h cells differing", 0, CountDiff(a.Grid.H, b.Grid.H));
                    ck.Exact(at + " walkableArea", a.WalkableArea, b.WalkableArea);
                    ck.Exact(at + " exits", a.Exits.Count, b.Exits.Count);
                    compared++;
                }
            }
            ck.Info.Add($"{compared} worlds compared; total build time: direct port {tRef:F0} ms, tiled stamper {tFast:F0} ms ({tRef / Math.Max(tFast, 1e-9):F1}× faster)");
            return ck;
        }

        // ---- timings ---------------------------------------------------------------------------------

        private static void Timings(Dictionary<string, object> g, int runs)
        {
            Console.WriteLine();
            Console.WriteLine($"[TIMING] min / median of {runs} warm runs, plus the first (cold) run:");
            string text = File.ReadAllText(Path.Combine(_networks, "path-013.json"));
            Measure("NetworkData.Parse(path-013.json)", runs * 3, () => NetworkData.Parse(text));
            foreach (object co in L(g["cases"]))
            {
                var c = O(co);
                var ids = new List<string>();
                foreach (object o in L(c["ids"])) ids.Add(S(o));
                var nets = new List<NetworkData>();
                foreach (string id in ids) nets.Add(LoadNet(id));
                if (S(c["kind"]) == "single") Measure($"new PathField({ids[0]})", runs, () => new PathField(nets[0], PathConfig.Default()));
                else Measure($"Stitcher.Stitch({S(c["layout"])}: {string.Join(", ", ids)})", runs, () => Stitcher.Stitch(nets, ids, PathConfig.Default(), S(c["layout"])));
            }
        }

        private static void Measure(string what, int runs, Action a)
        {
            var sw = Stopwatch.StartNew();
            a();
            double cold = sw.Elapsed.TotalMilliseconds;
            var t = new List<double>();
            for (int i = 0; i < runs; i++)
            {
                sw.Restart();
                a();
                t.Add(sw.Elapsed.TotalMilliseconds);
            }
            t.Sort();
            Console.WriteLine($"    {what,-58} min {t[0],8:F1} ms   median {t[t.Count / 2],8:F1} ms   cold {cold,8:F1} ms");
        }
    }
}
