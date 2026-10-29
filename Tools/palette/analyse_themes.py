#!/usr/bin/env python3
"""analyse_themes.py - v15's colours (NestTheme.Cyan) against the honey granary's and the leafcut garden's (NestTheme.Basic
as it builds them), sorted into the four layers, in OKLCH; and the same four layers measured on game stills. Prints a
Markdown report."""
import glob
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from oklch import srgb_to_oklch  # noqa: E402

W = (1.0, 1.0, 1.0)


def lerp(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def mul(a, k):
    return tuple(x * k for x in a)


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def grey(c):
    l = lum(c)
    return (l, l, l)


def basic(body6, acc, glow4, gacc, floor, fog, fill_gain=1.45):
    body = [lerp(grey(b), b, 0.7) for b in body6] + list(acc)
    g = glow4[0]
    glow = list(glow4) + [gacc[0], lerp(glow4[1], W, 0.35), lerp(glow4[2], W, 0.35), gacc[1]]
    return dict(body=body, glow=glow, scan=lerp(body[5], W, 0.5), ground=floor, floor=floor, bank=lerp(floor, body[3], 0.3), plateau=mul(floor, 0.8),
                edge=lerp(floor, W, 0.6), fog=fog, skyHorizon=mul(fog, 0.85), threadCore=lerp(g, W, 0.75), threadHalo=g,
                beacon=lerp(g, W, 0.15), grainGlow=mul(g, 0.9), lampCore=lerp(g, W, 0.85), moon=lerp((0.76, 0.76, 0.74), g, 0.12),
                fill=mul(fog, fill_gain), vineLeaf=mul(lerp(body[2], body[6], 0.3), 0.6), bloomTint=lerp(W, g, 0.12))


CYAN = dict(
    body=[(0.72, 0.80, 0.64), (0.66, 0.78, 0.50), (0.56, 0.78, 0.72), (0.64, 0.76, 0.78), (0.76, 0.80, 0.72), (0.54, 0.70, 0.64), (0.78, 0.88, 0.56), (0.84, 0.78, 0.60)],
    glow=[(0.55, 0.95, 0.98), (0.70, 0.96, 0.90), (0.78, 0.95, 0.70), (0.88, 0.72, 0.74), (0.78, 0.74, 0.90), (0.88, 0.84, 0.68), (0.85, 0.92, 0.92), (0.88, 0.76, 0.74)],
    ground=(0.5, 0.66, 0.62), floor=(0.45, 0.68, 0.62), bank=(0.55, 0.68, 0.62), plateau=(0.40, 0.55, 0.55), edge=(0.80, 0.86, 0.82),
    fog=(0.075, 0.13, 0.13), skyHorizon=(0.06, 0.11, 0.11), threadCore=(0.92, 0.95, 0.9), threadHalo=(0.55, 0.9, 0.95), beacon=(0.62, 0.86, 0.84),
    grainGlow=(0.42, 0.86, 0.84), lampCore=(0.95, 0.98, 0.96), moon=(0.66, 0.72, 0.72), fill=(0.13, 0.18, 0.18), vineLeaf=(0.3, 0.42, 0.26),
    bloomTint=(0.85, 1.0, 0.97), scan=(0.90, 0.94, 0.88))
HONEY = basic([(0.62, 0.42, 0.12), (0.58, 0.42, 0.16), (0.66, 0.50, 0.14), (0.62, 0.48, 0.26), (0.74, 0.60, 0.16), (0.84, 0.70, 0.30)],
              [(0.40, 0.24, 0.62), (0.86, 0.80, 0.62)],
              [(1.00, 0.92, 0.20), (1.00, 0.95, 0.30), (1.00, 0.90, 0.42), (0.98, 0.96, 0.55)], [(0.78, 0.95, 0.80), (0.95, 0.72, 0.60)],
              (0.62, 0.42, 0.20), (0.15, 0.10, 0.04))
LEAF = basic([(0.52, 0.66, 0.20), (0.58, 0.62, 0.42), (0.46, 0.66, 0.14), (0.60, 0.64, 0.46), (0.56, 0.68, 0.32), (0.78, 0.88, 0.56)],
             [(0.76, 0.28, 0.18), (0.94, 0.58, 0.20)],
             [(0.66, 1.00, 0.22), (0.78, 1.00, 0.40), (0.86, 1.00, 0.52), (0.72, 1.00, 0.30)], [(0.96, 0.95, 0.82), (0.95, 0.76, 0.46)],
             (0.56, 0.52, 0.24), (0.10, 0.11, 0.04))
LAYERS = [('1 底色', ['ground', 'floor', 'bank', 'plateau', 'fog', 'skyHorizon', 'fill']),
          ('2 中间层（植物色族）', ['body[0:6]', 'scan', 'vineLeaf', 'moon']),
          ('3 高光', ['glow[0:3]', 'threadCore', 'threadHalo', 'beacon', 'grainGlow', 'lampCore', 'bloomTint']),
          ('4 反差点缀', ['body[6:8]', 'glow[3:5]', 'glow[7:8]'])]


def colours(theme, key):
    if '[' in key:
        name, sl = key.split('[')
        a, b = map(int, sl[:-1].split(':'))
        return theme[name][a:b]
    return [theme[key]]


def fmt(theme, key):
    cs = colours(theme, key)
    lch = srgb_to_oklch(np.clip(np.array(cs), 0, 1))
    if len(cs) == 1:
        L, C, h = lch[0]
        return f'L {L:.2f} C {C:.3f} h {h:.0f}°'
    return f'L {lch[:, 0].min():.2f}–{lch[:, 0].max():.2f} C {lch[:, 1].min():.3f}–{lch[:, 1].max():.3f} h {", ".join(f"{h:.0f}" for h in lch[:, 2])}°'


def still_stats(paths):
    """the four layers on stills: the darkest half of the pixels (the base), the middle, the brightest 3% (highlights);
    their mean OKLCH lightness and chroma, the share of pixels with chroma over 0.10 and their mean hue spread."""
    out = []
    for p in paths:
        a = np.asarray(Image.open(p).convert('RGB').resize((480, 270)), dtype=np.float64) / 255
        h, w, _ = a.shape
        a = a[int(h * .07):int(h * .93), int(w * .07):int(w * .93)].reshape(-1, 3)
        out.append(srgb_to_oklch(a))
    lch = np.concatenate(out)
    L = lch[:, 0]
    q50, q97 = np.percentile(L, 50), np.percentile(L, 97)
    base, mid, high = lch[L <= q50], lch[(L > q50) & (L < q97)], lch[L >= q97]
    loud = (lch[:, 1] > 0.10).mean()
    return base[:, 0].mean(), base[:, 1].mean(), mid[:, 0].mean(), mid[:, 1].mean(), high[:, 0].mean(), high[:, 1].mean(), loud


def main():
    print('| 层 | 字段 | v15 蓝色真菌林 | 蜜罐粮仓（现在） | 切叶蚁菌圃（现在） |')
    print('|---|---|---|---|---|')
    for layer, keys in LAYERS:
        for k in keys:
            print(f'| {layer} | {k} | {fmt(CYAN, k)} | {fmt(HONEY, k)} | {fmt(LEAF, k)} |')
    runs = '/var/folders/0t/8nttc0td6kq_kgnx43bhqx_00000gn/T/antgarden_runs'
    sets = {'v15': sorted(glob.glob('/Users/xujiafeng/Desktop/AntGarden/07_Outputs/v15_参考/截图/r0_[123]_*_lab_*.png')),
            '蜜罐（现在）': sorted(glob.glob(f'{runs}/h1_shots_honey/out/r0_[123]_*_lab_*.png')),
            '切叶蚁（现在）': sorted(glob.glob(f'{runs}/h1_shots_leafcut/out/r0_[123]_*_lab_*.png'))}
    print()
    print('| 截图（入口、半路、深处；游戏视角和蚂蚁视角） | 底色 L / C | 中间 L / C | 高光 L / C | 彩度 > 0.10 的面积 |')
    print('|---|---|---|---|---|')
    for name, paths in sets.items():
        bL, bC, mL, mC, hL, hC, loud = still_stats(paths)
        print(f'| {name}（{len(paths)} 张） | {bL:.2f} / {bC:.3f} | {mL:.2f} / {mC:.3f} | {hL:.2f} / {hC:.3f} | {loud:.1%} |')


if __name__ == '__main__':
    main()
