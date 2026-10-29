#!/usr/bin/env python3
"""model_compare.py <before run> <after run> <out folder> [--renders DIR]
model_compare.py scene <before run> <after run> <out.jpg> <title> <shot>... (shot: r0_10_lab_frond)

Sheets comparing the particle nest's finely made plants before and after a change, three plants a sheet, one plant a
row: its concept (the front Tripo was given, 02_Concepts/Fronts_v13_候选), its points on their own before and after
(Tools/points_preview.py renders in DIR: <name>_0.jpg and after_<name>.jpg), and its close-up in the game before and
after (a sceneshots run's photo of the one nearest the way: the same spot when the plants stand where they stood). A run is a
Tools/dev/shots_run.sh folder; the plants are photographed in the blue fungal forest, where all of them grow.

scene: the same plants where they grow in another nest, as the game shows them: for each shot, the ant's eye and the
close-up, before and after, side by side.
"""
import glob
import os
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = '/Users/xujiafeng/Desktop/AntGarden/'
FONT = '/System/Library/Fonts/Hiragino Sans GB.ttc'
F1, F2 = ImageFont.truetype(FONT, 30), ImageFont.truetype(FONT, 21)
# plant, its name, its close-up in the blue forest's sceneshots (r0_<k>_photo_<this>.png; k differs between runs)
PLANTS = [
    ('curl_a_crozier', '卷头蕨 curl_a', 'lab_crozier'),
    ('curl_b_double', '双卷 curl_b', 'lab_double'),
    ('curl_c_hook', '钩卷 curl_c', 'lab_hook'),
    ('curl_d_cluster', '卷头蕨丛 curl_d', 'lab_cluster'),
    ('curl_e_corkscrew', '螺旋藤 curl_e', 'lab_corkscrew'),
    ('frond_a_feather', '羽叶蕨 frond_a', 'lab_frond'),
    ('puff_a_stalked', '绒球 puff_a', 'lab_puff'),
    ('puff_b_lantern', '灯笼荚 puff_b', 'lab_lantern'),
    ('coral_b_branching', '珊瑚 coral_b', 'lab_coral'),
]


def photo(run, shot):
    found = glob.glob(os.path.join(run, 'out', f'r*_photo_{shot}.png'))
    return found[0] if found else None
HEADS = ['概念图', '单独 · 之前的点', '单独 · 现在的点', '游戏里 · 之前', '游戏里 · 现在']


def tile(path, w, h, crop=None):
    if not path or not os.path.exists(path):
        return None
    im = Image.open(path).convert('RGB')
    if crop:
        W, H = im.size
        im = im.crop((int(W * crop[0]), int(H * crop[1]), int(W * crop[2]), int(H * crop[3])))
    im.thumbnail((w, h), Image.LANCZOS)
    return im


def scene(before, after, out, title, shots):
    W, H, G = 560, 315, 10
    sheet = Image.new('RGB', (G + 4 * (W + G), 70 + len(shots) * (H + 50)), (16, 17, 19))
    d = ImageDraw.Draw(sheet)
    d.text((G, 16), title, font=F1, fill=(236, 232, 222))
    y = 70
    for shot in shots:
        r, k, name = shot.split('_', 2)
        d.text((G, y + 8), name.replace('lab_', ''), font=F2, fill=(232, 196, 120))
        for j, (run, view, head) in enumerate([(before, 'eye', '蚂蚁视角 · 之前'), (after, 'eye', '蚂蚁视角 · 之后'),
                                                (before, 'photo', '特写 · 之前'), (after, 'photo', '特写 · 之后')]):
            im = tile(os.path.join(run, 'out', f'{r}_{k}_{view}_{name}.png'), W, H)
            if im:
                sheet.paste(im, (G + j * (W + G), y + 40))
            d.text((G + j * (W + G) + 200, y + 10), head, font=F2, fill=(200, 196, 188))
        y += H + 50
    sheet.save(out, quality=87)
    print(out)


def main():
    if sys.argv[1] == 'scene':
        return scene(*sys.argv[2:6], sys.argv[6:])
    before, after, out = sys.argv[1:4]
    renders = sys.argv[sys.argv.index('--renders') + 1] if '--renders' in sys.argv else '.'
    os.makedirs(out, exist_ok=True)
    SW, SH, GW, GH, G = 300, 425, 640, 425, 12
    cols = [SW, SW, SW, GW, GW]
    width = G + sum(c + G for c in cols)
    for n in range(0, len(PLANTS), 3):
        rows = PLANTS[n:n + 3]
        sheet = Image.new('RGB', (width, 60 + len(rows) * (SH + 56)), (16, 17, 19))
        d = ImageDraw.Draw(sheet)
        x = G
        for c, head in zip(cols, HEADS):
            d.text((x, 18), head, font=F2, fill=(200, 196, 188))
            x += c + G
        y = 60
        for name, label, shot in rows:
            d.text((G, y + 6), label, font=F1, fill=(232, 196, 120))
            ims = [tile(os.path.join(ROOT, '02_Concepts/Fronts_v13_候选', name + '.png'), SW, SH),
                   tile(os.path.join(renders, f'{name}_0.jpg'), SW, SH, (0, 0, 0.5, 1)),
                   tile(os.path.join(renders, f'after_{name}.jpg'), SW, SH, (0, 0, 0.5, 1)),
                   # (the middle of the photo: the plant, as large as the row allows)
                   tile(photo(before, shot), GW, GH, (0.16, 0, 0.84, 1)),
                   tile(photo(after, shot), GW, GH, (0.16, 0, 0.84, 1))]
            x = G
            for c, im in zip(cols, ims):
                if im:
                    sheet.paste(im, (x + (c - im.width) // 2, y + 48))
                x += c + G
            y += SH + 56
        path = os.path.join(out, f'对照_{n // 3 + 1}.jpg')
        sheet.save(path, quality=87)
        print(path)


if __name__ == '__main__':
    main()
