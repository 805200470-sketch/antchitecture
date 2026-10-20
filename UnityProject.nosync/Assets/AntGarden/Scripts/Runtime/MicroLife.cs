using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// The particle nest's small life (Shaders/MicroLife.shader): creatures much smaller than the ant but big enough to be
    /// seen, so the floor and the air are alive at a scale below the ant's: springtails wandering and now and then
    /// hopping, mites crawling, little swarms of midges hanging in the air near the plants, seed fluff drifting with the
    /// air. Placed once along the ways (never on them for the ones on the floor), in the palette's quiet colours; the
    /// GPU moves them, so they cost nothing on the CPU.
    /// </summary>
    public class MicroLife : MonoBehaviour
    {
        // none crawling on the floor: chains of beads there read as caterpillars (the ants are the floor's life)
        public int springtails = 0, mites = 0, swarms = 16, perSwarm = 9, fluff = 80;
        public float maxDistance = 26f;      // not drawn further from the camera than this
        public float gain = 1f;

        struct Creature { public Vector4 a, b; }   // a: home x, height (in the air), home z, kind; b: seed, size, range, colour

        Material mat;
        ComputeBuffer buffer;
        int count;
        World world;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly System.Random rng = new System.Random(2718);

        float R() => (float)rng.NextDouble();

        public void Init(World w, ParticleLook look, Material material)
        {
            world = w;
            mat = material;
            if (mat == null || look == null || !look.HasMaps) { enabled = false; return; }
            var list = new List<Creature>();
            // on the floor, near the ways but off them
            bool OnFloor(float x, float z, float minWay)
            {
                if (world.HexDist(x, z) > -1f || world.Dist(x, z) > -0.1f) return false;
                if (!float.IsInfinity(world.Ceiling(x, z))) return false;
                if (look.LayerAt(x, z).r < 0.5f) return false;
                if (R() > 0.15f + 0.85f * look.Focus(x, z)) return false;
                return look.WayDistance(x, z) > minWay;
            }
            Vector2 Somewhere() => new Vector2(Mathf.Lerp(world.minX, world.maxX, R()), Mathf.Lerp(world.minZ, world.maxZ, R()));
            for (int tries = 0, made = 0; made < springtails && tries < springtails * 200; tries++)
            {
                Vector2 p = Somewhere();
                if (!OnFloor(p.x, p.y, 0.5f)) continue;
                list.Add(new Creature { a = new Vector4(p.x, 0, p.y, 0), b = new Vector4(R() * 100, 0.2f + 0.14f * R(), 0.6f + 1.6f * R(), Pick(0, 1, 4, 6)) });
                made++;
            }
            for (int tries = 0, made = 0; made < mites && tries < mites * 200; tries++)
            {
                Vector2 p = Somewhere();
                if (!OnFloor(p.x, p.y, 0.4f)) continue;
                list.Add(new Creature { a = new Vector4(p.x, 0, p.y, 1), b = new Vector4(R() * 100, 0.09f + 0.06f * R(), 0.3f + 0.7f * R(), Pick(2, 3, 5, 7)) });
                made++;
            }
            for (int tries = 0, made = 0; made < swarms && tries < swarms * 200; tries++)
            {
                Vector2 p = Somewhere();
                if (!OnFloor(p.x, p.y, 0f)) continue;
                float h = 0.5f + 1.8f * R(), range = 0.6f + 0.8f * R();
                for (int i = 0; i < perSwarm; i++)
                    list.Add(new Creature { a = new Vector4(p.x, h, p.y, 2), b = new Vector4(R() * 100, 0.05f + 0.03f * R(), range, 0) });
                made++;
            }
            for (int tries = 0, made = 0; made < fluff && tries < fluff * 200; tries++)
            {
                Vector2 p = Somewhere();
                if (!OnFloor(p.x, p.y, 0f)) continue;
                list.Add(new Creature { a = new Vector4(p.x, 0.4f + 2.6f * R() * R(), p.y, 3), b = new Vector4(R() * 100, 0.16f + 0.12f * R(), 0, 0) });
                made++;
            }
            count = list.Count;
            if (count == 0) { enabled = false; return; }
            buffer = new ComputeBuffer(count, 32);
            buffer.SetData(list);
            Debug.Log($"[AntGarden] small life: {count} creatures");
        }

        /// <summary>One of the given places in the creatures' palette.</summary>
        float Pick(params int[] which) => which[(int)(R() * which.Length) % which.Length];

        void Update()
        {
            if (buffer == null) return;
            block.SetBuffer("_Creatures", buffer);
            block.SetFloat("_Gain", gain);
            block.SetFloat("_MaxDistance", maxDistance);
            block.SetVector("_MoonDir", new Vector4(0.32f, 0.9f, 0.28f, 0).normalized);
            block.SetVector("_MoonColor", new Color(0.66f, 0.72f, 0.72f));
            block.SetVector("_FillColor", new Color(0.13f, 0.18f, 0.18f));
            block.SetVector("_Wind", new Vector4(0.8f, 0.6f, 0, 0));
            // quiet colours from the card: silver-blue, bone, sage, dusty violet, ochre straw, teal-green, moss, coral
            var pal = new Vector4[]
            {
                new Color(0.62f, 0.70f, 0.72f).linear, new Color(0.80f, 0.80f, 0.74f).linear, new Color(0.48f, 0.53f, 0.42f).linear,
                new Color(0.49f, 0.43f, 0.59f).linear, new Color(0.71f, 0.60f, 0.35f).linear, new Color(0.30f, 0.45f, 0.42f).linear,
                new Color(0.45f, 0.51f, 0.31f).linear, new Color(0.70f, 0.46f, 0.48f).linear,
            };
            block.SetVectorArray("_Pal", pal);
            var bounds = new Bounds(new Vector3((world.minX + world.maxX) / 2, 0, (world.minZ + world.maxZ) / 2),
                new Vector3(world.maxX - world.minX + 40, 200, world.maxZ - world.minZ + 40));
            Graphics.DrawProcedural(mat, bounds, MeshTopology.Triangles, count * 36, 1, null, block, ShadowCastingMode.Off, false, 0);
        }

        void OnDestroy() => buffer?.Release();
    }
}
