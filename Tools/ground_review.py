#!/usr/bin/env python3
"""ground_review.py <folder> <set> [<set> ...] - a review sheet for a nest's new ground textures, before they go in.

  python3 Tools/ground_review.py 02_Concepts/Textures/v11 honey leafcut

For each set (<folder>/<set>_floor_a, _b, _c and <set>_bank .png, as Codex painted them) one sheet,
<folder>/00_审核_<set>.jpg: the four textures; the three floors mixed in large soft patches the way the terrain shader
mixes them, three tiles (about 21 units, the ant is 1) across; the wall tiled 2 x 2; and the floor as the particle nest
shows it, simulated: at night, from behind the ant, every point a dab in the texture's colour at its middle (leaning to
the nest's floor tint, NestTheme), the darkest ones left out as gaps, lit by the pool round the ant, the textures in the
game now beside the new ones. Nothing in the Unity project is touched (see make_ground_textures.py for that).
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_ground_textures as mg  # noqa: E402
from preview_ground_textures import lit, mixed  # noqa: E402

GROUND = os.path.join(mg.ROOT, 'UnityProject.nosync', 'Assets', 'AntGarden', 'Art', 'Ground')
TILE_UNITS = 1 / 0.14        # one floor tile in the world (Game: _FloorScale 0.14)
# set: its name, the floor tint (NestTheme), the night sky (its fog), the ground set it uses now and that set's turn
SETS = {
    'honey': ('蜜罐粮仓', (0.62, 0.42, 0.20), (0.15, 0.10, 0.04), 'amber', -25),
    'leafcut': ('切叶蚁菌圃', (0.56, 0.52, 0.24), (0.10, 0.11, 0.04), 'blue', 90),
    'moss': ('苔藓森林', (0.66, 0.75, 0.61), (0.10, 0.13, 0.10), 'blue', 55),
    'amber2': ('暖琥珀蚁穴', (0.80, 0.67, 0.54), (0.14, 0.11, 0.09), 'amber', 0),
    'kiln': ('古蚁圣殿 · 窑火', (0.77, 0.57, 0.53), (0.15, 0.105, 0.105), 'amber', -8),   # (KilnRecipe's floor and fog)
}
FONT = '/System/Library/Fonts/Hiragino Sans GB.ttc'
F1, F2, F3 = ImageFont.truetype(FONT, 34), ImageFont.truetype(FONT, 24), ImageFont.truetype(FONT, 19)


def lin(a):
    return np.where(a <= 0.04045, a / 12.92, ((a + 0.055) / 1.055) ** 2.4)


def srgb(a):
    a = np.clip(a, 0, 1)
    return np.where(a <= 0.0031308, a * 12.92, 1.055 * a ** (1 / 2.4) - 0.055)


def hue_shift(c, deg):
    """the game's turn of hue (AG_HueShift: YIQ), for a set used turned."""
    y = c @ np.array([0.299, 0.587, 0.114])
    i = c @ np.array([0.596, -0.274, -0.322])
    q = c @ np.array([0.211, -0.523, 0.312])
    a = np.radians(deg)
    i2, q2 = i * np.cos(a) - q * np.sin(a), i * np.sin(a) + q * np.cos(a)
    return np.clip(np.stack([y + 0.956 * i2 + 0.621 * q2, y - 0.272 * i2 - 0.647 * q2, y - 1.106 * i2 + 1.703 * q2], -1), 0, None)


def prepared(src, px=768):
    """a painted texture made seamless and smaller, with its height (as make_ground_textures does it)."""
    img = mg.seamless(src)
    img = np.asarray(Image.fromarray((img.clip(0, 1) * 255 + 0.5).astype(np.uint8)).resize((px, px), Image.LANCZOS), dtype=np.float64) / 255
    soft = np.dstack([mg.blur_wrap(img[..., k], 0.75) for k in range(3)])
    img = (soft + (img - soft) * mg.FINE_KEEP).clip(0, 1)
    return img, mg.height_map(img)


def game_floors(set_id, turn, px=768):
    """the floors the game uses now (Art/Ground/<set>/floor, floor2, floor3; height in alpha), turned as it turns them."""
    out = []
    for name in ('floor', 'floor2', 'floor3'):
        a = np.asarray(Image.open(os.path.join(GROUND, set_id, name + '.png')).convert('RGBA').resize((px, px), Image.LANCZOS), dtype=np.float64) / 255
        rgb = srgb(hue_shift(lin(a[..., :3]), turn)) if turn else a[..., :3]
        out.append((rgb, a[..., 3]))
    return out


def h32(x):
    x = (x.astype(np.uint64) * np.uint64(747796405) + np.uint64(2891336453)) & np.uint64(0xFFFFFFFF)
    x = ((x >> ((x >> np.uint64(28)) + np.uint64(4))) ^ x) * np.uint64(277803737) & np.uint64(0xFFFFFFFF)
    x = (x >> np.uint64(22)) ^ x
    return (x & np.uint64(0xFFFFFF)).astype(np.float64) / 16777216.0


def refl(t):
    return 1 - np.abs(t % 2 - 1)


def points_view(floor, tint, sky, span, W=1200, H=675, ss=2, step=0.024, texmix=0.6):
    """
    The floor as the particle nest draws it, simulated from behind the ant (camera 1.6 up, looking 26 degrees down):
    the ground a jittered grid of dabs (most near one size, some smaller, a few larger), each in the texture's colour at
    its middle leaning to the nest's floor tint (texmix of its own colour), the darkest fine ones left out as gaps; a
    pool of light round the ant, the rest night; fog with distance. span: how many units the floor image covers;
    ss: supersampling.
    """
    img = lin(floor)
    N = img.shape[0]
    lum = img @ np.array([0.3, 0.55, 0.15])
    mean = lum.mean()
    t = lin(np.array(tint))
    tl = t @ np.array([0.3, 0.55, 0.15])
    w, h = W * ss, H * ss
    fov, pitch, cam_y = np.radians(40), np.radians(-26), 2.2   # a long lens from further off: no wide-angle stretch at the edges
    yy, xx = np.mgrid[0:h, 0:w]
    nx = (xx + 0.5) / w * 2 - 1
    ny = 1 - (yy + 0.5) / h * 2
    th = np.tan(fov / 2)
    dx, dy, dz = nx * th * (w / h), ny * th, np.ones_like(nx)
    cp, sp = np.cos(pitch), np.sin(pitch)
    dy, dz = dy * cp + dz * sp, -dy * sp + dz * cp
    hit = dy < -1e-4
    tt = np.where(hit, cam_y / -np.where(hit, dy, -1), 0)
    X, Z = dx * tt, 2.0 + dz * tt
    i0, j0 = np.floor(X / step).astype(np.int64), np.floor(Z / step).astype(np.int64)
    best = np.full(X.shape, np.inf)
    col = np.zeros(X.shape + (3,))
    for di in (-1, 0, 1):
        for dj in (-1, 0, 1):
            ci, cj = i0 + di, j0 + dj
            cell = (ci * 73856093) ^ (cj * 19349663)
            cell = cell & 0xFFFFFFFF
            cx, cz = (ci + h32(cell + 1)) * step, (cj + h32(cell + 2)) * step
            g = (h32(cell + 911) + h32(cell + 912) + h32(cell + 913) - 1.5) * 1.6
            grow = np.clip(np.exp(g * 0.5), 0.45, 3.0)
            r = step * 0.62 * grow
            # (the mixed floor does not wrap: the view sits inside it, mirrored beyond its edges)
            u = (refl((cx + span / 2) / span) * (N - 1)).astype(np.int64)
            v = (refl((cz - 1.0) / span) * (N - 1)).astype(np.int64)
            al = lum[v, u]
            rel = al / mean
            big = np.clip((grow - 1) / 2, 0, 1)
            keep = np.clip(0.45 + 0.55 * rel ** 1.5, 0, 1)
            alive = h32(cell + 15485863) <= 1 - (1 - keep) * (1 - big)
            d = np.hypot(X - cx, Z - cz) / r
            take = alive & (d < 1) & (d < best)
            own = img[v, u]
            c = (1 - texmix) * t[None, None, :] * (al / tl)[..., None] + texmix * own
            c *= (0.88 + 0.24 * h32(cell + 999))[..., None] * (1 - 0.18 * np.clip((d - 0.4) / 0.6, 0, 1))[..., None]
            best = np.where(take, d, best)
            col = np.where(take[..., None], c, col)
    dist = np.hypot(X, Z - 2.0)
    pool = 0.14 + 1.1 * np.exp(-(X ** 2 + (Z - 3.2) ** 2) / (2 * 1.7 ** 2))
    light = pool * np.exp(-np.hypot(X, Z) / 14)
    out = col * light[..., None] * 1.6
    out = np.where(hit[..., None], out, lin(np.array(sky))[None, None, :] * 0.35)
    out = out.reshape(H, ss, W, ss, 3).mean((1, 3))
    return Image.fromarray((srgb(out) * 255 + 0.5).astype(np.uint8))


def sheet(folder, set_id):
    name, tint, sky, old_set, turn = SETS[set_id]
    srcs = {}
    for layer in ('floor_a', 'floor_b', 'floor_c', 'bank'):
        p = os.path.join(folder, f'{set_id}_{layer}.png')
        srcs[layer] = np.asarray(Image.open(p).convert('RGB'), dtype=np.float64) / 255 if os.path.exists(p) else None
    floors = [prepared(srcs[k]) for k in ('floor_a', 'floor_b', 'floor_c') if srcs[k] is not None]
    if len(floors) < 3:
        print(f'ground_review: {set_id}: not all three floors yet')
        return None
    new_img, new_hgt = mixed(floors)
    # (for the points: five tiles across, so the view never reaches the edge of the mixed floor)
    new_sim, _ = mixed([prepared(srcs[k], 512) for k in ('floor_a', 'floor_b', 'floor_c')], tiles=5)
    old_sim, _ = mixed(game_floors(old_set, turn, 512), tiles=5)
    T, G = 420, 16
    W = G + 4 * (T + G)
    page = Image.new('RGB', (W, 80 + T + 56 + 2 * T + G + 56 + 40 + 34 + W * 9 // 32 + 2 * G), (18, 19, 21))
    d = ImageDraw.Draw(page)
    version = os.path.basename(os.path.normpath(folder)).split('_')[0]   # (the batch: v11, v12_amber...)
    d.text((G, 22), f'{name} · 新的地面贴图（{version}）· 等你审核，确认后才放进游戏', font=F1, fill=(236, 232, 222))
    y = 80
    labels = {'floor_a': '谷底 A', 'floor_b': '谷底 B', 'floor_c': '谷底 C', 'bank': '岸坡（两侧的墙）'}
    for k, layer in enumerate(('floor_a', 'floor_b', 'floor_c', 'bank')):
        if srcs[layer] is None:
            continue
        im = Image.fromarray((srcs[layer] * 255 + 0.5).astype(np.uint8)).resize((T, T), Image.LANCZOS)
        page.paste(im, (G + k * (T + G), y + 34))
        d.text((G + k * (T + G), y), labels[layer], font=F2, fill=(232, 196, 120))
    y += T + 56
    d.text((G, y), '三种谷底混在一起，像游戏里一样一片一片（约 21 个单位见方，蚂蚁身长 1）', font=F2, fill=(232, 196, 120))
    mix = Image.fromarray((lit(new_img, new_hgt, mg.STRENGTH['floor']) * 255).astype(np.uint8)).resize((2 * T + G, 2 * T + G), Image.LANCZOS)
    page.paste(mix, (G, y + 40))
    if srcs['bank'] is not None:
        bimg, bh = prepared(srcs['bank'], 512)
        bank = Image.fromarray((np.tile(lit(bimg, bh, mg.STRENGTH['bank']), (2, 2, 1)) * 255).astype(np.uint8)).resize((2 * T + G, 2 * T + G), Image.LANCZOS)
        page.paste(bank, (G + 2 * (T + G), y + 40))
        d.text((G + 2 * (T + G), y), '岸坡 2×2 拼接', font=F2, fill=(232, 196, 120))
    y += 2 * T + G + 56
    d.text((G, y), '变成粒子以后（模拟：夜里、游戏视角、蚂蚁脚下有一圈光；不是游戏截图）', font=F2, fill=(232, 196, 120))
    y += 40
    vw = (W - 3 * G) // 2
    vh = vw * 9 // 16
    for k, (label, img) in enumerate((('现在游戏里用的', old_sim), ('换成新贴图', new_sim))):
        v = points_view(img, tint, sky, 5 * TILE_UNITS).resize((vw, vh), Image.LANCZOS)
        page.paste(v, (G + k * (vw + G), y + 34))
        d.text((G + k * (vw + G), y), label, font=F3, fill=(200, 196, 188))
    page = page.crop((0, 0, W, y + 34 + vh + G))
    out = os.path.join(folder, f'00_审核_{set_id}.jpg')
    page.save(out, quality=88)
    print(out, page.size)
    return out


def main():
    folder = os.path.join(mg.ROOT, sys.argv[1])
    for set_id in sys.argv[2:]:
        sheet(folder, set_id)


if __name__ == '__main__':
    sys.exit(main())
