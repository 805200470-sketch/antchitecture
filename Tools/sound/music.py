"""
A quiet bed of nest air (new) and a sketch of generative ambient music, one key per nest.

Keys: the colour wheel read as the circle of fifths, so neighbouring nests in the ring are a fifth
apart and share four of the five notes of their pentatonic scale:
  kiln 0° A-flat, amber 30° E-flat, honey 60° B-flat, leafcut 90° F, moss 120° C, blue 180° D.

The music: a slow pad (warm additive voices with a breath of air in them) moving through four chords
of the nest's key, and now and then a felt-piano note or two from the pentatonic, all in a large dark
reverb. It sits under the bed of air.
"""
import math

import numpy as np

from dsp import (SR, TAU, Rand, noise, pink, brown, lp, hp, bp, svf, onset, fade_out, t_axis, ms, secs, place,
                 peak, rms, fdn_reverb, midi_hz, pan, lufs_integrated)
from voices import felt_piano, glass_bell, plink, crackle, penta_note, tick

# root of each nest's key (MIDI, the tonic placed between F#3 and F4)
NEST_ROOT = {'kiln': 56, 'amber': 63, 'honey': 58, 'leafcut': 65, 'moss': 60, 'blue': 62}


def value_noise(n, rate_hz, seed, smooth=True):
    """Slow random wandering in [-1, 1]: random points every 1/rate_hz s, joined by cosine easing."""
    r = Rand(seed)
    step = max(1, int(SR / rate_hz))
    pts = [r.range(-1, 1) for _ in range(n // step + 3)]
    i = np.arange(n)
    k = i // step
    u = (i % step) / step
    if smooth:
        u = 0.5 - 0.5 * np.cos(np.pi * u)
    a = np.array(pts)[k]
    b = np.array(pts)[k + 1]
    return a + (b - a) * u


# ---------------------------------------------------------------- bed

def nest_bed(dur=40.0, seed=5, wet_drips=True, tunnel=False):
    """Night air inside the nest (stereo): a dark, slowly breathing room tone, a little air, the far
    patter of the colony (thousands of tiny feet, like the finest rain) and now and then a drip far off.
    tunnel: darker, no patter, drips more often."""
    n = secs(dur)
    out = np.zeros((2, n))
    for c in range(2):
        room = lp(brown(n, seed + c, leak=0.997), 120 if tunnel else 160, 0.5)
        room *= 0.65 + 0.35 * value_noise(n, 0.08, seed + 10 + c)
        air = bp(pink(n, seed + 20 + c), 330, 0.8)
        air *= 0.7 + 0.3 * value_noise(n, 0.11, seed + 60 + c)
        high = hp(pink(n, seed + 30 + c), 3500, 0.7)
        room = hp(hp(room, 40, 0.7), 40, 0.7)        # a room you feel, not a rumble that moves the speaker
        out[c] = room / rms(room) + 0.07 * air / rms(air) + 0.006 * high / rms(high)
    # the colony, far off: a fine patter, spread wide and softened by the distance
    if not tunnel:
        patter = np.zeros((2, n))
        for c in range(2):
            p = crackle(dur, lambda s: 55.0, seed + 40 + c, 1800, 4200, (0.3, 0.8), alpha=1.8)
            patter[c] = lp(p, 3000, 0.7)
        pr = fdn_reverb(patter[0] + patter[1], rt60=1.6, size=1.1, damp=0.5, predelay_ms=10, stereo=True, tail=0)
        patter = 0.6 * patter + 0.8 * pr[:, :n]
        out += 0.035 * patter / (rms(patter[0]) + 1e-9)
    # drips, far off in the dark
    if wet_drips:
        r = Rand(seed + 50)
        dr = np.zeros((2, n))
        t = r.range(1.5, 3.0)
        while t < dur - 3.5:
            d = plink(r.range(750, 1250), rise=r.range(1.1, 1.7), tau=0.014, dur=0.1) * r.range(0.4, 1.0)
            pp = pan(d, r.range(-0.8, 0.8))
            for c in range(2):
                place(dr[c], pp[c], secs(t))
            t += r.range(2.0, 4.5) if tunnel else r.range(3.0, 7.0)
        rv = fdn_reverb(dr[0] + dr[1], rt60=2.6, size=1.3, damp=0.55, predelay_ms=20, stereo=True, tail=0)
        out += 0.12 * (0.3 * dr + rv[:, :n])
    for c in range(2):
        out[c] = hp(out[c], 35, 0.7)
    return out


# ---------------------------------------------------------------- music

CHORDS = [            # semitones from the root (D4 in the blue nest): open voicings, low to high
    [-12, -5, 2, 4],      # I add9      D3 A3 E4 F#4
    [-15, -8, 0, 2],      # vi add11    B2 F#3 D4 E4
    [-19, -12, -3, 4],    # IV maj7     G2 D3 B3 F#4
    [-17, -10, -3, 0],    # V sus       A2 E3 B3 D4
]


def pad_voice(m, n, seed, level=1.0):
    """A warm sustained note: a few harmonics that wander slowly in level, two copies a few cents
    apart, and a breath of air at its pitch."""
    f = midi_hz(m)
    t = t_axis(n)
    y = np.zeros(n)
    harm = [1.0, 0.42, 0.2, 0.11, 0.05, 0.025]
    for h, a in enumerate(harm, start=1):
        if f * h > 4000:
            break
        drift = 1 + 0.3 * value_noise(n, 0.12, seed * 31 + h)
        for cents, g, ph in ((0.0, 0.5, 0.3 * h), (+2.6, 0.3, 1.7 * h), (-3.4, 0.22, 2.9 * h)):
            fh = f * h * 2 ** (cents / 1200)
            y += g * a * drift * np.sin(TAU * fh * t + ph)
    br = svf(pink(n, seed * 7 + 3), f, 40.0, 'bp')
    y += 0.25 * br / (rms(br) + 1e-9) * rms(y) * 0.5
    return y * level


def music_sketch(root=62, dur=40.0, seed=1, bed_lead=3.0, fade_at=33.0, melody_db=-2.0):
    """The sketch (stereo): the bed alone first, the music rising under it, four chords, a few notes,
    then the music leaves and the bed stays."""
    n = secs(dur)
    pad = np.zeros((2, n))
    chord_len = (fade_at - bed_lead) / len(CHORDS)
    xf = 3.0
    for ci, chord in enumerate(CHORDS):
        start = bed_lead + ci * chord_len - (xf if ci else 0)
        length = chord_len + (xf if ci else 0) + xf
        a, b = secs(max(0, start)), min(n, secs(start + length))
        m = b - a
        env = np.ones(m)
        k = secs(xf if ci else 4.0)
        env[:k] = 0.5 - 0.5 * np.cos(np.pi * np.arange(k) / k)
        k2 = secs(xf if ci < len(CHORDS) - 1 else 5.0)
        env[-k2:] *= 0.5 + 0.5 * np.cos(np.pi * np.arange(k2) / k2)
        for vi, iv in enumerate(chord):
            level = 0.55 if vi == 0 else (0.8 if vi < len(chord) - 1 else 0.65)
            v = pad_voice(root + iv, m, seed * 100 + ci * 10 + vi, level) * env
            p = pan(v, [-0.05, -0.35, 0.3, 0.45][vi % 4])
            pad[:, a:b] += p
    pad[0] = hp(lp(pad[0], 2400, 0.6), 40, 0.7)
    pad[1] = hp(lp(pad[1], 2400, 0.6), 40, 0.7)
    # a few notes from the pentatonic, wandering
    r = Rand(seed + 9)
    mel = np.zeros((2, n))
    t = bed_lead + 3.0
    deg = 5
    while t < fade_at - 3.0:
        deg = max(2, min(9, deg + r.pick([-2, -1, -1, 1, 1, 2])))
        note = penta_note(root, deg)
        v = felt_piano(note, vel=r.range(0.18, 0.3), dur=4.0, seed=r.int(1000))
        place_st(mel, pan(v, r.range(-0.3, 0.3)), secs(t))
        if r.u() < 0.3:
            d2 = max(1, min(10, deg + r.pick([-2, 2, 3])))
            v2 = felt_piano(penta_note(root, d2), vel=r.range(0.14, 0.22), dur=4.0, seed=r.int(1000))
            place_st(mel, pan(v2, r.range(-0.3, 0.3)), secs(t + r.range(0.35, 0.7)))
        t += r.range(2.6, 5.4)
    for at, d in ((12.5, 10), (26.0, 9)):
        g = glass_bell(penta_note(root, d), 0.3, dur=3.0, seed=int(at))
        place_st(mel, pan(g * 0.35, 0.25 if d == 10 else -0.25), secs(at))
    # one large dark room for all of it
    mono_pad = pad[0] + pad[1]
    rp = fdn_reverb(mono_pad, rt60=4.5, size=1.6, damp=0.45, predelay_ms=20, stereo=True, tail=0)
    rm = fdn_reverb(mel[0] + mel[1], rt60=4.5, size=1.6, damp=0.45, predelay_ms=20, stereo=True, tail=0)
    pad_w = pad / rms(pad) + 0.35 * rp[:, :n] / rms(rp)
    mel_w = mel / (rms(mel) + 1e-9) + 0.9 * rm[:, :n] / (rms(rm) + 1e-9)
    # the notes float a little under the pad: by loudness, melody_db relative to it
    pad_w *= 10 ** ((-20.0 - lufs_integrated(pad_w)) / 20)
    mel_w *= 10 ** ((-20.0 + melody_db - lufs_integrated(mel_w)) / 20)
    music = pad_w + mel_w
    for c in range(2):
        music[c] = fade_out(hp(hp(music[c], 35, 0.7), 35, 0.7), 200)
    return music


def place_st(dst, src, at):
    for c in range(2):
        place(dst[c], src[c], at)


# ================================================================ the parts the game plays (Music.cs)
#
# The game makes these once and plays them through a small scheduler: a chord every PERIOD seconds (each rising while
# the one before is released), now and then a note or two, rarely a glass bell. They are made in REF_ROOT and played
# higher or lower (AudioSource.pitch) for each nest's key, so one set serves all six.

REF_ROOT = 61                     # C#4: every nest's key is at most five semitones from it
PERIOD, ATTACK, RELEASE = 8.5, 3.0, 4.0
NOTE_DEGREES = list(range(2, 10))  # E4..B5 in D; the scheduler wanders over these
BELL_DEGREES = [9, 10]


def chord_clip(ci, root=REF_ROOT, seed=1):
    """One chord of the pad as the game plays it (stereo): rising over ATTACK, held until the next chord starts,
    released over RELEASE, with its large dark room baked in."""
    length = PERIOD + RELEASE
    n = secs(length)
    env = np.ones(n)
    k = secs(ATTACK)
    env[:k] = 0.5 - 0.5 * np.cos(np.pi * np.arange(k) / k)
    k2 = secs(RELEASE)
    env[n - k2:] *= 0.5 + 0.5 * np.cos(np.pi * np.arange(k2) / k2)
    pad = np.zeros((2, n))
    chord = CHORDS[ci]
    for vi, iv in enumerate(chord):
        level = 0.55 if vi == 0 else (0.8 if vi < len(chord) - 1 else 0.65)
        v = pad_voice(root + iv, n, seed * 100 + ci * 10 + vi, level) * env
        pp = pan(v, [-0.05, -0.35, 0.3, 0.45][vi % 4])
        pad += pp
    pad[0] = hp(lp(pad[0], 2400, 0.6), 40, 0.7)
    pad[1] = hp(lp(pad[1], 2400, 0.6), 40, 0.7)
    rp = fdn_reverb(pad[0] + pad[1], rt60=4.5, size=1.6, damp=0.45, predelay_ms=20, stereo=True, tail=4.5)
    out = np.zeros((2, rp.shape[1]))
    out[:, :n] += pad / rms(pad)
    out += 0.35 * rp / rms(rp)
    for c in range(2):
        out[c] = fade_out(hp(out[c], 35, 0.7), 300)
    return out


def note_clip(degree, root=REF_ROOT, seed=None, bell=False):
    """A felt-piano note of the pentatonic (or a glass bell), centred, with the same room baked in (stereo)."""
    m = penta_note(root, degree)
    seed = degree if seed is None else seed
    v = glass_bell(m, 0.3, dur=3.0, seed=seed) * 0.35 if bell else felt_piano(m, vel=0.24, dur=4.0, seed=seed)
    rm = fdn_reverb(v, rt60=4.5, size=1.6, damp=0.45, predelay_ms=20, stereo=True, tail=4.5)
    out = np.zeros((2, rm.shape[1]))
    out[:, :len(v)] += v * 0.7071 / rms(v)
    out += 0.9 * rm / rms(rm)
    for c in range(2):
        out[c] = fade_out(hp(out[c], 35, 0.7), 300)
    return out


def bed_loop(tunnel=False, dur=24.0, seed=5):
    """The bed of air as a seamless loop (stereo): the nest's, or a tunnel's (darker, no patter, more drips)."""
    n = secs(dur)
    m = secs(0.5)
    x = nest_bed(dur + 0.5, seed=seed + (100 if tunnel else 0), tunnel=tunnel)
    w = np.arange(m) / m
    out = x[:, :n].copy()
    out[:, :m] = x[:, :m] * w + x[:, n:n + m] * (1 - w)
    return out
