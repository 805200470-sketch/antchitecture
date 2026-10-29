#!/usr/bin/env python3
"""palette_cards.py [palettes.json] [out folder]

Cards of the four-layer palettes NestPalette makes (07_Outputs/配色/palettes.json, from the editor's PaletteDump):
  校验_v15.jpg  segment 6 against v15's own colours (NestTheme.Cyan), role by role, with the largest difference;
  色卡总览.jpg  all twelve segments, a row each: the light's own colour, a night impression (the four layers in v15's
               proportions and at the lightness they have in v15's stills), then each layer's swatches; and on the
               right how far each segment is from the one before it (OKLab distance of the night impression).
Prints the checks.
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from oklch import srgb_to_oklch, oklch_to_srgb, linear_to_oklab, srgb_to_linear  # noqa: E402

FONT = '/System/Library/Fonts/Hiragino Sans GB.ttc'
F1, F2, F3 = ImageFont.truetype(FONT, 30), ImageFont.truetype(FONT, 20), ImageFont.truetype(FONT, 16)
NAMES = ['0° 红', '30° 橙', '60° 黄 · 蜜罐粮仓', '90° 黄绿 · 切叶蚁菌圃', '120° 绿', '150° 青绿', '180° 青 · v15',
         '210° 天蓝', '240° 蓝', '270° 紫', '300° 品红', '330° 玫红']
LAYERS = {1: '底色', 2: '中间层', 3: '高光', 4: '点缀'}
# the four layers in v15's stills (Tools/palette/analyse_themes.py): their share of the picture and their lightness
SHARE = {1: 0.58, 2: 0.33, 3: 0.06, 4: 0.03}
NIGHT_L = {1: 0.14, 2: 0.31, 3: 0.71, 4: 0.36}
BASE_SHOWN = ['floor', 'bank', 'plateau', 'fog', 'skyHorizon', 'fill', 'floorTex']


def rgb8(c):
    return tuple(int(round(min(max(v, 0), 1) * 255)) for v in c)


def night(rgb, layer):
    """a palette colour as it reads at night in the game: the lightness its layer has in v15's stills, its chroma
    scaled with it (an HDR light first brought into range)."""
    rgb = np.array(rgb, dtype=np.float64)
    rgb = rgb / max(1.0, rgb.max())
    L, C, h = srgb_to_oklch(rgb)
    k = NIGHT_L[layer] / max(L, 1e-3)
    return oklch_to_srgb(np.array([NIGHT_L[layer], C * min(k, 1.0), h]))


def impression(seg, W, H):
    """a strip of the night: the layers side by side in their shares, each its colours in turn."""
    img = Image.new('RGB', (W, H))
    d = ImageDraw.Draw(img)
    x = 0
    for layer in (1, 2, 3, 4):
        sw = [s for s in seg['swatches'] if s['layer'] == layer and (layer != 1 or s['role'] in BASE_SHOWN)]
        if not sw:
            continue
        w = int(round(W * SHARE[layer]))
        for k, s in enumerate(sw):
            x0, x1 = x + w * k // len(sw), x + w * (k + 1) // len(sw)
            d.rectangle([x0, 0, x1, H], fill=rgb8(night(s['rgb'], layer)))
        x += w
    return img


def palette_strip(seg, W, H):
    """the palette in v15's proportions: the layers side by side in their shares, each its colours in turn."""
    img = Image.new('RGB', (W, H))
    d = ImageDraw.Draw(img)
    x = 0
    for layer in (1, 2, 3, 4):
        sw = [s for s in seg['swatches'] if s['layer'] == layer and (layer != 1 or s['role'] in BASE_SHOWN)]
        if not sw:
            continue
        w = int(round(W * SHARE[layer]))
        for k, s in enumerate(sw):
            c = np.array(s['rgb'])
            x0, x1 = x + w * k // len(sw), x + w * (k + 1) // len(sw)
            d.rectangle([x0, 0, x1, H], fill=rgb8(c / max(1.0, c.max())))
        x += w
    return img


def mean_lab(seg):
    labs = []
    for layer in (1, 2, 3, 4):
        sw = [s for s in seg['swatches'] if s['layer'] == layer and (layer != 1 or s['role'] in BASE_SHOWN)]
        for s in sw:
            labs.append((linear_to_oklab(srgb_to_linear(night(s['rgb'], layer))), SHARE[layer] / len(sw)))
    return labs


def distance(a, b):
    """how far apart two worlds' palettes are: the OKLab distance of the means of their base and middle colours, each
    layer weighted by its share (at night the base is too dark to tell them by; the game's stills are the real test)."""
    out = 0.0
    for layer in (1, 2):
        ca = [linear_to_oklab(srgb_to_linear(np.array(s['rgb']) / max(1.0, max(s['rgb'])))) for s in a['swatches'] if s['layer'] == layer and (layer != 1 or s['role'] in BASE_SHOWN)]
        cb = [linear_to_oklab(srgb_to_linear(np.array(s['rgb']) / max(1.0, max(s['rgb'])))) for s in b['swatches'] if s['layer'] == layer and (layer != 1 or s['role'] in BASE_SHOWN)]
        out += SHARE[layer] * np.linalg.norm(np.mean(ca, 0) - np.mean(cb, 0))
    return out / (SHARE[1] + SHARE[2])


def validation(data, out):
    seg6 = data['segments'][6]
    rows = []
    worst = (0, '')
    for s in seg6['swatches']:
        v = data['v15'][s['role']]
        sc = max(1.0, max(v), max(s['rgb']))
        de = float(np.linalg.norm(linear_to_oklab(srgb_to_linear(np.array(v) / sc)) - linear_to_oklab(srgb_to_linear(np.array(s['rgb']) / sc))))
        rows.append((s['role'], s['layer'], v, s['rgb'], de))
        worst = max(worst, (de, s['role']))
    cols = 4
    cw, ch = 330, 56
    img = Image.new('RGB', (cols * cw + 20, 90 + (len(rows) + cols - 1) // cols * ch), (18, 19, 21))
    d = ImageDraw.Draw(img)
    d.text((10, 14), f'校验：函数跑青色段（6）和 v15 本身的颜色逐个比较。最大差 ΔE {worst[0]:.4f}（{worst[1]}），看不出差别是 < 0.02', font=F2, fill=(236, 232, 222))
    d.text((10, 48), '每格左半是 v15，右半是函数算出来的', font=F3, fill=(190, 186, 178))
    for i, (role, layer, v, p, de) in enumerate(rows):
        x, y = 10 + (i % cols) * cw, 90 + (i // cols) * ch
        sc = max(1.0, max(v))
        d.rectangle([x, y, x + 60, y + 40], fill=rgb8(np.array(v) / sc))
        d.rectangle([x + 60, y, x + 120, y + 40], fill=rgb8(np.array(p) / sc))
        d.text((x + 128, y + 2), f'{role}', font=F3, fill=(220, 216, 206))
        d.text((x + 128, y + 22), f'{LAYERS[layer]} ΔE {de:.4f}', font=F3, fill=(170, 166, 158))
    img.save(out, quality=90)
    return worst


def overview(data, out):
    segs = data['segments']
    RH, G = 150, 14
    W = 1900
    img = Image.new('RGB', (W, 120 + len(segs) * (RH + G)), (18, 19, 21))
    d = ImageDraw.Draw(img)
    d.text((G, 16), '12 个色段的四层调色板（v15 的配方转到每个色段）', font=F1, fill=(236, 232, 222))
    d.text((G, 62), '左起：房间灯光的颜色 · 四层按 v15 的面积排在一起（上：调色板本身，下：夜里的明度）· 底色 · 中间层（植物色族）· 高光 · 点缀 · 和上一个色段的距离', font=F3, fill=(190, 186, 178))
    dists = []
    for i, seg in enumerate(segs):
        y = 110 + i * (RH + G)
        n = seg['segment']
        import colorsys
        light = colorsys.hsv_to_rgb(n * 30 / 360, 1, 1)
        d.rectangle([G, y, G + 60, y + RH], fill=rgb8(light))
        d.text((G + 72, y + 4), NAMES[n], font=F2, fill=(232, 196, 120))
        d.text((G + 72, y + 32), f'OKLCH 色相 {seg["hue"]:.0f}°  暖 {seg["warm"]:.2f}', font=F3, fill=(170, 166, 158))
        d.text((G + 72, y + 54), f'白平衡 {seg["temperature"]:+.1f}  地面 ×{seg["groundGain"]:.2f}', font=F3, fill=(170, 166, 158))
        img.paste(palette_strip(seg, 330, (RH - 80) // 2), (G + 72, y + 80))
        img.paste(impression(seg, 330, (RH - 80) // 2), (G + 72, y + 80 + (RH - 80) // 2))
        x = G + 420
        for layer, width in ((1, 330), (2, 520), (3, 300), (4, 160)):
            sw = [s for s in seg['swatches'] if s['layer'] == layer and (layer != 1 or s['role'] in BASE_SHOWN)]
            if layer == 2:
                sw = [s for s in sw if s['role'].startswith('body') or s['role'].startswith('micro') or s['role'] in ('scan', 'vineLeaf')]
            if layer == 3:
                sw = [s for s in sw if s['role'] in ('glow0', 'glow1', 'glow2', 'glow6', 'threadCore', 'threadHalo', 'beacon', 'grainGlow', 'lampCore', 'trail')]
            d.text((x, y), LAYERS[layer], font=F3, fill=(170, 166, 158))
            if sw:
                cw = width // len(sw)
                for k, s in enumerate(sw):
                    c = np.array(s['rgb'])
                    d.rectangle([x + k * cw, y + 24, x + (k + 1) * cw - 2, y + RH], fill=rgb8(c / max(1.0, c.max())))
            x += width + 12
        if i > 0:
            dist = distance(segs[i - 1], seg)
            dists.append((NAMES[segs[i - 1]['segment']], NAMES[n], dist))
            d.text((W - 150, y + 60), f'Δ {dist:.3f}', font=F2, fill=(232, 196, 120) if dist >= 0.04 else (240, 110, 90))
    img.save(out, quality=88)
    return dists


def main():
    src = sys.argv[1] if len(sys.argv) > 1 else '07_Outputs/配色/palettes.json'
    outdir = sys.argv[2] if len(sys.argv) > 2 else os.path.dirname(src)
    data = json.load(open(src))
    worst = validation(data, os.path.join(outdir, '校验_v15.jpg'))
    print(f'v15 check: largest difference {worst[0]:.5f} ({worst[1]})')
    for a, b, dist in overview(data, os.path.join(outdir, '色卡总览.jpg')):
        print(f'{a} -> {b}: {dist:.3f}')


if __name__ == '__main__':
    main()
