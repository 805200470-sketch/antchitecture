using System.Collections.Generic;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// One splat of a solid thing on the particle nest's floor (Shaders/ParticleSolid.shader): where it is, which way
    /// its surface faces (octahedral, two 16-bit halves), its colour (gamma bytes of a linear albedo; the fourth byte how
    /// much it glows of itself) and its radius in units. 24 bytes.
    /// </summary>
    public struct SolidPoint
    {
        public Vector3 pos;
        public uint nrm;
        public uint col;
        public float radius;
    }

    /// <summary>
    /// What the floor's things are drawn with (NestThingsLitter, NestThingsGrowth): opaque splats over their surfaces,
    /// about PerArea of them per unit² of surface, each lit by the shader from its normal, so a thing reads as a solid
    /// form, the way a Gaussian-splat capture shows one. Begin starts a thing with its own random sequence (so it is the
    /// same every time it is made); R() is that sequence; Count, Radius and Put draw it. Plain arithmetic only, nothing
    /// allocated but the splats: a worker thread may run it.
    /// </summary>
    public sealed class ThingPen
    {
        public readonly List<SolidPoint> into;
        /// <summary>Splats per unit² of a thing's surface (the ant is 1 unit long).</summary>
        public float PerArea = 1400f;
        uint s = 1;

        /// <summary>When set (alpha > 0), every splat's colour is drawn toward this one, its own light and dark kept:
        /// the nest's palette over each thing's own colours, so the floor is of a piece with the rest.</summary>
        public Color Tint = Color.clear;
        /// <summary>How much of a splat's own hue is kept under the tint (0..1).</summary>
        public float Keep = 0.2f;

        public ThingPen(List<SolidPoint> into) { this.into = into; }

        /// <summary>Starts a thing: its own random sequence from this seed.</summary>
        public void Begin(uint seed)
        {
            s = seed * 747796405u + 2891336453u;
            if (s == 0) s = 1;
        }

        /// <summary>The next number of the thing's random sequence, in [0, 1).</summary>
        public float R()
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return (s & 0xFFFFFF) / 16777216f;
        }

        /// <summary>How many splats cover this much surface (unit²) at PerArea, at least min.</summary>
        public int Count(float area, int min = 8) => Mathf.Max(min, Mathf.RoundToInt(area * PerArea));

        /// <summary>A splat's radius that covers a surface at PerArea, varied the way a capture's are: most near the
        /// middle size, some smaller, a few much larger (times scale).</summary>
        public float Radius(float scale = 1f)
        {
            float g = (R() + R() + R() - 1.5f) * 1.6f;   // roughly normal, -2.4..2.4
            return 0.62f / Mathf.Sqrt(PerArea) * scale * Mathf.Exp(g * 0.42f);
        }

        /// <summary>One splat: its centre, the surface's normal there (any length), its albedo (linear rgb, about
        /// 0.02..0.6; the shader lights it), its radius (units) and how much it glows of itself (0..1: bioluminescence,
        /// sparingly).</summary>
        public void Put(Vector3 pos, Vector3 normal, Color albedo, float radius, float glow = 0f)
        {
            if (Tint.a > 0)
            {
                float lum = albedo.r * 0.3f + albedo.g * 0.55f + albedo.b * 0.15f;
                albedo = Color.Lerp(Tint * (lum * 2.1f), albedo, Keep);
            }
            into.Add(new SolidPoint { pos = pos, nrm = EncodeNormal(normal), col = Pack(albedo, glow), radius = radius });
        }

        /// <summary>A normal from its two 16-bit halves (EncodeNormal's inverse).</summary>
        public static Vector3 DecodeNormal(uint e)
        {
            float x = (e & 0xFFFF) / 65535f * 2 - 1, z = (e >> 16) / 65535f * 2 - 1;
            var n = new Vector3(x, 1 - Mathf.Abs(x) - Mathf.Abs(z), z);
            if (n.y < 0)
            {
                float ox = (1 - Mathf.Abs(n.z)) * (n.x >= 0 ? 1 : -1), oz = (1 - Mathf.Abs(n.x)) * (n.z >= 0 ? 1 : -1);
                n.x = ox;
                n.z = oz;
            }
            return n.normalized;
        }

        /// <summary>A splat as the solid shader takes it, from parts already worked out (packed colour and normal).</summary>
        public static SolidPoint Splat(Vector3 pos, Vector3 normal, Color albedo, float radius, float glow) =>
            new SolidPoint { pos = pos, nrm = EncodeNormal(normal), col = Pack(albedo, glow), radius = radius };

        /// <summary>A normal as two 16-bit halves (octahedral).</summary>
        public static uint EncodeNormal(Vector3 n)
        {
            float l = Mathf.Abs(n.x) + Mathf.Abs(n.y) + Mathf.Abs(n.z);
            if (l < 1e-8f) return EncodeNormal(Vector3.up);
            float x = n.x / l, y = n.z / l;   // the plane facing up (y), folded below it
            if (n.y < 0)
            {
                float ox = (1 - Mathf.Abs(y)) * (x >= 0 ? 1 : -1), oy = (1 - Mathf.Abs(x)) * (y >= 0 ? 1 : -1);
                x = ox;
                y = oy;
            }
            uint a = (uint)Mathf.Clamp(Mathf.RoundToInt((x * 0.5f + 0.5f) * 65535f), 0, 65535);
            uint b = (uint)Mathf.Clamp(Mathf.RoundToInt((y * 0.5f + 0.5f) * 65535f), 0, 65535);
            return a | (b << 16);
        }

        static uint Pack(Color c, float glow)
        {
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(ToGamma(c.r) * 255f), 0, 255);
            uint g = (uint)Mathf.Clamp(Mathf.RoundToInt(ToGamma(c.g) * 255f), 0, 255);
            uint b = (uint)Mathf.Clamp(Mathf.RoundToInt(ToGamma(c.b) * 255f), 0, 255);
            uint a = (uint)Mathf.Clamp(Mathf.RoundToInt(glow * 255f), 0, 255);
            return r | (g << 8) | (b << 16) | (a << 24);
        }

        static float ToGamma(float x) => x <= 0.0031308f ? 12.92f * x : 1.055f * (float)System.Math.Pow(x, 1 / 2.4) - 0.055f;
    }
}
