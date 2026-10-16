#!/usr/bin/env python3
"""Convert a textured GLB model (e.g. from Tripo) into a Gaussian-splat asset (.ply).

Points are sampled uniformly over the mesh surface (area-weighted), coloured
from the base-colour texture (or vertex colours), and each becomes a flat
Gaussian lying on the surface — so generated 3D models share the particle look
of the rest of the scene. The bottom slice also gives an exact XZ collision
footprint. Pure numpy + Pillow: parses the GLB directly.

Usage: python3 tools/mesh_to_splat.py concept/3d/fan_tree.glb assets/splats/fan_tree_3d.ply --height 12 [--splats 150000]
"""
import argparse
import io
import json
import struct
from pathlib import Path

import numpy as np
from PIL import Image

from image_to_splat import quat_from_z, write_ply

COMP = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
NCOMP = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


def read_glb(path):
    data = Path(path).read_bytes()
    magic, version, _ = struct.unpack_from('<III', data, 0)
    if magic != 0x46546C67:
        raise SystemExit(f'{path}: not a GLB file')
    off = 12
    gltf, binchunk = None, b''
    while off < len(data):
        length, ctype = struct.unpack_from('<II', data, off)
        chunk = data[off + 8:off + 8 + length]
        if ctype == 0x4E4F534A:
            gltf = json.loads(chunk)
        elif ctype == 0x004E4942:
            binchunk = chunk
        off += 8 + length
    return gltf, binchunk


def accessor(gltf, binchunk, idx):
    acc = gltf['accessors'][idx]
    view = gltf['bufferViews'][acc['bufferView']]
    dtype = np.dtype(COMP[acc['componentType']])
    n = NCOMP[acc['type']]
    start = view.get('byteOffset', 0) + acc.get('byteOffset', 0)
    stride = view.get('byteStride', 0) or dtype.itemsize * n
    count = acc['count']
    raw = np.frombuffer(binchunk, dtype=np.uint8, count=stride * (count - 1) + dtype.itemsize * n, offset=start)
    arr = np.lib.stride_tricks.as_strided(raw, shape=(count, dtype.itemsize * n), strides=(stride, 1)).copy()
    out = arr.view(dtype).reshape(count, n).astype(np.float32)
    if acc.get('normalized'):
        out /= np.iinfo(dtype).max
    return out


def node_matrix(node):
    if 'matrix' in node:
        return np.array(node['matrix'], np.float64).reshape(4, 4).T
    t = np.array(node.get('translation', [0, 0, 0]), np.float64)
    x, y, z, w = node.get('rotation', [0, 0, 0, 1])
    s = np.array(node.get('scale', [1, 1, 1]), np.float64)
    r = np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ])
    m = np.eye(4)
    m[:3, :3] = r * s
    m[:3, 3] = t
    return m


def texture_image(gltf, binchunk, tex_index):
    src = gltf['textures'][tex_index]['source']
    img = gltf['images'][src]
    view = gltf['bufferViews'][img['bufferView']]
    start = view.get('byteOffset', 0)
    blob = binchunk[start:start + view['byteLength']]
    return np.asarray(Image.open(io.BytesIO(blob)).convert('RGB')).astype(np.float32) / 255.0


def collect_triangles(gltf, binchunk):
    """World-space triangles with per-corner normals, uvs and a colour sampler per primitive."""
    out = []

    def visit(ni, parent):
        node = gltf['nodes'][ni]
        m = parent @ node_matrix(node)
        if 'mesh' in node:
            nm = np.linalg.inv(m[:3, :3]).T
            for prim in gltf['meshes'][node['mesh']]['primitives']:
                attrs = prim['attributes']
                pos = accessor(gltf, binchunk, attrs['POSITION'])
                pos = (pos @ m[:3, :3].T + m[:3, 3]).astype(np.float32)
                nrm = accessor(gltf, binchunk, attrs['NORMAL']) @ nm.T if 'NORMAL' in attrs else None
                uv = accessor(gltf, binchunk, attrs['TEXCOORD_0']) if 'TEXCOORD_0' in attrs else None
                vcol = accessor(gltf, binchunk, attrs['COLOR_0'])[:, :3] if 'COLOR_0' in attrs else None
                idx = accessor(gltf, binchunk, prim['indices']).astype(np.int64).ravel() if 'indices' in prim else np.arange(len(pos))
                mat = gltf.get('materials', [{}])[prim.get('material', 0)] if gltf.get('materials') else {}
                pbr = mat.get('pbrMetallicRoughness', {})
                factor = np.array(pbr.get('baseColorFactor', [1, 1, 1, 1])[:3], np.float32)
                tex = texture_image(gltf, binchunk, pbr['baseColorTexture']['index']) if 'baseColorTexture' in pbr else None
                out.append(dict(pos=pos, nrm=nrm, uv=uv, vcol=vcol, tri=idx.reshape(-1, 3), tex=tex, factor=factor))
        for c in node.get('children', []):
            visit(c, m)

    scene = gltf['scenes'][gltf.get('scene', 0)]
    for ni in scene['nodes']:
        visit(ni, np.eye(4))
    return out


def convert(src, dst, height, n_splats, seed=3):
    gltf, binchunk = read_glb(src)
    prims = collect_triangles(gltf, binchunk)
    rng = np.random.default_rng(seed)
    areas = []
    for p in prims:
        a, b, c = (p['pos'][p['tri'][:, k]] for k in range(3))
        p['area'] = 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1)
        areas.append(p['area'].sum())
    total = float(sum(areas))
    xyz, nrm, col = [], [], []
    for p, area in zip(prims, areas):
        k = max(1, int(round(n_splats * area / total)))
        t = rng.choice(len(p['tri']), size=k, p=p['area'] / p['area'].sum())
        r1 = np.sqrt(rng.random(k))[:, None]
        r2 = rng.random(k)[:, None]
        w = np.hstack([1 - r1, r1 * (1 - r2), r1 * r2])  # barycentric
        tri = p['tri'][t]
        bary = lambda arr: (arr[tri[:, 0]] * w[:, :1] + arr[tri[:, 1]] * w[:, 1:2] + arr[tri[:, 2]] * w[:, 2:3])
        pts = bary(p['pos'])
        if p['nrm'] is not None:
            n = bary(p['nrm'])
        else:
            a, b, c = (p['pos'][tri[:, j]] for j in range(3))
            n = np.cross(b - a, c - a)
        n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-9
        if p['tex'] is not None and p['uv'] is not None:
            uv = bary(p['uv']) % 1.0
            h, wd = p['tex'].shape[:2]
            c = p['tex'][(uv[:, 1] * (h - 1)).astype(int), (uv[:, 0] * (wd - 1)).astype(int)]
        elif p['vcol'] is not None:
            c = bary(p['vcol'])
        else:
            c = np.full((k, 3), 0.6, np.float32)
        xyz.append(pts)
        nrm.append(n)
        col.append(np.clip(c * p['factor'], 0, 1))
    xyz = np.concatenate(xyz).astype(np.float32)
    nrm = np.concatenate(nrm).astype(np.float32)
    col = np.concatenate(col).astype(np.float32)
    # normalise: base at y = 0, centred in x/z, scaled to the requested height
    lo, hi = xyz.min(0), xyz.max(0)
    s = height / max(1e-6, hi[1] - lo[1])
    xyz = (xyz - np.array([(lo[0] + hi[0]) / 2, lo[1], (lo[2] + hi[2]) / 2], np.float32)) * s
    spacing = np.sqrt(total * s * s / len(xyz))
    nd = np.where((nrm[:, 2] < 0)[:, None], -nrm, nrm)  # a flat Gaussian is the same either way up
    scale = np.tile(np.array([0.62 * spacing, 0.62 * spacing, 0.16 * spacing], np.float32), (len(xyz), 1))
    write_ply(dst, xyz, nd, col, np.full(len(xyz), 0.98, np.float32), scale, quat_from_z(nd))
    # collision footprint: occupied XZ cells in the bottom slice
    band = xyz[xyz[:, 1] < min(1.3, 0.35 * height)]
    cell = 0.5
    foot = []
    if len(band):
        cells = np.unique(np.floor(band[:, [0, 2]] / cell).astype(int), axis=0)
        foot = [[round((cx + 0.5) * cell, 3), round((cz + 0.5) * cell, 3), round(cell * 0.75, 3)] for cx, cz in cells]
    size = (xyz.max(0) - xyz.min(0)).tolist()
    meta = {'source': str(src), 'splats': int(len(xyz)), 'height': height, 'size': [round(v, 3) for v in size],
            'footprint3d': foot, 'footRadius': float(max(size[0], size[2]) / 2), 'mode': 'mesh'}
    Path(dst).with_suffix('.json').write_text(json.dumps(meta, indent=2))
    return meta


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('src')
    ap.add_argument('dst')
    ap.add_argument('--height', type=float, required=True)
    ap.add_argument('--splats', type=int, default=150000)
    a = ap.parse_args()
    m = convert(a.src, a.dst, a.height, a.splats)
    print(json.dumps({k: m[k] for k in ('splats', 'size', 'height')}), 'footprint cells:', len(m['footprint3d']))
