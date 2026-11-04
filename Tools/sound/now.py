"""
The game's sounds as they are today (Sfx.cs and Voices.Chime, 2026-10-25), ported to Python only to
render them for the audition. Same recipes, same order of random draws: System.Random(11) is ported
exactly (.NET's seeded subtractive generator), so these are the very clips Sfx.Init makes.
"""
import math

import numpy as np

from dsp import SR, lfilter


class NetRandom:
    """System.Random(seed) as .NET/Mono compute it (Knuth's subtractive generator)."""
    MBIG = 2147483647
    MSEED = 161803398

    def __init__(self, seed):
        sa = [0] * 56
        subtraction = 2147483647 if seed == -2147483648 else abs(seed)
        mj = self.MSEED - subtraction
        sa[55] = mj
        mk = 1
        for i in range(1, 55):
            ii = (21 * i) % 55
            sa[ii] = mk
            mk = mj - mk
            if mk < 0:
                mk += self.MBIG
            mj = sa[ii]
        for _ in range(1, 5):
            for i in range(1, 56):
                sa[i] -= sa[1 + (i + 30) % 55]
                if sa[i] < 0:
                    sa[i] += self.MBIG
        self.sa = sa
        self.inext = 0
        self.inextp = 21

    def _sample(self):
        inext = self.inext + 1
        if inext >= 56:
            inext = 1
        inextp = self.inextp + 1
        if inextp >= 56:
            inextp = 1
        r = self.sa[inext] - self.sa[inextp]
        if r == self.MBIG:
            r -= 1
        if r < 0:
            r += self.MBIG
        self.sa[inext] = r
        self.inext = inext
        self.inextp = inextp
        return r * (1.0 / self.MBIG)

    def next_double(self):
        return self._sample()

    def next(self, max_value):
        return int(self._sample() * max_value)


class Now:
    """Sfx.Init's clips, in Sfx.Init's order."""

    def __init__(self):
        self.rng = NetRandom(11)
        self.steps_soft, self.steps_dry = [], []
        for _ in range(8):
            self.steps_soft.append(self.rustle(1100, 2600))
            self.steps_dry.append(self.rustle(1900, 4200))
        self.murmur = self.make_murmur()
        self.shimmer = self.make_shimmer(1.0)
        self.veil = self.make_shimmer(0.6)
        self.breath = self.make_breath()
        self.paper_open = self.paper(0.16)
        self.paper_close = self.paper(0.1)
        self.air_open = self.open_air()
        self.air_tunnel = self.tunnel_air()
        self.chime = chime()

    # -- helpers (as in Sfx.cs)
    def noise(self):
        return self.rng.next_double() * 2 - 1

    def noise_n(self, n):
        return np.array([self.rng.next_double() * 2 - 1 for _ in range(n)])

    @staticmethod
    def filter(x, freq, q, low_pass=False):
        w = 2 * math.pi * freq / SR
        cw = math.cos(w)
        alpha = math.sin(w) / (2 * q)
        a0, a1, a2 = 1 + alpha, -2 * cw, 1 - alpha
        if low_pass:
            b = [(1 - cw) / 2, 1 - cw, (1 - cw) / 2]
        else:
            b = [alpha, 0.0, -alpha]
        return lfilter(np.array(b) / a0, np.array([a0, a1, a2]) / a0, x)

    @staticmethod
    def normalise(x, pk):
        m = max(1e-6, float(np.max(np.abs(x))))
        return x * (pk / m)

    # -- recipes
    def rustle(self, centre, low_pass):
        n = int(SR * 0.16)
        x = np.zeros(n)
        t0 = 0.004
        grains = 2 + self.rng.next(3)
        for _ in range(grains):
            ln = 0.012 + self.rng.next_double() * 0.03
            amp = 0.35 + self.rng.next_double() * 0.65
            a, m = int(t0 * SR), int(ln * SR)
            for i in range(m):
                if a + i >= n:
                    break
                u = i / m
                env = math.sin(math.pi * u) ** 1.5 * (1 - 0.4 * u)
                x[a + i] += self.noise() * amp * env
            t0 += ln * (0.4 + self.rng.next_double() * 0.9) + 0.004 + self.rng.next_double() * 0.02
        x = self.filter(x, centre * (0.8 + self.rng.next_double() * 0.4), 0.55)
        x = self.filter(x, low_pass, 0.6, True)
        x = self.filter(x, low_pass * 0.8, 0.6, True)
        return self.normalise(x, 0.5)

    def make_murmur(self):
        n = int(SR * 0.3)
        x = np.zeros(n)
        phase = 0.0
        for i in range(n):
            t = i / SR
            f = 420 + (520 - 420) * (t / 0.13) if t < 0.13 else 500 + (450 - 500) * min(1.0, (t - 0.13) / 0.17)
            f *= 1 + 0.01 * math.sin(2 * math.pi * 7 * t)
            phase += 2 * math.pi * f / SR
            env = math.sin(math.pi * t / 0.13) if t < 0.13 else 0.85 * math.sin(math.pi * (t - 0.13) / 0.17)
            x[i] = (math.sin(phase) + 0.25 * math.sin(2 * phase) + 0.08 * self.noise()) * env
        x = self.filter(x, 2400, 0.7, True)
        return self.normalise(x, 0.7)

    def make_shimmer(self, bright):
        n = int(SR * 1.6)
        x = np.zeros(n)
        fs = [1318.5, 1975.5, 2637.0, 3135.9]
        t_all = np.arange(n) / SR
        for j, f in enumerate(fs):
            fj = f * (0.75 + 0.25 * bright) * (1 + 0.004 * j)
            ph = self.rng.next_double() * 6.28
            amp = 1.0 / (1 + j * 0.8)
            start = j * 0.035
            t = t_all - start
            env = np.minimum(1, np.maximum(t, 0) / 0.09) * np.exp(-np.maximum(t, 0) / (0.5 + 0.15 * j))
            x += np.where(t >= 0, amp * env * np.sin(2 * math.pi * fj * t + ph), 0.0)
        air = self.noise_n(n) * np.minimum(1, t_all / 0.12) * np.exp(-t_all / 0.35)
        air = self.filter(air, 2200 * bright + 600, 0.8)
        x = x + 0.5 * air
        x = self.filter(x, 5000, 0.7, True)
        return self.normalise(x, 0.6)

    def make_breath(self):
        n = int(SR * 1.2)
        x = np.zeros(n)
        slices = 24
        ln = n // slices
        for s in range(slices):
            seg = self.noise_n(ln + 400)
            u = s / (slices - 1)
            seg = self.filter(seg, 300 + (2200 - 300) * math.sin(u * math.pi * 0.85), 1.4)
            for i in range(len(seg)):
                k = s * ln + i
                if k >= n:
                    break
                w = i / 400 if i < 400 else (1 - (i - ln) / 400 if i > ln else 1)
                x[k] += seg[i] * w
        t = np.arange(n) / SR
        env = np.sin(math.pi * np.clip(t / 1.2, 0, 1))
        x = x * env * env + 0.35 * np.sin(2 * math.pi * 70 * t) * env
        x = self.filter(x, 2500, 0.7, True)
        return self.normalise(x, 0.7)

    def paper(self, length):
        n = int(SR * (length + 0.05))
        x = np.zeros(n)
        crackles = 9 if length > 0.12 else 6
        for _ in range(crackles):
            t0 = self.rng.next_double() * length
            amp = 0.3 + self.rng.next_double() * 0.7
            for i in range(int(t0 * SR), n):
                t = i / SR - t0
                if t > 0.02:
                    break
                x[i] += self.noise() * amp * math.exp(-t / 0.004)
        for i in range(n):
            x[i] += 0.15 * self.noise() * math.sin(math.pi * i / n)
        x = self.filter(x, 2400, 0.9)
        return self.normalise(x, 0.6)

    def open_air(self):
        secs = 24.0
        n = int(SR * secs)
        w = self.noise_n(n)
        wind = lfilter([0.05], [1, -0.995], w)
        wind = self.filter(wind, 420, 0.6, True)
        t = np.arange(n) / SR
        wind = wind * (0.6 + 0.4 * np.sin(2 * math.pi * t / secs * 2 + 1.3) * np.sin(2 * math.pi * t / secs * 3))
        x = self.normalise(wind, 0.5)
        t0 = 0.4
        while t0 < secs - 0.6:
            f = 4100 + self.rng.next_double() * 500
            amp = 0.025 + self.rng.next_double() * 0.03
            pulses = 3 + self.rng.next(4)
            for p in range(pulses):
                ts = t0 + p * 0.033
                i0, i1 = int(ts * SR), min(n, int((ts + 0.014) * SR))
                tt = np.arange(i0, i1) / SR - ts
                x[i0:i1] += amp * np.sin(2 * math.pi * f * tt) * np.sin(math.pi * tt / 0.014)
            t0 += 0.5 + self.rng.next_double() * 1.4
        return loop(x)

    def tunnel_air(self):
        secs = 16.0
        n = int(SR * secs)
        w = self.noise_n(n)
        x = lfilter([0.04], [1, -0.997], w)
        x = self.filter(x, 160, 0.7, True)
        x = self.normalise(x, 0.45)
        t = np.arange(n) / SR
        x = x + 0.05 * np.sin(2 * math.pi * 55 * t)
        t0 = 1.5
        while t0 < secs - 1:
            f0 = 1300 + self.rng.next_double() * 700
            amp = 0.08 + self.rng.next_double() * 0.06
            i0, i1 = int(t0 * SR), min(n, int((t0 + 0.35) * SR))
            tt = np.arange(i0, i1) / SR - t0
            f = f0 * (1 + 0.6 * np.exp(-tt / 0.01))
            x[i0:i1] += amp * np.sin(2 * math.pi * f * tt) * np.exp(-tt / 0.07) * np.minimum(1, tt * 2000)
            t0 += 2.5 + self.rng.next_double() * 3.5
        return loop(x)

    # -- what the game does when it plays them (Sfx.Step and friends)
    def step(self, dry, sprint=False):
        """Sfx.Step: a random variant, loudness 0.55-1 (x1.2 sprinting), pitch 0.92-1.08."""
        s = self.steps_dry if dry else self.steps_soft
        loud = (0.55 + 0.45 * self.rng.next_double()) * (1.2 if sprint else 1.0)
        clip = s[self.rng.next(len(s))]
        pitch = 0.92 + self.rng.next_double() * 0.16
        return clip, loud, pitch


def loop(x):
    m = SR // 2
    n = len(x)
    w = np.arange(m) / m
    x = x.copy()
    x[:m] = x[:m] * w + x[n - m:] * (1 - w)
    return x[:n - m]


def chime():
    """Voices.Chime: a soft bell, a few inharmonic partials, slow decay (2.2 s)."""
    n = int(SR * 2.2)
    t = np.arange(n) / SR
    f = [523.25, 1309.4, 2093.0, 2853.0]
    a = [1.0, 0.45, 0.22, 0.1]
    k = [2.2, 3.5, 5.0, 7.0]
    v = sum(a[j] * np.exp(-k[j] * t) * np.sin(2 * math.pi * f[j] * t) for j in range(4))
    return v * 0.22 * np.minimum(1, t * 400)
