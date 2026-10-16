using System.Collections.Generic;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Spatial hash of every ant, rebuilt each frame, so crowding and encounters only look at neighbours
    /// (hundreds of ants would make an all-pairs check the most expensive thing in the frame).
    /// </summary>
    public class AntGrid
    {
        readonly float cell;
        readonly Dictionary<long, List<AntController>> cells = new Dictionary<long, List<AntController>>();
        readonly List<List<AntController>> used = new List<List<AntController>>();

        public AntGrid(float cell = 2f) { this.cell = cell; }

        static long Key(int i, int j) => ((long)i << 32) ^ (uint)j;

        public void Clear()
        {
            foreach (var list in used) list.Clear();
            used.Clear();
        }

        public void Add(AntController a)
        {
            Vector3 p = a.Position;
            long k = Key(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.z / cell));
            if (!cells.TryGetValue(k, out var list))
            {
                list = new List<AntController>(4);
                cells[k] = list;
            }
            if (list.Count == 0) used.Add(list);
            list.Add(a);
        }

        /// <summary>Fill <paramref name="result"/> with the ants in the 3×3 cells around (x, z).</summary>
        public void Near(float x, float z, List<AntController> result)
        {
            result.Clear();
            int i0 = Mathf.FloorToInt(x / cell), j0 = Mathf.FloorToInt(z / cell);
            for (int j = j0 - 1; j <= j0 + 1; j++)
            for (int i = i0 - 1; i <= i0 + 1; i++)
                if (cells.TryGetValue(Key(i, j), out var list) && list.Count > 0) result.AddRange(list);
        }
    }
}
