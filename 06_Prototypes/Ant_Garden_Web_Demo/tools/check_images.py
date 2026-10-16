#!/usr/bin/env python3
"""Automatic pre-checks for concept images before splat conversion.

Reports, per image: size, background flatness (edge samples vs #E8ECEC),
object margins (cropping), object coverage and how much of the object is
very pale (cut-out risk). Observations only; the visual review decides.

Usage: python3 tools/check_images.py concept/env_*.png
"""
import json
import sys

import numpy as np
from PIL import Image

BG = np.array([0xE8, 0xEC, 0xEC], np.float32)


def check(path):
    im = np.asarray(Image.open(path).convert('RGB')).astype(np.float32)
    h, w = im.shape[:2]
    edge = np.concatenate([im[0], im[-1], im[:, 0], im[:, -1]])
    edge_dev = np.abs(edge - np.median(edge, 0)).max(1)
    bg_med = np.median(edge, 0)
    diff = np.sqrt(((im - bg_med) ** 2).sum(-1))
    obj = diff > 18
    ys, xs = np.nonzero(obj)
    res = {
        'file': path,
        'size': [w, h],
        'bgMedian': [int(c) for c in bg_med],
        'bgOffsetFromE8ECEC': round(float(np.abs(bg_med - BG).max()), 1),
        'edgeFlatPct': round(float((edge_dev <= 5).mean() * 100), 1),
    }
    if len(xs):
        res['marginsPct'] = {
            'left': round(xs.min() / w * 100, 1), 'right': round((w - 1 - xs.max()) / w * 100, 1),
            'top': round(ys.min() / h * 100, 1), 'bottom': round((h - 1 - ys.max()) / h * 100, 1),
        }
        res['coveragePct'] = round(float(obj.mean() * 100), 1)
        pale = (diff > 18) & (diff < 40)
        res['paleEdgePct'] = round(float(pale.sum() / max(1, obj.sum()) * 100), 1)
    return res


if __name__ == '__main__':
    for p in sys.argv[1:]:
        if '/._' in p or p.startswith('._'):
            continue
        print(json.dumps(check(p), ensure_ascii=False))
