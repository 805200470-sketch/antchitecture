// Ant Garden core — the sin / cos / atan / atan2 / log that JavaScript's Math uses in V8.
//
// V8 implements these with fdlibm 5.3 (src/base/ieee754.cc). This is a straight C# port, evaluated
// in plain IEEE double arithmetic, so it gives the same bits on every runtime (Mono, .NET, IL2CPP)
// instead of whatever the platform C library returns. It equals V8 exactly where V8 is compiled
// without fused multiply-add (x86-64). V8's Apple Silicon build is compiled with clang's default
// FP contraction, which turns some `a + b*c` into FMA; there about 0.5% of results differ from
// this port by one ulp (verified: the same code with those contractions matches V8 bit for bit).
//
// ====================================================
// Copyright (C) 1993-2004 by Sun Microsystems, Inc. All rights reserved.
//
// Permission to use, copy, modify, and distribute this
// software is freely granted, provided that this notice
// is preserved.
// ====================================================
using System;

namespace AntGarden.Core
{
    internal static class Fdlibm
    {
        private static int Hi(double x)
        {
            return (int)(BitConverter.DoubleToInt64Bits(x) >> 32);
        }

        private static uint Lo(double x)
        {
            return unchecked((uint)BitConverter.DoubleToInt64Bits(x));
        }

        private static double FromWords(int hi, uint lo)
        {
            return BitConverter.Int64BitsToDouble(((long)hi << 32) | lo);
        }

        // ---- atan / atan2 ------------------------------------------------------------------------

        private static readonly double[] AtanHi =
        {
            4.63647609000806093515e-01, // atan(0.5)hi 0x3FDDAC67, 0x0561BB4F
            7.85398163397448278999e-01, // atan(1.0)hi 0x3FE921FB, 0x54442D18
            9.82793723247329054082e-01, // atan(1.5)hi 0x3FEF730B, 0xD281F69B
            1.57079632679489655800e+00, // atan(inf)hi 0x3FF921FB, 0x54442D18
        };

        private static readonly double[] AtanLo =
        {
            2.26987774529616870924e-17, // atan(0.5)lo 0x3C7A2B7F, 0x222F65E2
            3.06161699786838301793e-17, // atan(1.0)lo 0x3C81A626, 0x33145C07
            1.39033110312309984516e-17, // atan(1.5)lo 0x3C700788, 0x7AF0CBBD
            6.12323399573676603587e-17, // atan(inf)lo 0x3C91A626, 0x33145C07
        };

        private static readonly double[] AT =
        {
            3.33333333333329318027e-01, // 0x3FD55555, 0x5555550D
            -1.99999999998764832476e-01, // 0xBFC99999, 0x9998EBC4
            1.42857142725034663711e-01, // 0x3FC24924, 0x920083FF
            -1.11111104054623557880e-01, // 0xBFBC71C6, 0xFE231671
            9.09088713343650656196e-02, // 0x3FB745CD, 0xC54C206E
            -7.69187620504482999495e-02, // 0xBFB3B0F2, 0xAF749A6D
            6.66107313738753120669e-02, // 0x3FB10D66, 0xA0D03D51
            -5.83357013379057348645e-02, // 0xBFADDE2D, 0x52DEFD9A
            4.97687799461593236017e-02, // 0x3FA97B4B, 0x24760DEB
            -3.65315727442169155270e-02, // 0xBFA2B444, 0x2C6A6C2F
            1.62858201153657823623e-02, // 0x3F90AD3A, 0xE322DA11
        };

        public static double Atan(double x)
        {
            int hx = Hi(x);
            int ix = hx & 0x7fffffff;
            int id;
            if (ix >= 0x44100000)
            {
                // |x| >= 2^66
                if (ix > 0x7ff00000 || (ix == 0x7ff00000 && Lo(x) != 0)) return x + x; // NaN
                return hx > 0 ? AtanHi[3] + AtanLo[3] : -AtanHi[3] - AtanLo[3];
            }
            if (ix < 0x3fdc0000)
            {
                // |x| < 0.4375
                if (ix < 0x3e400000) return x; // |x| < 2^-27
                id = -1;
            }
            else
            {
                x = Math.Abs(x);
                if (ix < 0x3ff30000)
                {
                    // |x| < 1.1875
                    if (ix < 0x3fe60000)
                    {
                        id = 0; // 7/16 <= |x| < 11/16
                        x = (2.0 * x - 1.0) / (2.0 + x);
                    }
                    else
                    {
                        id = 1; // 11/16 <= |x| < 19/16
                        x = (x - 1.0) / (x + 1.0);
                    }
                }
                else if (ix < 0x40038000)
                {
                    id = 2; // |x| < 2.4375
                    x = (x - 1.5) / (1.0 + 1.5 * x);
                }
                else
                {
                    id = 3; // 2.4375 <= |x| < 2^66
                    x = -1.0 / x;
                }
            }
            double z = x * x;
            double w = z * z;
            // break sum from i=0 to 10 aT[i]z**(i+1) into odd and even poly
            double s1 = z * (AT[0] + w * (AT[2] + w * (AT[4] + w * (AT[6] + w * (AT[8] + w * AT[10])))));
            double s2 = w * (AT[1] + w * (AT[3] + w * (AT[5] + w * (AT[7] + w * AT[9]))));
            if (id < 0) return x - x * (s1 + s2);
            z = AtanHi[id] - ((x * (s1 + s2) - AtanLo[id]) - x);
            return hx < 0 ? -z : z;
        }

        private const double Tiny = 1.0e-300;
        private const double PiO4 = 7.8539816339744827900e-01; // 0x3FE921FB, 0x54442D18
        private const double PiO2 = 1.5707963267948965580e+00; // 0x3FF921FB, 0x54442D18
        private const double Pi = 3.1415926535897931160e+00; // 0x400921FB, 0x54442D18
        private const double PiLo = 1.2246467991473531772e-16; // 0x3CA1A626, 0x33145C07

        public static double Atan2(double y, double x)
        {
            if (double.IsNaN(x) || double.IsNaN(y)) return x + y;
            int hx = Hi(x);
            int ix = hx & 0x7fffffff;
            uint lx = Lo(x);
            int hy = Hi(y);
            int iy = hy & 0x7fffffff;
            uint ly = Lo(y);
            if ((unchecked((uint)(hx - 0x3ff00000)) | lx) == 0) return Atan(y); // x = 1.0
            int m = ((hy >> 31) & 1) | ((hx >> 30) & 2); // 2*sign(x) + sign(y)

            // when y = 0
            if ((unchecked((uint)iy) | ly) == 0)
            {
                switch (m)
                {
                    case 0:
                    case 1: return y; // atan(+-0, +anything) = +-0
                    case 2: return Pi + Tiny; // atan(+0, -anything) = pi
                    default: return -Pi - Tiny; // atan(-0, -anything) = -pi
                }
            }
            // when x = 0
            if ((unchecked((uint)ix) | lx) == 0) return hy < 0 ? -PiO2 - Tiny : PiO2 + Tiny;

            // when x is INF
            if (ix == 0x7ff00000)
            {
                if (iy == 0x7ff00000)
                {
                    switch (m)
                    {
                        case 0: return PiO4 + Tiny; // atan(+INF, +INF)
                        case 1: return -PiO4 - Tiny; // atan(-INF, +INF)
                        case 2: return 3.0 * PiO4 + Tiny; // atan(+INF, -INF)
                        default: return -3.0 * PiO4 - Tiny; // atan(-INF, -INF)
                    }
                }
                switch (m)
                {
                    case 0: return 0.0; // atan(+..., +INF)
                    case 1: return -0.0; // atan(-..., +INF)
                    case 2: return Pi + Tiny; // atan(+..., -INF)
                    default: return -Pi - Tiny; // atan(-..., -INF)
                }
            }
            // when y is INF
            if (iy == 0x7ff00000) return hy < 0 ? -PiO2 - Tiny : PiO2 + Tiny;

            // compute y/x
            int k = (iy - ix) >> 20;
            double z;
            if (k > 60)
            {
                // |y/x| > 2^60
                z = PiO2 + 0.5 * PiLo;
                m &= 1;
            }
            else if (hx < 0 && k < -60)
            {
                z = 0.0; // 0 > |y|/x > -2^-60
            }
            else
            {
                z = Atan(Math.Abs(y / x)); // safe to do y/x
            }
            switch (m)
            {
                case 0: return z; // atan(+, +)
                case 1: return -z; // atan(-, +)
                case 2: return Pi - (z - PiLo); // atan(+, -)
                default: return (z - PiLo) - Pi; // atan(-, -)
            }
        }

        // ---- sin / cos -----------------------------------------------------------------------------

        private const double S1 = -1.66666666666666324348e-01; // 0xBFC55555, 0x55555549
        private const double S2 = 8.33333333332248946124e-03; // 0x3F811111, 0x1110F8A6
        private const double S3 = -1.98412698298579493134e-04; // 0xBF2A01A0, 0x19C161D5
        private const double S4 = 2.75573137070700676789e-06; // 0x3EC71DE3, 0x57B1FE7D
        private const double S5 = -2.50507602534068634195e-08; // 0xBE5AE5E6, 0x8A2B9CEB
        private const double S6 = 1.58969099521155010221e-10; // 0x3DE5D93A, 0x5ACFD57C

        /// <summary>sin on [-pi/4, pi/4]; y is the tail of x, iy = 0 when y is zero.</summary>
        private static double KernelSin(double x, double y, int iy)
        {
            int ix = Hi(x) & 0x7fffffff;
            if (ix < 0x3e400000) return x; // |x| < 2^-27 ((int)x == 0)
            double z = x * x;
            double v = z * x;
            double r = S2 + z * (S3 + z * (S4 + z * (S5 + z * S6)));
            if (iy == 0) return x + v * (S1 + z * r);
            return x - ((z * (0.5 * y - v * r) - y) - v * S1);
        }

        private const double C1 = 4.16666666666666019037e-02; // 0x3FA55555, 0x5555554C
        private const double C2 = -1.38888888888741095749e-03; // 0xBF56C16C, 0x16C15177
        private const double C3 = 2.48015872894767294178e-05; // 0x3EFA01A0, 0x19CB1590
        private const double C4 = -2.75573143513906633035e-07; // 0xBE927E4F, 0x809C52AD
        private const double C5 = 2.08757232129817482790e-09; // 0x3E21EE9E, 0xBDB4B1C4
        private const double C6 = -1.13596475577881948265e-11; // 0xBDA8FAE9, 0xBE8838D4

        /// <summary>cos on [-pi/4, pi/4]; y is the tail of x.</summary>
        private static double KernelCos(double x, double y)
        {
            int ix = Hi(x) & 0x7fffffff;
            if (ix < 0x3e400000) return 1.0; // |x| < 2^-27 ((int)x == 0)
            double z = x * x;
            double r = z * (C1 + z * (C2 + z * (C3 + z * (C4 + z * (C5 + z * C6)))));
            if (ix < 0x3fd33333) return 1.0 - (0.5 * z - (z * r - x * y)); // |x| < 0.3
            double qx = ix > 0x3fe90000 ? 0.28125 : FromWords(ix - 0x00200000, 0); // x > 0.78125 : x/4
            double hz = 0.5 * z - qx;
            double a = 1.0 - qx;
            return a - (hz - (z * r - x * y));
        }

        private static readonly int[] Npio2Hw =
        {
            0x3FF921FB, 0x400921FB, 0x4012D97C, 0x401921FB, 0x401F6A7A, 0x4022D97C,
            0x4025FDBB, 0x402921FB, 0x402C463A, 0x402F6A7A, 0x4031475C, 0x4032D97C,
            0x40346B9C, 0x4035FDBB, 0x40378FDB, 0x403921FB, 0x403AB41B, 0x403C463A,
            0x403DD85A, 0x403F6A7A, 0x40407E4C, 0x4041475C, 0x4042106C, 0x4042D97C,
            0x4043A28C, 0x40446B9C, 0x404534AC, 0x4045FDBB, 0x4046C6CB, 0x40478FDB,
            0x404858EB, 0x404921FB,
        };

        private const double InvPio2 = 6.36619772367581382433e-01; // 0x3FE45F30, 0x6DC9C883
        private const double Pio2_1 = 1.57079632673412561417e+00; // 0x3FF921FB, 0x54400000
        private const double Pio2_1t = 6.07710050650619224932e-11; // 0x3DD0B461, 0x1A626331
        private const double Pio2_2 = 6.07710050630396597660e-11; // 0x3DD0B461, 0x1A600000
        private const double Pio2_2t = 2.02226624879595063154e-21; // 0x3BA3198A, 0x2E037073
        private const double Pio2_3 = 2.02226624871116645580e-21; // 0x3BA3198A, 0x2E000000
        private const double Pio2_3t = 8.47842766036889956997e-32; // 0x397B839A, 0x252049C1

        /// <summary>
        /// x - n·π/2 as y0 + y1 for |x| ≤ 2^19·π (fdlibm's medium range); returns n. Larger arguments return int.MinValue
        /// (the caller falls back to System.Math; the canyon code never gets there).
        /// </summary>
        private static int RemPio2(double x, out double y0, out double y1)
        {
            int hx = Hi(x);
            int ix = hx & 0x7fffffff;
            if (ix <= 0x3fe921fb)
            {
                // |x| ~<= pi/4, no reduction needed
                y0 = x;
                y1 = 0;
                return 0;
            }
            if (ix < 0x4002d97c)
            {
                // |x| < 3pi/4, special case with n = +-1
                double z;
                if (hx > 0)
                {
                    z = x - Pio2_1;
                    if (ix != 0x3ff921fb)
                    {
                        // 33+53 bit pi is good enough
                        y0 = z - Pio2_1t;
                        y1 = (z - y0) - Pio2_1t;
                    }
                    else
                    {
                        // near pi/2, use 33+33+53 bit pi
                        z -= Pio2_2;
                        y0 = z - Pio2_2t;
                        y1 = (z - y0) - Pio2_2t;
                    }
                    return 1;
                }
                z = x + Pio2_1;
                if (ix != 0x3ff921fb)
                {
                    y0 = z + Pio2_1t;
                    y1 = (z - y0) + Pio2_1t;
                }
                else
                {
                    z += Pio2_2;
                    y0 = z + Pio2_2t;
                    y1 = (z - y0) + Pio2_2t;
                }
                return -1;
            }
            if (ix <= 0x413921fb)
            {
                // |x| ~<= 2^19*pi, medium size
                double t = Math.Abs(x);
                int n = (int)(t * InvPio2 + 0.5);
                double fn = n;
                double r = t - fn * Pio2_1;
                double w = fn * Pio2_1t; // 1st round good to 85 bit
                if (n < 32 && ix != Npio2Hw[n - 1])
                {
                    y0 = r - w; // quick check no cancellation
                }
                else
                {
                    int j = ix >> 20;
                    y0 = r - w;
                    int i = j - ((Hi(y0) >> 20) & 0x7ff);
                    if (i > 16)
                    {
                        // 2nd iteration needed, good to 118
                        t = r;
                        w = fn * Pio2_2;
                        r = t - w;
                        w = fn * Pio2_2t - ((t - r) - w);
                        y0 = r - w;
                        i = j - ((Hi(y0) >> 20) & 0x7ff);
                        if (i > 49)
                        {
                            // 3rd iteration needed, 151 bits acc
                            t = r;
                            w = fn * Pio2_3;
                            r = t - w;
                            w = fn * Pio2_3t - ((t - r) - w);
                            y0 = r - w;
                        }
                    }
                }
                y1 = (r - y0) - w;
                if (hx < 0)
                {
                    y0 = -y0;
                    y1 = -y1;
                    return -n;
                }
                return n;
            }
            y0 = 0;
            y1 = 0;
            return int.MinValue;
        }

        public static double Sin(double x)
        {
            int ix = Hi(x) & 0x7fffffff;
            if (ix <= 0x3fe921fb) return KernelSin(x, 0.0, 0);
            if (ix >= 0x7ff00000) return x - x; // NaN
            double y0, y1;
            int n = RemPio2(x, out y0, out y1);
            if (n == int.MinValue) return Math.Sin(x);
            switch (n & 3)
            {
                case 0: return KernelSin(y0, y1, 1);
                case 1: return KernelCos(y0, y1);
                case 2: return -KernelSin(y0, y1, 1);
                default: return -KernelCos(y0, y1);
            }
        }

        public static double Cos(double x)
        {
            int ix = Hi(x) & 0x7fffffff;
            if (ix <= 0x3fe921fb) return KernelCos(x, 0.0);
            if (ix >= 0x7ff00000) return x - x; // NaN
            double y0, y1;
            int n = RemPio2(x, out y0, out y1);
            if (n == int.MinValue) return Math.Cos(x);
            switch (n & 3)
            {
                case 0: return KernelCos(y0, y1);
                case 1: return -KernelSin(y0, y1, 1);
                case 2: return -KernelCos(y0, y1);
                default: return KernelSin(y0, y1, 1);
            }
        }

        // ---- log -------------------------------------------------------------------------------------

        private const double Ln2Hi = 6.93147180369123816490e-01; // 0x3FE62E42, 0xFEE00000
        private const double Ln2Lo = 1.90821492927058770002e-10; // 0x3DEA39EF, 0x35793C76
        private const double Two54 = 1.80143985094819840000e+16; // 0x43500000, 0x00000000
        private const double Lg1 = 6.666666666666735130e-01; // 0x3FE55555, 0x55555593
        private const double Lg2 = 3.999999999940941908e-01; // 0x3FD99999, 0x9997FA04
        private const double Lg3 = 2.857142874366239149e-01; // 0x3FD24924, 0x94229359
        private const double Lg4 = 2.222219843214978396e-01; // 0x3FCC71C5, 0x1D8E78AF
        private const double Lg5 = 1.818357216161805012e-01; // 0x3FC74664, 0x96CB03DE
        private const double Lg6 = 1.531383769920937332e-01; // 0x3FC39A09, 0xD078C69F
        private const double Lg7 = 1.479819860511658591e-01; // 0x3FC2F112, 0xDF3E5244

        public static double Log(double x)
        {
            int hx = Hi(x);
            uint lx = Lo(x);
            int k = 0;
            if (hx < 0x00100000)
            {
                // x < 2^-1022
                if ((unchecked((uint)(hx & 0x7fffffff)) | lx) == 0) return double.NegativeInfinity; // log(+-0) = -inf
                if (hx < 0) return double.NaN; // log(-#) = NaN
                k -= 54;
                x *= Two54; // subnormal number, scale up x
                hx = Hi(x);
            }
            if (hx >= 0x7ff00000) return x + x;
            k += (hx >> 20) - 1023;
            hx &= 0x000fffff;
            int i = (hx + 0x95f64) & 0x100000;
            x = FromWords(hx | (i ^ 0x3ff00000), Lo(x)); // normalize x or x/2
            k += i >> 20;
            double f = x - 1.0;
            double dk;
            if ((0x000fffff & (2 + hx)) < 3)
            {
                // |f| < 2^-20
                if (f == 0)
                {
                    if (k == 0) return 0;
                    dk = k;
                    return dk * Ln2Hi + dk * Ln2Lo;
                }
                double r0 = f * f * (0.5 - 0.33333333333333333 * f);
                if (k == 0) return f - r0;
                dk = k;
                return dk * Ln2Hi - ((r0 - dk * Ln2Lo) - f);
            }
            double s = f / (2.0 + f);
            dk = k;
            double z = s * s;
            int ii = hx - 0x6147a;
            double w = z * z;
            int j = 0x6b851 - hx;
            double t1 = w * (Lg2 + w * (Lg4 + w * Lg6));
            double t2 = z * (Lg1 + w * (Lg3 + w * (Lg5 + w * Lg7)));
            ii |= j;
            double R = t2 + t1;
            if (ii > 0)
            {
                double hfsq = 0.5 * f * f;
                if (k == 0) return f - (hfsq - s * (hfsq + R));
                return dk * Ln2Hi - ((hfsq - (s * (hfsq + R) + dk * Ln2Lo)) - f);
            }
            if (k == 0) return f - s * (f - R);
            return dk * Ln2Hi - ((s * (f - R) - dk * Ln2Lo) - f);
        }
    }
}
