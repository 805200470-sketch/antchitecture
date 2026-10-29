#!/usr/bin/env python3
"""measure_stills.py <label>=<run folder | glob> ... [--json out.json]

Colour measurements of game stills, for the colour reviewer (nest-colour-reviewer) and the palette checks. A run folder
(Tools/dev/shots_run.sh) means its way-in, halfway and deep stills from the game's view and the ant's eye
(out/r0_[123]_{play,eye}_lab_*.png); a glob means those files. For each set, in OKLCH:
  base / middle / highlights: the darkest half of the pixels, the middle, the brightest 3%: mean lightness and chroma
  loud: the share of pixels with chroma over 0.10 (v15: about 1%), neon: over 0.15
  world hue: the chroma-weighted mean hue of the middle layer; family spread: how widely the middle layer's hues
    spread round it (circular standard deviation, degrees, pixels with chroma over 0.03)
  accents: the share of middle and highlight pixels (chroma over 0.05) more than 90 degrees from the world hue
  floor: the bottom third of each still (mostly ground): mean linear luminance, mean chroma, and the share of it
    bright and coloured at once (lightness over 0.6 and chroma over 0.08: a glowing, dyed floor)
  brightness: mean linear luminance (7% border cropped; v15 0.036), colourfulness (Hasler and Süsstrunk's M)
  black: the share of near-black pixels (OKLCH lightness under 0.17, about sRGB 15 of 255), and the largest single
    near-black region of any still, as a share of its frame (lightness smoothed over 5 x 5 pixels first, so stars and
    sparse points in it do not break it up): a big hole of black, which the user does not want ("不要出现这样的一大片黑")
Prints a Markdown table; --json writes the numbers.
"""
import glob
import json
import os
import sys

from collections import deque

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from oklch import srgb_to_oklch, srgb_to_linear  # noqa: E402


def files(spec):
    if os.path.isdir(spec):
        return sorted(glob.glob(os.path.join(spec, 'out', 'r0_[123]_play_lab_*.png')) + glob.glob(os.path.join(spec, 'out', 'r0_[123]_eye_lab_*.png')))
    return sorted(glob.glob(spec))


def largest_hole(L):
    """The largest region of near-black (lightness, smoothed over 5 x 5, under 0.17), as a share of the frame; on a
    grid a third the size (4-connected flood fill, plain Python: run with -I, no scipy)."""
    k = 5
    pad = np.pad(L, k // 2, mode='edge')
    c = pad.cumsum(0).cumsum(1)
    c = np.pad(c, ((1, 0), (1, 0)))
    box = (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)
    dark = box[::3, ::3] < 0.17
    h, w = dark.shape
    seen = np.zeros_like(dark)
    best = 0
    for y0 in range(h):
        for x0 in range(w):
            if not dark[y0, x0] or seen[y0, x0]:
                continue
            n, q = 0, deque([(y0, x0)])
            seen[y0, x0] = True
            while q:
                y, x = q.popleft()
                n += 1
                for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                    if 0 <= yy < h and 0 <= xx < w and dark[yy, xx] and not seen[yy, xx]:
                        seen[yy, xx] = True
                        q.append((yy, xx))
            best = max(best, n)
    return best / dark.size


def measure(paths):
    lch_all, floor, rgb_all, lum_all, holes = [], [], [], [], []
    for p in paths:
        a = np.asarray(Image.open(p).convert('RGB').resize((480, 270)), dtype=np.float64) / 255
        h, w, _ = a.shape
        a = a[int(h * .07):int(h * .93), int(w * .07):int(w * .93)]
        lch = srgb_to_oklch(a)
        lch_all.append(lch.reshape(-1, 3))
        holes.append(largest_hole(lch[..., 0]))
        floor.append(lch[int(lch.shape[0] * 2 / 3):].reshape(-1, 3))
        lum_all.append((srgb_to_linear(a) @ np.array([0.2126, 0.7152, 0.0722])).reshape(-1))
        rgb_all.append(a.reshape(-1, 3))
    lch = np.concatenate(lch_all)
    fl = np.concatenate(floor)
    lum = np.concatenate(lum_all)
    rgb = np.concatenate(rgb_all)
    L, C, H = lch[:, 0], lch[:, 1], lch[:, 2]
    q50, q97 = np.percentile(L, 50), np.percentile(L, 97)
    base, mid, high = L <= q50, (L > q50) & (L < q97), L >= q97
    # the world hue: the middle layer's chroma-weighted mean hue
    m = mid & (C > 0.03)
    ang = np.radians(H[m])
    wgt = C[m]
    hue = float(np.degrees(np.arctan2((np.sin(ang) * wgt).sum(), (np.cos(ang) * wgt).sum())) % 360) if m.any() else float('nan')
    R = np.hypot((np.sin(ang) * wgt).sum(), (np.cos(ang) * wgt).sum()) / max(wgt.sum(), 1e-9)
    spread = float(np.degrees(np.sqrt(-2 * np.log(max(R, 1e-9))))) if m.any() else float('nan')
    col = (mid | high) & (C > 0.05)
    off = np.abs((H[col] - hue + 180) % 360 - 180)
    rg, yb = rgb[:, 0] - rgb[:, 1], 0.5 * (rgb[:, 0] + rgb[:, 1]) - rgb[:, 2]
    M = np.hypot(rg.std(), yb.std()) + 0.3 * np.hypot(rg.mean(), yb.mean())
    return dict(
        base_L=float(L[base].mean()), base_C=float(C[base].mean()), mid_L=float(L[mid].mean()), mid_C=float(C[mid].mean()),
        high_L=float(L[high].mean()), high_C=float(C[high].mean()), loud=float((C > 0.10).mean()), neon=float((C > 0.15).mean()),
        world_hue=hue, family_spread=spread, accents=float((off > 90).mean()) if col.any() else 0.0,
        floor_C=float(fl[:, 1].mean()), floor_glow=float(((fl[:, 0] > 0.6) & (fl[:, 1] > 0.08)).mean()),
        floor_L=float(fl[:, 0].mean()), brightness=float(lum.mean()), colourfulness=float(M * 100), stills=len(paths),
        black=float((L < 0.17).mean()), black_hole=max(holes))


def main():
    args = sys.argv[1:]
    out = None
    if '--json' in args:
        i = args.index('--json')
        out = args[i + 1]
        del args[i:i + 2]
    rows = {}
    for a in args:
        label, spec = a.split('=', 1)
        paths = files(spec)
        if paths:
            rows[label] = measure(paths)
    print('| 组 | 张数 | 亮度 | 底色 L / C | 中间 L / C | 高光 L / C | 彩度>0.10 面积 | >0.15 | 世界色相 | 色族展开 | 点缀 | 地面 L / C | 地面又亮又艳 | 鲜艳度 M | 死黑 | 最大黑块 |')
    print('|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|')
    for k, r in rows.items():
        print(f"| {k} | {r['stills']} | {r['brightness']:.3f} | {r['base_L']:.2f} / {r['base_C']:.3f} | {r['mid_L']:.2f} / {r['mid_C']:.3f} | "
              f"{r['high_L']:.2f} / {r['high_C']:.3f} | {r['loud']:.1%} | {r['neon']:.1%} | {r['world_hue']:.0f}° | {r['family_spread']:.0f}° | "
              f"{r['accents']:.1%} | {r['floor_L']:.2f} / {r['floor_C']:.3f} | {r['floor_glow']:.1%} | {r['colourfulness']:.1f} | "
              f"{r['black']:.0%} | {r['black_hole']:.0%} |")
    if out:
        json.dump(rows, open(out, 'w'), indent=1)


if __name__ == '__main__':
    main()
