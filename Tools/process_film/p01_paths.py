#!/usr/bin/env python3
"""p01_paths.py <nest> <out dir> [--map map.png] [--mirror] [--accent r,g,b] [--seconds 12]

Process film P01: a nest's line drawing becomes its recognised path network, then the game's map of it. 1920x1080
frames at 30 fps into <out dir> (f00000.jpg ...); Tools/frames_to_video.sh makes the MP4.

  the drawing on its paper (04_Paths/Images/path-NNN.png) fades in;
  the network Path Recognize found (StreamingAssets/Paths/path-NNN.json: nodes, then the edges growing from the junction)
  is drawn over it in the nest's colour;
  the paper goes, the network stays alone, clean, on the dark;
  the game's map of the nest (MapPainter, a sceneshots run's map.png) comes up under it, in place.

--mirror: the drawing and network flipped left to right, as the game turns the moss nest's (path-011~m).
The map lines up with the network by the hexagon the game frames each nest in (Core/PathField.InitNetwork: centred on
the image, circumradius grown until every inner feature fits) and the map's hexagon (its alpha).
"""
import argparse
import json
import math
import os
import sys

import cv2
import numpy as np
from PIL import Image

ROOT = os.path.expanduser('~/Desktop/AntGarden')
W_OUT, H_OUT, FPS = 1920, 1080, 30
BG = np.array([9, 11, 13], np.float32) / 255.0
PAPER = np.array([236, 233, 226], np.float32) / 255.0
INK = np.array([28, 28, 30], np.float32) / 255.0


def smooth(a, b, x):
    t = np.clip((x - a) / max(b - a, 1e-9), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def hex_radius(net, W, H):
    """PathField.InitNetwork's frame: circumradius in image px (WorldSize 106, FrameFill 0.985, border 0.035, pad 11)."""
    s = 106.0 / max(W, H)
    half_w, half_h = W * s / 2, H * s / 2
    border = 0.035 * max(W, H) * s
    r = min(W * s / math.sqrt(3), H * s / 2) * 0.985
    for e in net['edges']:
        for px, py in e['points']:
            x, z = (px - W / 2) * s, (py - H / 2) * s
            if abs(x) > half_w - border or abs(z) > half_h - border:
                continue
            sup = max(abs(x), abs(x) * 0.5 + abs(z) * 0.8660254) / 0.8660254
            r = max(r, sup + 11)
    return r / s


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('nest')                      # path-012
    ap.add_argument('out')
    ap.add_argument('--map')
    ap.add_argument('--mirror', action='store_true')
    ap.add_argument('--accent', default='120,226,240')
    ap.add_argument('--seconds', type=float, default=12.0)
    a = ap.parse_args()
    accent = np.array([float(v) for v in a.accent.split(',')], np.float32) / 255.0
    os.makedirs(a.out, exist_ok=True)

    net = json.load(open(os.path.join(ROOT, 'UnityProject.nosync/Assets/StreamingAssets/Paths', a.nest + '.json')))
    W, H = float(net['image']['width']), float(net['image']['height'])
    img = Image.open(os.path.join(ROOT, '04_Paths/Images', a.nest + '.png')).convert('L')
    nodes = {n['id']: n for n in net['nodes']}
    edges = net['edges']

    def doc(px, py):
        return (W - px if a.mirror else px), py

    # the hexagon (doc px) and the map's hexagon (map px)
    R_doc = hex_radius(net, W, H)
    hex_box = (W / 2 - R_doc * 0.8660254, H / 2 - R_doc, W / 2 + R_doc * 0.8660254, H / 2 + R_doc)
    map_rgba = None
    if a.map:
        m = np.asarray(Image.open(a.map).convert('RGBA')).astype(np.float32) / 255.0
        al = m[..., 3]
        ys = np.where(al.max(1) > 0.03)[0]
        mid = int((ys.min() + ys.max()) / 2)
        xs = np.where(al[mid] > 0.03)[0]
        mc = ((xs.min() + xs.max() + 1) / 2, (ys.min() + ys.max() + 1) / 2)
        R_map = (ys.max() - ys.min() + 1) / 2
        map_rgba = (m, mc, R_map)
    # the frame fits the paper first, then pulls back to the whole hexagon as the map comes (both centred on the image)
    k_paper = min((H_OUT - 90) / H, (W_OUT - 200) / W)
    k_hex = min((H_OUT - 70) / (hex_box[3] - hex_box[1]), (W_OUT - 200) / (hex_box[2] - hex_box[0])) if a.map else k_paper
    dc = (W / 2, H / 2)

    # the drawing, at the largest scale used (the end of the push-in), as ink 0..1
    zoom_end = 1.04
    pw, ph = int(round(W * k_paper * zoom_end)), int(round(H * k_paper * zoom_end))
    ink_big = 1.0 - np.asarray(img.resize((pw, ph), Image.LANCZOS)).astype(np.float32) / 255.0
    if a.mirror:
        ink_big = ink_big[:, ::-1].copy()
    ink_big = np.clip((ink_big - 0.04) / 0.9, 0, 1)

    # edges in doc px, each grown from its junction end
    polys = []
    for e in edges:
        pts = np.array([doc(px, py) for px, py in e['points']], np.float64)
        src, dst = nodes.get(e['source']), nodes.get(e['target'])
        if src and dst and src['type'] != 'junction' and dst['type'] == 'junction':
            pts = pts[::-1]
        seg = np.sqrt((np.diff(pts, axis=0) ** 2).sum(1))
        cum = np.concatenate([[0], np.cumsum(seg)])
        polys.append((pts, cum))
    longest = max(c[-1] for _, c in polys)

    T = a.seconds
    n = int(round(T * FPS))
    t_nodes, t_grow0, t_grow1 = 1.4, 2.2, 5.6
    t_paper0, t_paper1 = 5.9, 6.9
    t_pull0, t_pull1 = 7.8, 10.0
    t_map0, t_map1 = 8.2, 9.8
    print(f'{a.nest}: {len(nodes)} nodes, {len(edges)} edges, longest {longest:.0f} px, hex R {R_doc:.0f} px (image {W:.0f}x{H:.0f}), scale {k_paper:.4f} -> {k_hex:.4f}')

    for f in range(n):
        t = f / FPS
        pull = smooth(t_pull0, t_pull1, t)
        k = (k_paper * (1.0 + (zoom_end - 1.0) * smooth(0, t_pull0, t))) ** (1 - pull) * (k_hex * (1.0 + 0.02 * smooth(t_pull1, T, t))) ** pull

        def scr(p):
            return np.stack([W_OUT / 2 + (p[:, 0] - dc[0]) * k, H_OUT / 2 + (p[:, 1] - dc[1]) * k], 1)

        frame = np.empty((H_OUT, W_OUT, 3), np.float32)
        frame[:] = BG
        paper = 1.0 - smooth(t_paper0, t_paper1, t)
        ink_a = 1.0 - smooth(t_paper0 - 0.1, t_paper1 - 0.3, t)
        # the game's map, in place under the network
        mapa = smooth(t_map0, t_map1, t) if map_rgba is not None else 0.0
        if mapa > 0:
            m, mc, R_map = map_rgba
            s = R_doc * k / R_map
            hc = scr(np.array([[W / 2, H / 2]]))[0]
            M = np.array([[s, 0, hc[0] - mc[0] * s], [0, s, hc[1] - mc[1] * s]], np.float32)
            warped = cv2.warpAffine(m, M, (W_OUT, H_OUT), flags=cv2.INTER_AREA if s < 1 else cv2.INTER_LINEAR, borderValue=(0, 0, 0, 0))
            wa = warped[..., 3:4] * mapa
            frame = frame * (1 - wa) + warped[..., :3] * wa
        # the paper and its drawing
        if paper > 0.002 or ink_a > 0.002:
            tl = scr(np.array([[0.0, 0.0]]))[0]
            cw, ch = int(round(W * k)), int(round(H * k))
            ink = cv2.resize(ink_big, (cw, ch), interpolation=cv2.INTER_AREA)
            xa, ya = int(round(tl[0])), int(round(tl[1]))
            xb, yb = max(0, xa), max(0, ya)
            xe, ye = min(W_OUT, xa + cw), min(H_OUT, ya + ch)
            sub = ink[yb - ya:ye - ya, xb - xa:xe - xa, None]
            region = frame[yb:ye, xb:xe]
            region[:] = region * (1 - paper) + PAPER * paper
            region[:] = region * (1 - sub * ink_a * (0.85 if paper > 0.5 else 0.85 * paper + 0.15)) + INK * sub * ink_a * (0.85 if paper > 0.5 else 0.85 * paper + 0.15)
            frame[yb:ye, xb:xe] = region
        # the network: edges growing, nodes
        line = np.zeros((H_OUT, W_OUT), np.float32)
        wide = np.zeros((H_OUT, W_OUT), np.float32)
        grown = smooth(t_grow0, t_grow1, t) * longest * 1.0001
        for pts, cum in polys:
            m_ = int(np.searchsorted(cum, grown, side='right'))
            if m_ < 2:
                continue
            p = pts[:m_]
            if m_ < len(pts):
                u = (grown - cum[m_ - 1]) / max(cum[m_] - cum[m_ - 1], 1e-9)
                p = np.vstack([p, pts[m_ - 1] + (pts[m_] - pts[m_ - 1]) * u])
            sp = np.round(scr(p) * 16).astype(np.int32)
            cv2.polylines(line, [sp], False, 1.0, thickness=4, lineType=cv2.LINE_AA, shift=4)
            cv2.polylines(wide, [sp], False, 1.0, thickness=26, lineType=cv2.LINE_AA, shift=4)
        for i, nd in enumerate(net['nodes']):
            appear = smooth(t_nodes + 0.18 * i, t_nodes + 0.18 * i + 0.35, t)
            if appear <= 0:
                continue
            c = scr(np.array([doc(nd['x'], nd['y'])]))[0]
            c16 = (int(round(c[0] * 16)), int(round(c[1] * 16)))
            pulse = 1.0 + 1.6 * (1 - appear)
            if nd['type'] == 'junction':
                cv2.circle(line, c16, int(11 * pulse * 16), appear, thickness=-1, lineType=cv2.LINE_AA, shift=4)
            else:
                cv2.circle(line, c16, int(10 * pulse * 16), appear, thickness=3, lineType=cv2.LINE_AA, shift=4)
        dark = 1.0 - paper
        glow = cv2.GaussianBlur(line, (0, 0), 7) * (0.35 + 0.9 * dark)
        corridor = cv2.GaussianBlur(wide, (0, 0), 6) * 0.16 * smooth(t_paper0, t_paper1, t) * (1 - 0.7 * mapa)
        lines_a = np.clip(line * (1 - 0.7 * mapa), 0, 1)[..., None]
        # on the paper the line covers the ink in a deeper shade of the colour; on the dark it glows
        on_paper = accent * (0.55 + 0.45 * dark)
        frame = frame * (1 - lines_a) + on_paper * lines_a
        frame += (glow * (1 - 0.75 * mapa))[..., None] * accent * dark + corridor[..., None] * accent
        fade = smooth(0, 0.6, t) * (1 - smooth(T - 0.6, T, t))
        out = np.clip(frame * fade, 0, 1)
        cv2.imwrite(os.path.join(a.out, f'f{f:05d}.jpg'), (out[..., ::-1] * 255 + 0.5).astype(np.uint8), [cv2.IMWRITE_JPEG_QUALITY, 95])
        if f % 60 == 0:
            print(f'  frame {f}/{n}', flush=True)
    print('done', n, 'frames')


if __name__ == '__main__':
    main()
