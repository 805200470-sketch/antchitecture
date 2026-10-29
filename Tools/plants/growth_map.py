#!/usr/bin/env python3
"""growth_map.py [name ...]

Each plant's growth map, so its glowing markings follow how it grows (ParticleLook.BioMark: the user, on bands cut
level through a corkscrew's stem and leaves alike: "要顺着植物本身的生长逻辑去生长 … 要贴合它的形体"). Per stored point:
  g  how far up the plant it is, measured along its surface from its foot (geodesic, over a graph of near points), as
     a share of the farthest any point is: its iso-lines run square to a stem wherever the stem turns, and round a
     shelf or a cup as its growth zones do;
  t  how far it is, along the surface, from the nearest growing tip (the end of a shoot, leaflet, branch, tendril or
     leaf: a point farther from the foot than every point near it), in the plant's own units (its height is 1);
  s  how much it is part of a stem, a narrow tube, rather than a blade or a body (0..255: the normals round it spread
     round an axis, not along one direction);
  e  a crystal's (crystal_*): how much it lies on a ridge, where two of its faces meet bending away from each other, or
     on a point (0..255; the others 0), so its ridges catch the light as thin lines (ParticleLook.CrystalSplat; the
     user: "没有一些棱棱角角这些内容，只有一个剪影形状"). A crystal's s is instead how far it lies from its nearest
     ridge, its face's middle 255 (its core: lit from within there, so it reads as translucent, not frosted stone).
Written to UnityProject.nosync/Assets/AntGarden/Resources/AntGarden/Points/<name>_growth.bytes: "AGG2", n (int32), the
points file's mark (uint32: FNV-1a over every 997th of its bytes), then per point g (uint16, of 65535), t (uint16, of
65535 = one height), s (uint8), e (uint8). The points' order is the points file's: run it again whenever AssetBuilder
makes that file again (the game checks the mark and ignores a map made for other points). With no names: every plant's
points file (not the ants'). Prints, per plant, how many tips it found and how much of it is stem.
"""
import glob
import os
import struct
import sys

import numpy as np
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components, dijkstra
from scipy.spatial import cKDTree

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
from points_preview import ROOT, load  # noqa: E402

NODES = 24000      # the graph's points (a sample of the plant's)
K = 12             # each joined to this many nearest
REACH = 3.0        # but never further than this many typical spacings (no jumping between a spiral's turns)
TIP_REACH = 0.035  # a tip is the farthest point within this (of the plant's height)
# a plant whose surface is bumpy all over, so every bump is a tip at TIP_REACH: its own (the kiln's fire morels, k7: their
# caps' ridges made tips from foot to top, 1464 of them, and their tip markings lay over the caps as frost)
TIP_REACH_OF = {'fire_morel': 0.15}
# and one whose tips are its tops, not its farthest points from the foot (a morel's cap is hollow: along its surface the
# farthest points lay inside and under its caps, its apexes 0.29 of its height from the nearest tip)
TIPS_BY_HEIGHT = {'fire_morel'}
STEM_REACH = 0.02  # a stem: a tube no wider than about this
RIDGE_KINDS = ('crystal_',)   # whose ridges are found (its name starts so)
RIDGE_REACH = 0.008  # a ridge: where the points within this (of the plant's height) stop lying in one plane
RIDGE_OFF = (0.004, 0.03)   # by how much (their spread out of their best plane, of all of it): from none to all
CORE_REACH = 0.025   # a crystal's face's middle: this far from its nearest ridge (of the plant's height), or further


def graph(p, k, reach):
    tree = cKDTree(p)
    d, j = tree.query(p, k + 1)
    d, j = d[:, 1:], j[:, 1:]
    spacing = float(np.median(d[:, 0]))
    keep = d <= reach * spacing
    rows = np.repeat(np.arange(len(p)), k)[keep.ravel()]
    m = coo_matrix((d.ravel()[keep.ravel()], (rows, j.ravel()[keep.ravel()])), shape=(len(p), len(p))).tocsr()
    return m.maximum(m.T), tree, spacing


def growth(pos, nrm, tip_reach=TIP_REACH, by_height=False):
    n = len(pos)
    rng = np.random.default_rng(0)
    pick = np.sort(rng.choice(n, min(n, NODES), replace=False))
    p, nm = pos[pick], nrm[pick]
    m, tree, spacing = graph(p, K, REACH)
    # from the foot: every node within the lowest 3 % of its height
    y = p[:, 1]
    foot = np.where(y <= y.min() + 0.03 * (y.max() - y.min()))[0]
    g = dijkstra(m, indices=foot, min_only=True)
    # parts the graph does not join to the foot (a leaf lying apart, a seed head): each from its own lowest point,
    # starting at that point's straight distance from the nearest reached node and that node's own
    ncomp, comp = connected_components(m, directed=False)
    for c in range(ncomp):
        members = np.where(comp == c)[0]
        if np.isfinite(g[members]).any():
            continue
        reached = np.where(np.isfinite(g))[0]
        low = members[np.argmin(y[members])]
        if len(reached):
            rt = cKDTree(p[reached])
            dd, q = rt.query(p[low])
            base = g[reached[q]] + dd
        else:
            base = 0.0
        sub = dijkstra(m, indices=[low], min_only=True)
        g[members] = base + sub[members]
    gmax = float(np.max(g))
    # growing tips: nodes farther from the foot than every node within TIP_REACH of them (one to a leaf, a leaflet, a
    # branch's end; the noise along a leaf's far edge makes many smaller maxima), not low down
    tt = cKDTree(p)
    near = tt.query_ball_point(p, tip_reach)
    if by_height:
        tips = [i for i, js in enumerate(near) if y[i] >= y[js].max() - 1e-9 and y[i] > y.min() + 0.3 * (y.max() - y.min())]
    else:
        tips = [i for i, js in enumerate(near) if g[i] >= g[js].max() - 1e-9 and g[i] > 0.12 * gmax]
    t = dijkstra(m, indices=tips, min_only=True) if tips else np.full(len(p), np.inf)
    t[~np.isfinite(t)] = 1.0
    # stem: the spread of the normals within STEM_REACH of each node (a tube's go round its axis: two strong
    # directions; a blade's and a body's surface one; a thin stem is all round within that reach)
    dd, nb = tt.query(p, 64, distance_upper_bound=STEM_REACH)
    ok = np.isfinite(dd)
    nn = nm[np.where(ok, nb, 0)] * ok[..., None]          # (m, 64, 3), missing neighbours zero
    cov = np.einsum('mki,mkj->mij', nn, nn) / np.maximum(ok.sum(1), 1)[:, None, None]
    ev = np.linalg.eigvalsh(cov)                          # ascending
    stem = np.clip((ev[:, 1] / np.maximum(ev[:, 2], 1e-6) - 0.25) / 0.45, 0, 1)
    # smoothed over its neighbours twice (alone it flickers between stem and blade along a stem's edge)
    _, sn = tt.query(p, 16)
    for _ in range(2):
        stem = stem[sn].mean(1)
    # every stored point takes its nearest node's (its tip distance a little more, by how far it lies from the node)
    d, q = tree.query(pos)
    return g[q] / max(gmax, 1e-9), np.minimum(t[q] + d, 1.0), stem[q], len(tips), float(stem.mean())


def ridges(pos, nrm):
    # for every stored point (a sample would blur a line this thin): how far the points round it (within RIDGE_REACH)
    # stop lying in one plane, where the surface falls away behind its normal (a ridge or a point, not the fold between
    # two crystals). (By its normals it was found at the points alone: they are smoothed over the mesh's triangles, so
    # across a ridge they turn gradually; the points' places do not.)
    tree = cKDTree(pos)
    out = np.zeros(len(pos))
    for a in range(0, len(pos), 20000):
        b = min(len(pos), a + 20000)
        d, j = tree.query(pos[a:b], 96, distance_upper_bound=RIDGE_REACH)
        ok = np.isfinite(d)
        jj = np.where(ok, j, 0)
        cnt = np.maximum(ok.sum(1), 1)
        mean = (pos[jj] * ok[..., None]).sum(1) / cnt[:, None]
        dev = (pos[jj] - mean[:, None, :]) * ok[..., None]
        ev = np.linalg.eigvalsh(np.einsum('mki,mkj->mij', dev, dev) / cnt[:, None, None])
        off = ev[:, 0] / np.maximum(ev.sum(1), 1e-12)   # the spread out of its best plane, of all its spread
        convex = np.einsum('mi,mi->m', mean - pos[a:b], nrm[a:b]) < 0
        out[a:b] = np.clip((off - RIDGE_OFF[0]) / (RIDGE_OFF[1] - RIDGE_OFF[0]), 0, 1) * convex
    return out


def mark(data):
    h = 2166136261
    for b in data[::997]:
        h = ((h ^ b) * 16777619) & 0xFFFFFFFF
    return h


def main():
    names = sys.argv[1:] or sorted(os.path.basename(f)[:-6] for f in glob.glob(ROOT + '*.bytes')
                                   if not os.path.basename(f).startswith('ant_') and not f.endswith('_growth.bytes'))
    for name in names:
        try:
            pos, nrm, col, ao, area = load(name)
        except AssertionError:
            print(f'{name}: not an AGP4 points file, skipped')
            continue
        g, t, s, tips, stem = growth(pos, nrm, TIP_REACH_OF.get(name, TIP_REACH), name in TIPS_BY_HEIGHT)
        out = bytearray(b'AGG2') + struct.pack('<iI', len(pos), mark(open(ROOT + name + '.bytes', 'rb').read()))
        rec = np.zeros(len(pos), dtype=np.dtype([('g', '<u2'), ('t', '<u2'), ('s', 'u1'), ('e', 'u1')]))
        rec['g'] = np.round(np.clip(g, 0, 1) * 65535)
        rec['t'] = np.round(np.clip(t, 0, 1) * 65535)
        rec['s'] = np.round(np.clip(s, 0, 1) * 255)
        e = ridges(pos, nrm) if name.startswith(RIDGE_KINDS) else None
        if e is not None:
            rec['e'] = np.round(e * 255)
            # its core: how far from the nearest point well on a ridge
            on = pos[e > 0.5]
            if len(on):
                d, _ = cKDTree(on).query(pos, distance_upper_bound=CORE_REACH * 2)
                rec['s'] = np.round(np.clip(np.nan_to_num(d, posinf=CORE_REACH) / CORE_REACH, 0, 1) * 255)
        out += rec.tobytes()
        open(ROOT + name + '_growth.bytes', 'wb').write(out)
        print(f'{name}: {len(pos)} points, {tips} tips, stem {stem:.0%}' + (f', on a ridge {np.mean(e > 0.5):.0%}' if e is not None else ''))


if __name__ == '__main__':
    main()
