"""photo_to_cutout.py - the presentation video's S30a (11-04): a visitor's photo as it came, a selection box dragged round
the thing, then the photo's ground fading away while the view comes in on the thing, until only the cut-out thing is left.
Numbered JPEG frames at 1920x1080, 30 a second, for Tools/frames_to_video.sh.

  python3 Tools/dev/story/photo_to_cutout.py <photo.jpg> <mask.png> <out frames dir> [seconds 7]

The mask is the export's (white where the thing is, the photo's size). Every frame is drawn from the full photo at twice the
frame's size and brought down, so the push in stays sharp and the photo's fine patterns do not shimmer. The visitors'
photos stay on this machine: copy them to $TMPDIR first (08_Uploads.nosync is never committed), write the frames there too.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

W, H, FPS, SS = 1920, 1080, 30, 2
GROUND = (9, 12, 14)        # the game's night, nearly black
DIM = 0.42                  # outside the box while it is held: this much darker


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * x * (x * (6 * x - 15) + 10)


def ramp(t, a, b):
    return smooth((t - a) / (b - a)) if b > a else float(t >= b)


def warp(img, k, c, fill, size):
    """img (photo pixels) seen at scale k (screen px per photo px) with photo point c in the middle of a size frame."""
    w, h = size
    a = 1.0 / k
    return img.transform(size, Image.AFFINE, (a, 0, c[0] - (w / 2) * a, 0, a, c[1] - (h / 2) * a), resample=Image.BICUBIC, fillcolor=fill)


def main():
    photo_path, mask_path, out = sys.argv[1], sys.argv[2], sys.argv[3]
    seconds = float(sys.argv[4]) if len(sys.argv) > 4 else 7.0
    os.makedirs(out, exist_ok=True)
    photo = Image.open(photo_path).convert('RGB')
    mask = Image.open(mask_path).convert('L')
    if mask.size != photo.size:
        mask = mask.resize(photo.size, Image.BILINEAR)
    mask = mask.filter(ImageFilter.GaussianBlur(1.5))
    whole = Image.new('L', photo.size, 255)                     # where the photo is at all
    pw, ph = photo.size
    m = np.asarray(mask, np.float32) / 255.0
    ys, xs = np.nonzero(m > 0.5)
    pad = 0.06 * max(xs.max() - xs.min(), ys.max() - ys.min())
    x0, y0 = max(0, xs.min() - pad), max(0, ys.min() - pad)
    x1, y1 = min(pw - 1, xs.max() + pad), min(ph - 1, ys.max() + pad)
    # the view: first the photo whole (as large as fits with room round it), at the end the thing (its box filling about
    # two thirds of the frame's height; never further off than the photo was)
    k0 = min(1180 / pw, 900 / ph)
    c0 = (pw / 2, ph / 2)
    k1 = max(k0 * 1.05, min(0.66 * H / (y1 - y0), 0.72 * W / (x1 - x0)))
    c1 = ((x0 + x1) / 2, (y0 + y1) / 2)

    n = int(round(seconds * FPS))
    # (seconds) the photo comes, is held, the box is dragged open, held, the ground fades as the view comes in, the box goes
    t_in, t_box0, t_box1, t_bg0, t_bg1, t_off0, t_off1 = 0.6, 1.3, 2.4, 3.1, 4.9, 4.8, 5.4
    ground = np.empty((H * SS, W * SS, 3), np.float32)
    ground[:] = GROUND
    for f in range(n):
        t = f / FPS
        appear = ramp(t, 0.0, t_in)
        drag = ramp(t, t_box0, t_box1)
        bg = 1.0 - ramp(t, t_bg0, t_bg1)               # the photo's ground: 1 there, 0 gone
        come = ramp(t, t_bg0 - 0.3, seconds - 0.2)     # the view coming in on the thing
        k = k0 * (k1 / k0) ** come
        c = (c0[0] + (c1[0] - c0[0]) * come, c0[1] + (c1[1] - c0[1]) * come)
        size = (W * SS, H * SS)
        rgb = np.asarray(warp(photo, k * SS, c, GROUND, size), np.float32)
        mm = np.asarray(warp(mask, k * SS, c, 0, size), np.float32) / 255.0
        inph = np.asarray(warp(whole, k * SS, c, 0, size), np.float32) / 255.0

        def to_screen(px, py):
            return ((px - c[0]) * k + W / 2, (py - c[1]) * k + H / 2)
        bx0, by0 = to_screen(x0, y0)
        bx1, by1 = to_screen(x0 + (x1 - x0) * drag, y0 + (y1 - y0) * drag)
        # outside the box dimmed while it is held, until the ground goes
        yy, xx = np.mgrid[0:H * SS, 0:W * SS]
        inside = (xx >= bx0 * SS) & (xx <= bx1 * SS) & (yy >= by0 * SS) & (yy <= by1 * SS)
        dim = np.where(inside, 1.0, 1.0 - DIM * drag * bg)
        alpha = (mm + (1.0 - mm) * bg) * inph * appear
        # a soft shade under the thing as it comes to stand alone
        shade = np.asarray(Image.fromarray((mm * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(14 * SS)), np.float32) / 255.0
        frame = ground * (1.0 - (shade * (1.0 - bg) * 0.55 * appear))[..., None]
        frame = frame * (1.0 - alpha[..., None]) + rgb * dim[..., None] * alpha[..., None]
        img = Image.fromarray(np.clip(frame, 0, 255).astype(np.uint8)).resize((W, H), Image.LANCZOS)
        box_a = 1.0 - ramp(t, t_off0, t_off1)
        if drag > 0 and box_a > 0:
            overlay = Image.new('RGBA', (W, H), (0, 0, 0, 0))
            d = ImageDraw.Draw(overlay)
            ai = int(round(235 * box_a))
            dash, run = 9, int(f * 1.5)   # marching ants: light and dark dashes running round the box
            pts = [(bx0, by0), (bx1, by0), (bx1, by1), (bx0, by1), (bx0, by0)]
            for (xa, ya), (xb, yb) in zip(pts, pts[1:]):
                length = max(abs(xb - xa), abs(yb - ya))
                for s0 in range(-dash * 2, int(length), dash):
                    s1, s0c = min(length, s0 + dash), max(0, s0)
                    if s1 <= s0c:
                        continue
                    col = (245, 245, 240, ai) if ((s0 + run) // dash) % 2 == 0 else (20, 24, 26, ai)
                    fa, fb = s0c / max(1, length), s1 / max(1, length)
                    d.line([(xa + (xb - xa) * fa, ya + (yb - ya) * fa), (xa + (xb - xa) * fb, ya + (yb - ya) * fb)], fill=col, width=2)
            if t >= t_box1:
                hs = 5
                for (hx, hy) in [(bx0, by0), (bx1, by0), (bx1, by1), (bx0, by1)]:
                    d.rectangle([hx - hs, hy - hs, hx + hs, hy + hs], fill=(245, 245, 240, ai), outline=(20, 24, 26, ai))
            img = Image.alpha_composite(img.convert('RGBA'), overlay).convert('RGB')
        img.save(os.path.join(out, f'f{f:05d}.jpg'), quality=95)
    print(f'photo_to_cutout: {n} frames in {out} (photo {pw}x{ph}, view {k0:.3f} -> {k1:.3f} px per photo px)')


if __name__ == '__main__':
    main()
