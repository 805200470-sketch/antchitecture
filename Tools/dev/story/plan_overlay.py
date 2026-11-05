"""plan_overlay.py - the presentation video's S28 (11-04): a nest's line drawing laid exactly where the game draws its paths on
the crane shot's first frame (straight down on the nest), so the edit can dissolve from the drawing to the game.

  python3 Tools/dev/story/plan_overlay.py <drawing.png> <S28_plan_<nest>.json> <out prefix> [plan still 1080.png]

StoryFilm writes the json (where the drawing's corners lie on the 1920x1080 frame, the game's mirroring included). Writes
<prefix>_1080.png (black lines on white, as drawn), <prefix>_lines_1080.png (white lines on transparent, to lay over the
footage), the same two at 3840x2160 (_4k), and with a plan still a check image (<prefix>_check.jpg: the lines over it).
"""
import json
import sys

import numpy as np
from PIL import Image

Image.MAX_IMAGE_PIXELS = None


def place(drawing, corners, w, h, scale):
    """The drawing (L, black lines on white) mapped onto a w x h frame by its corners (frame pixels at 1080 times scale)."""
    W, H = drawing.size
    a = np.array(corners['top_left'], float) * scale
    b = np.array(corners['top_right'], float) * scale
    c = np.array(corners['bottom_left'], float) * scale
    # drawn a little larger than needed, then brought down: the lines stay smooth
    ss = 2
    k = np.linalg.norm(b - a) / W * ss
    small = drawing.resize((max(1, int(round(W * k))), max(1, int(round(H * k)))), Image.LANCZOS)
    sw, sh = small.size
    # frame (supersampled) -> small drawing pixel: [x,y] = ss*(a + M [u,v]), M columns (b-a)/sw, (c-a)/sh
    m = np.column_stack(((b - a) / sw, (c - a) / sh)) * ss
    inv = np.linalg.inv(m)
    off = -inv @ (a * ss)
    data = (inv[0, 0], inv[0, 1], off[0], inv[1, 0], inv[1, 1], off[1])
    big = small.transform((w * ss, h * ss), Image.AFFINE, data, resample=Image.BICUBIC, fillcolor=255)
    return big.resize((w, h), Image.LANCZOS)


def main():
    drawing_path, plan_path, prefix = sys.argv[1], sys.argv[2], sys.argv[3]
    still = sys.argv[4] if len(sys.argv) > 4 else None
    plan = json.load(open(plan_path))
    drawing = Image.open(drawing_path).convert('L')
    for scale, tag in ((1, '1080'), (2, '4k')):
        w, h = 1920 * scale, 1080 * scale
        lines = place(drawing, plan['corners'], w, h, scale)
        lines.convert('RGB').save(f'{prefix}_{tag}.png')
        ink = 255 - np.asarray(lines, np.uint8)
        rgba = np.dstack([np.full_like(ink, 255)] * 3 + [ink])
        Image.fromarray(rgba, 'RGBA').save(f'{prefix}_lines_{tag}.png')
        if scale == 1 and still:
            base = np.asarray(Image.open(still).convert('RGB'), np.float32)
            a = (ink.astype(np.float32) / 255.0)[..., None]
            check = base * (1 - a) + np.array([255, 120, 60], np.float32) * a
            Image.fromarray(check.clip(0, 255).astype(np.uint8)).save(f'{prefix}_check.jpg', quality=92)
    print(f'plan_overlay: {prefix}_*.png ({plan["nest"]}, {plan["path"]}{", mirrored" if plan.get("mirrored") else ""})')


if __name__ == '__main__':
    main()
