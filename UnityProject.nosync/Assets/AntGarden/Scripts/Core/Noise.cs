// Ant Garden core — port of src/noise.js: seeded random numbers and noise (simplex 2D/3D, fbm,
// cellular). Everything is deterministic for a given seed and reproduces the JavaScript values
// bit for bit (integer hashing with 32-bit wrap-around, Uint8 permutation tables).
using System;
using System.Collections.Generic;

namespace AntGarden.Core
{
    /// <summary>mulberry32 PRNG (noise.js <c>mulberry32</c>): one uniform double in [0, 1) per call.</summary>
    public sealed class Mulberry32
    {
        private uint _a;

        /// <summary>Seeds like JavaScript <c>seed &gt;&gt;&gt; 0</c> (truncated, wrapped to 32 bits).</summary>
        public Mulberry32(double seed)
        {
            _a = JsMath.ToUint32(seed);
        }

        /// <summary>Next value in [0, 1).</summary>
        public double Next()
        {
            unchecked
            {
                _a += 0x6d2b79f5u;
                uint t = _a;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }

    /// <summary>Small seeded RNG wrapper (noise.js <c>Rng</c>), same sequence as JavaScript.</summary>
    public sealed class Rng
    {
        private readonly Mulberry32 _next;

        /// <summary>Creates the generator; <paramref name="seed"/> is wrapped like <c>seed &gt;&gt;&gt; 0</c>.</summary>
        public Rng(double seed)
        {
            _next = new Mulberry32(seed);
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double Next()
        {
            return _next.Next();
        }

        /// <summary>Uniform double in [a, b).</summary>
        public double Float(double a = 0, double b = 1)
        {
            return a + (b - a) * Next();
        }

        /// <summary>Uniform integer in [a, b] (JS <c>Math.floor(this.float(a, b + 1))</c>).</summary>
        public int Int(int a, int b)
        {
            return (int)Math.Floor(Float(a, b + 1.0));
        }

        /// <summary>A uniformly chosen element.</summary>
        public T Pick<T>(IList<T> list)
        {
            return list[(int)Math.Floor(Next() * list.Count)];
        }

        /// <summary>True with probability <paramref name="p"/>.</summary>
        public bool Chance(double p)
        {
            return Next() < p;
        }

        /// <summary>Standard normal sample (Box–Muller).</summary>
        public double Gauss()
        {
            double u = Math.Max(1e-9, Next());
            double v = Next();
            return Math.Sqrt(-2 * JsMath.Log(u)) * JsMath.Cos(2 * Math.PI * v);
        }
    }

    /// <summary>Result of <see cref="Noise.Cellular2"/>.</summary>
    public struct CellularResult
    {
        /// <summary>Distance to the nearest feature point (cell units).</summary>
        public double F1;
        /// <summary>Distance to the second-nearest feature point.</summary>
        public double F2;
        /// <summary>Random value tied to the nearest cell.</summary>
        public double R1;
        /// <summary>Second random value tied to the nearest cell.</summary>
        public double R2;
    }

    /// <summary>Seeded simplex noise with fbm helpers (noise.js <c>Noise</c>). Output range ≈ [-1, 1].</summary>
    /// <remarks>Immutable after construction: safe to call from several threads at once.</remarks>
    public sealed class Noise
    {
        // GRAD3 is a Float32Array in JS, but every entry is 0 or ±1, so doubles are exact.
        private static readonly double[] Grad3 =
        {
            1, 1, 0, -1, 1, 0, 1, -1, 0, -1, -1, 0,
            1, 0, 1, -1, 0, 1, 1, 0, -1, -1, 0, -1,
            0, 1, 1, 0, -1, 1, 0, 1, -1, 0, -1, -1,
        };

        private static readonly double F2 = 0.5 * (Math.Sqrt(3) - 1);
        private static readonly double G2 = (3 - Math.Sqrt(3)) / 6;
        private const double F3 = 1.0 / 3;
        private const double G3 = 1.0 / 6;

        private readonly byte[] _perm = new byte[512];
        private readonly byte[] _permMod12 = new byte[512];

        /// <summary>Builds the permutation table from mulberry32(<paramref name="seed"/>).</summary>
        public Noise(double seed)
        {
            var rnd = new Mulberry32(seed);
            var p = new byte[256];
            for (int i = 0; i < 256; i++) p[i] = (byte)i;
            for (int i = 255; i > 0; i--)
            {
                int j = (int)Math.Floor(rnd.Next() * (i + 1));
                byte t = p[i];
                p[i] = p[j];
                p[j] = t;
            }
            for (int i = 0; i < 512; i++)
            {
                _perm[i] = p[i & 255];
                _permMod12[i] = (byte)(_perm[i] % 12);
            }
        }

        /// <summary>2D simplex noise.</summary>
        public double Noise2(double xin, double yin)
        {
            byte[] perm = _perm;
            byte[] pm = _permMod12;
            double s = (xin + yin) * F2;
            double i = Math.Floor(xin + s);
            double j = Math.Floor(yin + s);
            double t = (i + j) * G2;
            double x0 = xin - (i - t);
            double y0 = yin - (j - t);
            int i1 = x0 > y0 ? 1 : 0;
            int j1 = x0 > y0 ? 0 : 1;
            double x1 = x0 - i1 + G2;
            double y1 = y0 - j1 + G2;
            double x2 = x0 - 1 + 2 * G2;
            double y2 = y0 - 1 + 2 * G2;
            // JS `i & 255`: ToInt32 of an integral double (inline fast path, JsMath.ToInt32 otherwise)
            int ii = (i >= -2147483648.0 && i <= 2147483647.0 ? (int)i : JsMath.ToInt32(i)) & 255;
            int jj = (j >= -2147483648.0 && j <= 2147483647.0 ? (int)j : JsMath.ToInt32(j)) & 255;
            double n0 = 0;
            double n1 = 0;
            double n2 = 0;
            double t0 = 0.5 - x0 * x0 - y0 * y0;
            if (t0 > 0)
            {
                int g = pm[ii + perm[jj]] * 3;
                t0 *= t0;
                n0 = t0 * t0 * (Grad3[g] * x0 + Grad3[g + 1] * y0);
            }
            double t1 = 0.5 - x1 * x1 - y1 * y1;
            if (t1 > 0)
            {
                int g = pm[ii + i1 + perm[jj + j1]] * 3;
                t1 *= t1;
                n1 = t1 * t1 * (Grad3[g] * x1 + Grad3[g + 1] * y1);
            }
            double t2 = 0.5 - x2 * x2 - y2 * y2;
            if (t2 > 0)
            {
                int g = pm[ii + 1 + perm[jj + 1]] * 3;
                t2 *= t2;
                n2 = t2 * t2 * (Grad3[g] * x2 + Grad3[g + 1] * y2);
            }
            return 70 * (n0 + n1 + n2);
        }

        /// <summary>3D simplex noise.</summary>
        public double Noise3(double xin, double yin, double zin)
        {
            byte[] perm = _perm;
            byte[] pm = _permMod12;
            double s = (xin + yin + zin) * F3;
            double i = Math.Floor(xin + s);
            double j = Math.Floor(yin + s);
            double k = Math.Floor(zin + s);
            double t = (i + j + k) * G3;
            double x0 = xin - (i - t);
            double y0 = yin - (j - t);
            double z0 = zin - (k - t);
            int i1, j1, k1, i2, j2, k2;
            if (x0 >= y0)
            {
                if (y0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
                else if (x0 >= z0) { i1 = 1; j1 = 0; k1 = 0; i2 = 1; j2 = 0; k2 = 1; }
                else { i1 = 0; j1 = 0; k1 = 1; i2 = 1; j2 = 0; k2 = 1; }
            }
            else if (y0 < z0) { i1 = 0; j1 = 0; k1 = 1; i2 = 0; j2 = 1; k2 = 1; }
            else if (x0 < z0) { i1 = 0; j1 = 1; k1 = 0; i2 = 0; j2 = 1; k2 = 1; }
            else { i1 = 0; j1 = 1; k1 = 0; i2 = 1; j2 = 1; k2 = 0; }
            double x1 = x0 - i1 + G3;
            double y1 = y0 - j1 + G3;
            double z1 = z0 - k1 + G3;
            double x2 = x0 - i2 + 2 * G3;
            double y2 = y0 - j2 + 2 * G3;
            double z2 = z0 - k2 + 2 * G3;
            double x3 = x0 - 1 + 3 * G3;
            double y3 = y0 - 1 + 3 * G3;
            double z3 = z0 - 1 + 3 * G3;
            int ii = (i >= -2147483648.0 && i <= 2147483647.0 ? (int)i : JsMath.ToInt32(i)) & 255;
            int jj = (j >= -2147483648.0 && j <= 2147483647.0 ? (int)j : JsMath.ToInt32(j)) & 255;
            int kk = (k >= -2147483648.0 && k <= 2147483647.0 ? (int)k : JsMath.ToInt32(k)) & 255;
            double n = 0;
            double t0 = 0.6 - x0 * x0 - y0 * y0 - z0 * z0;
            if (t0 > 0)
            {
                int g = pm[ii + perm[jj + perm[kk]]] * 3;
                t0 *= t0;
                n += t0 * t0 * (Grad3[g] * x0 + Grad3[g + 1] * y0 + Grad3[g + 2] * z0);
            }
            double t1 = 0.6 - x1 * x1 - y1 * y1 - z1 * z1;
            if (t1 > 0)
            {
                int g = pm[ii + i1 + perm[jj + j1 + perm[kk + k1]]] * 3;
                t1 *= t1;
                n += t1 * t1 * (Grad3[g] * x1 + Grad3[g + 1] * y1 + Grad3[g + 2] * z1);
            }
            double t2 = 0.6 - x2 * x2 - y2 * y2 - z2 * z2;
            if (t2 > 0)
            {
                int g = pm[ii + i2 + perm[jj + j2 + perm[kk + k2]]] * 3;
                t2 *= t2;
                n += t2 * t2 * (Grad3[g] * x2 + Grad3[g + 1] * y2 + Grad3[g + 2] * z2);
            }
            double t3 = 0.6 - x3 * x3 - y3 * y3 - z3 * z3;
            if (t3 > 0)
            {
                int g = pm[ii + 1 + perm[jj + 1 + perm[kk + 1]]] * 3;
                t3 *= t3;
                n += t3 * t3 * (Grad3[g] * x3 + Grad3[g + 1] * y3 + Grad3[g + 2] * z3);
            }
            return 32 * n;
        }

        /// <summary>Fractal sum of <see cref="Noise2"/> octaves, normalised by the total amplitude.</summary>
        public double Fbm2(double x, double y, int octaves = 4, double lacunarity = 2, double gain = 0.5)
        {
            double sum = 0;
            double amp = 1;
            double norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Noise2(x, y);
                norm += amp;
                amp *= gain;
                x *= lacunarity;
                y *= lacunarity;
            }
            return sum / norm;
        }

        /// <summary>Fractal sum of <see cref="Noise3"/> octaves, normalised by the total amplitude.</summary>
        public double Fbm3(double x, double y, double z, int octaves = 4, double lacunarity = 2, double gain = 0.5)
        {
            double sum = 0;
            double amp = 1;
            double norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Noise3(x, y, z);
                norm += amp;
                amp *= gain;
                x *= lacunarity;
                y *= lacunarity;
                z *= lacunarity;
            }
            return sum / norm;
        }

        /// <summary>Integer lattice hash → [0, 1) (noise.js <c>ihash</c>; arguments go through <c>| 0</c>).</summary>
        public static double IHash(double x, double y, double z, double seed)
        {
            return IHash(JsMath.ToInt32(x), JsMath.ToInt32(y), JsMath.ToInt32(z), JsMath.ToInt32(seed));
        }

        /// <summary>Integer lattice hash → [0, 1).</summary>
        public static double IHash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed ^ (uint)x * 374761393u ^ (uint)y * 668265263u ^ (uint)z * 1274126177u;
                h = (h ^ (h >> 13)) * 1103515245u;
                h ^= h >> 16;
                h *= 2246822519u;
                h ^= h >> 13;
                return h / 4294967296.0;
            }
        }

        /// <summary>
        /// 2D cellular (Worley) noise (noise.js <c>cellular2</c>): distances to the nearest and
        /// second-nearest feature point, plus two random values tied to the nearest cell.
        /// </summary>
        public static CellularResult Cellular2(double x, double y, double seed)
        {
            double ix = Math.Floor(x);
            double iy = Math.Floor(y);
            double f1 = 1e9;
            double f2 = 1e9;
            double cx = 0;
            double cy = 0;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    double gx = ix + dx;
                    double gy = iy + dy;
                    double px = gx + IHash(gx, gy, 0, seed);
                    double py = gy + IHash(gx, gy, 1, seed);
                    double ddx = px - x;
                    double ddy = py - y;
                    double d = Math.Sqrt(ddx * ddx + ddy * ddy);
                    if (d < f1)
                    {
                        f2 = f1;
                        f1 = d;
                        cx = gx;
                        cy = gy;
                    }
                    else if (d < f2)
                    {
                        f2 = d;
                    }
                }
            }
            return new CellularResult { F1 = f1, F2 = f2, R1 = IHash(cx, cy, 2, seed), R2 = IHash(cx, cy, 3, seed) };
        }
    }
}
