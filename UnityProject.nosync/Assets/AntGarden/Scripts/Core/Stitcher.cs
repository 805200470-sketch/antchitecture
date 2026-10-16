// Ant Garden core — port of src/stitch.js: stitch 2–3 hexagonal nest modules into one world (and, beyond the
// prototype, a honeycomb of up to nineteen).
//
// 1. Every module is rebuilt at one shared hexagon size and laid out so that
//    neighbours share an edge (a row, or a triangle cluster of three).
// 2. Each module may be turned in 60° steps. All rotation combinations are
//    scored: exits that meet on a shared edge should line up (small offset
//    along the edge) and as few exits as possible should be left unmatched.
// 3. Matched exits are bridged by a smooth S-shaped connector canyon across
//    the shared edge; unmatched ones on inner edges end in a dead-end chamber;
//    exits on the outer rim stay exits.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace AntGarden.Core
{
    /// <summary>Summary of a stitch (stitch.js <c>report</c>).</summary>
    public sealed class StitchReport
    {
        /// <summary>"path-010（转 180°）" per module, as the prototype prints it.</summary>
        public List<string> Modules;
        /// <summary>Connector canyons between modules (links + branches + bridges).</summary>
        public int Connectors;
        /// <summary>Mean offset along the shared edge of the matched exits (world units).</summary>
        public double MeanOffset;
        /// <summary>Rotation combinations tried (6^(modules-1)).</summary>
        public int Combos;
        /// <summary>Chosen rotation of each module in degrees.</summary>
        public List<int> Rotations;
    }

    /// <summary>Stitches 2–3 module networks into one <see cref="PathField"/> (stitch.js <c>stitchModules</c>).</summary>
    /// <remarks>Self-contained: may run on a worker thread.</remarks>
    public static class Stitcher
    {
        private static readonly double SQRT3 = Math.Sqrt(3);
        private const double STEP = Math.PI / 3;

        private sealed class Port
        {
            public Edge Edge;
            public bool AtEnd;
            public int Keep; // points kept after trimming (from the far end)
            public int K;
            public double S; // position along the edge
            public double[] Point;
            public double[] Dir;
            public double Hw;
        }

        private sealed class Candidate
        {
            public Port Pa, Pb;
            public double Gap;
        }

        private sealed class Adjacency
        {
            public int I, J, Ki, Kj;
        }

        private sealed class Match
        {
            public int I, J;
            public Port A, B;
            public double Gap;
        }

        private sealed class Best
        {
            public List<int> Rot;
            public double Cost;
            public List<Match> Matches;
        }

        private sealed class ExtraLink
        {
            public bool Bridge;
            public int From, To;
            public Port Port;
        }

        /// <summary>Rotate (x, z) by r × 60° (from +x toward +z).</summary>
        private static double[] Rotate(double x, double z, int r)
        {
            double a = r * STEP;
            double c = JsMath.Cos(a);
            double s = JsMath.Sin(a);
            return new[] { x * c - z * s, x * s + z * c };
        }

        /// <summary>Which of the six hexagon edges (normals at k × 60°) a direction points through.</summary>
        private static int EdgeIndexOf(double x, double z)
        {
            double k = JsMath.Round(JsMath.Atan2(z, x) / STEP);
            int ki = (int)k;
            return ((ki % 6) + 6) % 6;
        }

        private static string Str(int v)
        {
            return v.ToString(CultureInfo.InvariantCulture);
        }

        // JS template literals print undefined ids as "undefined".
        private static string Str(string s)
        {
            return s ?? "undefined";
        }

        /// <summary>
        /// Tail ends of a module's paths that run out through its frame. A tail is cut back
        /// portInset inside the frame (or as far as it is long) so the connector canyon has room to curve.
        /// </summary>
        private static List<Port> CollectPorts(PathField field, PathConfig cfg)
        {
            var ports = new List<Port>();
            double[] insets = { cfg.StitchPortInset, cfg.FrameWallMargin + 2.5 };
            foreach (Edge e in field.Edges)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    bool atEnd = pass == 1;
                    List<double[]> pts = atEnd ? JsMath.Reversed(e.Pts) : e.Pts; // walk from the outer end inward
                    if (field.HexDist(pts[0][0], pts[0][1]) < -2) continue; // this end is inside: not a tail
                    foreach (double inset in insets)
                    {
                        double[] cross = pts[0];
                        int i = 0;
                        while (i < pts.Count - 1 && field.HexDist(pts[i][0], pts[i][1]) > -inset)
                        {
                            if (field.HexDist(pts[i][0], pts[i][1]) > 0) cross = pts[i];
                            i++;
                        }
                        if (i <= 0 || i >= pts.Count - 1) continue;
                        double px = pts[i][0];
                        double pz = pts[i][1];
                        double[] o = pts[Math.Max(0, i - 3)];
                        double len = JsMath.OrOne(JsMath.Hypot(o[0] - px, o[1] - pz));
                        int k = EdgeIndexOf(cross[0], cross[1]);
                        double tx = -JsMath.Sin(k * STEP);
                        double tz = JsMath.Cos(k * STEP);
                        ports.Add(new Port
                        {
                            Edge = e,
                            AtEnd = atEnd,
                            // As in the prototype: for a tail at the start of the edge this keeps i + 1
                            // points from the far end (not Count - i); kept for bit-for-bit parity.
                            Keep = atEnd ? e.Pts.Count - i : i + 1,
                            K = k,
                            S = cross[0] * tx + cross[1] * tz,
                            Point = new[] { px, pz },
                            Dir = new[] { (o[0] - px) / len, (o[1] - pz) / len },
                            Hw = atEnd ? e.Hws[e.Hws.Count - 1 - i] : e.Hws[i],
                        });
                        break;
                    }
                }
            }
            return ports;
        }

        /// <summary>Greedy pairing of exits meeting on one shared edge; returns pairs and the cost.</summary>
        private static List<Candidate> PairUp(List<Port> a, List<Port> b, double penalty, double maxGap, out double cost)
        {
            var pairs = new List<Candidate>();
            var usedB = new HashSet<Port>();
            var cand = new List<Candidate>();
            foreach (Port pa in a)
            {
                foreach (Port pb in b) cand.Add(new Candidate { Pa = pa, Pb = pb, Gap = Math.Abs(pa.S - -pb.S) });
            }
            // Array.prototype.sort is stable: insertion sort with the same comparator (x.gap - y.gap).
            for (int i = 1; i < cand.Count; i++)
            {
                Candidate x = cand[i];
                int j = i - 1;
                while (j >= 0 && cand[j].Gap - x.Gap > 0)
                {
                    cand[j + 1] = cand[j];
                    j--;
                }
                cand[j + 1] = x;
            }
            var usedA = new HashSet<Port>();
            cost = 0;
            foreach (Candidate c in cand)
            {
                if (usedA.Contains(c.Pa) || usedB.Contains(c.Pb) || c.Gap > maxGap) continue;
                usedA.Add(c.Pa);
                usedB.Add(c.Pb);
                pairs.Add(c);
                cost += c.Gap;
            }
            cost += (a.Count - usedA.Count + b.Count - usedB.Count) * penalty;
            return pairs;
        }

        private static List<double[]> Bezier(double[] p0, double[] p1, double[] p2, double[] p3, double step)
        {
            double approx = JsMath.Hypot(p3[0] - p0[0], p3[1] - p0[1]) * 1.3;
            double nd = JsMath.Max(4, Math.Ceiling(approx / step));
            int n = double.IsNaN(nd) ? 0 : (int)nd;
            var outPts = new List<double[]>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                double t = (double)i / n;
                double u = 1 - t;
                outPts.Add(new[]
                {
                    u * u * u * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t * t * t * p3[0],
                    u * u * u * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t * t * t * p3[1],
                });
            }
            return outPts;
        }

        private static List<Port> PortsOn(List<Port> ports, int rot, int k)
        {
            var list = new List<Port>();
            foreach (Port p in ports)
            {
                if ((p.K + rot) % 6 == k) list.Add(p);
            }
            return list;
        }

        /// <summary>
        /// Stitches the module networks into one world.
        /// </summary>
        /// <param name="nets">Path Recognize networks, one per module</param>
        /// <param name="ids">module ids (labels), same count as <paramref name="nets"/></param>
        /// <param name="cfg">configuration (null = defaults)</param>
        /// <param name="layout">"row" (default), "cluster" (three modules in a triangle) or "honeycomb" (rings round the first)</param>
        /// <exception cref="InvalidNetworkException">A network is not usable.</exception>
        /// <param name="widthScale">optional, per module: its canyons' widths are multiplied by this (a ruin's tunnels
        /// are narrower than a nest's halls); the connectors across the seams keep the neighbours' width</param>
        public static (PathField field, StitchReport report) Stitch(IList<NetworkData> nets, IList<string> ids, PathConfig cfg, string layout = "row", IList<double> widthScale = null)
        {
            if (nets == null || nets.Count == 0) throw new ArgumentException("at least one network is required", nameof(nets));
            if (ids == null || ids.Count != nets.Count) throw new ArgumentException("ids must name every network", nameof(ids));
            cfg = cfg ?? PathConfig.Default();

            var radii = new List<double>(nets.Count);
            foreach (NetworkData n in nets) radii.Add(PathField.ModuleOnly(n, cfg).R);
            double R = JsMath.MaxOf(radii);
            PathConfig cfgR = cfg.Clone();
            cfgR.FrameForceR = R;
            var mods = new List<PathField>(nets.Count);
            foreach (NetworkData n in nets) mods.Add(PathField.ModuleOnly(n, cfgR));
            double a = (R * SQRT3) / 2;

            var centers = new List<double[]>();
            if (layout == "cluster" && mods.Count == 3)
            {
                centers.Add(new[] { 0.0, 0.0 });
                centers.Add(new[] { 2 * a, 0.0 });
                centers.Add(new[] { a, 1.5 * R });
            }
            else if (layout == "honeycomb")
            {
                // a honeycomb: the first module in the middle, the next six round it, then the second ring
                for (int i = 0; i < mods.Count; i++)
                {
                    (int q, int r) = HoneycombCell(i);
                    centers.Add(new[] { 2 * a * (q + r / 2.0), 1.5 * R * r });
                }
            }
            else
            {
                for (int i = 0; i < mods.Count; i++) centers.Add(new[] { i * 2 * a, 0.0 });
            }
            double sumX = 0;
            double sumZ = 0;
            foreach (double[] c in centers) sumX += c[0];
            foreach (double[] c in centers) sumZ += c[1];
            double mx = sumX / centers.Count;
            double mz = sumZ / centers.Count;
            var shifted = new List<double[]>(centers.Count);
            foreach (double[] c in centers) shifted.Add(new[] { c[0] - mx, c[1] - mz });
            centers = shifted;
            var centerTuples = new List<(double x, double z)>(centers.Count);
            foreach (double[] c in centers) centerTuples.Add((c[0], c[1]));
            var frame = new HexFrame(centerTuples, R);

            // A straight passage from p to q that keeps clear of the outer rim (not across a notch).
            bool ClearPassage(double[] p, double[] q, double hw)
            {
                double nd = JsMath.Max(2, Math.Ceiling(JsMath.Hypot(q[0] - p[0], q[1] - p[1]) / 2));
                int n = double.IsNaN(nd) ? 0 : (int)nd;
                for (int k = 0; k <= n; k++)
                {
                    double t = (double)k / n;
                    if (frame.Dist(p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t) > -(hw + 3)) return false;
                }
                return true;
            }

            var adj = new List<Adjacency>();
            for (int i = 0; i < centers.Count; i++)
            {
                for (int j = i + 1; j < centers.Count; j++)
                {
                    double dx = centers[j][0] - centers[i][0];
                    double dz = centers[j][1] - centers[i][1];
                    if (Math.Abs(JsMath.Hypot(dx, dz) - 2 * a) < 1)
                    {
                        int ki = EdgeIndexOf(dx, dz);
                        adj.Add(new Adjacency { I = i, J = j, Ki = ki, Kj = (ki + 3) % 6 });
                    }
                }
            }

            var ports = new List<List<Port>>(mods.Count);
            foreach (PathField m in mods) ports.Add(CollectPorts(m, cfg));
            double penalty = a * 0.9;
            double maxGap = a * 0.95;

            // Try every combination of 60° turns (the first module stays put). More than six modules have too
            // many combinations for that (6^14 for fifteen): see TurnGreedily.
            Best best = null;
            int combos = 1;
            if (mods.Count > 6) best = TurnGreedily(mods.Count, adj, ports, penalty, maxGap, a, out combos);
            else
                for (int i = 1; i < mods.Count; i++) combos *= 6;
            for (int c = 0; c < (mods.Count > 6 ? 0 : combos); c++)
            {
                var rot = new List<int> { 0 };
                int v = c;
                for (int i = 1; i < mods.Count; i++)
                {
                    rot.Add(v % 6);
                    v /= 6;
                }
                double cost = 0;
                var matches = new List<Match>();
                foreach (Adjacency e in adj)
                {
                    List<Port> pa = PortsOn(ports[e.I], rot[e.I], e.Ki);
                    List<Port> pb = PortsOn(ports[e.J], rot[e.J], e.Kj);
                    double rc;
                    List<Candidate> pairs = PairUp(pa, pb, penalty, maxGap, out rc);
                    cost += rc;
                    if (pairs.Count == 0) cost += a * 4; // a shared edge nobody can walk across
                    foreach (Candidate pr in pairs) matches.Add(new Match { I = e.I, J = e.J, A = pr.Pa, B = pr.Pb, Gap = pr.Gap });
                }
                if (best == null || cost < best.Cost || (cost == best.Cost && matches.Count > best.Matches.Count))
                    best = new Best { Rot = rot, Cost = cost, Matches = matches };
            }

            // Which (module, rotated edge) pairs are inner, shared edges.
            var sharedEdge = new HashSet<string>(StringComparer.Ordinal);
            foreach (Adjacency e in adj)
            {
                sharedEdge.Add(Str(e.I) + ":" + Str(e.Ki));
                sharedEdge.Add(Str(e.J) + ":" + Str(e.Kj));
            }

            double[] ToWorld(int i, double x, double z)
            {
                double[] r = Rotate(x, z, best.Rot[i]);
                return new[] { r[0] + centers[i][0], r[1] + centers[i][1] };
            }

            var edges = new List<Edge>();
            var nodes = new List<Node>();
            var portNode = new Dictionary<Port, string>();
            int portCount = 0;
            for (int i = 0; i < mods.Count; i++)
            {
                PathField m = mods[i];
                string prefix = "m" + Str(i) + ":";
                foreach (Node n in m.Nodes)
                {
                    double[] w = ToWorld(i, n.X, n.Z);
                    nodes.Add(new Node { Id = prefix + Str(n.Id), X = w[0], Z = w[1], Type = n.Type, Boundary = n.Boundary });
                }
                foreach (Edge e in m.Edges)
                {
                    var pts = new List<double[]>(e.Pts.Count);
                    foreach (double[] p in e.Pts) pts.Add(ToWorld(i, p[0], p[1]));
                    var hws = new List<double>(e.Hws);
                    if (widthScale != null && widthScale[i] != 1)
                        for (int k = 0; k < hws.Count; k++) hws[k] *= widthScale[i];
                    string source = prefix + Str(e.Source);
                    string target = prefix + Str(e.Target);
                    foreach (Port p in ports[i])
                    {
                        if (p.Edge != e || !sharedEdge.Contains(Str(i) + ":" + Str((p.K + best.Rot[i]) % 6))) continue;
                        // a tail running into a neighbour: cut it back inside this module
                        string id = prefix + "port" + Str(portCount++);
                        double[] w = ToWorld(i, p.Point[0], p.Point[1]);
                        nodes.Add(new Node { Id = id, X = w[0], Z = w[1], Type = "endpoint", Boundary = false });
                        portNode[p] = id;
                        if (p.AtEnd)
                        {
                            pts = JsMath.Slice(pts, 0, p.Keep);
                            hws = JsMath.Slice(hws, 0, p.Keep);
                            target = id;
                        }
                        else
                        {
                            pts = JsMath.Slice(pts, pts.Count - p.Keep);
                            hws = JsMath.Slice(hws, hws.Count - p.Keep);
                            source = id;
                        }
                    }
                    if (pts.Count >= 2) edges.Add(new Edge { Id = prefix + Str(e.Id), Source = source, Target = target, Pts = pts, Hws = hws });
                }
            }

            // Every shared edge must be walkable: force the closest pair when nothing matched,
            // or branch off the neighbour's nearest path when only one side has an exit.
            var extraLinks = new List<ExtraLink>();
            var bridges = new List<Adjacency>();
            foreach (Adjacency e in adj)
            {
                bool matched = false;
                foreach (Match mt in best.Matches)
                {
                    if ((mt.I == e.I && mt.J == e.J) || (mt.I == e.J && mt.J == e.I))
                    {
                        matched = true;
                        break;
                    }
                }
                if (matched) continue;
                List<Port> pa = PortsOn(ports[e.I], best.Rot[e.I], e.Ki);
                List<Port> pb = PortsOn(ports[e.J], best.Rot[e.J], e.Kj);
                if (pa.Count > 0 && pb.Count > 0)
                {
                    double unused;
                    List<Candidate> pairs = PairUp(pa, pb, penalty, double.PositiveInfinity, out unused);
                    if (pairs.Count > 0) best.Matches.Add(new Match { I = e.I, J = e.J, A = pairs[0].Pa, B = pairs[0].Pb, Gap = pairs[0].Gap });
                }
                else if (pa.Count > 0 || pb.Count > 0)
                {
                    extraLinks.Add(pa.Count > 0 ? new ExtraLink { From = e.I, Port = pa[0], To = e.J } : new ExtraLink { From = e.J, Port = pb[0], To = e.I });
                }
                else
                {
                    bridges.Add(e);
                }
            }
            // the port side was trimmed above only for matched ports; trim these too
            foreach (ExtraLink xl in extraLinks)
            {
                if (xl.Bridge) continue;
                int i = xl.From;
                double[] wp = ToWorld(i, xl.Port.Point[0], xl.Port.Point[1]);
                double wx = wp[0];
                double wz = wp[1];
                string id;
                portNode.TryGetValue(xl.Port, out id); // ports on shared edges were already trimmed above
                Edge edge = null;
                if (string.IsNullOrEmpty(id))
                {
                    string wanted = "m" + Str(i) + ":" + Str(xl.Port.Edge.Id);
                    foreach (Edge ed in edges)
                    {
                        if (ed.Id == wanted)
                        {
                            edge = ed;
                            break;
                        }
                    }
                    id = "m" + Str(i) + ":port" + Str(portCount++);
                    nodes.Add(new Node { Id = id, X = wx, Z = wz, Type = "endpoint", Boundary = false });
                    portNode[xl.Port] = id;
                }
                if (edge != null)
                {
                    if (xl.Port.AtEnd)
                    {
                        edge.Pts = JsMath.Slice(edge.Pts, 0, xl.Port.Keep);
                        edge.Hws = JsMath.Slice(edge.Hws, 0, xl.Port.Keep);
                        edge.Target = id;
                    }
                    else
                    {
                        edge.Pts = JsMath.Slice(edge.Pts, edge.Pts.Count - xl.Port.Keep);
                        edge.Hws = JsMath.Slice(edge.Hws, edge.Hws.Count - xl.Port.Keep);
                        edge.Source = id;
                    }
                }
                // nearest point on the neighbour's paths: split that edge there and branch to the port
                Edge hitEdge = null;
                int hitK = 0;
                double hitD = 0;
                string toPrefix = "m" + Str(xl.To) + ":";
                var portPos = new[] { wx, wz };
                foreach (Edge ed in edges)
                {
                    if (ed.Id == null || !ed.Id.StartsWith(toPrefix, StringComparison.Ordinal)) continue;
                    for (int k = 0; k < ed.Pts.Count; k++)
                    {
                        if (k < 2 || k > ed.Pts.Count - 3) continue;
                        double x = ed.Pts[k][0];
                        double z = ed.Pts[k][1];
                        double d = JsMath.Hypot(x - wx, z - wz);
                        if ((hitEdge == null || d < hitD) && ClearPassage(portPos, new[] { x, z }, xl.Port.Hw))
                        {
                            hitEdge = ed;
                            hitK = k;
                            hitD = d;
                        }
                    }
                }
                if (hitEdge == null) continue;
                Edge sed = hitEdge;
                string splitId = "m" + Str(xl.To) + ":split" + Str(portCount++);
                double sx = sed.Pts[hitK][0];
                double sz = sed.Pts[hitK][1];
                nodes.Add(new Node { Id = splitId, X = sx, Z = sz, Type = "junction", Boundary = false });
                edges.Add(new Edge { Id = sed.Id + "b", Source = splitId, Target = sed.Target, Pts = JsMath.Slice(sed.Pts, hitK), Hws = JsMath.Slice(sed.Hws, hitK) });
                sed.Pts = JsMath.Slice(sed.Pts, 0, hitK + 1);
                sed.Hws = JsMath.Slice(sed.Hws, 0, hitK + 1);
                sed.Target = splitId;
                double[] d0 = Rotate(xl.Port.Dir[0], xl.Port.Dir[1], best.Rot[i]);
                double L = hitD * 0.4;
                List<double[]> bpts = Bezier(new[] { wx, wz }, new[] { wx + d0[0] * L, wz + d0[1] * L }, new[] { sx, sz }, new[] { sx, sz }, cfg.ResampleStep);
                double hw = xl.Port.Hw;
                var bhws = new List<double>(bpts.Count);
                for (int k = 0; k < bpts.Count; k++) bhws.Add(hw);
                edges.Add(new Edge { Id = "branch" + Str(portCount), Source = id, Target = splitId, Pts = bpts, Hws = bhws, Connector = true });
            }

            // Split an edge at point index k; returns the new junction node id.
            string SplitAt(Edge ed, int k, string tag)
            {
                string id = Str(ed.Id) + ":" + tag + Str(portCount++);
                double x = ed.Pts[k][0];
                double z = ed.Pts[k][1];
                nodes.Add(new Node { Id = id, X = x, Z = z, Type = "junction", Boundary = false });
                edges.Add(new Edge { Id = Str(ed.Id) + "~" + Str(portCount), Source = id, Target = ed.Target, Pts = JsMath.Slice(ed.Pts, k), Hws = JsMath.Slice(ed.Hws, k) });
                ed.Pts = JsMath.Slice(ed.Pts, 0, k + 1);
                ed.Hws = JsMath.Slice(ed.Hws, 0, k + 1);
                ed.Target = id;
                return id;
            }
            // No exits on either side: open a passage between the closest stretches of path.
            foreach (Adjacency e in bridges)
            {
                Edge bestA = null;
                Edge bestB = null;
                int bestKa = 0;
                int bestKb = 0;
                double bestD = 0;
                string pi = "m" + Str(e.I) + ":";
                string pj = "m" + Str(e.J) + ":";
                var ea = new List<Edge>();
                var eb = new List<Edge>();
                foreach (Edge ed in edges)
                {
                    if (ed.Id != null && ed.Id.StartsWith(pi, StringComparison.Ordinal)) ea.Add(ed);
                }
                foreach (Edge ed in edges)
                {
                    if (ed.Id != null && ed.Id.StartsWith(pj, StringComparison.Ordinal)) eb.Add(ed);
                }
                foreach (Edge A in ea)
                {
                    for (int ka = 2; ka < A.Pts.Count - 2; ka += 2)
                    {
                        foreach (Edge B in eb)
                        {
                            for (int kb = 2; kb < B.Pts.Count - 2; kb += 2)
                            {
                                double d = JsMath.Hypot(A.Pts[ka][0] - B.Pts[kb][0], A.Pts[ka][1] - B.Pts[kb][1]);
                                if (bestA != null && d >= bestD) continue;
                                double hwp = JsMath.Min(A.Hws[ka], B.Hws[kb]);
                                if (ClearPassage(A.Pts[ka], B.Pts[kb], hwp))
                                {
                                    bestA = A;
                                    bestKa = ka;
                                    bestB = B;
                                    bestKb = kb;
                                    bestD = d;
                                }
                            }
                        }
                    }
                }
                if (bestA == null) continue;
                double[] pa = bestA.Pts[bestKa];
                double[] pb = bestB.Pts[bestKb];
                double hw = JsMath.Min(bestA.Hws[bestKa], bestB.Hws[bestKb]);
                string ida = SplitAt(bestA, bestKa, "bridge");
                string idb = SplitAt(bestB, bestKb, "bridge");
                var mid = new[] { (pa[0] + pb[0]) / 2, (pa[1] + pb[1]) / 2 };
                List<double[]> bpts = Bezier(pa, mid, mid, pb, cfg.ResampleStep);
                var bhws = new List<double>(bpts.Count);
                for (int k = 0; k < bpts.Count; k++) bhws.Add(hw);
                edges.Add(new Edge { Id = "bridge" + Str(portCount), Source = ida, Target = idb, Pts = bpts, Hws = bhws, Connector = true });
                extraLinks.Add(new ExtraLink { Bridge = true });
            }

            // Bridge matched exits with a smooth connector canyon across the shared edge. The
            // control directions lean toward the other port so a tail that meets the edge at a
            // slant does not hook back; the canyon swells a little at the seam (a gate hall).
            double bulge = cfg.StitchLinkBulge;
            for (int n = 0; n < best.Matches.Count; n++)
            {
                Match mt = best.Matches[n];
                double[] pa = ToWorld(mt.I, mt.A.Point[0], mt.A.Point[1]);
                double[] pb = ToWorld(mt.J, mt.B.Point[0], mt.B.Point[1]);
                double[] da = Rotate(mt.A.Dir[0], mt.A.Dir[1], best.Rot[mt.I]);
                double[] db = Rotate(mt.B.Dir[0], mt.B.Dir[1], best.Rot[mt.J]);
                double D = JsMath.OrOne(JsMath.Hypot(pb[0] - pa[0], pb[1] - pa[1]));
                double ux = (pb[0] - pa[0]) / D;
                double uz = (pb[1] - pa[1]) / D;
                double[] ca = Unit(da[0] + ux * 0.7, da[1] + uz * 0.7);
                double[] cb = Unit(db[0] - ux * 0.7, db[1] - uz * 0.7);
                double L = D * 0.42;
                List<double[]> pts = Bezier(pa, new[] { pa[0] + ca[0] * L, pa[1] + ca[1] * L }, new[] { pb[0] + cb[0] * L, pb[1] + cb[1] * L }, pb, cfg.ResampleStep);
                var hws = new List<double>(pts.Count);
                for (int k = 0; k < pts.Count; k++)
                {
                    double t = (double)k / (pts.Count - 1);
                    hws.Add((mt.A.Hw + (mt.B.Hw - mt.A.Hw) * t) * (1 + bulge * JsMath.Sin(Math.PI * t)));
                }
                string src;
                string dst;
                portNode.TryGetValue(mt.A, out src);
                portNode.TryGetValue(mt.B, out dst);
                edges.Add(new Edge { Id = "link" + Str(n), Source = src, Target = dst, Pts = pts, Hws = hws, Connector = true });
            }

            var modules = new List<ModuleInfo>(ids.Count);
            for (int i = 0; i < ids.Count; i++) modules.Add(new ModuleInfo { Id = ids[i], Rot = best.Rot[i] * 60, X = centers[i][0], Z = centers[i][1] });
            PathField field = PathField.FromComposite(R, centerTuples, edges, nodes, string.Join(" + ", ids), modules, cfg);

            double gapSum = 0;
            foreach (Match mt in best.Matches) gapSum += mt.Gap;
            var report = new StitchReport
            {
                Modules = new List<string>(ids.Count),
                Connectors = best.Matches.Count + extraLinks.Count,
                MeanOffset = best.Matches.Count > 0 ? gapSum / best.Matches.Count : 0,
                Combos = combos,
                Rotations = new List<int>(ids.Count),
            };
            for (int i = 0; i < ids.Count; i++)
            {
                report.Modules.Add(ids[i] + "（转 " + Str(best.Rot[i] * 60) + "°）");
                report.Rotations.Add(best.Rot[i] * 60);
            }
            return (field, report);
        }

        /// <summary>
        /// Axial cell (pointy-top hexagons) of the i-th module of a honeycomb: the middle, the six round it,
        /// the six that sit between two of those, then the ring's corners.
        /// </summary>
        private static (int q, int r) HoneycombCell(int i)
        {
            (int, int)[] cells =
            {
                (0, 0),
                (1, 0), (0, 1), (-1, 1), (-1, 0), (0, -1), (1, -1),
                (1, 1), (-1, 2), (-2, 1), (-1, -1), (1, -2), (2, -1),
                (0, 2), (0, -2), (2, 0), (-2, 0), (2, -2), (-2, 2),
            };
            if (i >= cells.Length) throw new ArgumentException("a honeycomb holds at most " + cells.Length + " modules");
            return cells[i];
        }

        /// <summary>
        /// Turns for many modules: each module in turn (the first stays put) takes the turn that suits the
        /// neighbours it shares an edge with best, until nothing changes, from a dozen starting points; the
        /// cheapest result wins. Deterministic (fixed seed). <paramref name="evaluations"/> counts the tries.
        /// </summary>
        private static Best TurnGreedily(int count, List<Adjacency> adj, List<List<Port>> ports, double penalty, double maxGap, double a, out int evaluations)
        {
            double EdgeCost(Adjacency e, List<int> rot, List<Match> collect)
            {
                List<Port> pa = PortsOn(ports[e.I], rot[e.I], e.Ki);
                List<Port> pb = PortsOn(ports[e.J], rot[e.J], e.Kj);
                double rc;
                List<Candidate> pairs = PairUp(pa, pb, penalty, maxGap, out rc);
                if (collect != null)
                    foreach (Candidate pr in pairs) collect.Add(new Match { I = e.I, J = e.J, A = pr.Pa, B = pr.Pb, Gap = pr.Gap });
                return rc + (pairs.Count == 0 ? a * 4 : 0);   // a shared edge nobody can walk across
            }
            var rng = new Random(7);
            List<int> bestRot = null;
            double bestCost = double.MaxValue;
            evaluations = 0;
            for (int start = 0; start < 12; start++)
            {
                var rot = new List<int> { 0 };
                for (int i = 1; i < count; i++) rot.Add(start == 0 ? 0 : rng.Next(6));
                for (int pass = 0; pass < 8; pass++)
                {
                    bool changed = false;
                    for (int i = 1; i < count; i++)
                    {
                        int keep = rot[i], pick = keep;
                        double cheapest = double.MaxValue;
                        for (int turn = 0; turn < 6; turn++)
                        {
                            rot[i] = turn;
                            double c = 0;
                            foreach (Adjacency e in adj) if (e.I == i || e.J == i) c += EdgeCost(e, rot, null);
                            evaluations++;
                            if (c < cheapest - 1e-9) { cheapest = c; pick = turn; }
                        }
                        rot[i] = pick;
                        if (pick != keep) changed = true;
                    }
                    if (!changed) break;
                }
                double total = 0;
                foreach (Adjacency e in adj) total += EdgeCost(e, rot, null);
                if (total < bestCost) { bestCost = total; bestRot = new List<int>(rot); }
            }
            var matches = new List<Match>();
            double cost = 0;
            foreach (Adjacency e in adj) cost += EdgeCost(e, bestRot, matches);
            return new Best { Rot = bestRot, Cost = cost, Matches = matches };
        }

        private static double[] Unit(double x, double z)
        {
            double l = JsMath.OrOne(JsMath.Hypot(x, z));
            return new[] { x / l, z / l };
        }
    }
}
