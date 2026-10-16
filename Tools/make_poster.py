#!/usr/bin/env python3
"""make_poster.py - compose a 16:9 promotional poster from a 4K in-game still.

  python3 Tools/make_poster.py <still.jpg> <out.png> [top|left]

top:  title centred in the upper part of the frame (for shots with open sky above the scene)
left: title block lower left (for shots whose lower half is quiet ground)
All text is English. Fonts are macOS system fonts (Didot, Avenir Next); output is 3840x2160 PNG plus a JPEG
beside it, written over any earlier version in place.
"""
import sys

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

FONTS = '/System/Library/Fonts/'
SUPP = FONTS + 'Supplemental/'
INK = (242, 248, 246)
GLOW = (110, 235, 228)
SHADE = (3, 9, 16)

TITLE = 'ANT GARDEN'
TAGLINE = 'A canyon world grown from the tunnels of real ant nests'
FEATURES = 'EXPLORE THE NESTS   ·   MEET THE COLONY   ·   CROSS THE LIGHT GATES'


def font(path, size, index=0):
    return ImageFont.truetype(path, size, index=index)


def tracked_width(draw, text, f, tracking):
    w = 0
    for i, ch in enumerate(text):
        w += draw.textlength(ch, font=f)
        if i < len(text) - 1:
            w += tracking
    return w


def draw_tracked(draw, xy, text, f, tracking, fill):
    x, y = xy
    for ch in text:
        draw.text((x, y), ch, font=f, fill=fill)
        x += draw.textlength(ch, font=f) + tracking


def gradient(size, stops, color):
    """Vertical shade: stops = [(y fraction, alpha 0..1), ...], linear between them."""
    w, h = size
    mask = Image.new('L', (1, h))
    px = mask.load()
    for y in range(h):
        t = y / (h - 1)
        a = stops[-1][1]
        for (t0, a0), (t1, a1) in zip(stops, stops[1:]):
            if t0 <= t <= t1:
                a = a0 + (a1 - a0) * (t - t0) / max(1e-6, t1 - t0)
                break
        px[0, y] = int(255 * max(0, min(1, a)))
    return Image.new('RGB', size, color), mask.resize(size)


def vignette(size, strength):
    w, h = size
    small = Image.new('L', (w // 8, h // 8), 0)
    d = ImageDraw.Draw(small)
    sw, sh = small.size
    for i in range(40):
        t = i / 40
        d.ellipse((-sw * 0.25 + t * sw * 0.45, -sh * 0.35 + t * sh * 0.5, sw * 1.25 - t * sw * 0.45, sh * 1.35 - t * sh * 0.5),
                  fill=int(255 * strength * (1 - t)))
    small = small.filter(ImageFilter.GaussianBlur(sw / 12))
    inv = Image.eval(small.resize(size), lambda v: int(255 * strength) - v if v < 255 * strength else 0)
    return inv


def glow_text(base, layer_fn, radius, color, amount):
    """Draw text onto a mask with layer_fn(draw), blur it and add it as coloured light, then draw it crisp."""
    mask = Image.new('L', base.size, 0)
    layer_fn(ImageDraw.Draw(mask), 255)
    halo = mask.filter(ImageFilter.GaussianBlur(radius))
    light = Image.new('RGB', base.size, color)
    halo = halo.point(lambda v: int(v * amount))
    base.paste(light, (0, 0), halo)


def compose(src, out, layout):
    img = Image.open(src).convert('RGB').resize((3840, 2160), Image.LANCZOS)
    W, H = img.size
    img = ImageEnhance.Contrast(img).enhance(1.06)
    img = ImageEnhance.Color(img).enhance(1.08)
    if layout == 'top':
        shade, mask = gradient(img.size, [(0, 0.72), (0.22, 0.45), (0.5, 0.0), (0.8, 0.0), (1, 0.6)], SHADE)
    else:
        shade, mask = gradient(img.size, [(0, 0.35), (0.25, 0.0), (0.5, 0.15), (1, 0.82)], SHADE)
    img.paste(shade, (0, 0), mask)
    img.paste(Image.new('RGB', img.size, (0, 0, 0)), (0, 0), vignette(img.size, 0.42))

    d = ImageDraw.Draw(img)
    big = layout == 'top'
    f_title = font(SUPP + 'Didot.ttc', 300 if big else 240)
    f_tag = font(FONTS + 'Avenir Next.ttc', 60 if big else 52, index=7)
    f_feat = font(FONTS + 'Avenir Next.ttc', 40 if big else 36, index=5)
    t_track, f_track = (72, 10) if big else (58, 9)
    # measured glyph extents (offsets from the drawing origin), so the lines sit at exact gaps
    _, cap_top, _, cap_bot = d.textbbox((0, 0), TITLE, font=f_title)
    _, tag_top, _, _ = d.textbbox((0, 0), TAGLINE, font=f_tag)
    tag_w = d.textlength(TAGLINE, font=f_tag)

    if layout == 'top':
        tw = tracked_width(d, TITLE, f_title, t_track)
        title_xy = ((W - tw) / 2, 250 - cap_top)
        rule_y = 250 + (cap_bot - cap_top) + 78
        rule_x0, rule_x1 = W / 2 - 300, W / 2 + 300
        tag_xy = ((W - tag_w) / 2, rule_y + 46 - tag_top)
        feat_w = tracked_width(d, FEATURES, f_feat, f_track)
        feat_xy = ((W - feat_w) / 2, H - 150)
    else:
        # the title block sits below the scene's subjects, in the quiet ground at the bottom left
        x0 = 210
        feat_xy = (x0 + 6, H - 165)
        tag_xy = (x0 + 6, H - 300 - tag_top)
        rule_y = H - 338
        rule_x0, rule_x1 = x0 + 8, x0 + 520
        title_xy = (x0, rule_y - 70 - cap_bot)

    def title_layer(draw, fill):
        draw_tracked(draw, title_xy, TITLE, f_title, t_track, fill)

    glow_text(img, title_layer, 36, GLOW, 0.55)
    # a soft dark bed under the small lines, so they read over glowing plants
    bed = Image.new('L', img.size, 0)
    bd = ImageDraw.Draw(bed)
    for (x, y), text, f in ((tag_xy, TAGLINE, f_tag), (feat_xy, FEATURES, f_feat)):
        w = d.textlength(text, font=f) + (len(text) * f_track if f is f_feat else 0)
        bd.rounded_rectangle((x - 40, y - 18, x + w + 40, y + f.size * 1.35), radius=30, fill=150)
    bed = bed.filter(ImageFilter.GaussianBlur(28))
    img.paste(Image.new('RGB', img.size, SHADE), (0, 0), bed)
    d = ImageDraw.Draw(img)
    draw_tracked(d, title_xy, TITLE, f_title, t_track, INK)
    d.line((rule_x0, rule_y, rule_x1, rule_y), fill=(150, 220, 214), width=3)
    mid = (rule_x0 + rule_x1) / 2 if layout == 'top' else rule_x0 + 40
    d.polygon([(mid, rule_y - 10), (mid + 10, rule_y), (mid, rule_y + 10), (mid - 10, rule_y)], fill=(190, 245, 238))
    d.text(tag_xy, TAGLINE, font=f_tag, fill=(214, 236, 232))
    draw_tracked(d, feat_xy, FEATURES, f_feat, f_track, (176, 214, 210))

    img.save(out, optimize=True)
    img.save(out.rsplit('.', 1)[0] + '.jpg', quality=95)
    print(f'make_poster: wrote {out}')


if __name__ == '__main__':
    compose(sys.argv[1], sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else 'top')
