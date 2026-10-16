#!/usr/bin/env python3
"""make_ground_textures.py - turn the painted ground textures into game-ready terrain sets.

  python3 Tools/make_ground_textures.py                         # all sets found in 02_Concepts/Textures
  python3 Tools/make_ground_textures.py 02_Concepts/Textures/v7 # another folder of painted textures
  python3 Tools/make_ground_textures.py 02_Concepts/Textures/v8 blue_floor   # only these textures (a trial)

Input:  <folder>/<set>_<layer>.png  (set: blue, amber; layer: floor, bank, plateau; or floor_a, floor_b, floor_c:
        sister floors mixed in patches by the terrain shader, written as floor, floor2, floor3)
Output: UnityProject.nosync/Assets/AntGarden/Art/Ground/<set>/<layer>.png (RGB colour, alpha = height)
        and <layer>_normal.png

Per texture:
  1. colour grade: the blue set is turned to the teal of the blue fungal forest (the hue and saturation
     of the original ground texture), the amber set keeps its own colours;
  2. made seamless: the half-offset copy (whose edges wrap) covers the original seams with a soft cross;
  3. resized to 2048 px with a light sharpen;
  4. height map in the alpha channel: mostly the pebble- and plate-sized shapes (bright stones raised,
     dark cracks and gaps low), only a little fine grain, so the parallax relief in the terrain shader shows
     structure rather than a field of tiny pits;
  5. normal map (OpenGL, green up) from that same height, gradients wrap around so it tiles exactly.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, '02_Concepts', 'Textures')
DST = os.path.join(ROOT, 'UnityProject.nosync', 'Assets', 'AntGarden', 'Art', 'Ground')
REFERENCE = os.path.join(DST, 'ground_floor.png')     # the original teal floor: target colour for the blue set
SIZE = 2048
VARIANTS = {'floor_a': 'floor', 'floor_b': 'floor2', 'floor_c': 'floor3'}
FINE_KEEP = 0.55   # share of the finest speckle contrast that is kept
STRENGTH = {'floor': 3.5, 'bank': 6.0, 'plateau': 4.0}   # height is 0..1 now (was a raw brightness difference)


def rgb_to_hsv(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx, mn = a.max(-1), a.min(-1)
    d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    rc = np.where(m, (mx - r) / np.where(m, d, 1), 0)
    gc = np.where(m, (mx - g) / np.where(m, d, 1), 0)
    bc = np.where(m, (mx - b) / np.where(m, d, 1), 0)
    h = np.where(r == mx, bc - gc, np.where(g == mx, 2.0 + rc - bc, 4.0 + gc - rc))
    h = np.where(m, (h / 6.0) % 1.0, 0)
    s = np.where(mx > 1e-6, d / np.where(mx > 1e-6, mx, 1), 0)
    return np.stack([h, s, mx], -1)


def hsv_to_rgb(a):
    h, s, v = a[..., 0], a[..., 1], a[..., 2]
    i = np.floor(h * 6).astype(int) % 6
    f = h * 6 - np.floor(h * 6)
    p, q, t = v * (1 - s), v * (1 - s * f), v * (1 - s * (1 - f))
    out = np.zeros(a.shape)
    for k, (cr, cg, cb) in enumerate([(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)]):
        sel = i == k
        out[..., 0][sel], out[..., 1][sel], out[..., 2][sel] = cr[sel], cg[sel], cb[sel]
    return out


def mean_hue_sat(hsv):
    w = hsv[..., 1] * hsv[..., 2]
    ang = hsv[..., 0] * 2 * np.pi
    hue = (np.arctan2((np.sin(ang) * w).sum(), (np.cos(ang) * w).sum()) / (2 * np.pi)) % 1.0
    return hue, float(hsv[..., 1].mean())


def grade(img, target):
    hsv = rgb_to_hsv(img)
    hue, sat = mean_hue_sat(hsv)
    hsv[..., 0] = (hsv[..., 0] + (target[0] - hue)) % 1.0
    # draw the hues together around the target (a painted texture may mix greens and blues)
    diff = (hsv[..., 0] - target[0] + 0.5) % 1.0 - 0.5
    hsv[..., 0] = (target[0] + diff * 0.4) % 1.0
    hsv[..., 1] = np.clip(hsv[..., 1] * np.clip(target[1] / max(sat, 1e-3), 0.7, 1.3), 0, 1)
    return hsv_to_rgb(hsv)


def seamless(img):
    h, w = img.shape[:2]
    shifted = np.roll(np.roll(img, h // 2, 0), w // 2, 1)
    # the shifted copy wraps perfectly at its edges but has the original seams as a cross in the middle;
    # cover that cross with the original (seamless in its middle), fading over a soft band
    band = 0.16
    yy = np.abs(np.linspace(-1, 1, h))[:, None]
    xx = np.abs(np.linspace(-1, 1, w))[None, :]
    cy = np.clip(1 - yy / band, 0, 1)
    cx = np.clip(1 - xx / band, 0, 1)
    mask = np.maximum(cy, cx)
    mask = mask * mask * (3 - 2 * mask)
    return shifted * (1 - mask[..., None]) + img * mask[..., None]


def blur_wrap(a, radius):
    """Gaussian blur through the FFT: the convolution is periodic, so a tile stays tileable."""
    h, w = a.shape
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    kernel = np.exp(-2 * (np.pi * radius) ** 2 * (fx * fx + fy * fy))
    return np.real(np.fft.ifft2(np.fft.fft2(a) * kernel))


def height_map(img):
    """0..1, tileable: stones and plates high, gaps low; big light/dark patches removed."""
    lum = img[..., 0] * 0.3 + img[..., 1] * 0.55 + img[..., 2] * 0.15
    shapes = blur_wrap(lum, 5.0) - blur_wrap(lum, 64)     # pebble / clod / plate scale
    grain = blur_wrap(lum, 1.0) - blur_wrap(lum, 5.0)     # a little surface grain
    h = shapes + 0.06 * grain
    lo, hi = np.percentile(h, 1.5), np.percentile(h, 99.0)
    h = np.clip((h - lo) / max(hi - lo, 1e-6), 0, 1)
    return blur_wrap(h, 1.4).clip(0, 1)


def normal_map(hgt, strength):

    def grad(hm):
        gx = np.roll(hm, -1, 1) - np.roll(hm, 1, 1)
        gy = np.roll(hm, 1, 0) - np.roll(hm, -1, 0)   # rows run down; +y is up in the normal map
        return gx, gy

    gx, gy = grad(hgt)
    n = np.stack([-gx * strength, -gy * strength, np.ones_like(gx)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return (n * 0.5 + 0.5).clip(0, 1)


def save(arr, path):
    Image.fromarray((arr.clip(0, 1) * 255 + 0.5).astype(np.uint8)).save(path, optimize=True)


def main():
    ref = np.asarray(Image.open(REFERENCE).convert('RGB'), dtype=np.float64) / 255
    target = mean_hue_sat(rgb_to_hsv(ref))
    print(f'make_ground_textures: blue target hue {target[0] * 360:.0f} deg, saturation {target[1]:.2f}')
    src = os.path.join(ROOT, sys.argv[1]) if len(sys.argv) > 1 else SRC
    only = set(sys.argv[2:])
    for name in sorted(os.listdir(src)):
        stem, ext = os.path.splitext(name)
        if ext.lower() != '.png' or '_' not in stem:
            continue
        set_id, layer = stem.split('_', 1)
        out_layer = VARIANTS.get(layer, layer)
        if only and stem not in only:
            continue
        if layer.split('_')[0] not in STRENGTH:
            continue
        img = np.asarray(Image.open(os.path.join(src, name)).convert('RGB'), dtype=np.float64) / 255
        if set_id == 'blue':
            img = grade(img, target)
        img = seamless(img)
        im = Image.fromarray((img.clip(0, 1) * 255 + 0.5).astype(np.uint8)).resize((SIZE, SIZE), Image.LANCZOS)
        im = im.filter(ImageFilter.UnsharpMask(radius=1.2, percent=40, threshold=2))
        img = np.asarray(im, dtype=np.float64) / 255
        # calm the finest speckle (grains a couple of pixels wide) so the ground does not read as a field of
        # tiny dots; pebbles, clods and cracks keep their full contrast
        soft = np.dstack([blur_wrap(img[..., k], 2.0) for k in range(3)])
        img = (soft + (img - soft) * FINE_KEEP).clip(0, 1)
        out = os.path.join(DST, set_id)
        os.makedirs(out, exist_ok=True)
        hgt = height_map(img)
        save(np.dstack([img, hgt]), os.path.join(out, f'{out_layer}.png'))
        save(normal_map(hgt, STRENGTH[layer.split('_')[0]]), os.path.join(out, f'{out_layer}_normal.png'))
        print(f'make_ground_textures: {set_id}/{out_layer} from {name} ({SIZE}px, height in alpha, + normal)')


if __name__ == '__main__':
    sys.exit(main())
