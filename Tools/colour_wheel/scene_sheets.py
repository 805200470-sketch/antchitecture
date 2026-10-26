#!/usr/bin/env python3
"""scene_sheets.py <runs dir> <prefix> <out dir> [biome ...]

One review sheet per particle nest from its captures (<runs>/<prefix>_shots_<biome>, <prefix>_interact_<biome>):
the player's camera and the ant's eye at the way in, halfway and the heart; up to eight close-ups; the interaction
moments (idle, touched, walked past, the heart); measured hue (Tools/colour_wheel/measure_hue.py's method) and
brightness in the title. Also writes <out>/measures.json. Default biomes: all nine built nests.
"""
import glob
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
os.environ.setdefault('VMIN', '0.6')   # the thresholds every nest's hue was measured with (mapping.md)
os.environ.setdefault('SMIN', '0.3')
import measure_hue  # noqa: E402

NESTS = {  # biome: (segment, name)
    'kiln': ('0° 红', '古蚁圣殿 · 窑火'), 'amber': ('30° 橙', '暖琥珀蚁穴'), 'honey': ('60° 黄', '蜜罐粮仓'),
    'leafcut': ('90° 黄绿', '切叶蚁菌圃'), 'moss': ('120° 绿', '苔藓森林'), 'verdigris': ('150° 青绿', '铜绿机城'),
    'blue': ('180° 青', '蓝色真菌林'), 'azure': ('210° 天蓝', '巨人遗物'), 'cistern': ('240° 蓝', '雨水潭'),
}
FONT = '/System/Library/Fonts/Hiragino Sans GB.ttc'
F1, F2, F3 = ImageFont.truetype(FONT, 34), ImageFont.truetype(FONT, 22), ImageFont.truetype(FONT, 18)
BG, WHITE, GREY, GOLD = (16, 17, 19), (236, 232, 222), (150, 152, 156), (232, 196, 120)


def find(folder, pat):
    """The capture whose name holds pat (a trailing '.' means the name ends there: 'eye_lab_heart.' is not the
    heart's close-up)."""
    if pat.endswith('.'):
        f = sorted(glob.glob(os.path.join(folder, f'*{pat}png')))
    else:
        f = sorted(glob.glob(os.path.join(folder, f'*{pat}*.png')))
    return f[0] if f else None


def tile(path, w, h):
    if not path or not os.path.exists(path):
        im = Image.new('RGB', (w, h), (34, 34, 36))
        ImageDraw.Draw(im).text((10, 10), '（没有这张）', font=F3, fill=GREY)
        return im
    im = Image.open(path).convert('RGB')
    if im.width >= 1900 and im.height > 1000:
        im = im.crop((0, 0, im.width, 998))
    sw, sh = im.size
    if sw / sh > w / h:
        cw = round(sh * w / h)
        im = im.crop(((sw - cw) // 2, 0, (sw - cw) // 2 + cw, sh))
    else:
        ch = round(sw * h / w)
        im = im.crop((0, (sh - ch) // 2, sw, (sh - ch) // 2 + ch))
    return im.resize((w, h), Image.LANCZOS)


def brightness(paths):
    vals = []
    for p in paths:
        if not p:
            continue
        a = np.asarray(Image.open(p).convert('RGB').resize((640, 360)), dtype=np.float32) / 255
        a = a[:333]
        lin = np.where(a <= 0.04045, a / 12.92, ((a + 0.055) / 1.055) ** 2.4)
        vals.append(float((lin @ np.array([0.2126, 0.7152, 0.0722])).mean()))
    return round(float(np.mean(vals)), 3) if vals else None


def main():
    runs, prefix, out = sys.argv[1], sys.argv[2], sys.argv[3]
    biomes = sys.argv[4:] or list(NESTS)
    os.makedirs(out, exist_ok=True)
    measures = {}
    for b in biomes:
        seg, name = NESTS[b]
        shots = os.path.join(runs, f'{prefix}_shots_{b}', 'out')
        inter = os.path.join(runs, f'{prefix}_interact_{b}')
        eyes = [find(shots, 'eye_lab_way_in'), find(shots, 'eye_lab_heart.')]
        hue = None
        if all(eyes):
            r = measure_hue.measure([e for e in eyes])
            hue = r
        bright = brightness([find(shots, p) for p in ('eye_lab_way_in', 'eye_lab_halfway', 'eye_lab_heart.', 'play_lab_way_in', 'play_lab_halfway')])
        report = open(os.path.join(inter, 'interact_report.txt')).read().strip() if os.path.exists(os.path.join(inter, 'interact_report.txt')) else ''
        measures[b] = {'hue': hue, 'brightness': bright, 'interact': report}
        W, H, G = 620, 340, 10
        photos = sorted(glob.glob(os.path.join(shots, '*_photo_lab_*.png')))[:8]
        rows = 2 + (len(photos) + 3) // 4 + 1
        sheet = Image.new('RGB', (G + 4 * (W + G), 120 + rows * (H + 40)), BG)
        d = ImageDraw.Draw(sheet)
        d.text((G + 10, 24), f'{seg} · {name}', font=F1, fill=WHITE)
        hue_txt = f"主光色相 {hue['hue']:.0f}°（{hue['segment_name']}段，占 {hue['segment_share']:.0%}）" if hue else '主光色相：没测（缺图）'
        d.text((G + 10, 76), f"{hue_txt} · 亮度 {bright} · {report.split(': ', 1)[-1] if report else '没有交互记录'}", font=F2, fill=GREY)
        y = 120
        for i, (pat, cap) in enumerate((('play_lab_way_in', '入口 · 游戏视角'), ('play_lab_halfway', '半路 · 游戏视角'), ('play_lab_heart.', '心脏 · 游戏视角'), ('r0_9_aerial', '俯视'))):
            sheet.paste(tile(find(shots, pat), W, H), (G + i * (W + G), y))
            d.text((G + i * (W + G) + 8, y + H + 6), cap, font=F3, fill=WHITE)
        y += H + 40
        for i, (pat, cap) in enumerate((('eye_lab_way_in', '入口 · 蚂蚁视角'), ('eye_lab_halfway', '半路 · 蚂蚁视角'), ('eye_lab_heart.', '心脏 · 蚂蚁视角'))):
            sheet.paste(tile(find(shots, pat), W, H), (G + i * (W + G), y))
            d.text((G + i * (W + G) + 8, y + H + 6), cap, font=F3, fill=WHITE)
        y += H + 40
        for i, p in enumerate(photos):
            x, yy = G + (i % 4) * (W + G), y + (i // 4) * (H + 40)
            sheet.paste(tile(p, W, H), (x, yy))
            d.text((x + 8, yy + H + 6), '特写 · ' + os.path.basename(p).split('_photo_lab_')[-1][:-4], font=F3, fill=WHITE)
        y += ((len(photos) + 3) // 4) * (H + 40)
        for i, (f, cap) in enumerate((('1_idle.png', '静止'), ('2_touched.png', '触角点亮'), ('3_walked_past.png', '走过点亮'), ('4_heart.png', '到达心脏'))):
            sheet.paste(tile(os.path.join(inter, f), W, H), (G + i * (W + G), y))
            d.text((G + i * (W + G) + 8, y + H + 6), '交互 · ' + cap, font=F3, fill=WHITE)
        name_file = f"{seg.split('°')[0].zfill(3)}_{name.replace(' · ', '_')}.jpg"
        sheet.save(os.path.join(out, name_file), quality=86)
        print(b, name_file, measures[b]['brightness'], hue['hue'] if hue else None)
    json.dump(measures, open(os.path.join(out, 'measures.json'), 'w'), ensure_ascii=False, indent=1)


if __name__ == '__main__':
    main()
