#!/usr/bin/env python3
"""blind_package.py <spec.json> <out folder>

A blind review package: samples whose source the reviewer is not told, so its verdicts on cases the user has already
judged can be checked against the user's (calibration), and its verdicts on new results trusted only if those agree.

spec.json: {"title": "...", "samples": [{"label": "<what it really is: kept in the key>", "expect": "通过|不通过|新",
            "stills": ["<png>", ...]}, ...]}
Writes, in the out folder: the samples shuffled (with a fixed seed) and numbered 1..n as 样本_<n>/out/<file name> (so
Tools/palette/measure_stills.py works on them as on a run folder), one sheet per sample (样本_<n>.jpg: its stills in a
grid, labelled only with the number); and, away from it (_keys/<out folder's name>_key.json beside the out folder, not
in it), the key: number -> label, expected verdict, for the caller only.
"""
import json
import os
import random
import shutil
import sys

from PIL import Image, ImageDraw, ImageFont

FONT = '/System/Library/Fonts/Hiragino Sans GB.ttc'


def sheet(paths, title, out):
    W, H, G = 960, 540, 10
    cols = 2 if len(paths) > 1 else 1
    rows = (len(paths) + cols - 1) // cols
    img = Image.new('RGB', (G + cols * (W + G), 60 + rows * (H + G)), (16, 17, 19))
    d = ImageDraw.Draw(img)
    d.text((G, 14), title, font=ImageFont.truetype(FONT, 30), fill=(236, 232, 222))
    for i, p in enumerate(paths):
        im = Image.open(p).convert('RGB')
        im.thumbnail((W, H), Image.LANCZOS)
        img.paste(im, (G + (i % cols) * (W + G), 60 + (i // cols) * (H + G)))
    img.save(out, quality=86)


def main():
    spec = json.load(open(sys.argv[1]))
    out = sys.argv[2]
    os.makedirs(out, exist_ok=True)
    samples = list(spec['samples'])
    random.Random(20261027).shuffle(samples)
    key = {}
    for n, s in enumerate(samples, 1):
        folder = os.path.join(out, f'样本_{n}', 'out')
        os.makedirs(folder, exist_ok=True)
        for p in s['stills']:
            shutil.copyfile(p, os.path.join(folder, os.path.basename(p)))
        sheet(s['stills'], f'样本 {n}', os.path.join(out, f'样本_{n}.jpg'))
        key[n] = {'label': s['label'], 'expect': s['expect']}
    keys = os.path.join(os.path.dirname(out.rstrip('/')), '_keys')
    os.makedirs(keys, exist_ok=True)
    json.dump(key, open(os.path.join(keys, os.path.basename(out.rstrip('/')) + '_key.json'), 'w'), ensure_ascii=False, indent=1)
    print(f'{len(samples)} samples in {out}')


if __name__ == '__main__':
    main()
