"""
Portable DSP for Ant Garden's sound recipes (the audition of 2026-11-04).

Everything here is written so it can be ported line for line into C# (Sfx.cs) for the game and the
Quest build: a 32-bit hash noise and xorshift random (uint maths), RBJ biquads, one-poles, a
Simper/TPT state-variable filter, a Karplus-Strong loop and a small feedback-delay-network reverb.
numpy/scipy only make the same maths fast here (lfilter == the C# biquad loop).
"""
import math
import wave

import numpy as np
from scipy.signal import lfilter

SR = 44100
TAU = 2 * math.pi


# ---------------------------------------------------------------- random

class Rand:
    """xorshift32. C#: uint s; s ^= s << 13; s ^= s >> 17; s ^= s << 5; u = (s >> 8) / 16777216f."""

    def __init__(self, seed):
        self.s = ((seed * 2654435761) + 0x6D2B79F5) & 0xFFFFFFFF or 1
        for _ in range(4):
            self.next_u32()

    def next_u32(self):
        s = self.s
        s ^= (s << 13) & 0xFFFFFFFF
        s ^= s >> 17
        s ^= (s << 5) & 0xFFFFFFFF
        self.s = s
        return s

    def u(self):
        return (self.next_u32() >> 8) / 16777216.0

    def range(self, a, b):
        return a + (b - a) * self.u()

    def int(self, n):
        return min(n - 1, int(self.u() * n))

    def pick(self, seq):
        return seq[self.int(len(seq))]

    def sign(self):
        return 1.0 if self.u() < 0.5 else -1.0


def noise(n, seed, offset=0):
    """White noise in [-1, 1) from a counter hash (lowbias32): sample i of stream `seed`.
    C#: unchecked { uint h = i * 0x9E3779B1u + seed * 0x85EBCA77u; h ^= h >> 16; h *= 0x7FEB352Du;
    h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16; return h / 2147483648f - 1f; }"""
    m = np.uint64(0xFFFFFFFF)
    i = np.arange(offset, offset + n, dtype=np.uint64)
    h = (i * np.uint64(0x9E3779B1) + np.uint64((seed * 0x85EBCA77) & 0xFFFFFFFF)) & m
    h ^= h >> np.uint64(16)
    h = (h * np.uint64(0x7FEB352D)) & m
    h ^= h >> np.uint64(15)
    h = (h * np.uint64(0x846CA68B)) & m
    h ^= h >> np.uint64(16)
    return h.astype(np.float64) / 2147483648.0 - 1.0


def pink(n, seed):
    """Paul Kellet's economy pink filter over the hash noise (three one-poles, summed)."""
    w = noise(n, seed)
    b0 = lfilter([0.0990460], [1, -0.99765], w)
    b1 = lfilter([0.2965164], [1, -0.96300], w)
    b2 = lfilter([1.0526913], [1, -0.57000], w)
    return (b0 + b1 + b2 + 0.1848 * w) * 0.22


def brown(n, seed, leak=0.996):
    """Brown(ian) noise: a leaky integrator of white noise."""
    return lfilter([0.06], [1, -leak], noise(n, seed))


# ---------------------------------------------------------------- filters

def biquad(kind, f, q=0.7071, gain_db=0.0):
    """RBJ cookbook coefficients (b, a), a[0] = 1. kind: lp hp bp (0 dB peak) peak lowshelf highshelf."""
    f = min(f, SR * 0.49)
    w = TAU * f / SR
    cw, sw = math.cos(w), math.sin(w)
    alpha = sw / (2 * q)
    A = 10 ** (gain_db / 40)
    if kind == 'lp':
        b = [(1 - cw) / 2, 1 - cw, (1 - cw) / 2]
        a = [1 + alpha, -2 * cw, 1 - alpha]
    elif kind == 'hp':
        b = [(1 + cw) / 2, -(1 + cw), (1 + cw) / 2]
        a = [1 + alpha, -2 * cw, 1 - alpha]
    elif kind == 'bp':
        b = [alpha, 0.0, -alpha]
        a = [1 + alpha, -2 * cw, 1 - alpha]
    elif kind == 'peak':
        b = [1 + alpha * A, -2 * cw, 1 - alpha * A]
        a = [1 + alpha / A, -2 * cw, 1 - alpha / A]
    elif kind == 'lowshelf':
        s = 2 * math.sqrt(A) * alpha
        b = [A * ((A + 1) - (A - 1) * cw + s), 2 * A * ((A - 1) - (A + 1) * cw), A * ((A + 1) - (A - 1) * cw - s)]
        a = [(A + 1) + (A - 1) * cw + s, -2 * ((A - 1) + (A + 1) * cw), (A + 1) + (A - 1) * cw - s]
    elif kind == 'highshelf':
        s = 2 * math.sqrt(A) * alpha
        b = [A * ((A + 1) + (A - 1) * cw + s), -2 * A * ((A - 1) + (A + 1) * cw), A * ((A + 1) + (A - 1) * cw - s)]
        a = [(A + 1) - (A - 1) * cw + s, 2 * ((A - 1) - (A + 1) * cw), (A + 1) - (A - 1) * cw - s]
    else:
        raise ValueError(kind)
    a0 = a[0]
    return np.array(b) / a0, np.array(a) / a0


def filt(x, kind, f, q=0.7071, gain_db=0.0):
    b, a = biquad(kind, f, q, gain_db)
    return lfilter(b, a, x)


def lp(x, f, q=0.7071):
    return filt(x, 'lp', f, q)


def hp(x, f, q=0.7071):
    return filt(x, 'hp', f, q)


def bp(x, f, q=1.0):
    return filt(x, 'bp', f, q)


def onepole_lp(x, fc):
    k = 1 - math.exp(-TAU * fc / SR)
    return lfilter([k], [1, -(1 - k)], x)


def svf(x, fc, q, mode='bp'):
    """Simper's TPT state-variable filter with a per-sample cutoff (array or number), stable when swept.
    mode: lp, bp (unity gain at the centre), hp."""
    n = len(x)
    fc = np.broadcast_to(np.asarray(fc, dtype=np.float64), (n,))
    g = np.tan(np.pi * np.minimum(fc, SR * 0.45) / SR)
    k = 1.0 / q
    a1 = 1.0 / (1.0 + g * (g + k))
    a2 = g * a1
    a3 = g * a2
    out = np.empty(n)
    ic1 = ic2 = 0.0
    xs = np.asarray(x, dtype=np.float64)
    for i in range(n):
        v3 = xs[i] - ic2
        v1 = a1[i] * ic1 + a2[i] * v3
        v2 = ic2 + a2[i] * ic1 + a3[i] * v3
        ic1 = 2 * v1 - ic1
        ic2 = 2 * v2 - ic2
        if mode == 'lp':
            out[i] = v2
        elif mode == 'bp':
            out[i] = k * v1
        else:
            out[i] = xs[i] - k * v1 - v2
    return out


def resonator(x, f, tau):
    """Two-pole resonator: a mode at f Hz ringing with time constant tau (s), unity-ish peak."""
    r = math.exp(-1.0 / (tau * SR))
    w = TAU * f / SR
    return lfilter([1 - r], [1, -2 * r * math.cos(w), r * r], x)


# ---------------------------------------------------------------- envelopes and helpers

def t_axis(n):
    return np.arange(n) / SR


def ms(v):
    return int(round(v * SR / 1000.0))


def secs(v):
    return int(round(v * SR))


def onset(x, ms_len):
    """Raised-cosine fade-in over ms_len (a soft contact; no click)."""
    k = ms(ms_len)
    if k > 1:
        k = min(k, len(x))
        x[:k] *= 0.5 - 0.5 * np.cos(np.pi * np.arange(k) / k)
    return x


def fade_out(x, ms_len):
    k = ms(ms_len)
    if k > 1:
        k = min(k, len(x))
        x[-k:] *= 0.5 + 0.5 * np.cos(np.pi * np.arange(k) / k)
    return x


def place(dst, src, at, gain=1.0):
    """Mix src into dst starting at sample `at` (clipped to dst)."""
    if at >= len(dst) or gain == 0:
        return dst
    m = min(len(src), len(dst) - at)
    if at < 0:
        src = src[-at:]
        m = min(len(src), len(dst))
        at = 0
    dst[at:at + m] += gain * src[:m]
    return dst


def pad_to(x, n):
    if len(x) >= n:
        return x[:n]
    return np.concatenate([x, np.zeros(n - len(x))])


def rms(x):
    return float(np.sqrt(np.mean(np.square(x)) + 1e-20))


def peak(x):
    return float(np.max(np.abs(x)) + 1e-20)


def norm_rms(x, target):
    return x * (target / rms(x))


def norm_peak(x, target):
    return x * (target / peak(x))


def trim_tail(x, floor_db=-70.0, keep_ms=30):
    """Drop the silent end (below floor_db of the peak) and fade the last keep_ms."""
    thr = peak(x) * 10 ** (floor_db / 20)
    idx = np.nonzero(np.abs(x) > thr)[0]
    end = int(idx[-1]) + ms(keep_ms) if len(idx) else len(x)
    x = x[:min(len(x), end)].copy()
    return fade_out(x, keep_ms)


def resample_pitch(x, pitch):
    """Play faster/slower like AudioSource.pitch (linear interpolation)."""
    if abs(pitch - 1.0) < 1e-6:
        return x.copy()
    n = int(len(x) / pitch)
    pos = np.arange(n) * pitch
    return np.interp(pos, np.arange(len(x)), x)


def midi_hz(m):
    return 440.0 * 2 ** ((m - 69) / 12.0)


def pan(x, p):
    """Equal-power pan of a mono signal, p in [-1, 1] -> (2, n)."""
    a = (p + 1) * math.pi / 4
    return np.vstack([x * math.cos(a), x * math.sin(a)])


# ---------------------------------------------------------------- Karplus-Strong

def ks_string(f0, n, excitation, tau, bright=0.5):
    """A plucked string: a delay loop with a one-pole low-pass (bright = 0..1) and an all-pass for the
    fractional delay, decaying with time constant tau (s) at the fundamental.
    The loop is linear, so it runs here as one lfilter; in C# it is the obvious per-sample loop."""
    L = SR / f0
    s = 0.15 + 0.6 * (1 - bright)              # loop low-pass pole (more = darker, faster high decay)
    lp_delay = s / (1 - s)                     # its phase delay at low frequency, in samples
    M = int(math.floor(L - lp_delay - 0.1))
    D = L - lp_delay - M                       # fractional part, done by the all-pass
    c = (1 - D) / (1 + D)
    g = math.exp(-1.0 / (f0 * tau))           # loop gain per period
    # H(z) = 1 / (1 - g * LP(z) * AP(z) * z^-M), LP = (1-s)/(1-s z^-1), AP = (c + z^-1)/(1 + c z^-1)
    den_lpap = np.polymul([1, -s], [1, c])
    num = den_lpap
    den = np.zeros(M + 2)
    den[:3] = den_lpap
    den[M] -= g * (1 - s) * c
    den[M + 1] -= g * (1 - s)
    x = pad_to(excitation, n)
    return lfilter(num, den, x)


# ---------------------------------------------------------------- reverb (FDN)

_H8 = None


def _hadamard8():
    global _H8
    if _H8 is None:
        h = np.array([[1.0]])
        for _ in range(3):
            h = np.block([[h, h], [h, -h]])
        _H8 = h / math.sqrt(8)
    return _H8


_REV_NORM = {}


def fdn_reverb(x, rt60=1.5, size=1.0, damp=0.35, predelay_ms=8.0, stereo=False, tail=None):
    """An 8-line feedback delay network (Hadamard mix, a one-pole low-pass in each line for darker high
    decay, two input all-pass diffusers). Returns only the wet signal, scaled so its impulse response has
    unit energy: mix as dry + wet * this. Block-processed here (blocks no longer than the shortest line);
    C#: the same per sample with ring buffers."""
    key = (rt60, size, damp, predelay_ms, stereo)
    if key not in _REV_NORM:
        imp = np.zeros(secs(rt60 * 1.6 + 0.2))
        imp[0] = 1.0
        ir = _fdn_core(imp, rt60, size, damp, predelay_ms, stereo)
        _REV_NORM[key] = 1.0 / math.sqrt(np.sum(np.square(ir)) / (2 if stereo else 1) + 1e-20)
    if tail is None:
        tail = rt60 * 1.1
    xx = np.concatenate([x, np.zeros(secs(tail))])
    return _fdn_core(xx, rt60, size, damp, predelay_ms, stereo) * _REV_NORM[key]


def _fdn_core(x, rt60, size, damp, predelay_ms, stereo):
    base = [1153, 1327, 1559, 1801, 2063, 2357, 2671, 2999]
    d = [max(97, int(b * size)) for b in base]
    gains = np.array([10 ** (-3.0 * di / (SR * rt60)) for di in d])
    pre = ms(predelay_ms)
    u = np.concatenate([np.zeros(pre), x])[:len(x)]
    # input diffusion: two Schroeder all-passes
    for D, gch in ((int(142 * size) + 1, 0.6), (int(379 * size) + 1, 0.55)):
        b = np.zeros(D + 1); b[0] = -gch; b[D] = 1.0
        a = np.zeros(D + 1); a[0] = 1.0; a[D] = -gch
        u = lfilter(b, a, u)
    n = len(u)
    maxd = max(d)
    B = min(d)
    H = _hadamard8()
    hist = np.zeros((8, n + maxd))
    zi = [np.zeros(1) for _ in range(8)]
    inj = np.array([1, -1, 1, -1, 1, -1, 1, -1], dtype=np.float64) * 0.5
    cl = np.array([1, 1, -1, -1, 1, -1, 1, -1], dtype=np.float64) / math.sqrt(8)
    cr = np.array([1, -1, -1, 1, -1, 1, 1, -1], dtype=np.float64) / math.sqrt(8)
    outl = np.zeros(n)
    outr = np.zeros(n) if stereo else None
    k = 1.0 - damp
    for s in range(0, n, B):
        e = min(n, s + B)
        m = e - s
        f = np.empty((8, m))
        for i in range(8):
            seg = hist[i, s + maxd - d[i]: s + maxd - d[i] + m]
            y, zi[i] = lfilter([k], [1, -damp], seg, zi=zi[i])
            f[i] = y * gains[i]
        outl[s:e] = cl @ f
        if stereo:
            outr[s:e] = cr @ f
        hist[:, s + maxd: s + maxd + m] = H @ f + np.outer(inj, u[s:e])
    if stereo:
        return np.vstack([outl, outr])
    return outl


def with_reverb(x, wet, **kw):
    """dry + wet * reverb (mono). The result is longer by the tail."""
    r = fdn_reverb(x, **kw)
    y = np.zeros(len(r))
    y[:len(x)] += x
    return y + wet * r


# ---------------------------------------------------------------- loudness (ITU-R BS.1770, for matching levels)

def _k_weight(x):
    b1, a1 = biquad('highshelf', 1500.0, 1 / math.sqrt(2), 4.0)
    b2, a2 = biquad('hp', 38.0, 0.5)
    return lfilter(b2, a2, lfilter(b1, a1, x))


def momentary(x, hop_ms=25):
    """Momentary loudness (400 ms windows) in LUFS over time. x: mono (n,) or stereo (2, n)."""
    xs = x if x.ndim == 2 else x[None, :]
    p = sum(np.square(_k_weight(c)) for c in xs)
    w = secs(0.4)
    if len(p) < w:
        p = np.concatenate([p, np.zeros(w - len(p))])
    c = np.concatenate([[0.0], np.cumsum(p)])
    hop = ms(hop_ms)
    starts = np.arange(0, len(p) - w + 1, hop)
    msq = (c[starts + w] - c[starts]) / w
    return -0.691 + 10 * np.log10(msq + 1e-20)


def lufs_max(x):
    return float(np.max(momentary(x)))


def lufs_integrated(x):
    m = momentary(x, hop_ms=100)
    m = m[m > -70]
    if len(m) == 0:
        return -70.0
    rel = 10 * np.log10(np.mean(10 ** (m / 10))) - 10
    m = m[m > rel]
    return float(10 * np.log10(np.mean(10 ** (m / 10))))


# ---------------------------------------------------------------- WAV

def write_wav(path, x, bits=16):
    """Mono (n,) or stereo (2, n) float in [-1, 1] -> 16-bit PCM WAV, with TPDF dither."""
    xs = x if x.ndim == 2 else x[None, :]
    ch = xs.shape[0]
    d = (noise(xs.shape[1], 991) + noise(xs.shape[1], 992)) * 0.5 / 32768.0
    ints = np.clip(np.round((xs + d) * 32767.0), -32768, 32767).astype('<i2')
    inter = ints.T.reshape(-1)
    with wave.open(str(path), 'wb') as w:
        w.setnchannels(ch)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(inter.tobytes())
