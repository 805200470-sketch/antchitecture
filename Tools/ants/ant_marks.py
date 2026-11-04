#!/usr/bin/env python3
"""ant_marks.py [Ant_001 ...]

Each ant's markings, the parts of its shell that glow strongly in the particle nests (ParticleAnts, the ParticleAnt
shader; 11-04, the user's taste for every nest's living things: the body dark and quiet in the world's colour, a
species' markings glowing strongly). They are its own texture's pattern, not drawn anew: the thin light lines and
dots it already has, the rims of its gaster's segments, the rings at its legs' and antennae's joints, the seams of
its thorax, which follow the body's form as markings must (the user, on a plant's: "要顺着…生长逻辑…贴合它的形体").
Found as the texture's lightness over the shell, a little smoothed (over SMALL of the ant's length: no single grains),
less its lightness over a wider patch round it (LARGE): what is lighter than its surroundings by the most, from the
LO-th to the HI-th hundredth of the ant's shell, marks it (a soft edge between), about a twentieth of its points.
Light patches as wide as LARGE (a whole pale thorax) are left out: they are its colour, not a marking.
And its pits (11-04, the user: "这个蚂蚁原来上面是有很多像小洞的，可以用这个东西也让它有一些颜色"): the small holes all
over its shell, the texture darker than round it by the most (PIT_LO-th to PIT_HI-th hundredth from the dark end, over a
smaller patch, PIT_SMALL and PIT_LARGE), weighted toward its markings (a third of their strength far from any, all of it
beside one): they take the nest's light faintly, so its colour is in the shell all over, not in its bands alone.
Written to UnityProject.nosync/Assets/AntGarden/Resources/AntGarden/Points/ant_<id>_marks.bytes: "AGM2", n (int32), the
points file's mark (uint32: FNV-1a over every 997th of its bytes, as growth_map.py's), then per point two bytes, how
much it is a marking and how much a pit (0..255; eyes and wings 0). The points' order is the points file's: run it again whenever
AssetBuilder makes the ants' points again (unity_build.sh ants; the game checks the mark and leaves out markings made
for other points). With no ids: every ant_*.bytes. Prints, per ant, how much of its shell is marked.
"""
import glob
import os
import struct
import sys

# (this folder's inspect.py, for Blender, would stand in for the standard library's when numpy imports it)
sys.path = [p for p in sys.path if os.path.abspath(p or '.') != os.path.dirname(os.path.abspath(__file__))]
import numpy as np  # noqa: E402
from scipy import sparse  # noqa: E402
from scipy.spatial import cKDTree  # noqa: E402

ROOT = '/Users/xujiafeng/Desktop/AntGarden/UnityProject.nosync/Assets/AntGarden/Resources/AntGarden/Points/'
SMALL = 0.004    # of the ant's length: its lightness smoothed over this (a grain of the texture is no marking)
LARGE = 0.03     # and compared with its lightness over this
LO, HI = 88, 97  # from this hundredth of its shell (none) to this (all), by how much lighter it is than round it
SAMPLE = 40000   # points the wider lightness is taken from
PIT_SMALL, PIT_LARGE = 0.003, 0.02   # a pit: its lightness over this, against this round it (of the ant's length)
PIT_LO, PIT_HI = 12, 3   # from this hundredth from the dark end (none) to this (all)
PIT_NEAR = 0.05  # how far from a marking a pit still takes most of its light (of the ant's length)


def read7(b, o):
    n = s = 0
    while True:
        c = b[o]; o += 1; n |= (c & 0x7F) << s; s += 7
        if c < 0x80: return n, o


def load(path):
    """An ant's points (AGA3): positions in bind space, the texture's colours, what each point is (0 shell, 1 eye, 2 wing)."""
    b = open(path, 'rb').read()
    magic, ns = struct.unpack_from('<ii', b, 0)
    assert magic == 0x33414741, hex(magic)
    o = 8
    for _ in range(ns):
        ln, o = read7(b, o)
        o += ln + 64
    n, = struct.unpack_from('<i', b, o)
    o += 8   # (and the surface's area)
    rec = np.frombuffer(b, dtype=np.dtype([('p', '<f4', 3), ('n', '<f4', 3), ('c', 'u1', 4), ('s', '<u2', 2), ('w', '<f4')]), count=n, offset=o)
    return rec['p'].astype(np.float64), rec['c'][:, :3].astype(np.float64) / 255.0, rec['c'][:, 3], b


def mark(data):
    h = 2166136261
    for c in data[::997]:
        h = ((h ^ c) * 16777619) & 0xFFFFFFFF
    return h


def lightness(col):
    lin = np.where(col <= 0.04045, col / 12.92, ((col + 0.055) / 1.055) ** 2.4)
    return lin @ np.array([0.3, 0.55, 0.15])


def lift(p, lum, tree, length, small_r, large_r):
    """How much lighter each point's patch (small_r across) is than round it (large_r): below 0, darker."""
    near = tree.sparse_distance_matrix(tree, small_r * length, output_type='coo_matrix')
    w = sparse.coo_matrix((np.ones_like(near.data), (near.row, near.col)), shape=near.shape).tocsr() + sparse.identity(len(p), format='csr')
    small = (w @ lum) / np.asarray(w.sum(1)).ravel()
    pick = np.random.default_rng(1).choice(len(p), min(len(p), SAMPLE), replace=False)
    wide = cKDTree(p[pick]).query_ball_point(p, large_r * length, workers=-1)
    large = np.array([lum[pick][j].mean() if len(j) else l for j, l in zip(wide, small)])
    return small - large


def ramp(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def marks(pos, col, kind):
    """How much each point is a marking, and how much a pit (both 0..1; eyes and wings 0)."""
    shell = np.where(kind == 0)[0]
    p, lum = pos[shell], lightness(col[shell])
    length = float((pos.max(0) - pos.min(0)).max())
    tree = cKDTree(p)
    up = lift(p, lum, tree, length, SMALL, LARGE)
    lo, hi = np.percentile(up, [LO, HI])
    m = ramp(up, lo, max(hi, lo + 1e-6))
    down = lift(p, lum, tree, length, PIT_SMALL, PIT_LARGE)
    plo, phi = np.percentile(down, [PIT_LO, PIT_HI])
    pit = ramp(-down, -plo, max(-phi, -plo + 1e-6))
    marked = p[m > 0.5]
    if len(marked):
        d, _ = cKDTree(marked).query(p, workers=-1)
        pit *= 0.35 + 0.65 * np.exp(-d / (PIT_NEAR * length))
    out_m, out_p = np.zeros(len(pos)), np.zeros(len(pos))
    out_m[shell], out_p[shell] = m, pit * (1 - m)
    return out_m, out_p


def main():
    ids = sys.argv[1:] or sorted(os.path.basename(f)[4:-6] for f in glob.glob(ROOT + 'ant_*.bytes') if not f.endswith('_marks.bytes'))
    for id in ids:
        path = ROOT + 'ant_' + id + '.bytes'
        pos, col, kind, data = load(path)
        m, pit = marks(pos, col, kind)
        both = np.stack([np.round(m * 255), np.round(pit * 255)], 1).astype(np.uint8)
        out = bytearray(b'AGM2') + struct.pack('<iI', len(pos), mark(data)) + both.tobytes()
        open(ROOT + 'ant_' + id + '_marks.bytes', 'wb').write(out)
        shell = kind == 0
        print(f'{id}: {len(pos)} points, shell {shell.mean():.0%}, marked (over half) {(m[shell] > 0.5).mean():.1%}, pits (over a third) {(pit[shell] > 0.33).mean():.1%}')


if __name__ == '__main__':
    main()
