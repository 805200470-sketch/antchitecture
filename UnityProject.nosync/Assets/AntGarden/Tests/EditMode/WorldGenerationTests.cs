using System.Collections.Generic;
using System.IO;
using AntGarden.Core;
using NUnit.Framework;
using UnityEngine;

namespace AntGarden.Tests
{
    /// <summary>
    /// World generation invariants on the real path data: every module builds, the ant spawns on walkable
    /// floor, exits sit on the outer rim, and stitched worlds are one connected walkable area.
    /// </summary>
    public class WorldGenerationTests
    {
        static string PathsDir => Path.Combine(Application.streamingAssetsPath, "Paths");

        static NetworkData Load(string id) => NetworkData.Parse(File.ReadAllText(Path.Combine(PathsDir, id + ".json")));

        static IEnumerable<string> Modules()
        {
            for (int i = 1; i <= 13; i++) yield return $"path-{i:000}";
        }

        [TestCaseSource(nameof(Modules))]
        public void Module_SpawnIsWalkable_AndExitsAreOnTheRim(string id)
        {
            var field = new PathField(Load(id), PathConfig.Default());
            var sp = field.SpawnPoint();
            Assert.Less(field.Dist(sp.x, sp.z), -0.5, $"{id}: spawn point is not on walkable floor");
            Assert.Greater(field.Edges.Count, 0);
            foreach (Exit ex in field.Exits)
                Assert.Greater(field.HexDist(ex.X, ex.Z), -8.0, $"{id}: exit at ({ex.X:F1}, {ex.Z:F1}) is not near the frame");
        }

        static readonly string[] Presets =
        {
            "row:path-010,path-013",
            "row:path-006,path-010,path-013",
            "cluster:path-001,path-002,path-003",
        };

        [TestCaseSource(nameof(Presets))]
        public void Stitched_World_IsOneConnectedWalkableArea(string preset)
        {
            string[] parts = preset.Split(':');
            string[] ids = parts[1].Split(',');
            var nets = new List<NetworkData>();
            foreach (string id in ids) nets.Add(Load(id));
            var (field, report) = Stitcher.Stitch(nets, ids, PathConfig.Default(), parts[0]);
            Assert.GreaterOrEqual(report.Connectors, ids.Length - 1, "every shared edge needs a passage");

            // flood fill the walkable grid from the spawn point
            const double step = 0.8, margin = 0.7;
            Bounds2 b = field.Bounds;
            int nx = (int)((b.MaxX - b.MinX) / step) + 1, nz = (int)((b.MaxZ - b.MinZ) / step) + 1;
            var ok = new bool[nx * nz];
            var seen = new bool[nx * nz];
            int total = 0;
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                ok[j * nx + i] = field.Dist(b.MinX + i * step, b.MinZ + j * step) < -margin;
                if (ok[j * nx + i]) total++;
            }
            var sp = field.SpawnPoint();
            int start = Nearest(ok, nx, nz, (int)((sp.x - b.MinX) / step), (int)((sp.z - b.MinZ) / step));
            Assert.GreaterOrEqual(start, 0, "no walkable cell near the spawn point");
            var stack = new Stack<int>();
            stack.Push(start);
            seen[start] = true;
            int reached = 1;
            while (stack.Count > 0)
            {
                int k = stack.Pop(), i = k % nx, j = k / nx;
                foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int ii = i + di, jj = j + dj;
                    if (ii < 0 || jj < 0 || ii >= nx || jj >= nz) continue;
                    int kk = jj * nx + ii;
                    if (!ok[kk] || seen[kk]) continue;
                    seen[kk] = true;
                    reached++;
                    stack.Push(kk);
                }
            }
            Assert.Greater(reached / (double)total, 0.99, $"{preset}: only {reached}/{total} walkable cells are reachable");
        }

        static int Nearest(bool[] ok, int nx, int nz, int ci, int cj)
        {
            for (int r = 0; r < 8; r++)
                for (int dj = -r; dj <= r; dj++)
                for (int di = -r; di <= r; di++)
                {
                    int i = ci + di, j = cj + dj;
                    if (i >= 0 && j >= 0 && i < nx && j < nz && ok[j * nx + i]) return j * nx + i;
                }
            return -1;
        }
    }
}
