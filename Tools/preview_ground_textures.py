#!/usr/bin/env python3
"""preview_ground_textures.py <folder> - a review sheet for a set of painted ground textures, before they go in.

  python3 Tools/preview_ground_textures.py 02_Concepts/Textures/v8

For every <set>_<layer>.png in the folder: the painted original, then the game version (same colour grading,
seamless blend and height map as make_ground_textures.py) tiled 2 x 2 and lit by a low side light, so the
repetition and the relief can be judged. Sister floors (<set>_floor_a / _b / _c) also get a mixed preview: the
three laid out in large soft patches the way the terrain shader mixes them, higher surfaces winning at the edges. The style reference board (_style_reference.jpg), if present, goes
at the top. Writes <folder>/00_审核总览.jpg; nothing in the Unity project is touched.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_ground_textures as mg  # noqa: E402

NAMES = {'blue': '蓝色蚁穴', 'amber': '琥珀蚁穴', 'floor': '谷底', 'bank': '岸坡', 'plateau': '高台'}


def lit(img, hgt, strength):
    """albedo under a low light from the upper left, plus soft ambient."""
    n = mg.normal_map(hgt, strength) * 2 - 1
    light = np.array([-0.55, 0.55, 0.63])
    light /= np.linalg.norm(light)
    shade = 0.55 + 0.75 * np.clip((n * light).sum(-1), 0, 1)
    return (img * shade[..., None]).clip(0, 1)


def value_noise(h, w, cells, seed):
    rng = np.random.default_rng(seed)
    g = rng.random((cells + 1, cells + 1))
    y = np.linspace(0, cells, h, endpoint=False)[:, None]
    x = np.linspace(0, cells, w, endpoint=False)[None, :]
    yi, xi = y.astype(int), x.astype(int)
    fy, fx = y - yi, x - xi
    fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
    a, b, c, d = g[yi, xi], g[yi, xi + 1], g[yi + 1, xi], g[yi + 1, xi + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def mixed(variants, tiles=3):
    """three sister floors in soft patches, like the terrain shader (heights decide the seams)."""
    (ia, ha), (ib, hb), (ic, hc) = variants
    big = lambda a: np.tile(a, (tiles, tiles) + ((1,) if a.ndim == 3 else ()))
    ia, ib, ic, ha, hb, hc = map(big, (ia, ib, ic, ha, hb, hc))
    H, W = ha.shape
    n1 = value_noise(H, W, 4, 1) * 0.7 + value_noise(H, W, 11, 2) * 0.3
    n2 = value_noise(H, W, 4, 5) * 0.7 + value_noise(H, W, 9, 4) * 0.3
    wb = smooth(0.46, 0.6, n1 + (hb - ha) * 0.22)[..., None]
    wc = smooth(0.52, 0.66, n2 + (hc - np.maximum(ha, hb)) * 0.22)[..., None]
    img = (ia * (1 - wb) + ib * wb) * (1 - wc) + ic * wc
    hgt = ((ha * (1 - wb[..., 0]) + hb * wb[..., 0]) * (1 - wc[..., 0]) + hc * wc[..., 0])
    return img, hgt


def main():
    folder = os.path.join(mg.ROOT, sys.argv[1]) if len(sys.argv) > 1 else mg.SRC
    ref = np.asarray(Image.open(mg.REFERENCE).convert('RGB'), dtype=np.float64) / 255
    target = mg.mean_hue_sat(mg.rgb_to_hsv(ref))
    big = ImageFont.truetype('/System/Library/Fonts/STHeiti Medium.ttc', 30)
    mid = ImageFont.truetype('/System/Library/Fonts/STHeiti Medium.ttc', 22)
    small = ImageFont.truetype('/System/Library/Fonts/STHeiti Light.ttc', 18)
    T = 420                        # one tile on the sheet
    rows = []
    for set_id in ('blue', 'amber'):
        for layer in ('floor', 'bank', 'plateau'):
            path = os.path.join(folder, f'{set_id}_{layer}.png')
            if not os.path.exists(path):
                continue
            src = np.asarray(Image.open(path).convert('RGB'), dtype=np.float64) / 255
            img = mg.grade(src, target) if set_id == 'blue' else src
            img = mg.seamless(img)
            im = Image.fromarray((img.clip(0, 1) * 255 + 0.5).astype(np.uint8)).resize((1024, 1024), Image.LANCZOS)
            img = np.asarray(im, dtype=np.float64) / 255
            soft = np.dstack([mg.blur_wrap(img[..., k], 1.0) for k in range(3)])
            img = (soft + (img - soft) * mg.FINE_KEEP).clip(0, 1)
            hgt = mg.height_map(img)
            tiled = np.tile(lit(img, hgt, mg.STRENGTH[layer]), (2, 2, 1))
            rows.append((f'{NAMES[set_id]} · {NAMES[layer]}', src, tiled))
        sisters = []
        for v in 'abc':
            path = os.path.join(folder, f'{set_id}_floor_{v}.png')
            if not os.path.exists(path):
                break
            src = np.asarray(Image.open(path).convert('RGB'), dtype=np.float64) / 255
            img = mg.grade(src, target) if set_id == 'blue' else src
            img = mg.seamless(img)
            img = np.asarray(Image.fromarray((img.clip(0, 1) * 255 + 0.5).astype(np.uint8)).resize((768, 768), Image.LANCZOS), dtype=np.float64) / 255
            soft = np.dstack([mg.blur_wrap(img[..., k], 0.75) for k in range(3)])
            img = (soft + (img - soft) * mg.FINE_KEEP).clip(0, 1)
            sisters.append((src, img, mg.height_map(img)))
            tiled = np.tile(lit(img, sisters[-1][2], mg.STRENGTH['floor']), (2, 2, 1))
            rows.append((f'{NAMES[set_id]} · 谷底 {v.upper()}', src, tiled))
        if len(sisters) == 3:
            img, hgt = mixed([(s[1], s[2]) for s in sisters])
            rows.append((f'{NAMES[set_id]} · 三种谷底混在一起（约 21 个单位见方，蚂蚁身长是 1）', None, lit(img, hgt, mg.STRENGTH['floor'])))
    W = 40 + T + 30 + 2 * T
    head = 70
    board_h = 0
    board_path = os.path.join(folder, '_style_reference.jpg')
    if os.path.exists(board_path):
        board = Image.open(board_path).convert('RGB')
        board = board.resize((W - 80, int(board.height * (W - 80) / board.width)))
        board_h = board.height + 50
    H = head + board_h + len(rows) * (2 * T + 50)
    sheet = Image.new('RGB', (W, H), (22, 24, 27))
    d = ImageDraw.Draw(sheet)
    d.text((40, 20), f'地面贴图 {os.path.basename(folder)} · 等你审核（确认后才放进游戏）', fill=(240, 240, 240), font=big)
    y = head
    if board_h:
        d.text((40, y), '风格参考：游戏里的蚂蚁和植物', fill=(200, 205, 210), font=small)
        sheet.paste(board, (40, y + 28))
        y += board_h
    for label, src, tiled in rows:
        d.text((40, y), label, fill=(255, 210, 140), font=mid)
        if src is not None:
            d.text((40 + T + 30, y + 4), '进游戏后的样子：统一调色、无缝拼接（2×2 块）、加侧光看起伏', fill=(190, 195, 200), font=small)
        y += 36
        b = Image.fromarray((tiled * 255).astype(np.uint8)).resize((2 * T, 2 * T), Image.LANCZOS)
        if src is not None:
            a = Image.fromarray((src * 255).astype(np.uint8)).resize((T, T), Image.LANCZOS)
            sheet.paste(a, (40, y))
            d.text((40, y + T + 6), 'Codex 原图', fill=(190, 195, 200), font=small)
        sheet.paste(b, (40 + T + 30, y))
        y += 2 * T + 14
    out = os.path.join(folder, '00_审核总览.jpg')
    sheet.save(out, quality=88)
    print(out, sheet.size)


if __name__ == '__main__':
    sys.exit(main())
