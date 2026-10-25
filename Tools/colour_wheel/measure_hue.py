"""Dominant glow / accent hue of a set of images: hue histogram of bright, saturated pixels.

    VMIN=0.6 SMIN=0.3 python3 Tools/colour_wheel/measure_hue.py '{"amber": ["a.png", "b.png"]}' out.json


Pixels with V >= VMIN and S >= SMIN are kept and weighted by S * V^2 (so glows count more than lit surfaces).
The histogram (1 degree bins) is smoothed circularly (sigma 5 degrees); the dominant hue is the weighted circular
mean within +-20 degrees of the highest peak. Also reports a secondary peak and the share of weight in each
12-segment bin (segment k = centre 30k, range 30k-15 .. 30k+15).
"""
import json
import sys

import numpy as np
from PIL import Image

import os
VMIN, SMIN = float(os.environ.get('VMIN', 0.45)), float(os.environ.get('SMIN', 0.35))
NAMES = ['红', '橙', '黄', '黄绿', '绿', '青绿', '青', '天蓝', '蓝', '紫', '品红', '玫红']


def hsv(path, maxw=960):
    im = Image.open(path).convert('RGB')
    if im.width > maxw:
        im = im.resize((maxw, round(im.height * maxw / im.width)), Image.BILINEAR)
    a = np.asarray(im).astype(np.float64) / 255
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx, mn = a.max(-1), a.min(-1)
    d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    rm = m & (mx == r)
    gm = m & (mx == g) & ~rm
    bm = m & ~rm & ~gm
    h[rm] = ((g - b)[rm] / d[rm]) % 6
    h[gm] = (b - r)[gm] / d[gm] + 2
    h[bm] = (r - g)[bm] / d[bm] + 4
    h = h * 60
    s = np.where(mx > 0, d / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def measure(paths):
    hist = np.zeros(360)
    total_px = kept_px = 0
    for p in paths:
        h, s, v = hsv(p)
        keep = (v >= VMIN) & (s >= SMIN)
        total_px += h.size
        kept_px += keep.sum()
        w = (s * v * v)[keep]
        hist += np.bincount(np.floor(h[keep]).astype(int) % 360, weights=w, minlength=360)
    # circular gaussian smoothing
    x = np.arange(-20, 21)
    k = np.exp(-0.5 * (x / 5.0) ** 2)
    k /= k.sum()
    sm = np.zeros(360)
    for i, kv in zip(x, k):
        sm += kv * np.roll(hist, i)
    peak = int(np.argmax(sm))
    win = [(peak + d) % 360 for d in range(-20, 21)]
    ang = np.deg2rad(np.array(win))
    wts = hist[win]
    mean = float(np.rad2deg(np.arctan2((np.sin(ang) * wts).sum(), (np.cos(ang) * wts).sum())) % 360)
    # secondary peak at least 40 degrees away
    sm2 = sm.copy()
    for d in range(-40, 41):
        sm2[(peak + d) % 360] = 0
    peak2 = int(np.argmax(sm2))
    seg_w = np.zeros(12)
    for deg in range(360):
        seg_w[((deg + 15) // 30) % 12] += hist[deg]
    seg_share = seg_w / max(seg_w.sum(), 1e-9)
    seg = int(((mean + 15) // 30) % 12)
    return {
        'hue': round(mean, 1), 'peak': peak, 'segment': seg, 'segment_name': NAMES[seg],
        'segment_share': round(float(seg_share[seg]), 3),
        'second_peak': peak2, 'second_strength': round(float(sm[peak2] / sm[peak]), 3),
        'kept_fraction': round(kept_px / total_px, 4),
        'top_segments': [(NAMES[int(i)], int(i) * 30, round(float(seg_share[int(i)]), 3)) for i in np.argsort(-seg_share)[:3]],
        'hist': [round(float(v), 4) for v in sm],
    }


if __name__ == '__main__':
    groups = json.loads(sys.argv[1])
    out = {}
    for name, paths in groups.items():
        r = measure(paths)
        out[name] = r
        print(f"{name:18s} hue {r['hue']:6.1f}  peak {r['peak']:3d}  seg {r['segment']*30:3d} {r['segment_name']}"
              f"  share {r['segment_share']:.2f}  2nd {r['second_peak']:3d} ({r['second_strength']:.2f})"
              f"  kept {r['kept_fraction']:.3%}  top {r['top_segments']}")
    if len(sys.argv) > 2:
        json.dump(out, open(sys.argv[2], 'w'), ensure_ascii=False, indent=1)
