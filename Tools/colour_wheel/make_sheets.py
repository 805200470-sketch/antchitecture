"""
The colour-wheel sheets for the particle nests (02_Concepts/Particle_Styles_v1): 00_总览.jpg (the styles' effect
images, a row each) and 01_色轮对应.jpg (the room lights' HSV hue wheel in 12 segments, each style's thumbnail at its
segment, its measured hue marked, the empty segments with a suggested theme).

    python3 Tools/colour_wheel/measure_hue.py '<{"key": [images...]}>' meas.json   # dominant glow hue per style
    python3 Tools/colour_wheel/make_sheets.py meas.json config.json [wheel|overview|both]

The config (see 02_Concepts/Particle_Styles_v1/wheel_config_v2.json) lists the rows, the styles (key, name, thumb,
optional segment to place it at, optional note under it), the suggestions for empty segments and the texts.
SHEETS_OUT sets the folder; WHEEL_NAME / OVERVIEW_NAME the file names.
"""
import colorsys
import json
import math
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = '/Users/xujiafeng/Desktop/AntGarden/'
import os
OUT = os.environ.get('SHEETS_OUT', ROOT + '02_Concepts/Particle_Styles_v1/')
MED = '/System/Library/Fonts/STHeiti Medium.ttc'
LIGHT = '/System/Library/Fonts/STHeiti Light.ttc'
BG = (19, 20, 22)
WHITE = (236, 236, 236)
GREY = (150, 152, 156)
GOLD = (232, 184, 98)
SEG_NAMES = ['红', '橙', '黄', '黄绿', '绿', '青绿', '青', '天蓝', '蓝', '紫', '品红', '玫红']
SEG_EN = ['Red', 'Orange', 'Yellow', 'Chartreuse', 'Green', 'Spring green', 'Cyan', 'Azure', 'Blue', 'Violet',
          'Magenta', 'Rose']


def font(path, size):
    return ImageFont.truetype(path, size)


def hue_rgb(h, s=0.78, v=0.92):
    r, g, b = colorsys.hsv_to_rgb((h % 360) / 360, s, v)
    return int(r * 255), int(g * 255), int(b * 255)


def fit(im, w, h):
    """cover-crop to w x h."""
    sc = max(w / im.width, h / im.height)
    im = im.resize((max(w, round(im.width * sc)), max(h, round(im.height * sc))), Image.LANCZOS)
    x, y = (im.width - w) // 2, (im.height - h) // 2
    return im.crop((x, y, x + w, y + h))


PUNCT = '，。、：；）」』！？,.:;)'


def deg(h):
    """degrees rounded half up (26.5 -> 27), the same way the text and the table round."""
    return int(math.floor(h + 0.5))


def wrap(draw, text, fnt, width):
    """wrap into as few lines as needed, but balanced: break near equal widths, preferring after punctuation."""
    total = draw.textlength(text, font=fnt)
    if total <= width:
        return [text]
    n = math.ceil(total / width)
    lines, rest = [], text
    for k in range(n - 1, 0, -1):
        target = draw.textlength(rest, font=fnt) / (k + 1)
        best, best_score = None, None
        for i in range(1, len(rest)):
            if rest[i] in PUNCT:          # never start a line with punctuation
                continue
            w = draw.textlength(rest[:i], font=fnt)
            if w > width:
                break
            score = abs(w - target) - (0.6 * target if rest[i - 1] in PUNCT else 0)
            if best_score is None or score < best_score:
                best, best_score = i, score
        best = best or 1
        lines.append(rest[:best])
        rest = rest[best:]
    lines.append(rest)
    return lines


def overview(cfg, meas):
    W_IMG, H_IMG, GAP, M = 1040, 585, 24, 60
    rows = cfg['rows']
    title_h, row_head, cap_h, row_gap = 170, 74, 54, 46
    W = M * 2 + W_IMG * 2 + GAP
    H = title_h + len(rows) * (row_head + H_IMG + cap_h + row_gap) + 20
    sheet = Image.new('RGB', (W, H), BG)
    d = ImageDraw.Draw(sheet)
    d.text((M, 44), cfg['overview_title'], font=font(MED, 50), fill=WHITE)
    d.text((M, 112), cfg['overview_sub'], font=font(LIGHT, 27), fill=GREY)
    y = title_h
    for row in rows:
        m = meas[row['key']]
        hue = m['hue']
        seg = row.get('segment', m['segment'] * 30) // 30
        d.ellipse((M, y + 18, M + 26, y + 44), fill=hue_rgb(hue))
        d.text((M + 42, y + 10), row['title'], font=font(MED, 38), fill=WHITE)
        tw = d.textlength(row['title'], font=font(MED, 38))
        mseg = m['segment']
        if seg == mseg:
            info = f"主光色相 {deg(hue)}° · {SEG_NAMES[seg]}段（{seg * 30}°）"
        else:
            info = f"主光色相 {deg(hue)}°（{SEG_NAMES[mseg]}段上沿）→ 建议放{SEG_NAMES[seg]}段（{seg * 30}°）"
        if row.get('note'):
            info += f"  {row['note']}"
        d.text((M + 42 + tw + 26, y + 20), info, font=font(LIGHT, 27), fill=GREY)
        y += row_head
        for i, (path, cap) in enumerate(row['images']):
            im = fit(Image.open(ROOT + path).convert('RGB'), W_IMG, H_IMG)
            x = M + i * (W_IMG + GAP)
            sheet.paste(im, (x, y))
            d.text((x, y + H_IMG + 12), cap, font=font(MED, 28), fill=GOLD)
        y += H_IMG + cap_h + row_gap
    name = os.environ.get('OVERVIEW_NAME', '00_总览.jpg')
    sheet.save(OUT + name, quality=90)
    print(name, sheet.size)


def wheel(cfg, meas):
    W, H = 2800, 3600
    cx, cy = W // 2, 1800
    R_OUT, R_IN = 560, 405
    sheet = Image.new('RGB', (W, H), BG)
    # hue ring, anti-aliased by supersampling
    S = 3
    size = 2 * R_OUT * S + 8
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float64)
    c = size / 2
    dx, dy = xx - c, yy - c
    rr = np.hypot(dx, dy) / S
    ang = (np.degrees(np.arctan2(dx, -dy)) + 360) % 360      # 0 at top, clockwise
    hsv = np.stack([ang / 360, np.full_like(ang, 0.72), np.full_like(ang, 0.86)], -1)
    rgb = np.zeros_like(hsv)
    i = np.floor(hsv[..., 0] * 6).astype(int) % 6
    f = hsv[..., 0] * 6 - np.floor(hsv[..., 0] * 6)
    v, s = hsv[..., 2], hsv[..., 1]
    p, q, t = v * (1 - s), v * (1 - f * s), v * (1 - (1 - f) * s)
    for k, (a, b, cc) in enumerate([(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)]):
        m = i == k
        rgb[..., 0][m], rgb[..., 1][m], rgb[..., 2][m] = a[m], b[m], cc[m]
    ring = (rr <= R_OUT) & (rr >= R_IN)
    # thin separators at segment boundaries (centre +- 15)
    bd = np.abs(((ang - 15) % 30) - 15)          # distance in degrees to nearest boundary... boundary at 15+30k
    bd = np.minimum(((ang - 15) % 30), 30 - ((ang - 15) % 30))
    sep = (np.radians(bd) * rr) < 2.2
    img = np.zeros((size, size, 3))
    img[:] = np.array(BG) / 255
    img[ring] = rgb[ring]
    img[ring & sep] = np.array(BG) / 255
    ring_im = Image.fromarray((img * 255).astype(np.uint8)).resize((size // S, size // S), Image.LANCZOS)
    sheet.paste(ring_im, (cx - ring_im.width // 2, cy - ring_im.height // 2))
    d = ImageDraw.Draw(sheet)

    def pol(h, r):
        a = math.radians(h)
        return cx + r * math.sin(a), cy - r * math.cos(a)

    # segment labels inside the ring
    for k in range(12):
        x, y = pol(k * 30, R_IN - 72)
        f1, f2 = font(MED, 34), font(LIGHT, 22)
        t1, t2 = SEG_NAMES[k], f'{k * 30}°'
        d.text((x - d.textlength(t1, font=f1) / 2, y - 30), t1, font=f1, fill=WHITE)
        d.text((x - d.textlength(t2, font=f2) / 2, y + 10), t2, font=f2, fill=GREY)
    d.text((cx - d.textlength('HSV 色相环', font=font(LIGHT, 28)) / 2, cy - 40), 'HSV 色相环', font=font(LIGHT, 28), fill=GREY)
    d.text((cx - d.textlength('12 段 × 30°', font=font(LIGHT, 28)) / 2, cy + 6), '12 段 × 30°', font=font(LIGHT, 28), fill=GREY)

    # styles, grouped by segment
    by_seg = {}
    for st in cfg['styles']:
        by_seg.setdefault(st.get('segment', meas[st['key']]['segment'] * 30) // 30, []).append(st)
    TW1, TH1 = 380, 214
    placed = []   # (style, hue, cx, cy, tw, th)
    for seg in range(12):
        sts = sorted(by_seg.get(seg, []), key=lambda st: meas[st['key']]['hue'])
        if sts:
            n = len(sts)
            tw = TW1 if n == 1 else 330
            th = round(tw * 9 / 16)
            tx, ty = pol(seg * 30, 930 if n == 1 else 1060)
            if n > 1 and seg not in (0, 6):
                tx += 50 if seg < 6 else -50
            for j, st in enumerate(sts):
                ox = (j - (n - 1) / 2) * (tw + 36)
                # a style moved to a segment (with a note saying from where) is marked at the segment's centre
                hue = st['segment'] if 'segment' in st and st.get('note') else meas[st['key']]['hue']
                placed.append((st, hue, tx + ox, ty, tw, th))
        else:
            name, desc = cfg['suggestions'][str(seg * 30)]
            tx, ty = pol(seg * 30, 820)
            f1, f15, f2 = font(MED, 30), font(MED, 31), font(LIGHT, 25)
            lines = wrap(d, desc, f2, 420)
            block_h = 40 + 44 + 33 * len(lines)
            y0 = ty - block_h / 2
            label = f'待定 · {SEG_NAMES[seg]} {seg * 30}°'
            lw = d.textlength(label, font=f1)
            d.rounded_rectangle((tx - lw / 2 - 36, y0 + 7, tx - lw / 2 - 12, y0 + 31), 5,
                                fill=hue_rgb(seg * 30, 0.6, 0.72))
            d.text((tx - lw / 2, y0), label, font=f1, fill=GOLD)
            d.text((tx - d.textlength(name, font=f15) / 2, y0 + 42), name, font=f15, fill=WHITE)
            for j, ln in enumerate(lines):
                d.text((tx - d.textlength(ln, font=f2) / 2, y0 + 86 + j * 33), ln, font=f2, fill=GREY)
    # connectors first, then markers and thumbnails on top
    for st, hue, tx, ty, tw, th in placed:
        ex, ey = pol(hue, R_OUT + 38)
        # aim at the thumbnail edge point nearest the wheel
        vx, vy = cx - tx, cy - ty
        L = math.hypot(vx, vy)
        kx = min(tw / 2 / max(abs(vx), 1e-6), th / 2 / max(abs(vy), 1e-6))
        end = (tx + vx * kx, ty + vy * kx)
        if ty + vy * kx >= ty + th / 2 - 1:
            if abs(vx) > 0.35 * L:
                end = (tx + math.copysign(tw / 2, vx), ty + th * 0.25)   # below and to the side: from the side, above the name
            else:
                end = (tx, ty + th / 2 + 96)   # the wheel is below: from under the name and the note, not across them
        d.line([(ex, ey), end], fill=(118, 120, 124), width=2)
    for st, hue, tx, ty, tw, th in placed:
        tri = [pol(hue, R_OUT + 4), pol(hue - 2.2, R_OUT + 34), pol(hue + 2.2, R_OUT + 34)]
        d.polygon(tri, fill=WHITE)
        thumb = fit(Image.open(ROOT + st['thumb']).convert('RGB'), tw, th)
        x0, y0 = int(tx - tw / 2), int(ty - th / 2)
        d.rectangle((x0 - 4, y0 - 4, x0 + tw + 3, y0 + th + 3), fill=hue_rgb(hue))
        sheet.paste(thumb, (x0, y0))
        f1, f2 = font(MED, 33), font(LIGHT, 25)
        d.text((tx - d.textlength(st['name'], font=f1) / 2, y0 + th + 14), st['name'], font=f1, fill=WHITE)
        sub = f"测得 {deg(hue)}°" + (f" · {st['sub']}" if st.get('sub') else '')
        if 'segment' in st and st['segment'] // 30 != meas[st['key']]['segment']:
            sub = f"测得 {deg(hue)}°（{SEG_NAMES[meas[st['key']]['segment']]}段上沿）→ 建议{SEG_NAMES[st['segment'] // 30]}"
        if st.get('note'):
            sub = st['note']
        d.text((tx - d.textlength(sub, font=f2) / 2, y0 + th + 57), sub, font=f2, fill=GREY)
    # crop to content, then add header and footer
    a = np.asarray(sheet).astype(int)
    rows = np.where((np.abs(a - np.array(BG)).sum(-1) > 30).any(1))[0]
    body = sheet.crop((0, max(rows.min() - 50, 0), W, min(rows.max() + 50, H)))
    M = 80
    head_h = 140 + 44 * len(cfg['wheel_sub']) + 30
    foot_lines = cfg['wheel_foot'] if isinstance(cfg['wheel_foot'], list) else [cfg['wheel_foot']]
    foot_h = 40 + 40 * len(foot_lines) + 50
    final = Image.new('RGB', (W, head_h + body.height + foot_h), BG)
    final.paste(body, (0, head_h))
    d = ImageDraw.Draw(final)
    d.text((M, 60), cfg['wheel_title'], font=font(MED, 56), fill=WHITE)
    yy0 = 140
    for ln in cfg['wheel_sub']:
        d.text((M, yy0), ln, font=font(LIGHT, 29), fill=GREY)
        yy0 += 44
    yy0 = head_h + body.height + 30
    d.line([(M, yy0 - 10), (W - M, yy0 - 10)], fill=(48, 50, 54), width=2)
    for ln in foot_lines:
        d.text((M, yy0 + 10), ln, font=font(LIGHT, 26), fill=GREY)
        yy0 += 40
    sheet = final
    name = os.environ.get('WHEEL_NAME', '01_色轮对应.jpg')
    sheet.save(OUT + name, quality=90)
    print(name, sheet.size)


if __name__ == '__main__':
    meas = json.load(open(sys.argv[1]))
    cfg = json.load(open(sys.argv[2]))
    what = sys.argv[3] if len(sys.argv) > 3 else 'both'
    if what in ('overview', 'both'):
        overview(cfg, meas)
    if what in ('wheel', 'both'):
        wheel(cfg, meas)
