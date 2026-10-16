// fdlibm 5.3 sin / cos / atan2 / log, as compiled into V8 (src/base/ieee754.cc), written once with
// every multiply-add that clang may fuse spelled out as F(a, b, c) = a·b + c.
//
//   makeFdlibm(plainMulAdd)  — plain IEEE evaluation: what the C# port (Fdlibm.cs) computes and what
//                              V8 computes on x86-64.
//   makeFdlibm(exactFma)     — the same code with fused multiply-adds: what V8 computes on Apple
//                              Silicon, where clang's default -ffp-contract=on fuses them.
//
// Copyright (C) 1993-2004 by Sun Microsystems, Inc. All rights reserved. Permission to use, copy,
// modify, and distribute this software is freely granted, provided that this notice is preserved.

const f64 = new Float64Array(1);
const u32 = new Uint32Array(f64.buffer); // little-endian: [lo, hi]
const HI = (x) => { f64[0] = x; return u32[1] | 0; };
const LO = (x) => { f64[0] = x; return u32[0] >>> 0; };
const fromWords = (hi, lo) => { u32[1] = hi >>> 0; u32[0] = lo >>> 0; return f64[0]; };

export const plainMulAdd = (a, b, c) => a * b + c;

function decompose(x) {
  f64[0] = x;
  const hi = u32[1] >>> 0;
  const lo = u32[0] >>> 0;
  const exp = (hi >>> 20) & 0x7ff;
  let mant = (BigInt(hi & 0xfffff) << 32n) | BigInt(lo);
  let e;
  if (exp === 0) e = -1074;
  else { mant |= 1n << 52n; e = exp - 1075; }
  return [hi >>> 31 ? -mant : mant, e];
}

/** Correctly rounded a·b + c (exact BigInt arithmetic; fine for the magnitudes used here). */
export function exactFma(a, b, c) {
  if (a === 0 || b === 0 || !Number.isFinite(a) || !Number.isFinite(b) || !Number.isFinite(c)) return a * b + c;
  if (c === 0) return a * b;
  const [ma, ea] = decompose(a);
  const [mb, eb] = decompose(b);
  const [mc, ec] = decompose(c);
  const ep = ea + eb;
  const E = Math.min(ep, ec);
  const S = ((ma * mb) << BigInt(ep - E)) + (mc << BigInt(ec - E));
  if (S === 0n) return 0;
  return Number(S) * 2 ** E;
}

export function makeFdlibm(F) {
  // ---- atan / atan2 ----
  const atanhi = [4.63647609000806093515e-01, 7.85398163397448278999e-01, 9.82793723247329054082e-01, 1.57079632679489655800e+00];
  const atanlo = [2.26987774529616870924e-17, 3.06161699786838301793e-17, 1.39033110312309984516e-17, 6.12323399573676603587e-17];
  const aT = [3.33333333333329318027e-01, -1.99999999998764832476e-01, 1.42857142725034663711e-01, -1.11111104054623557880e-01,
    9.09088713343650656196e-02, -7.69187620504482999495e-02, 6.66107313738753120669e-02, -5.83357013379057348645e-02,
    4.97687799461593236017e-02, -3.65315727442169155270e-02, 1.62858201153657823623e-02];
  function atan(x) {
    const hx = HI(x);
    const ix = hx & 0x7fffffff;
    let id;
    if (ix >= 0x44100000) {
      if (ix > 0x7ff00000 || (ix === 0x7ff00000 && LO(x) !== 0)) return x + x;
      return hx > 0 ? atanhi[3] + atanlo[3] : -atanhi[3] - atanlo[3];
    }
    if (ix < 0x3fdc0000) {
      if (ix < 0x3e400000) return x;
      id = -1;
    } else {
      x = Math.abs(x);
      if (ix < 0x3ff30000) {
        if (ix < 0x3fe60000) { id = 0; x = F(2.0, x, -1.0) / (2.0 + x); }
        else { id = 1; x = (x - 1.0) / (x + 1.0); }
      } else if (ix < 0x40038000) { id = 2; x = (x - 1.5) / F(1.5, x, 1.0); }
      else { id = 3; x = -1.0 / x; }
    }
    const z = x * x;
    const w = z * z;
    const s1 = z * F(w, F(w, F(w, F(w, F(w, aT[10], aT[8]), aT[6]), aT[4]), aT[2]), aT[0]);
    const s2 = w * F(w, F(w, F(w, F(w, aT[9], aT[7]), aT[5]), aT[3]), aT[1]);
    if (id < 0) return F(-x, s1 + s2, x);
    const r = atanhi[id] - (F(x, s1 + s2, -atanlo[id]) - x);
    return hx < 0 ? -r : r;
  }
  const tiny = 1.0e-300, pi_o_4 = 7.8539816339744827900e-01, pi_o_2 = 1.5707963267948965580e+00, pi = 3.1415926535897931160e+00, pi_lo = 1.2246467991473531772e-16;
  function atan2(y, x) {
    if (Number.isNaN(x) || Number.isNaN(y)) return x + y;
    const hx = HI(x), ix = hx & 0x7fffffff, lx = LO(x);
    const hy = HI(y), iy = hy & 0x7fffffff, ly = LO(y);
    if ((((hx - 0x3ff00000) | lx) >>> 0) === 0) return atan(y);
    let m = ((hy >> 31) & 1) | ((hx >> 30) & 2);
    if ((iy | ly) === 0) {
      switch (m) { case 0: case 1: return y; case 2: return pi + tiny; default: return -pi - tiny; }
    }
    if ((ix | lx) === 0) return hy < 0 ? -pi_o_2 - tiny : pi_o_2 + tiny;
    if (ix === 0x7ff00000) {
      if (iy === 0x7ff00000) {
        switch (m) { case 0: return pi_o_4 + tiny; case 1: return -pi_o_4 - tiny; case 2: return 3.0 * pi_o_4 + tiny; default: return -3.0 * pi_o_4 - tiny; }
      }
      switch (m) { case 0: return 0; case 1: return -0; case 2: return pi + tiny; default: return -pi - tiny; }
    }
    if (iy === 0x7ff00000) return hy < 0 ? -pi_o_2 - tiny : pi_o_2 + tiny;
    const k = (iy - ix) >> 20;
    let z;
    if (k > 60) { z = F(0.5, pi_lo, pi_o_2); m &= 1; }
    else if (hx < 0 && k < -60) z = 0.0;
    else z = atan(Math.abs(y / x));
    switch (m) {
      case 0: return z;
      case 1: return -z;
      case 2: return pi - (z - pi_lo);
      default: return (z - pi_lo) - pi;
    }
  }

  // ---- sin / cos ----
  const S1 = -1.66666666666666324348e-01, S2 = 8.33333333332248946124e-03, S3 = -1.98412698298579493134e-04,
    S4 = 2.75573137070700676789e-06, S5 = -2.50507602534068634195e-08, S6 = 1.58969099521155010221e-10;
  function kSin(x, y, iy) {
    if ((HI(x) & 0x7fffffff) < 0x3e400000) return x;
    const z = x * x;
    const v = z * x;
    const r = F(z, F(z, F(z, F(z, S6, S5), S4), S3), S2);
    if (iy === 0) return F(v, F(z, r, S1), x);
    return x - F(-v, S1, F(z, F(0.5, y, -(v * r)), -y));
  }
  const C1 = 4.16666666666666019037e-02, C2 = -1.38888888888741095749e-03, C3 = 2.48015872894767294178e-05,
    C4 = -2.75573143513906633035e-07, C5 = 2.08757232129817482790e-09, C6 = -1.13596475577881948265e-11;
  function kCos(x, y) {
    const ix = HI(x) & 0x7fffffff;
    if (ix < 0x3e400000) return 1.0;
    const z = x * x;
    const r = z * F(z, F(z, F(z, F(z, F(z, C6, C5), C4), C3), C2), C1);
    if (ix < 0x3fd33333) return 1.0 - F(0.5, z, -F(z, r, -(x * y)));
    const qx = ix > 0x3fe90000 ? 0.28125 : fromWords(ix - 0x00200000, 0);
    const hz = F(0.5, z, -qx);
    const a = 1.0 - qx;
    return a - (hz - F(z, r, -(x * y)));
  }
  const npio2_hw = [0x3FF921FB, 0x400921FB, 0x4012D97C, 0x401921FB, 0x401F6A7A, 0x4022D97C, 0x4025FDBB, 0x402921FB, 0x402C463A, 0x402F6A7A,
    0x4031475C, 0x4032D97C, 0x40346B9C, 0x4035FDBB, 0x40378FDB, 0x403921FB, 0x403AB41B, 0x403C463A, 0x403DD85A, 0x403F6A7A, 0x40407E4C,
    0x4041475C, 0x4042106C, 0x4042D97C, 0x4043A28C, 0x40446B9C, 0x404534AC, 0x4045FDBB, 0x4046C6CB, 0x40478FDB, 0x404858EB, 0x404921FB];
  const invpio2 = 6.36619772367581382433e-01, pio2_1 = 1.57079632673412561417e+00, pio2_1t = 6.07710050650619224932e-11,
    pio2_2 = 6.07710050630396597660e-11, pio2_2t = 2.02226624879595063154e-21, pio2_3 = 2.02226624871116645580e-21, pio2_3t = 8.47842766036889956997e-32;
  function remPio2(x, y) {
    const hx = HI(x);
    const ix = hx & 0x7fffffff;
    if (ix <= 0x3fe921fb) { y[0] = x; y[1] = 0; return 0; }
    if (ix < 0x4002d97c) {
      let z;
      if (hx > 0) {
        z = x - pio2_1;
        if (ix !== 0x3ff921fb) { y[0] = z - pio2_1t; y[1] = (z - y[0]) - pio2_1t; }
        else { z -= pio2_2; y[0] = z - pio2_2t; y[1] = (z - y[0]) - pio2_2t; }
        return 1;
      }
      z = x + pio2_1;
      if (ix !== 0x3ff921fb) { y[0] = z + pio2_1t; y[1] = (z - y[0]) + pio2_1t; }
      else { z += pio2_2; y[0] = z + pio2_2t; y[1] = (z - y[0]) + pio2_2t; }
      return -1;
    }
    if (ix <= 0x413921fb) {
      let t = Math.abs(x);
      const n = F(t, invpio2, 0.5) | 0;
      const fn = n;
      let r = F(-fn, pio2_1, t);
      let w = fn * pio2_1t;
      if (n < 32 && ix !== npio2_hw[n - 1]) y[0] = r - w;
      else {
        const j = ix >> 20;
        y[0] = r - w;
        let i = j - ((HI(y[0]) >> 20) & 0x7ff);
        if (i > 16) {
          t = r; w = fn * pio2_2; r = t - w; w = F(fn, pio2_2t, -((t - r) - w)); y[0] = r - w;
          i = j - ((HI(y[0]) >> 20) & 0x7ff);
          if (i > 49) { t = r; w = fn * pio2_3; r = t - w; w = F(fn, pio2_3t, -((t - r) - w)); y[0] = r - w; }
        }
      }
      y[1] = (r - y[0]) - w;
      if (hx < 0) { y[0] = -y[0]; y[1] = -y[1]; return -n; }
      return n;
    }
    throw new Error('fdlibm_ref: |x| > 2^19·π (large-argument reduction) not supported');
  }
  function sin(x) {
    const ix = HI(x) & 0x7fffffff;
    if (ix <= 0x3fe921fb) return kSin(x, 0, 0);
    if (ix >= 0x7ff00000) return x - x;
    const y = [0, 0];
    const n = remPio2(x, y);
    switch (n & 3) { case 0: return kSin(y[0], y[1], 1); case 1: return kCos(y[0], y[1]); case 2: return -kSin(y[0], y[1], 1); default: return -kCos(y[0], y[1]); }
  }
  function cos(x) {
    const ix = HI(x) & 0x7fffffff;
    if (ix <= 0x3fe921fb) return kCos(x, 0);
    if (ix >= 0x7ff00000) return x - x;
    const y = [0, 0];
    const n = remPio2(x, y);
    switch (n & 3) { case 0: return kCos(y[0], y[1]); case 1: return -kSin(y[0], y[1], 1); case 2: return -kCos(y[0], y[1]); default: return kSin(y[0], y[1], 1); }
  }

  // ---- log ----
  const ln2_hi = 6.93147180369123816490e-01, ln2_lo = 1.90821492927058770002e-10, two54 = 1.80143985094819840000e+16,
    Lg1 = 6.666666666666735130e-01, Lg2 = 3.999999999940941908e-01, Lg3 = 2.857142874366239149e-01, Lg4 = 2.222219843214978396e-01,
    Lg5 = 1.818357216161805012e-01, Lg6 = 1.531383769920937332e-01, Lg7 = 1.479819860511658591e-01;
  function log(x) {
    let hx = HI(x);
    const lx = LO(x);
    let k = 0;
    if (hx < 0x00100000) {
      if (((hx & 0x7fffffff) | lx) === 0) return -Infinity;
      if (hx < 0) return NaN;
      k -= 54; x *= two54; hx = HI(x);
    }
    if (hx >= 0x7ff00000) return x + x;
    k += (hx >> 20) - 1023;
    hx &= 0x000fffff;
    const i = (hx + 0x95f64) & 0x100000;
    x = fromWords(hx | (i ^ 0x3ff00000), LO(x));
    k += i >> 20;
    const f = x - 1.0;
    if ((0x000fffff & (2 + hx)) < 3) {
      if (f === 0) { if (k === 0) return 0; const dk = k; return F(dk, ln2_hi, dk * ln2_lo); }
      const R = f * f * F(-0.33333333333333333, f, 0.5);
      if (k === 0) return f - R;
      const dk = k;
      return F(dk, ln2_hi, -(F(-dk, ln2_lo, R) - f));
    }
    const s = f / (2.0 + f);
    const dk = k;
    const z = s * s;
    let ii = hx - 0x6147a;
    const w = z * z;
    const j = 0x6b851 - hx;
    const t1 = w * F(w, F(w, Lg6, Lg4), Lg2);
    const t2 = z * F(w, F(w, F(w, Lg7, Lg5), Lg3), Lg1);
    ii |= j;
    const R = t2 + t1;
    if (ii > 0) {
      const hfsq = 0.5 * f * f;
      if (k === 0) return f - F(-s, hfsq + R, hfsq);
      return F(dk, ln2_hi, -((hfsq - F(s, hfsq + R, dk * ln2_lo)) - f));
    }
    if (k === 0) return F(-s, f - R, f);
    return F(dk, ln2_hi, -(F(s, f - R, -(dk * ln2_lo)) - f));
  }

  return { sin, cos, atan, atan2, log };
}
