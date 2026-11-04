"""
Three directions for the garden's sound effects (audition 2026-11-04). Each covers the same events:

  step(dry, v)   the player's feet (soft ground / dry ground), variant v
  touch(v)       two ants touching antennae
  flare(v)       a glowing plant lighting up under an antenna
  map_open()     opening the map
  map_close()    closing it
  gate()         going through a light gate / a teleport
  guide()        the guiding light calling (new: a beam the ant walks toward)
  reply(pitch)   an answer: an ant's line, a relic, a story, a tutorial step (today Voices.Chime)

A  Natural  : no pitch at all. Chitin ticks on dry litter, a puff of spores, a dry leaf, a gust in a
             tunnel, breath across a hollow reed, a drop in a cave.
B  Musical  : felt piano, small glass bells and a plucked harp, in the nest's key (major pentatonic).
C  Water and wood : natural materials that ring in tune. Damp moss underfoot, bamboo knocks, drops
             falling into a buried jar that answers with a bell (suikinkutsu), breath through a tube.

Keys: B and C take root_midi (the nest's key); the audition uses D (62, the blue nest).
"""
import math

import numpy as np

from dsp import (Rand, noise, pink, brown, lp, hp, bp, filt, svf, onset, fade_out, t_axis, ms, secs, place,
                 peak, with_reverb, midi_hz, resample_pitch)
from voices import (penta_note, tick, burst, crackle, felt_piano, glass_bell, harp, bamboo, plink, drop_bell,
                    air_tone, swell, modes, rise_fall)


def unit(x):
    return x / peak(x)


def finish(y, fade_ms):
    """Every finished sound: a 35 Hz high-pass (no DC, no rumble nobody hears), the end faded."""
    return fade_out(hp(y, 35, 0.7), fade_ms)


def degree_for_pitch(pitch):
    """Voices.Chime's pitch (0.6-1.9 per speaker) to a pentatonic step from the root."""
    return int(round(5 * math.log2(max(0.3, pitch))))


# ================================================================ A · natural

class Natural:
    key = 'A'
    title = '自然'

    def step(self, dry, v):
        r = Rand(1000 + v * 7 + (500 if dry else 0))
        y = np.zeros(secs(0.17))
        feet = 3 if r.u() < 0.7 else 2
        t = 0.003
        for _ in range(feet):
            at = secs(t)
            # the ground under the foot: a soft pat
            pat = burst(r.range(6, 10), r.int(1 << 30), 'bp', r.range(700, 1300) if dry else r.range(420, 820), 0.9,
                        attack_ms=1.0 if dry else 1.4, tau_ms=r.range(2.0, 3.2))
            place(y, unit(pat) * r.range(0.6, 1.0) * (0.7 if dry else 1.0), at)
            # the foot itself: a tiny tick of chitin
            f = r.range(3000, 5000) if dry else r.range(2400, 3600)
            ft = tick(f, r.range(0.45, 0.9) / 1000 * (1.0 if dry else 1.3), f2_ratio=r.range(1.5, 1.9))
            place(y, unit(ft) * r.range(0.6, 1.0) * (0.5 if dry else 0.16), at + ms(0.4))
            if dry:
                for _ in range(r.int(4)):
                    c = tick(r.range(2500, 6500), r.range(0.3, 0.7) / 1000, f2_ratio=r.range(1.4, 2.0))
                    place(y, unit(c) * 0.25 * r.u() ** 2, at + ms(r.range(4, 45)))
            t += r.range(0.006, 0.02)
        y = lp(y, 7500 if dry else 3000, 0.7)
        y = hp(y, 180, 0.7)
        return finish(y, 8)

    def touch(self, v):
        """Antenna stroking antenna: a few soft brushes, a hint of chitin under each."""
        r = Rand(2000 + v * 13)
        y = np.zeros(secs(0.55))
        strokes = 4 + r.int(3)
        t = 0.015
        for i in range(strokes):
            a = r.range(0.6, 1.0) * (1 - 0.4 * i / strokes)
            br = burst(r.range(16, 26), r.int(1 << 30), 'bp', r.range(1700, 2600), 1.4, attack_ms=r.range(4, 7),
                       tau_ms=r.range(5, 8))
            place(y, unit(br) * a * 0.5, secs(t))
            tk = tick(r.range(2600, 3600), 0.0006, f2_ratio=1.8, onset_ms=0.5)
            place(y, unit(tk) * a * 0.12, secs(t) + ms(r.range(2, 6)))
            t += r.range(0.045, 0.08)
        y = hp(lp(y, 4500, 0.7), 400, 0.7)
        y = with_reverb(y, 0.1, rt60=0.5, size=0.45, damp=0.5, predelay_ms=3)
        return finish(y, 20)

    def flare(self, v):
        """A plant lighting up: a slow, soft breath of light, never a hit (the user, 11-04: the light-up was jarring).
        The bloom rises over 0.12-0.2 s with no transient, a faint air rides on it, and it dies away into a small
        room, long enough that it is never cut. Eight of these differ in pitch, sweep, rise and length."""
        r = Rand(3000 + v * 17)
        dur = 2.0
        n = secs(dur)
        t = t_axis(n)
        src = pink(n, r.int(1 << 30))
        f0 = r.range(540, 760)
        rise = r.range(1.3, 1.65)
        attack = r.range(0.12, 0.2)
        tau = r.range(0.3, 0.45)
        q = r.range(2.2, 3.0)
        fc = f0 * (1 + (rise - 1) * (1 - np.exp(-t / 0.2))) * (1 - 0.15 * (1 - np.exp(-t / 0.7)))
        bloom = svf(src, fc, q, 'bp') + 0.28 * svf(src, fc * 2.02, q * 1.3, 'bp')
        env = rise_fall(n, attack, tau)
        air = hp(pink(n, r.int(1 << 30)), 3800, 0.7)
        y = unit(lp(bloom * env, 3600, 0.7)) + 0.04 * unit(air * env)
        y = fade_out(y, 600)                                   # the breath itself ends at nothing, under the room
        y = with_reverb(y, 0.22, rt60=1.3, size=0.8, damp=0.55, predelay_ms=8)
        return finish(y, 300)

    def _leaf(self, dur, clusters, span, seed, sweep, attack, release):
        r = Rand(seed)
        n = secs(dur)
        t = t_axis(n)
        y = np.zeros(n)
        for _ in range(clusters):
            c0 = r.range(*span)
            for _ in range(2 + r.int(4)):
                c0 += r.range(0.002, 0.009)
                a = min(0.55, 0.15 * (1 - r.u()) ** (-1 / 2.2))
                cr = burst(r.range(0.8, 2.4), r.int(1 << 30), 'bp', r.range(1400, 5000), r.range(1, 3), attack_ms=0.25,
                           tau_ms=0.6)
                place(y, unit(cr) * a * r.sign(), secs(c0))
                if r.u() < 0.15:
                    place(y, unit(tick(r.range(700, 1200), 0.004)) * a * 0.3, secs(c0))
        sw = svf(pink(n, r.int(1 << 30)), np.interp(t, [0, dur], sweep), 0.7, 'lp')
        sw *= swell(n, attack, release)
        y = unit(y) + 0.55 * unit(sw)
        y = lp(hp(y, 250, 0.7), 6500, 0.7)
        return with_reverb(y, 0.08, rt60=0.4, size=0.4, damp=0.5, predelay_ms=2)

    def map_open(self):
        return finish(self._leaf(0.5, 7, (0.03, 0.4), 4001, (600, 2600), 0.22, 0.26), 30)

    def map_close(self):
        return finish(self._leaf(0.32, 4, (0.02, 0.2), 4002, (2200, 700), 0.06, 0.22), 30)

    def gate(self, soft=False):
        """Through a light gate: a gust of air through a tunnel. soft: the veil between two nests of a stitched
        world, shorter and darker."""
        r = Rand(5002 if soft else 5001)
        dur = 1.8 if soft else 2.8
        n = secs(dur)
        t = t_axis(n)
        env = swell(n, 0.5, 1.2, 1.2) if soft else swell(n, 0.8, 1.9, 1.2)
        src = pink(n, r.int(1 << 30))
        gust = svf(src, (300 + 1300 * env ** 1.5) if soft else (350 + 2000 * env ** 1.5), 0.6, 'lp')
        bend = 1 - 0.05 * t / dur
        tube = svf(src, 170 * bend, 5, 'bp') + 0.6 * svf(src, 345 * bend, 5, 'bp')
        rumble = hp(hp(lp(brown(n, r.int(1 << 30)), 110, 0.7), 40, 0.7), 40, 0.7)
        y = (unit(gust) + (0.35 if soft else 0.5) * unit(tube) + (0.0 if soft else 0.4) * unit(rumble)) * env
        y = hp(y, 45, 0.7)
        y = with_reverb(y, 0.25 if soft else 0.3, rt60=2.2, size=1.2, damp=0.45, predelay_ms=12)
        return finish(y, 200)

    def veil(self):
        return self.gate(soft=True)

    def guide(self):
        dur = 2.4
        n = secs(dur)
        y = air_tone(dur, [640, 1290], [70, 50], [1.0, 0.25], 6001, swell(n, 0.55, 1.5), breath_gain=0.06,
                     breath_f=1100, wobble=0.003)
        y = lp(y, 4000, 0.7)
        y = with_reverb(y, 0.5, rt60=2.4, size=1.2, damp=0.5, predelay_ms=20)
        return finish(y, 200)

    def reply_clip(self, v=0):
        """An answer (an ant's line, a relic, a story, a step of the tutorial, the end of a chapter): one drop in a
        cave, softened at the start (no click), low, left to ring in the dark. The game plays it at pitch**0.35 of
        the speaker's pitch."""
        r = Rand(7000 + v * 11)
        y = plink(r.range(560, 680), rise=r.range(0.9, 1.2), tau=0.022, chirp_ms=14, dur=0.15, onset_ms=5.0)
        y = lp(y, 2600, 0.7)
        y = with_reverb(y, 0.55, rt60=2.2, size=1.1, damp=0.55, predelay_ms=15)
        return finish(y, 250)

    def reply(self, pitch=1.0):
        return resample_pitch(self.reply_clip(0), pitch ** 0.35)


# ================================================================ B · musical

class Musical:
    key = 'B'
    title = '音乐'

    def __init__(self, root=62):
        self.root = root

    def n(self, degree):
        return penta_note(self.root, degree)

    def step(self, dry, v):
        r = Rand(1100 + v * 7 + (500 if dry else 0))
        m = self.n(-5 + r.int(5))                          # the octave below the root
        y = felt_piano(m, vel=r.range(0.15, 0.3) * (1.3 if dry else 1.0), dur=0.3,
                       damped_after=r.range(0.025, 0.045) * (0.8 if dry else 1.0), seed=v, bright=0.75 if dry else 0.6,
                       thump=0.35)
        y = lp(y, 3200 if dry else 2400, 0.7)
        return finish(y, 30)

    def touch(self, v):
        r = Rand(2100 + v * 13)
        d = r.pick([3, 4, 5])
        y = np.zeros(secs(1.9))
        place(y, glass_bell(self.n(d), 0.45, dur=1.8, seed=v), 0)
        place(y, glass_bell(self.n(d + 2), 0.5, dur=1.7, seed=v + 50) * 0.9, secs(0.11))
        y = with_reverb(y, 0.3, rt60=1.5, size=0.9, damp=0.45, predelay_ms=8)
        return finish(y, 150)

    def flare(self, v):
        r = Rand(3100 + v * 17)
        d = 5 + r.int(5)                                    # one octave up from the root
        y = np.zeros(secs(2.2))
        place(y, glass_bell(self.n(d - 1), 0.3, dur=1.5, seed=v + 3) * 0.45, 0)
        place(y, glass_bell(self.n(d), 0.55, dur=2.1, seed=v + 4), secs(0.05))
        n = secs(0.5)
        airy = bp(pink(n, 3100 + v), 3200, 0.8) * swell(n, 0.03, 0.3)
        place(y, unit(airy) * 0.05, 0)
        y = with_reverb(y, 0.32, rt60=1.8, size=1.0, damp=0.45, predelay_ms=10)
        return finish(y, 200)

    def map_open(self):
        y = np.zeros(secs(1.3))
        place(y, harp(self.n(-2), 0.5, dur=1.2, seed=1), 0)
        place(y, harp(self.n(0), 0.55, dur=1.2, seed=2), secs(0.07))
        y = with_reverb(y, 0.15, rt60=1.0, size=0.7, damp=0.5, predelay_ms=5)
        return finish(y, 150)

    def map_close(self):
        y = np.zeros(secs(0.9))
        place(y, harp(self.n(0), 0.45, dur=0.8, seed=3, tau=0.6), 0)
        place(y, harp(self.n(-2), 0.4, dur=0.8, seed=4, tau=0.5), secs(0.06))
        y = with_reverb(y, 0.15, rt60=1.0, size=0.7, damp=0.5, predelay_ms=5)
        return finish(y, 120)

    def gate(self):
        y = np.zeros(secs(4.2))
        chord = [self.n(-5), self.n(-2), self.n(1), self.n(2), self.n(3)]   # D3 A3 E4 F#4 A4: D add9
        for i, m in enumerate(chord):
            place(y, felt_piano(m, vel=0.42 - 0.04 * i, dur=4.0, seed=20 + i), secs(0.045 * i))
        n = secs(3.6)
        t = t_axis(n)
        pad = np.zeros(n)
        for m in (self.n(0), self.n(3), self.n(6)):
            f = midi_hz(m)
            for h, a in ((1, 1.0), (2, 0.3), (3, 0.12)):
                pad += a * np.sin(2 * math.pi * f * h * t) * (1 + 0.15 * np.sin(2 * math.pi * 0.4 * t + m))
        pad *= swell(n, 0.6, 2.6)
        place(y, unit(pad) * 0.18, secs(0.1))
        y = with_reverb(y, 0.38, rt60=3.0, size=1.3, damp=0.45, predelay_ms=15)
        return finish(y, 300)

    def guide(self):
        y = np.zeros(secs(3.0))
        place(y, glass_bell(self.n(3), 0.4, dur=2.5, seed=31), 0)
        place(y, glass_bell(self.n(6), 0.35, dur=2.4, seed=32) * 0.85, secs(0.42))
        y = lp(y, 3500, 0.7)
        y = with_reverb(y, 0.55, rt60=2.6, size=1.2, damp=0.5, predelay_ms=25)
        return finish(y, 250)

    def reply(self, pitch=1.0):
        m = self.n(degree_for_pitch(pitch))
        y = np.zeros(secs(2.6))
        place(y, felt_piano(m, vel=0.45, dur=2.5, seed=40), 0)
        place(y, glass_bell(m + 12, 0.35, dur=2.0, seed=41) * 0.2, secs(0.01))
        y = with_reverb(y, 0.3, rt60=1.8, size=1.0, damp=0.45, predelay_ms=10)
        return finish(y, 200)


# ================================================================ C · water and wood

class WaterWood:
    key = 'C'
    title = '水和木'

    def __init__(self, root=62):
        self.root = root

    def n(self, degree):
        return penta_note(self.root, degree)

    def step(self, dry, v):
        r = Rand(1200 + v * 7 + (500 if dry else 0))
        y = np.zeros(secs(0.18))
        feet = 3 if r.u() < 0.65 else 2
        t = 0.003
        for _ in range(feet):
            at = secs(t)
            pat = burst(r.range(9, 16), r.int(1 << 30), 'lp', r.range(650, 1000) * (1.5 if dry else 1.0), 0.7,
                        attack_ms=1.5, tau_ms=r.range(3.5, 6.0))
            place(y, unit(pat) * r.range(0.6, 1.0), at)
            sq = burst(4, r.int(1 << 30), 'bp', r.range(1600, 2200), 2.0, attack_ms=0.5, tau_ms=1.2)
            place(y, unit(sq) * (0.06 if dry else 0.1), at + ms(1))
            if not dry and r.u() < 0.2:
                place(y, plink(r.range(2200, 2800), rise=0.6, tau=0.004, dur=0.02) * 0.06, at + ms(r.range(5, 30)))
            if dry and r.u() < 0.35:
                place(y, unit(tick(r.range(1800, 3000), 0.0012)) * 0.12, at + ms(r.range(2, 20)))
            t += r.range(0.006, 0.02)
        y = lp(y, 3600 if dry else 2400, 0.7)
        y = hp(y, 110, 0.7)
        return finish(y, 10)

    def touch(self, v):
        r = Rand(2200 + v * 13)
        d = r.pick([3, 4, 5])
        y = np.zeros(secs(0.9))
        place(y, bamboo(self.n(d), 0.45, dur=0.6, seed=v, short=0.8), 0)
        place(y, bamboo(self.n(d + 2), 0.5, dur=0.6, seed=v + 50, short=0.8) * 0.9, secs(0.095))
        y = with_reverb(y, 0.14, rt60=0.9, size=0.6, damp=0.5, predelay_ms=5)
        return finish(y, 80)

    def flare(self, v):
        r = Rand(3200 + v * 17)
        d = 5 + r.int(5)
        y = drop_bell(self.n(d), 0.55, dur=2.4, seed=v)
        y = with_reverb(y, 0.4, rt60=2.2, size=1.1, damp=0.55, predelay_ms=12)
        return finish(y, 200)

    def _scroll(self, degrees, gaps, seed, sweep, dur):
        r = Rand(seed)
        y = np.zeros(secs(dur))
        t = 0.0
        for i, d in enumerate(degrees):
            k = bamboo(self.n(d), r.range(0.25, 0.35), dur=0.3, seed=seed + i, short=0.45, hollow=0.5)
            place(y, k * (1 - 0.25 * i / len(degrees)), secs(t))
            if i < len(gaps):
                t += gaps[i]
        n = secs(dur)
        tt = t_axis(n)
        sw = svf(pink(n, seed + 99), np.interp(tt, [0, dur], sweep), 0.7, 'lp') * swell(n, 0.12, dur - 0.15)
        place(y, unit(sw) * 0.12 * peak(y), 0)
        return with_reverb(y, 0.12, rt60=0.8, size=0.6, damp=0.5, predelay_ms=4)

    def map_open(self):
        return finish(self._scroll([0, 1, 2, 3, 4], [0.03, 0.034, 0.038, 0.044], 8001, (900, 1800), 0.7), 80)

    def map_close(self):
        return finish(self._scroll([4, 3, 2, 0], [0.026, 0.026, 0.03], 8002, (1600, 700), 0.55), 80)

    def gate(self):
        dur = 3.4
        n = secs(dur)
        f = midi_hz(self.n(-5))                          # a tube sounding D3: its odd harmonics
        y = air_tone(dur, [f, 3 * f, 5 * f, 7 * f], [30, 30, 25, 20], [1.0, 0.55, 0.3, 0.15], 9001,
                     swell(n, 0.8, 2.0), breath_gain=0.25, breath_f=800, wobble=0.003)
        y = unit(y)
        y = np.concatenate([y, np.zeros(secs(1.6))])
        place(y, drop_bell(self.n(8), 0.3, dur=2.0, seed=91) * 0.35, secs(1.1))
        place(y, drop_bell(self.n(5), 0.25, dur=2.0, seed=92) * 0.3, secs(1.6))
        y = with_reverb(y, 0.38, rt60=2.6, size=1.3, damp=0.5, predelay_ms=15)
        return finish(y, 300)

    def guide(self):
        y = np.zeros(secs(3.6))
        for (d, at, vel, s) in ((6, 0.0, 0.35, 61), (4, 0.55, 0.3, 62), (5, 1.2, 0.32, 63)):
            place(y, drop_bell(self.n(d), vel, dur=2.3, seed=s), secs(at))
        y = lp(y, 3500, 0.7)
        y = with_reverb(y, 0.6, rt60=2.8, size=1.3, damp=0.55, predelay_ms=25)
        return finish(y, 300)

    def reply(self, pitch=1.0):
        m = self.n(degree_for_pitch(pitch))
        y = np.zeros(secs(2.4))
        place(y, bamboo(m, 0.5, dur=0.8, seed=70), 0)
        place(y, drop_bell(m + 12, 0.3, dur=2.2, seed=71) * 0.45, secs(0.06))
        y = with_reverb(y, 0.3, rt60=1.8, size=1.0, damp=0.5, predelay_ms=10)
        return finish(y, 200)
