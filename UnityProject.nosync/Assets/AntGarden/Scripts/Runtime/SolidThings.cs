using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// The particle nest's floor at ant scale: the things lying and growing on it, each drawn as opaque lit splats
    /// (Shaders/ParticleSolid.shader) so it reads as what it is: fallen leaves, twigs, petals, seeds, fern fronds,
    /// feathers and the odd fallen blossom where litter gathers (NestThingsLitter); moss cushions, clover, mushrooms,
    /// seedlings and lichen where it is damp (NestThingsGrowth); pebbles, crystal grains and snail shells where it is
    /// stony; and roots creeping out from the foot of the walls. At ant scale they are big: a leaf is several ants
    /// long, a twig a log. They gather along the ways the ant is led, never on them (the glowing way stays clear),
    /// each kind in its own patches; flat things drape over the floor; tall ones block the ants. Made once, when the
    /// nest is built; far away the shader thins them out.
    /// </summary>
    public class SolidThings : MonoBehaviour
    {
        public int maxThings = 460;
        public float nearDensity = 0.3f;    // things per unit² of floor along the ways
        public float farDensity = 0.07f;    // and far from them
        public int roots = 44;
        public float perArea = 2200f;       // splats per unit² of a thing's surface
        public float gain = 0.5f;           // night: the things are seen by moonlight, darker than the glowing plants
        public Vector4 lod = new Vector4(14f, 48f, 0.28f, 0);   // all splats within x units, thinned to z of them by y

        enum Kind { Leaf, Twig, Petal, Seed, Fern, Feather, Blossom, Moss, Clover, Mushrooms, Seedling, Lichen, Pebbles, Crystals, Shell }

        World world;
        ParticleLook look;
        Material mat;
        ComputeBuffer buffer;
        int count;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        uint rs = 0x9E3779B9u;

        public int Count => count;

        public void Init(World w, ParticleLook particles, Material material)
        {
            world = w;
            look = particles;
            mat = material;
            if (mat == null || world == null || look == null || !look.HasMaps) { enabled = false; return; }
            var splats = new List<SolidPoint>(1 << 20);
            var pen = new ThingPen(splats) { PerArea = perArea };
            float t0 = Time.realtimeSinceStartup;
            int things = Place(pen), rootsMade = Roots(pen);
            count = splats.Count;
            if (count == 0) { enabled = false; return; }
            buffer = new ComputeBuffer(count, 24);
            buffer.SetData(splats);
            Debug.Log($"[AntGarden] solid things: {things} things and {rootsMade} roots, {count} splats ({Time.realtimeSinceStartup - t0:F2}s)");
        }

        float R()
        {
            rs ^= rs << 13;
            rs ^= rs >> 17;
            rs ^= rs << 5;
            return (rs & 0xFFFFFF) / 16777216f;
        }

        /// <summary>Places the things: patches of litter, of damp growth and of stones along the ways, never on them.</summary>
        int Place(ThingPen pen)
        {
            var occupied = new CircleGrid(2f);
            int made = 0;
            for (int tries = 0; tries < maxThings * 120 && made < maxThings; tries++)
            {
                float x = Mathf.Lerp(world.minX, world.maxX, R()), z = Mathf.Lerp(world.minZ, world.maxZ, R());
                if (world.HexDist(x, z) > -1.5f || world.Dist(x, z) > -0.15f) continue;   // on the canyon floor
                if (look.LayerAt(x, z).r < 0.55f) continue;
                if (!float.IsInfinity(world.Ceiling(x, z))) continue;                       // not under a tunnel roof
                float focus = look.Focus(x, z);
                if (R() > Mathf.Lerp(farDensity, nearDensity, focus) / nearDensity) continue;
                // which patch: litter, damp growth or stones (each in its own broad patches)
                float litter = Noise(x * 0.12f + 3.1f, z * 0.12f - 7.7f), damp = Noise(x * 0.1f - 11.3f, z * 0.1f + 4.9f), stony = Noise(x * 0.14f + 21.7f, z * 0.14f + 13.1f);
                float wl = litter * litter * 1.3f + 0.12f, wd = damp * damp * 1.2f + 0.1f, ws = stony * stony * stony * 1.1f + 0.05f;
                float pick = R() * (wl + wd + ws);
                Kind kind = pick < wl ? Pick(KLitter, WLitter) : pick < wl + wd ? Pick(KDamp, WDamp) : Pick(KStony, WStony);
                float size = Size(kind);
                float r = Footprint(kind, size);
                // clear of the way, of the plants and props, of each other, and on fairly flat floor
                if (look.WayDistance(x, z) < r + 0.7f) continue;
                if (world.reserved.Overlaps(x, z, r * 0.8f) || world.colliders.Overlaps(x, z, r * 0.6f) || occupied.Overlaps(x, z, r * 0.85f)) continue;
                float h0 = look.HeightAt(x, z), lo = h0, hi = h0;
                for (int k = 0; k < 6; k++)
                {
                    float a = k * 1.0472f, hh = look.HeightAt(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r);
                    lo = Mathf.Min(lo, hh);
                    hi = Mathf.Max(hi, hh);
                }
                if (hi - lo > Mathf.Max(0.12f, r * 0.45f)) continue;
                occupied.Add(new Circle(x, z, r, 1f));
                Make(pen, kind, x, z, size, (uint)made * 2654435761u ^ 0x5bd1e995u);
                Block(kind, x, z, size);
                made++;
            }
            return made;
        }

        static readonly Kind[] KLitter = { Kind.Leaf, Kind.Twig, Kind.Petal, Kind.Seed, Kind.Fern, Kind.Feather, Kind.Blossom, Kind.Pebbles };
        static readonly float[] WLitter = { 40, 16, 8, 12, 10, 4, 3, 5 };
        static readonly Kind[] KDamp = { Kind.Moss, Kind.Clover, Kind.Mushrooms, Kind.Seedling, Kind.Lichen, Kind.Pebbles, Kind.Shell, Kind.Leaf };
        static readonly float[] WDamp = { 30, 18, 14, 12, 10, 5, 5, 8 };
        static readonly Kind[] KStony = { Kind.Pebbles, Kind.Crystals, Kind.Lichen, Kind.Shell, Kind.Seed, Kind.Twig };
        static readonly float[] WStony = { 34, 20, 20, 10, 10, 6 };

        Kind Pick(Kind[] kinds, float[] weights)
        {
            float sum = 0;
            foreach (float w in weights) sum += w;
            float r = R() * sum;
            for (int i = 0; i < kinds.Length; i++) if ((r -= weights[i]) < 0) return kinds[i];
            return kinds[kinds.Length - 1];
        }

        /// <summary>A thing's size at ant scale (the ant is 1 long): its length, diameter or height.</summary>
        float Size(Kind k)
        {
            float u = R();
            switch (k)
            {
                case Kind.Leaf: return 1.6f + 2.2f * u;
                case Kind.Twig: return 2f + 3f * u;
                case Kind.Petal: return 0.8f + 1.2f * u;
                case Kind.Seed: return 0.45f + 0.45f * u;
                case Kind.Fern: return 2f + 2f * u;
                case Kind.Feather: return 1.6f + 1.8f * u;
                case Kind.Blossom: return 1.1f + 1.1f * u;
                case Kind.Moss: return 0.7f + 1.3f * u;
                case Kind.Clover: return 0.6f + 0.8f * u;
                case Kind.Mushrooms: return 0.55f + 1.2f * u;
                case Kind.Seedling: return 0.9f + 1.2f * u;
                case Kind.Lichen: return 0.6f + 0.9f * u;
                case Kind.Pebbles: return 0.3f + 0.65f * u;
                case Kind.Crystals: return 0.18f + 0.3f * u;
                default: return 0.6f + 0.7f * u;   // shell
            }
        }

        static float Footprint(Kind k, float size)
        {
            switch (k)
            {
                case Kind.Clover: return size * 1.05f;
                case Kind.Mushrooms: return size * 0.45f;
                case Kind.Seedling: return size * 0.38f;
                case Kind.Pebbles: return size * 0.9f;
                case Kind.Crystals: return size * 1.3f;
                default: return size * 0.52f;
            }
        }

        /// <summary>Tall things block the ants (the flat ones are walked over).</summary>
        void Block(Kind k, float x, float z, float size)
        {
            float r = 0, h = 0;
            switch (k)
            {
                case Kind.Mushrooms: r = size * 0.22f; h = size; break;
                case Kind.Seedling: r = size * 0.1f; h = size; break;
                case Kind.Shell: r = size * 0.42f; h = size * 0.8f; break;
                case Kind.Pebbles: if (size > 0.5f) { r = size * 0.45f; h = size * 0.5f; } break;
                case Kind.Moss: if (size > 1.3f) { r = size * 0.35f; h = size * 0.35f; } break;
            }
            if (r > 0) world.colliders.Add(new Circle(x, z, r, h));
        }

        void Make(ThingPen pen, Kind k, float x, float z, float size, uint seed)
        {
            Vector3 n = world.Normal(x, z), ta = Vector3.Cross(n, Vector3.forward).normalized;
            if (ta.sqrMagnitude < 0.5f) ta = Vector3.Cross(n, Vector3.right).normalized;
            Vector3 tb = Vector3.Cross(ta, n);
            var p = new Vector3(x, look.HeightAt(x, z), z);
            int from = pen.into.Count;
            pen.Begin(seed);
            switch (k)
            {
                case Kind.Leaf: NestThingsLitter.FallenLeaf(pen, p, n, ta, tb, size); break;
                case Kind.Twig: NestThingsLitter.Twig(pen, p, n, ta, tb, size); break;
                case Kind.Petal: NestThingsLitter.Petal(pen, p, n, ta, tb, size); break;
                case Kind.Seed: NestThingsLitter.Seed(pen, p, n, ta, tb, size); break;
                case Kind.Fern: NestThingsLitter.FernFrond(pen, p, n, ta, tb, size); break;
                case Kind.Feather: NestThingsLitter.Feather(pen, p, n, ta, tb, size); break;
                case Kind.Blossom: NestThingsLitter.FallenBlossom(pen, p, n, ta, tb, size); break;
                case Kind.Moss: NestThingsGrowth.MossCushion(pen, p, n, ta, tb, size); break;
                case Kind.Clover: NestThingsGrowth.CloverPatch(pen, p, n, ta, tb, size); break;
                case Kind.Mushrooms: NestThingsGrowth.Mushrooms(pen, p, n, ta, tb, size); break;
                case Kind.Seedling: NestThingsGrowth.Seedling(pen, p, n, ta, tb, size); break;
                case Kind.Lichen: NestThingsGrowth.LichenCrust(pen, p, n, ta, tb, size); break;
                case Kind.Pebbles: NestThingsGrowth.Pebbles(pen, p, n, ta, tb, size); break;
                case Kind.Crystals: NestThingsGrowth.Crystals(pen, p, n, ta, tb, size); break;
                default: NestThingsGrowth.SnailShell(pen, p, n, ta, tb, size); break;
            }
            // flat things drape over the floor: each splat keeps its height above the plane through p, over the floor
            // under it (so a big leaf follows the ground's rise and fall instead of floating or sinking)
            if (k == Kind.Leaf || k == Kind.Petal || k == Kind.Fern || k == Kind.Feather || k == Kind.Blossom || k == Kind.Lichen)
                for (int i = from; i < pen.into.Count; i++)
                {
                    SolidPoint sp = pen.into[i];
                    Vector3 d = sp.pos - p;
                    float above = Vector3.Dot(d, n);
                    sp.pos.y = look.HeightAt(sp.pos.x, sp.pos.z) + above;
                    pen.into[i] = sp;
                }
        }

        /// <summary>
        /// Roots creeping out from the foot of the walls across the floor: each a tapering tube wandering over the
        /// ground (its upper half drawn), bark brown-grey, with a few fine rootlets and pale tips; they keep off the way.
        /// </summary>
        int Roots(ThingPen pen)
        {
            int made = 0;
            for (int tries = 0; tries < roots * 200 && made < roots; tries++)
            {
                float x = Mathf.Lerp(world.minX, world.maxX, R()), z = Mathf.Lerp(world.minZ, world.maxZ, R());
                if (world.HexDist(x, z) > -1.5f || !float.IsInfinity(world.Ceiling(x, z))) continue;
                Color lw = look.LayerAt(x, z);
                if (lw.g < 0.45f || lw.r < 0.15f) continue;   // at the foot of a wall
                if (R() > 0.15f + 0.85f * look.Focus(x, z) || look.WayDistance(x, z) < 1.2f) continue;
                pen.Begin((uint)made * 2246822519u ^ 0x27d4eb2fu);
                // into the canyon: down the slope of the distance field
                float e = 0.3f, gx = world.Dist(x + e, z) - world.Dist(x - e, z), gz = world.Dist(x, z + e) - world.Dist(x, z - e);
                float dir = Mathf.Atan2(-gz, -gx) + (pen.R() - 0.5f) * 1.4f;
                Root(pen, x, z, dir, 2.2f + 3.8f * pen.R(), 0.07f + 0.08f * pen.R(), 0);
                made++;
            }
            return made;
        }

        void Root(ThingPen pen, float x, float z, float dir, float length, float radius, int depth)
        {
            float step = Mathf.Max(0.012f, radius * 0.5f);
            int steps = Mathf.Max(4, Mathf.RoundToInt(length / step));
            float curl = (pen.R() - 0.5f) * 0.05f;
            Color bark = Color.Lerp(new Color(0.14f, 0.1f, 0.07f), new Color(0.11f, 0.12f, 0.13f), pen.R()), tip = new Color(0.42f, 0.36f, 0.27f);
            for (int i = 0; i < steps; i++)
            {
                float u = i / (float)steps;
                dir += curl + (pen.R() - 0.5f) * 0.12f;
                float cs = Mathf.Cos(dir), sn = Mathf.Sin(dir);
                float nx = x + cs * step, nz = z + sn * step;
                if (look.WayDistance(nx, nz) < 0.7f || world.HexDist(nx, nz) > -0.8f) break;
                x = nx;
                z = nz;
                float r = radius * (1 - 0.85f * u) + 0.008f;
                float y = look.HeightAt(x, z) + r * 0.8f;
                var along = new Vector3(cs, 0, sn);
                var side = new Vector3(-sn, 0, cs);
                Color c = Color.Lerp(bark, tip, Mathf.Clamp01((u - 0.82f) / 0.18f));
                // its upper half, ridged along its length
                int around = 3 + Mathf.RoundToInt(r * 60f);
                for (int k = 0; k < around; k++)
                {
                    float a = (k + pen.R()) / around * Mathf.PI;
                    Vector3 nrm = side * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a);
                    float ridge = 0.85f + 0.3f * Mathf.Abs(Mathf.Sin(a * 7f + i * 0.35f));
                    pen.Put(new Vector3(x, y, z) + nrm * r + along * ((pen.R() - 0.5f) * step), nrm, c * ridge, Mathf.Max(pen.Radius(0.7f), r * 0.55f));
                }
                if (depth < 2 && pen.R() < 0.025f)
                    Root(pen, x, z, dir + (pen.R() < 0.5f ? 1f : -1f) * (0.6f + 0.5f * pen.R()), length * (0.2f + 0.3f * pen.R()) * (1 - u), r * 0.55f, depth + 1);
            }
        }

        void Update()
        {
            if (count == 0 || buffer == null) return;
            block.SetBuffer("_Solid", buffer);
            block.SetFloat("_Gain", gain);
            block.SetVector("_Lod", lod);
            block.SetVector("_MoonDir", new Vector4(0.32f, 0.9f, 0.28f, 0).normalized);
            block.SetVector("_MoonColor", new Color(0.5f, 0.62f, 0.9f));
            block.SetVector("_FillColor", new Color(0.09f, 0.12f, 0.19f));
            var bounds = new Bounds(new Vector3((world.minX + world.maxX) / 2, 0, (world.minZ + world.maxZ) / 2),
                new Vector3(world.maxX - world.minX + 40, 200, world.maxZ - world.minZ + 40));
            Graphics.DrawProcedural(mat, bounds, MeshTopology.Triangles, count * 3, 1, null, block, ShadowCastingMode.Off, false, 0);
        }

        void OnDestroy() => buffer?.Release();

        static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}
