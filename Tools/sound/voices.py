"""
Instrument voices for the new sound directions: small physical models, all portable to C#.

  tick         a resonant click (a foot, an antenna, a grain of litter)
  crackle      a cloud of ticks with power-law loudness (dry leaf, spores, a far colony)
  felt_piano   a piano with felt on the hammers: stretched partials, two strings beating, soft thump
  glass_bell   a small glass/celesta bell, soft mallet, slowly shimmering
  harp         a plucked string (Karplus-Strong)
  bamboo       a hollow bamboo/wood knock with a pitch
  plink        a water drop: a bubble whose pitch rises as it rings
  drop_bell    a drop falling into a buried jar that rings (suikinkutsu)
  air_tone     breath through a hollow (noise through narrow resonances)
"""
import math

import numpy as np

from dsp import (SR, TAU, Rand, noise, pink, lp, hp, bp, filt, onset, fade_out, t_axis, ms, secs, place,
                 pad_to, midi_hz, resonator, ks_string, svf)


# ---------------------------------------------------------------- scales

PENTA = [0, 2, 4, 7, 9]           # major pentatonic degrees (semitones)


def penta_note(root_midi, degree):
    """Degree 0,1,2... up the major pentatonic from root (negative goes down)."""
    octave, d = divmod(degree, 5)
    return root_midi + 12 * octave + PENTA[d]


# ---------------------------------------------------------------- small things

def mallet_weight(f, contact_ms):
    """How much of a mode a soft mallet of this contact time excites (a smooth 2nd-order low-pass)."""
    fc = 0.6 / (contact_ms / 1000.0)
    return 1.0 / math.sqrt(1.0 + (f / fc) ** 4)


def modes(dur, freqs, amps, taus, contact_ms=2.0, phase_seed=None):
    """Sum of decaying sine modes struck by a soft mallet (contact_ms), with a soft onset."""
    n = secs(dur)
    t = t_axis(n)
    y = np.zeros(n)
    r = Rand(phase_seed) if phase_seed is not None else None
    for f, a, tau in zip(freqs, amps, taus):
        if f >= SR * 0.45 or a == 0:
            continue
        ph = r.range(0, TAU) if r else 0.0
        y += a * mallet_weight(f, contact_ms) * np.exp(-t / tau) * np.sin(TAU * f * t + ph)
    y = onset(y, contact_ms)
    return fade_out(y, max(30.0, dur * 150.0))      # the last 15 %: a note cut short never clicks


def tick(f, tau, amp=1.0, f2_ratio=None, onset_ms=0.25, dur=None):
    """A resonant click: one or two very short modes (chitin, a twig, an antenna)."""
    if dur is None:
        dur = tau * 7 + 0.002
    n = secs(dur)
    t = t_axis(n)
    y = np.exp(-t / tau) * np.sin(TAU * f * t)
    if f2_ratio:
        y += 0.45 * np.exp(-t / (tau * 0.7)) * np.sin(TAU * f * f2_ratio * t + 1.1)
    return onset(y * amp, onset_ms)


def burst(dur_ms, seed, kind='bp', f=1000.0, q=1.0, attack_ms=0.5, tau_ms=None):
    """A short burst of filtered noise with a soft attack and an exponential fall."""
    n = ms(dur_ms) + 1
    t = t_axis(n)
    tau = (tau_ms if tau_ms else dur_ms / 3.0) / 1000.0
    x = noise(n, seed) * np.exp(-t / tau)
    x = onset(x, attack_ms)
    if kind == 'bp':
        x = bp(x, f, q)
    elif kind == 'lp':
        x = lp(x, f, q)
    elif kind == 'hp':
        x = hp(x, f, q)
    return fade_out(x, min(dur_ms * 0.3, 3.0))


def crackle(dur, rate_fn, seed, f_lo=2500, f_hi=6500, tau_ms=(0.3, 0.9), alpha=1.6, amp=1.0):
    """A cloud of ticks: Poisson times at rate_fn(t) per second, Pareto (power-law) loudness, so most
    are faint and a few stand out, as in dry litter or settling dust."""
    r = Rand(seed)
    n = secs(dur)
    y = np.zeros(n)
    t = 0.0
    peak_rate = max(rate_fn(x * dur / 50.0) for x in range(51)) + 1e-6
    while True:
        t += -math.log(1.0 - r.u() * 0.999999) / peak_rate      # thinning: candidates at the peak rate
        if t >= dur:
            break
        if r.u() > rate_fn(t) / peak_rate:
            continue
        a = min(1.0, 0.08 * (1.0 - r.u()) ** (-1.0 / alpha))   # Pareto, mostly small
        f = r.range(f_lo, f_hi)
        tau = r.range(*tau_ms) / 1000.0
        place(y, tick(f, tau, a * r.sign(), f2_ratio=r.range(1.4, 2.1)), secs(t))
    return y * amp


# ---------------------------------------------------------------- felt piano

def felt_piano(m, vel=0.5, dur=None, damped_after=None, seed=0, bright=1.0, thump=1.0):
    """A felt piano note: partials stretched by the string's stiffness, the hammer's felt taking the
    highs off (softer = darker), two strings a fraction of a cent apart beating, a two-stage decay
    (prompt, then the long aftersound), and the soft thump of the hammer. damped_after (s): the damper
    falls then (a muted note)."""
    f0 = midi_hz(m)
    r = Rand(seed + 101)
    B = 1.0e-4 * 2 ** ((m - 60) / 18.0)
    tau1 = float(np.clip(3.0 * (261.6 / f0) ** 0.65, 0.5, 7.0))
    if dur is None:
        dur = min(6.0, tau1 * 1.6)
    n = secs(dur)
    t = t_axis(n)
    fc = (450 + 2000 * vel) * bright
    y = np.zeros(n)
    beta = 0.12                                    # where the hammer strikes along the string
    for k in range(1, 18):
        fk = k * f0 * math.sqrt(1 + B * k * k)
        if fk > 7500:
            break
        a = abs(math.sin(math.pi * k * beta)) / k / (1 + (fk / fc) ** 2)
        tau = tau1 / (1 + 0.5 * (k - 1)) ** 0.85 / (1 + (fk / 3500.0) ** 2)
        cents = 0.7 * r.range(0.5, 1.5)
        fa, fb = fk * 2 ** (cents / 1200), fk * 2 ** (-cents / 1200)
        env = 0.55 * np.exp(-t / (0.22 * tau)) + 0.45 * np.exp(-t / tau)
        ph = r.range(0, TAU)
        y += a * env * 0.5 * (np.sin(TAU * fa * t + ph) + np.sin(TAU * fb * t + ph))
    y = onset(y, 3.0 + 3.0 * (1 - vel))
    # the felt hammer and the key: a soft low thump and a faint wooden knock
    th = lp(noise(secs(0.06), seed + 7) * np.exp(-t_axis(secs(0.06)) / 0.009), 240, 0.7)
    th = onset(th, 1.5) * (0.5 + 0.8 * vel)
    kn = bp(noise(secs(0.02), seed + 8) * np.exp(-t_axis(secs(0.02)) / 0.0025), 1150, 1.4)
    kn = onset(kn, 0.5) * 0.12
    place(y, th * (0.25 * thump * np.max(np.abs(y)) / (np.max(np.abs(th)) + 1e-9)), 0)
    place(y, kn * (np.max(np.abs(y)) / (np.max(np.abs(kn)) + 1e-9)) * 0.08, 0)
    if damped_after is not None:
        k0 = secs(damped_after)
        if k0 < n:
            y[k0:] *= np.exp(-(t[k0:] - t[k0]) / 0.03)
    y = lp(y, 5200, 0.6)
    return fade_out(y, max(30.0, dur * 150.0))


# ---------------------------------------------------------------- glass bell

def glass_bell(m, vel=0.5, dur=2.6, seed=0, contact_ms=2.4):
    """A small glass/celesta bell: a clear fundamental that shimmers slowly (two modes a hair apart),
    a soft octave, and the bar's inharmonic modes (2.76, 5.40) dying fast for the glassy edge."""
    f0 = midi_hz(m)
    sc = (523.25 / f0) ** 0.4
    r = Rand(seed + 202)
    beat = r.range(0.5, 1.1)
    freqs = [f0 - beat / 2, f0 + beat / 2, 2.0 * f0, 2.76 * f0, 4.07 * f0, 5.40 * f0]
    amps = [0.84, 0.16, 0.14, 0.09 * (0.6 + vel), 0.04 * (0.6 + vel), 0.025 * (0.6 + vel)]
    taus = [1.7 * sc, 1.7 * sc, 0.75 * sc, 0.4 * sc, 0.22 * sc, 0.14 * sc]
    y = modes(dur, freqs, amps, taus, contact_ms=contact_ms, phase_seed=seed + 3)
    tk = hp(noise(secs(0.004), seed + 9) * np.exp(-t_axis(secs(0.004)) / 0.0008), 3000) * 0.04 * vel
    place(y, tk, 0)
    return lp(y, 6500, 0.6)


# ---------------------------------------------------------------- harp

def harp(m, vel=0.5, dur=2.2, seed=0, tau=None, bright=0.45):
    """A plucked string (Karplus-Strong): a soft, finger-plucked burst into a tuned loop."""
    f0 = midi_hz(m)
    if tau is None:
        tau = float(np.clip(1.6 * (220.0 / f0) ** 0.5, 0.5, 3.0))
    n = secs(dur)
    L = int(SR / f0)
    exc = noise(L, seed + 31)
    exc = lp(exc, 900 + 2600 * vel, 0.6)                 # a finger, not a pick
    p = max(1, int(0.18 * L))                             # plucked near one end
    exc = exc - np.concatenate([np.zeros(p), exc[:-p]])
    exc = onset(exc, 1.0)
    y = ks_string(f0, n, exc, tau, bright=bright)
    y = filt(y, 'peak', 220, 1.0, 3.0)                    # a little body
    y = filt(y, 'highshelf', 3000, 0.7, -4.0)
    return fade_out(lp(y, 6000, 0.6), max(30.0, dur * 200.0))


# ---------------------------------------------------------------- bamboo / wood

def bamboo(m, vel=0.5, dur=0.7, seed=0, short=1.0, hollow=1.0):
    """A hollow bamboo or wood knock with a pitch: the air in the tube (1, 3) rings a moment, the
    tube's own wood (2.42, 5.1) only a few milliseconds, over the knock of the mallet."""
    f0 = midi_hz(m)
    sc = (440.0 / f0) ** 0.3 * short
    freqs = [f0, 3.0 * f0, 2.42 * f0, 5.1 * f0]
    amps = [1.0, 0.2, 0.26, 0.09]
    taus = [0.17 * sc, 0.065 * sc, 0.032 * sc, 0.016 * sc]
    y = modes(dur, freqs, amps, taus, contact_ms=1.3, phase_seed=seed + 5)
    kn = burst(6, seed + 41, 'bp', 2100, 1.1, attack_ms=0.3, tau_ms=1.3)
    place(y, kn * (0.22 * vel / (np.max(np.abs(kn)) + 1e-9)) * np.max(np.abs(y)), 0)
    air = resonator(noise(secs(0.08), seed + 42) * np.exp(-t_axis(secs(0.08)) / 0.012), f0, 0.045)
    air = onset(air, 1.0)
    place(y, air * (0.18 * hollow / (np.max(np.abs(air)) + 1e-9)) * np.max(np.abs(y)), 0)
    return lp(y, 6500, 0.6)


# ---------------------------------------------------------------- water

def plink(f0=1000.0, rise=1.5, tau=0.014, chirp_ms=11.0, dur=0.09, onset_ms=0.25):
    """A water drop: the bubble's note rises as it shrinks and rings out (after van den Doel)."""
    n = secs(dur)
    t = t_axis(n)
    f = f0 * (1 + rise * (1 - np.exp(-t / (chirp_ms / 1000.0))))
    ph = TAU * np.cumsum(f) / SR
    y = np.sin(ph) * np.exp(-t / tau)
    return onset(y, onset_ms)


def rise_fall(n, attack, tau):
    """A raised-cosine rise over `attack` s, then an exponential fall with time constant tau."""
    t = t_axis(n)
    e = np.exp(-np.maximum(t - attack, 0.0) / tau)
    k = min(n, secs(attack))
    e[:k] = 0.5 - 0.5 * np.cos(np.pi * np.arange(k) / max(1, secs(attack)))
    return e


def drop_bell(m, vel=0.5, dur=2.6, seed=0):
    """A drop into a buried jar (suikinkutsu): a small plink, and the jar answering with a soft bell."""
    f = midi_hz(m)
    r = Rand(seed + 303)
    pl = plink(r.range(900, 1400), rise=r.range(1.2, 1.8), tau=0.011)
    sc = (700.0 / f) ** 0.3
    beat = r.range(0.4, 0.9)
    freqs = [f - beat / 2, f + beat / 2, 2.01 * f, 2.76 * f, 4.07 * f]
    amps = [0.84, 0.16, 0.22, 0.12, 0.05]
    taus = [1.35 * sc, 1.35 * sc, 0.55 * sc, 0.3 * sc, 0.15 * sc]
    jar = modes(dur, freqs, amps, taus, contact_ms=4.0, phase_seed=seed + 11)
    y = np.zeros(secs(dur))
    place(y, pl * 0.32, 0)
    place(y, jar * (0.6 + 0.4 * vel), ms(4))
    return lp(y, 5000, 0.6)


# ---------------------------------------------------------------- air

def air_tone(dur, freqs, qs, gains, seed, env, breath_gain=0.1, breath_f=900, wobble=0.004):
    """Breath through a hollow: pink noise through narrow resonances (a bottle, a reed, a tube),
    each drifting a little in pitch, over a broad breath. env: array of len secs(dur)."""
    n = secs(dur)
    src = pink(n, seed)
    y = np.zeros(n)
    t = t_axis(n)
    r = Rand(seed + 77)
    for f, q, g in zip(freqs, qs, gains):
        drift = 1 + wobble * np.sin(TAU * r.range(0.15, 0.35) * t + r.range(0, TAU))
        y += g * svf(src, f * drift, q, 'bp') * math.sqrt(q)
    y += breath_gain * bp(src, breath_f, 0.6)
    return y * env


def swell(n, attack, release, curve=1.0):
    """A smooth rise over `attack` s then a fall over `release` s (raised-cosine segments)."""
    a, rl = secs(attack), secs(release)
    e = np.zeros(n)
    k = min(a, n)
    e[:k] = (0.5 - 0.5 * np.cos(np.pi * np.arange(k) / max(1, a))) ** curve
    m = min(rl, n - k)
    if m > 0:
        e[k:k + m] = (0.5 + 0.5 * np.cos(np.pi * np.arange(m) / max(1, rl))) ** curve
    return e
