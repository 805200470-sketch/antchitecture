#!/usr/bin/env python3
"""before_after.py <before run> <after run> <out.jpg> <title> <still>... [--also <run>] [--labels 之前,之后[,…]]

One sheet: the same stills of two Tools/dev/shots_run.sh runs side by side, before on the left, after on the right
(a still is a file name in the runs' out/ folders without .png, e.g. r0_1_play_lab_way_in), one row each, with each
row's name above it and the mean brightness of each still (linear luminance, 7% border cropped, as
Tools/colour_wheel/scene_sheets.py measures it); --also adds a third column (a reference: a run, or a folder holding the
stills themselves, as 07_Outputs/v15_参考/截图). A still missing from a run is left blank. Readable on a phone.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

FONT = '/System/Library/Fonts/Hiragino Sans GB.ttc'
F1, F2 = ImageFont.truetype(FONT, 32), ImageFont.truetype(FONT, 22)
NAMES = {'play': '游戏视角', 'eye': '蚂蚁视角', 'photo': '特写', 'way_in': '入口', 'halfway': '半路', 'heart': '深处',
         'ant_close': '蚂蚁近看', 'ant_side': '蚂蚁侧面'}


def brightness(img):
    a = np.asarray(img, dtype=np.float32) / 255.0
    h, w, _ = a.shape
    a = a[int(h * .07):int(h * .93), int(w * .07):int(w * .93)]
    lin = np.where(a <= 0.04045, a / 12.92, ((a + 0.055) / 1.055) ** 2.4)
    return float((lin @ np.array([0.2126, 0.7152, 0.0722])).mean())


def label(still):
    rest = still.split('_', 2)[2]               # play_lab_way_in, ant_close
    if rest in NAMES:
        return NAMES[rest]
    view, _, place = rest.partition('_lab_')
    return f"{NAMES.get(place, place)} · {NAMES.get(view, view)}"


def main():
    args = sys.argv[1:]
    labels = ['之前', '之后']
    if '--labels' in args:
        i = args.index('--labels')
        labels = args[i + 1].split(',')
        del args[i:i + 2]
    also = []
    if '--also' in args:
        i = args.index('--also')
        also = [args[i + 1]]
        del args[i:i + 2]
    before, after, out, title = args[:4]
    stills = args[4:]
    runs = [before, after] + also
    W, H, G = 960, 540, 12
    sheet = Image.new('RGB', (G + len(runs) * (W + G), 70 + len(stills) * (H + 56)), (16, 17, 19))
    d = ImageDraw.Draw(sheet)
    d.text((G, 18), title, font=F1, fill=(236, 232, 222))
    y = 70
    for still in stills:
        d.text((G, y + 12), label(still), font=F2, fill=(232, 196, 120))
        for i, run in enumerate(runs):
            f = os.path.join(run, 'out', still + '.png')
            if not os.path.exists(f):
                f = os.path.join(run, still + '.png')
            if not os.path.exists(f):
                continue
            im = Image.open(f).convert('RGB')
            d.text((G + i * (W + G) + 260, y + 12), f'{labels[i]}　亮度 {brightness(im):.3f}', font=F2, fill=(200, 196, 188))
            sheet.paste(im.resize((W, H), Image.LANCZOS), (G + i * (W + G), y + 46))
        y += H + 56
    sheet.save(out, quality=86)
    print(out)


if __name__ == '__main__':
    main()
