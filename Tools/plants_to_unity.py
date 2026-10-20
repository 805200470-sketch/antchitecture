#!/usr/bin/env python3
"""plants_to_unity.py - turn Tripo-generated plant GLBs into Unity-ready FBX + LODs + textures.

One file, two runtimes:
  * system python3 (needs numpy + Pillow): orchestration, front detection, texture transfer,
    preview composition, plants.json. Entry point: `python3 plants_to_unity.py run <glb...>`
    (plants_to_unity.sh is a thin wrapper around exactly that).
  * Blender 5.x in background mode: geometry. Started by `run` as
      Blender -b --factory-startup -P plants_to_unity.py -- build  ...   (one per GLB)
      Blender -b --factory-startup -P plants_to_unity.py -- verify ...   (fresh session per FBX)

Per model <name> (GLB file name minus a trailing _p1/_p2... suffix) it writes to <out>/<name>/:
  <name>.fbx            root "<name>" with children <name>_LOD0 (original), _LOD1 (~30%), _LOD2 (~8%)
  <name>_BaseColor.png  sRGB, <name>_Normal.png (OpenGL, green up), <name>_ORM.png (linear: R=AO G=rough B=metal)
and updates <out>/plants.json. Preview renders go to <glb folder>/<name>_lods_preview.png.

Usage:
  Tools/plants_to_unity.sh 03_Models/Plants/*_p1.glb            # all defaults (paths below)
  Tools/plants_to_unity.sh new_plant_p1.glb --facing -Z --jobs 2
  Per-plant settings (glow-part rules, forced front axis) live in Tools/plants_to_unity.json.
  Re-running for a subset updates only those entries in plants.json. Intermediate files go to
  $TMPDIR/plants_to_unity_work (logs, per-plant entry/verify JSON, preview tiles).
  Defaults: --out <AntGarden>/UnityProject.nosync/Assets/AntGarden/Art/Plants,
            previews next to each GLB, --ratios 0.30,0.08, --tex 2048, --facing +Z.

Pipeline (why each step exists):
  1. Weld the GLB's seam-split vertices (Tripo stores every UV seam as a cut; decimating that rips holes).
  2. Find the model's front by matching silhouettes + colours against the Tripo source image listed in
     tripo_log.jsonl (Tripo P1 puts the input view on glTF +X, not +Z). Rotate it to face Unity +Z.
  3. Normalise: height exactly 1, pivot = x/z centre of the lowest 5 % slice, y = lowest point.
  4. Re-atlas. Tripo atlases put ~50 % of all vertices on UV-island corners, so no decimator can reach
     30 % / 8 % without dragging texture across islands. New charts come from a smoothed copy of the mesh,
     are flattened with SLIM (minimum stretch), packed, and the Color / ORM / Normal maps are re-sampled
     into the new layout (normal map re-expressed in the new MikkTSpace tangent frames).
  5. LODs with a seam-aware quadric decimator (below): the merged vertex goes to the quadric optimum and
     takes the UV of that point on the same island, so textures do not smear; ground-contact and top
     vertices are pinned (base stays on y=0, height stays 1). Triangle budget is split between the mesh's
     connected parts so that every part ends with about the same silhouette + surface error (otherwise a
     dense moss base eats the budget and a thin curl collapses). Faces buried inside other parts are left
     out of LOD1/LOD2. LOD normals are transferred from LOD0.
  6. Export FBX (Y up, -Z forward axis setting, data pre-rotated, FBX All scaling => Unity scale 1, no
     textures embedded or referenced), then verify in a fresh Blender session and render previews.
"""
import argparse
import heapq
import io
import json
import math
import os
import re
import struct
import subprocess
import sys
import time

try:
    import bpy  # noqa: F401
    IN_BLENDER = True
except ImportError:
    IN_BLENDER = False

import numpy as np

SCRIPT = os.path.abspath(__file__)
TOOLS_DIR = os.path.dirname(SCRIPT)
ROOT_DIR = os.path.dirname(TOOLS_DIR)  # .../AntGarden
DEFAULT_OUT = os.path.join(ROOT_DIR, 'UnityProject.nosync', 'Assets', 'AntGarden', 'Art', 'Plants')
DEFAULT_BLENDER = '/Applications/Blender.app/Contents/MacOS/Blender'
CONFIG_PATH = os.path.join(TOOLS_DIR, 'plants_to_unity.json')

SLICE_PIVOT = 0.05      # pivot: x/z centre of the lowest 5 % of the height
SLICE_FOOT = 0.15       # collision footprint: bottom 15 % of the height
PIN_BAND = 0.002        # vertices this close to the bottom / top are pinned during decimation


def log(*a):
    print('[plants_to_unity]', *a, flush=True)


def plant_name(glb):
    base = os.path.splitext(os.path.basename(glb))[0]
    return re.sub(r'_(p\d+|v\d+)$', '', base)   # _p1 (low-poly line), _v31 (high-precision line)


def load_config():
    if os.path.exists(CONFIG_PATH):
        with open(CONFIG_PATH) as f:
            return json.load(f)
    return {}


def plant_config(name):
    return load_config().get('plants', {}).get(name, {})


# ============================================================================ GLB reading (numpy only)
_COMP = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
_NCOMP = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


def read_glb(path):
    with open(path, 'rb') as f:
        data = f.read()
    if struct.unpack_from('<I', data, 0)[0] != 0x46546C67:
        raise SystemExit(f'{path}: not a GLB file')
    off, gltf, binc = 12, None, b''
    while off < len(data):
        ln, ct = struct.unpack_from('<II', data, off)
        chunk = data[off + 8:off + 8 + ln]
        if ct == 0x4E4F534A:
            gltf = json.loads(chunk)
        elif ct == 0x004E4942:
            binc = chunk
        off += 8 + ln
    return gltf, binc


def glb_accessor(g, b, idx):
    a = g['accessors'][idx]
    v = g['bufferViews'][a['bufferView']]
    dt = np.dtype(_COMP[a['componentType']])
    n = _NCOMP[a['type']]
    start = v.get('byteOffset', 0) + a.get('byteOffset', 0)
    stride = v.get('byteStride', 0) or dt.itemsize * n
    cnt = a['count']
    raw = np.frombuffer(b, np.uint8, count=stride * (cnt - 1) + dt.itemsize * n, offset=start)
    arr = np.lib.stride_tricks.as_strided(raw, shape=(cnt, dt.itemsize * n), strides=(stride, 1)).copy()
    out = arr.view(dt).reshape(cnt, n).astype(np.float64)
    if a.get('normalized'):
        out /= np.iinfo(dt).max
    return out


def _node_matrix(node):
    if 'matrix' in node:
        return np.array(node['matrix'], np.float64).reshape(4, 4).T
    t = np.array(node.get('translation', [0, 0, 0]), np.float64)
    x, y, z, w = node.get('rotation', [0, 0, 0, 1])
    s = np.array(node.get('scale', [1, 1, 1]), np.float64)
    r = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                  [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                  [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
    m = np.eye(4); m[:3, :3] = r * s; m[:3, 3] = t
    return m


def glb_geometry(path):
    """World-space triangles of the GLB (glTF axes, Y up): P (n,3), UV (n,2), I (m,3)."""
    g, b = read_glb(path)
    Ps, UVs, Is, off = [], [], [], 0

    def walk(ni, parent):
        nonlocal off
        node = g['nodes'][ni]
        m = parent @ _node_matrix(node)
        if 'mesh' in node:
            for prim in g['meshes'][node['mesh']]['primitives']:
                if prim.get('mode', 4) != 4:
                    continue
                P = glb_accessor(g, b, prim['attributes']['POSITION'])
                P = P @ m[:3, :3].T + m[:3, 3]
                uv = (glb_accessor(g, b, prim['attributes']['TEXCOORD_0']) if 'TEXCOORD_0' in prim['attributes']
                      else np.zeros((len(P), 2)))
                I = (glb_accessor(g, b, prim['indices']).astype(np.int64).reshape(-1, 3) if 'indices' in prim
                     else np.arange(len(P)).reshape(-1, 3))
                Ps.append(P); UVs.append(uv); Is.append(I + off); off += len(P)
        for c in node.get('children', []):
            walk(c, m)
    scene = g['scenes'][g.get('scene', 0)]
    for ni in scene['nodes']:
        walk(ni, np.eye(4))
    return np.concatenate(Ps), np.concatenate(UVs), np.concatenate(Is)


def glb_textures(path):
    """{'color'|'orm'|'normal': PIL RGB image} from the first material that has textures."""
    from PIL import Image
    g, b = read_glb(path)
    out = {}
    for mat in g.get('materials', []):
        pbr = mat.get('pbrMetallicRoughness', {})
        slots = {'color': pbr.get('baseColorTexture'), 'orm': pbr.get('metallicRoughnessTexture'),
                 'normal': mat.get('normalTexture')}
        for k, s in slots.items():
            if not s or k in out:
                continue
            im = g['images'][g['textures'][s['index']]['source']]
            if 'bufferView' in im:
                v = g['bufferViews'][im['bufferView']]
                st = v.get('byteOffset', 0)
                out[k] = Image.open(io.BytesIO(b[st:st + v['byteLength']])).convert('RGB')
            elif 'uri' in im and not im['uri'].startswith('data:'):
                out[k] = Image.open(os.path.join(os.path.dirname(path), im['uri'])).convert('RGB')
        if out:
            break
    return out


# ============================================================================ front detection (system python)
# camera on +axis looking back at the origin; screen right vector, in glTF axes (Y up)
_FRONT_VIEWS = {'+X': (np.array([0, 0, -1.0]), np.array([1.0, 0, 0])),
                '-X': (np.array([0, 0, 1.0]), np.array([-1.0, 0, 0])),
                '+Z': (np.array([1.0, 0, 0]), np.array([0, 0, 1.0])),
                '-Z': (np.array([-1.0, 0, 0]), np.array([0, 0, -1.0]))}


def tripo_source_image(glb):
    """Source image recorded by tools/tripo.mjs in tripo_log.jsonl next to the GLB (or None)."""
    logf = os.path.join(os.path.dirname(glb), 'tripo_log.jsonl')
    if not os.path.exists(logf):
        return None, None
    found = (None, None)
    with open(logf) as f:
        for line in f:
            try:
                e = json.loads(line)
            except ValueError:
                continue
            if os.path.basename(e.get('out', '')) == os.path.basename(glb):
                found = (e.get('image'), e.get('task'))
    img = found[0]
    if img and not os.path.exists(img):
        alt = os.path.join(ROOT_DIR, '02_Concepts', 'Fronts_for_3D', os.path.basename(img))
        img = alt if os.path.exists(alt) else None
    return img, found[1]


def detect_front(glb, image, res=160):
    """Compare the four horizontal views of the textured mesh with the source image.
    Score = silhouette IoU + 0.5 * luminance correlation; returns (axis, scores)."""
    from PIL import Image, ImageFilter
    P, UV, I = glb_geometry(glb)
    tex = glb_textures(glb).get('color')
    texa = np.asarray(tex.resize((512, 512)), np.float64) / 255.0 if tex is not None else None
    im = np.asarray(Image.open(image).convert('RGB'), np.float64)
    border = np.concatenate([im[:4].reshape(-1, 3), im[-4:].reshape(-1, 3), im[:, :4].reshape(-1, 3), im[:, -4:].reshape(-1, 3)])
    bg = np.median(border, axis=0)
    m = np.linalg.norm(im - bg, axis=2) > 30
    m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MedianFilter(5))) > 127

    def norm(mask, col):
        ys, xs = np.nonzero(mask)
        if len(ys) == 0:
            return np.zeros((res, 3 * res), bool), np.zeros((res, 3 * res, 3))
        y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        w = max(1, int(round((x1 - x0) * res / (y1 - y0))))
        w = min(w, 3 * res)
        mi = Image.fromarray((mask[y0:y1, x0:x1] * 255).astype(np.uint8)).resize((w, res), Image.Resampling.BILINEAR)
        ci = Image.fromarray((np.clip(col[y0:y1, x0:x1], 0, 1) * 255).astype(np.uint8)).resize((w, res), Image.Resampling.BILINEAR)
        cm = np.zeros((res, 3 * res), bool); cc = np.zeros((res, 3 * res, 3))
        o = (3 * res - w) // 2
        cm[:, o:o + w] = np.asarray(mi) > 127
        cc[:, o:o + w] = np.asarray(ci) / 255.0
        return cm, cc
    sm, sc = norm(m, im / 255.0)
    rng = np.random.default_rng(0)
    v0, v1, v2 = P[I[:, 0]], P[I[:, 1]], P[I[:, 2]]
    area = np.linalg.norm(np.cross(v1 - v0, v2 - v0), axis=1) / 2
    n = 300000
    f = rng.choice(len(I), size=n, p=area / area.sum())
    r1 = np.sqrt(rng.random(n)); r2 = rng.random(n)
    wa, wb, wc = 1 - r1, r1 * (1 - r2), r1 * r2
    pts = wa[:, None] * v0[f] + wb[:, None] * v1[f] + wc[:, None] * v2[f]
    if texa is not None:
        uv = wa[:, None] * UV[I[f, 0]] + wb[:, None] * UV[I[f, 1]] + wc[:, None] * UV[I[f, 2]]
        col = texa[np.clip((uv[:, 1] % 1.0 * 512).astype(int), 0, 511), np.clip((uv[:, 0] % 1.0 * 512).astype(int), 0, 511)]
    else:
        col = np.full((n, 3), 0.5)
    scores = {}
    G = 400
    for axis, (right, toward) in _FRONT_VIEWS.items():
        u = pts @ right; v = pts[:, 1]; d = pts @ toward
        lo = np.array([u.min(), v.min()]); span = max(np.ptp(u), np.ptp(v))
        iu = np.clip(((u - lo[0]) / span * (G - 1)).astype(int), 0, G - 1)
        iv = np.clip(((v.max() - v) / span * (G - 1)).astype(int), 0, G - 1)
        pix = iv * G + iu
        order = np.lexsort((-d, pix))
        ps = pix[order]; first = np.r_[True, ps[1:] != ps[:-1]]
        img = np.zeros((G * G, 3)); mm = np.zeros(G * G, bool)
        img[ps[first]] = col[order][first]; mm[ps[first]] = True
        img = img.reshape(G, G, 3); mm = mm.reshape(G, G)
        mm = np.asarray(Image.fromarray((mm * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.MinFilter(3))) > 127
        rm, rc = norm(mm, img)
        inter = sm & rm
        iou = inter.sum() / max(1, (sm | rm).sum())
        corr = float(np.corrcoef(sc[inter].mean(1), rc[inter].mean(1))[0, 1]) if inter.sum() > 10 else 0.0
        scores[axis] = round(float(iou + 0.5 * corr), 4)
    best = max(scores, key=scores.get)
    return best, scores


# ============================================================================ texture transfer (system python)
def _bilinear(img, u, v):
    """img HxWxC (row 0 = top); u, v in Blender UV convention (v up); repeat wrap."""
    H, W = img.shape[:2]
    x = u * W - 0.5
    y = (1.0 - v) * H - 0.5
    x0 = np.floor(x).astype(np.int64); y0 = np.floor(y).astype(np.int64)
    fx = (x - x0)[:, None].astype(np.float32); fy = (y - y0)[:, None].astype(np.float32)
    x0m = np.mod(x0, W); x1m = np.mod(x0 + 1, W); y0m = np.mod(y0, H); y1m = np.mod(y0 + 1, H)
    return ((img[y0m, x0m] * (1 - fx) + img[y0m, x1m] * fx) * (1 - fy)
            + (img[y1m, x0m] * (1 - fx) + img[y1m, x1m] * fx) * fy)


def _raster_uv(uv_new, R, chunk=4000):
    """Pixel centres covered by each UV triangle (half-pixel conservative); yields
    (tri, px, py, barycentrics, strictly_inside)."""
    T = len(uv_new)
    P = uv_new * R
    mn = np.clip(np.floor(P.min(1) - 0.5).astype(np.int64), 0, R - 1)
    mx = np.clip(np.ceil(P.max(1) + 0.5).astype(np.int64), 0, R - 1)
    w = mx[:, 0] - mn[:, 0] + 1; h = mx[:, 1] - mn[:, 1] + 1
    for s in range(0, T, chunk):
        idx = np.arange(s, min(T, s + chunk))
        cnt = w[idx] * h[idx]
        tri = np.repeat(idx, cnt)
        k = np.arange(cnt.sum()) - np.repeat(np.cumsum(cnt) - cnt, cnt)
        px = mn[tri, 0] + k % w[tri]; py = mn[tri, 1] + k // w[tri]
        cx = px + 0.5; cy = py + 0.5
        a = P[tri, 0]; b = P[tri, 1]; c = P[tri, 2]
        v0 = b - a; v1 = c - a
        d = v0[:, 0] * v1[:, 1] - v0[:, 1] * v1[:, 0]
        ok = np.abs(d) > 1e-12
        dd = np.where(ok, d, 1.0)
        qx = cx - a[:, 0]; qy = cy - a[:, 1]
        l1 = (qx * v1[:, 1] - qy * v1[:, 0]) / dd
        l2 = (v0[:, 0] * qy - v0[:, 1] * qx) / dd
        l0 = 1 - l1 - l2
        e = np.maximum(np.maximum(np.hypot(*(c - b).T), np.hypot(*(c - a).T)), np.hypot(*(b - a).T))
        tol = 0.5 * e / np.maximum(np.abs(dd), 1e-12)
        inside = ok & (l0 >= -tol) & (l1 >= -tol) & (l2 >= -tol)
        strict = (l0 >= 0) & (l1 >= 0) & (l2 >= 0)
        bc = np.clip(np.stack([l0, l1, l2], 1)[inside], -0.5, 1.5)
        yield tri[inside], px[inside], py[inside], bc, strict[inside]


def _push_pull(img, mask):
    """Fill uncovered texels with a smooth extension of the covered ones (gutter padding)."""
    levels = [(img * mask[..., None], mask.astype(np.float32))]
    while min(levels[-1][1].shape) > 1 and levels[-1][1].shape[0] % 2 == 0 and levels[-1][1].shape[1] % 2 == 0:
        c, m = levels[-1]
        H, W = m.shape
        levels.append((c.reshape(H // 2, 2, W // 2, 2, -1).sum((1, 3)), m.reshape(H // 2, 2, W // 2, 2).sum((1, 3))))
    norm = [(c / np.maximum(m, 1e-8)[..., None], m > 0) for c, m in levels]
    filled = norm[-1][0]
    for c, m in reversed(norm[:-1]):
        filled = np.where(m[..., None], c, np.repeat(np.repeat(filled, 2, 0), 2, 1))
    return np.where(mask[..., None], img, filled)


def _srgb_to_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def _lin_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def transfer_textures(npz, glb, out_dir, name, size=2048):
    """Re-sample the GLB's Color / ORM / Normal maps from the Tripo UVs into the new atlas.
    Works at 2x the output size, pads gutters, box-filters down. The tangent-space normal map is
    decoded with the old MikkTSpace frame and re-encoded in the new one (OpenGL convention kept)."""
    from PIL import Image
    t0 = time.time()
    d = np.load(npz)
    uv_old = d['uv_old'].astype(np.float64)
    uv_new = d['uv_new'].astype(np.float64)
    nrm = d['normal'].astype(np.float32)
    t_old = d['tan_old'].astype(np.float32)
    t_new = d['tan_new'].astype(np.float32)
    src = {k: np.asarray(v, np.float32) / 255.0 for k, v in glb_textures(glb).items()}
    if not src:
        log(f'{name}: the GLB has no textures, skipping texture transfer')
        return {}
    R = 2 * size
    acc = {k: np.zeros((R, R, 3), np.float32) for k in src}
    best = np.full((R, R), -1, np.int8)
    for tri, px, py, bc, strict in _raster_uv(uv_new, R):
        pr = strict.astype(np.int8)
        row = R - 1 - py
        take = pr >= best[row, px]
        tri, px, row, bc, pr = tri[take], px[take], row[take], bc[take], pr[take]
        best[row, px] = pr
        uo = np.einsum('ni,nij->nj', bc, uv_old[tri])
        for k, img in src.items():
            val = _bilinear(img, uo[:, 0], uo[:, 1])
            if k == 'normal':
                n = val * 2 - 1
                N = np.einsum('ni,nij->nj', bc, nrm[tri])
                To = np.einsum('ni,nij->nj', bc, t_old[tri][:, :, :3])
                so = np.where(np.einsum('ni,ni->n', bc, t_old[tri][:, :, 3]) < 0, -1.0, 1.0)[:, None]
                Bo = so * np.cross(N, To)
                w = To * n[:, :1] + Bo * n[:, 1:2] + N * n[:, 2:3]
                Tn = np.einsum('ni,nij->nj', bc, t_new[tri][:, :, :3])
                sn = np.where(np.einsum('ni,ni->n', bc, t_new[tri][:, :, 3]) < 0, -1.0, 1.0)[:, None]
                Bn = sn * np.cross(N, Tn)
                M = np.stack([Tn, Bn, N], axis=2)
                det = np.linalg.det(M)
                good = np.abs(det) > 1e-6
                nt = np.zeros_like(w)
                if good.any():
                    nt[good] = np.linalg.solve(M[good], w[good][:, :, None])[:, :, 0]
                nt[~good] = np.stack([np.einsum('ij,ij->i', w[~good], Tn[~good]), np.einsum('ij,ij->i', w[~good], Bn[~good]),
                                      np.einsum('ij,ij->i', w[~good], N[~good])], 1)
                nt[:, 2] = np.maximum(nt[:, 2], 0.05)
                nt /= np.linalg.norm(nt, axis=1, keepdims=True)
                val = nt * 0.5 + 0.5
            acc[k][row, px] = val
    mask = best >= 0
    files = {}
    names = {'color': 'BaseColor', 'normal': 'Normal', 'orm': 'ORM'}
    for k, img in acc.items():
        if k == 'color':
            img = _srgb_to_lin(img)
        img = _push_pull(img, mask)
        img = img.reshape(size, 2, size, 2, 3).mean((1, 3))
        if k == 'color':
            img = _lin_to_srgb(img)
        if k == 'normal':
            n = img * 2 - 1
            n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-6)
            img = n * 0.5 + 0.5
        path = os.path.join(out_dir, f'{name}_{names[k]}.png')
        Image.fromarray(np.clip(np.round(img * 255), 0, 255).astype(np.uint8), 'RGB').save(path, compress_level=6)
        files[k] = path
    log(f'{name}: textures {size}x{size} written ({time.time() - t0:.1f}s, atlas coverage {mask.mean() * 100:.1f}%)')
    return files


# ============================================================================ preview composition (system python)
def _font(size):
    from PIL import ImageFont
    for p in ('/System/Library/Fonts/Supplemental/Arial.ttf', '/System/Library/Fonts/Helvetica.ttc',
              '/Library/Fonts/Arial.ttf', '/System/Library/Fonts/SFNS.ttf'):
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except OSError:
                pass
    return ImageFont.load_default(size=size)


def compose_preview(meta_path):
    """Grid: rows = views, columns = LODs. Also returns silhouette IoU of each LOD vs LOD0."""
    from PIL import Image, ImageDraw
    with open(meta_path) as f:
        meta = json.load(f)
    tiles = meta['tiles']  # [view][lod] -> png path
    views, lods = meta['views'], meta['lods']
    first = Image.open(tiles[0][0])
    tw, th = first.size
    head, side, foot = 124, 150, 44
    W = side + tw * len(lods)
    H = head + th * len(views) + foot
    bg = (58, 60, 66)
    out = Image.new('RGB', (W, H), bg)
    dr = ImageDraw.Draw(out)
    f_big, f_mid, f_small = _font(30), _font(22), _font(17)
    dr.text((16, 12), meta['title'], fill=(235, 235, 235), font=f_big)
    dr.text((16, 52), meta['subtitle'], fill=(180, 184, 190), font=f_small)
    ious = {}
    for vi, view in enumerate(views):
        dr.text((16, head + vi * th + th // 2 - 12), view, fill=(220, 220, 220), font=f_mid)
        ref = None
        for li, lod in enumerate(lods):
            im = Image.open(tiles[vi][li]).convert('RGBA')
            a = np.asarray(im)[..., 3] > 127
            if li == 0:
                ref = a
            else:
                ious.setdefault(lod['name'], []).append(float((a & ref).sum() / max(1, (a | ref).sum())))
            canvas = Image.new('RGBA', im.size, (*bg, 255))
            canvas.alpha_composite(im)
            out.paste(canvas.convert('RGB'), (side + li * tw, head + vi * th))
    for li, lod in enumerate(lods):
        x = side + li * tw + 14
        txt = f"{lod['name']}  {lod['tris']:,} tris"
        if li:
            txt += f"  ({lod['tris'] / lods[0]['tris'] * 100:.1f}%)  silhouette IoU {np.mean(ious[lod['name']]):.3f}"
        dr.text((x, head - 28), txt, fill=(235, 235, 235), font=f_small)
    dr.text((16, H - 32), meta['footer'], fill=(170, 174, 180), font=f_small)
    out.save(meta['out'])
    return {k: round(float(np.mean(v)), 4) for k, v in ious.items()}


# ============================================================================ seam-aware QEM decimator (pure python)
def _plane_quadric(n, d, w):
    a, b, c = n
    return [w * a * a, w * a * b, w * a * c, w * a * d, w * b * b, w * b * c, w * b * d, w * c * c, w * c * d, w * d * d]


def _qeval(q, p):
    x, y, z = p
    return (q[0] * x * x + 2 * q[1] * x * y + 2 * q[2] * x * z + 2 * q[3] * x + q[4] * y * y + 2 * q[5] * y * z
            + 2 * q[6] * y + q[7] * z * z + 2 * q[8] * z + q[9])


def _closest_bary(p, a, b, c):
    """Barycentric weights of the point of triangle abc closest to p, and the squared distance."""
    abx, aby, abz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
    acx, acy, acz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
    apx, apy, apz = p[0] - a[0], p[1] - a[1], p[2] - a[2]
    d1 = abx * apx + aby * apy + abz * apz
    d2 = acx * apx + acy * apy + acz * apz
    if d1 <= 0 and d2 <= 0:
        w = (1.0, 0.0, 0.0)
    else:
        bpx, bpy, bpz = p[0] - b[0], p[1] - b[1], p[2] - b[2]
        d3 = abx * bpx + aby * bpy + abz * bpz
        d4 = acx * bpx + acy * bpy + acz * bpz
        if d3 >= 0 and d4 <= d3:
            w = (0.0, 1.0, 0.0)
        else:
            vc = d1 * d4 - d3 * d2
            if vc <= 0 and d1 >= 0 and d3 <= 0:
                t = d1 / (d1 - d3)
                w = (1 - t, t, 0.0)
            else:
                cpx, cpy, cpz = p[0] - c[0], p[1] - c[1], p[2] - c[2]
                d5 = abx * cpx + aby * cpy + abz * cpz
                d6 = acx * cpx + acy * cpy + acz * cpz
                if d6 >= 0 and d5 <= d6:
                    w = (0.0, 0.0, 1.0)
                else:
                    vb = d5 * d2 - d1 * d6
                    if vb <= 0 and d2 >= 0 and d6 <= 0:
                        t = d2 / (d2 - d6)
                        w = (1 - t, 0.0, t)
                    else:
                        va = d3 * d6 - d5 * d4
                        if va <= 0 and (d4 - d3) >= 0 and (d5 - d6) >= 0:
                            t = (d4 - d3) / ((d4 - d3) + (d5 - d6))
                            w = (0.0, 1 - t, t)
                        else:
                            den = 1.0 / (va + vb + vc)
                            w = (1 - (vb + vc) * den, vb * den, vc * den)
    qx = a[0] * w[0] + b[0] * w[1] + c[0] * w[2] - p[0]
    qy = a[1] * w[0] + b[1] * w[1] + c[1] * w[2] - p[1]
    qz = a[2] * w[0] + b[2] * w[1] + c[2] * w[2] - p[2]
    return w, qx * qx + qy * qy + qz * qz


_ISL_SAMPLES = [(0.5, 0.5, 0.0), (0.0, 0.5, 0.5), (0.5, 0.0, 0.5), (0.75, 0.25, 0.0), (0.25, 0.75, 0.0),
                (0.0, 0.75, 0.25), (0.0, 0.25, 0.75), (0.25, 0.0, 0.75), (0.75, 0.0, 0.25), (1 / 3, 1 / 3, 1 / 3)]


class Decimator:
    """Edge-collapse decimation with Garland-Heckbert quadrics and UV-island awareness.

    The merged vertex goes to the quadric optimum (falls back to the best point on the edge, then
    the end points). Every UV island that contains the collapsed edge gets the UV of that point on
    its own old faces, so the texture stays put. Corners on islands the edge does not reach keep
    their UV; that texture slide is charged as error (area * displacement^2 / 6 * uv_weight).
    Rejected: non-manifold results (link condition), face flips (>~72 deg) and slivers, UV flips,
    collapses across open boundaries. Vertices in a pinned group never move; they only merge with
    vertices of the same group at their original positions (keeps the ground contact and the top).
    """

    def __init__(self, P, F, FW, UV, pinned_groups=(), boundary_weight=10.0, flip_cos=0.3,
                 min_quality=0.02, uv_weight=1.0, islands=None):
        """islands: optional (island_id_image[v, u], island_of_wedge) - a collapse is refused when a
        face's new UV triangle reaches into texels of another island (a concave island border)."""
        if islands is not None:
            self.isl_img, self.wisland = islands[0], list(islands[1])
            self.isl_res = islands[0].shape[0]
        else:
            self.isl_img, self.wisland = None, None
        self.P = [tuple(p) for p in np.asarray(P, float).tolist()]
        self.F = [list(f) for f in np.asarray(F, int).tolist()]
        self.FW = [list(f) for f in np.asarray(FW, int).tolist()]
        self.UV = [tuple(u) for u in np.asarray(UV, float).tolist()]
        self.flip_cos, self.min_quality, self.uv_weight = flip_cos, min_quality, uv_weight
        nv, nf = len(self.P), len(self.F)
        self.falive = [True] * nf
        self.vf = [set() for _ in range(nv)]
        for fi, f in enumerate(self.F):
            for x in f:
                self.vf[x].add(fi)
        self.valive = [len(s) > 0 for s in self.vf]
        self.nfaces = nf
        self.anchor = [0] * nv
        for gi, g in enumerate(pinned_groups):
            for v in np.nonzero(np.asarray(g, bool))[0].tolist():
                self.anchor[v] |= 1 << gi
        self.frozen = [False] * nv
        ecount = {}
        for a, b, c in self.F:
            for e0, e1 in ((a, b), (b, c), (c, a)):
                k = (e0, e1) if e0 < e1 else (e1, e0)
                ecount[k] = ecount.get(k, 0) + 1
        self.boundary = [False] * nv
        bedges = []
        for (a, b), n in ecount.items():
            if n == 1:
                self.boundary[a] = self.boundary[b] = True
                bedges.append((a, b))
            elif n > 2:
                self.frozen[a] = self.frozen[b] = True
        for v in range(nv):
            if self.valive[v] and not self.frozen[v] and self._fans(v) > 1:
                self.frozen[v] = True  # bow-tie vertex
        Q = [[0.0] * 10 for _ in range(nv)]
        fnorm = {}
        P_ = self.P
        for fi, (a, b, c) in enumerate(self.F):
            pa, pb, pc = P_[a], P_[b], P_[c]
            e1 = (pb[0] - pa[0], pb[1] - pa[1], pb[2] - pa[2])
            e2 = (pc[0] - pa[0], pc[1] - pa[1], pc[2] - pa[2])
            n = (e1[1] * e2[2] - e1[2] * e2[1], e1[2] * e2[0] - e1[0] * e2[2], e1[0] * e2[1] - e1[1] * e2[0])
            ln = math.sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2])
            if ln < 1e-20:
                continue
            n = (n[0] / ln, n[1] / ln, n[2] / ln)
            fnorm[fi] = n
            q = _plane_quadric(n, -(n[0] * pa[0] + n[1] * pa[1] + n[2] * pa[2]), 0.5 * ln)
            for x in (a, b, c):
                qx = Q[x]
                for i in range(10):
                    qx[i] += q[i]
        for a, b in bedges:
            f = next(iter(self.vf[a] & self.vf[b]))
            if f not in fnorm:
                continue
            pa, pb = P_[a], P_[b]
            e = (pb[0] - pa[0], pb[1] - pa[1], pb[2] - pa[2])
            fn = fnorm[f]
            m = (e[1] * fn[2] - e[2] * fn[1], e[2] * fn[0] - e[0] * fn[2], e[0] * fn[1] - e[1] * fn[0])
            lm = math.sqrt(m[0] * m[0] + m[1] * m[1] + m[2] * m[2])
            if lm < 1e-20:
                continue
            m = (m[0] / lm, m[1] / lm, m[2] / lm)
            q = _plane_quadric(m, -(m[0] * pa[0] + m[1] * pa[1] + m[2] * pa[2]),
                               boundary_weight * (e[0] * e[0] + e[1] * e[1] + e[2] * e[2]))
            for x in (a, b):
                qx = Q[x]
                for i in range(10):
                    qx[i] += q[i]
        self.Q = Q
        self.stamp = [0] * nv
        self.heap = []
        self.sliding = 0

    def _fans(self, v):
        faces = list(self.vf[v])
        if len(faces) <= 1:
            return len(faces)
        parent = {f: f for f in faces}

        def find(x):
            while parent[x] != x:
                parent[x] = parent[parent[x]]
                x = parent[x]
            return x
        owner = {}
        for f in faces:
            for x in self.F[f]:
                if x == v:
                    continue
                if x in owner:
                    ra, rb = find(owner[x]), find(f)
                    if ra != rb:
                        parent[ra] = rb
                else:
                    owner[x] = f
        return len({find(f) for f in faces})

    def _neighbors(self, v):
        s = set()
        F = self.F
        for f in self.vf[v]:
            s.update(F[f])
        s.discard(v)
        return s

    def _ring_geom_ok(self, x, fuv, pn):
        P_ = self.P
        fc, mq = self.flip_cos, self.min_quality
        for f in self.vf[x]:
            if f in fuv:
                continue
            fx = self.F[f]
            i = fx.index(x)
            a, b, c = P_[fx[0]], P_[fx[1]], P_[fx[2]]
            e1x, e1y, e1z = b[0] - a[0], b[1] - a[1], b[2] - a[2]
            e2x, e2y, e2z = c[0] - a[0], c[1] - a[1], c[2] - a[2]
            nox, noy, noz = e1y * e2z - e1z * e2y, e1z * e2x - e1x * e2z, e1x * e2y - e1y * e2x
            if i == 0:
                a = pn
            elif i == 1:
                b = pn
            else:
                c = pn
            e1x, e1y, e1z = b[0] - a[0], b[1] - a[1], b[2] - a[2]
            e2x, e2y, e2z = c[0] - a[0], c[1] - a[1], c[2] - a[2]
            nnx, nny, nnz = e1y * e2z - e1z * e2y, e1z * e2x - e1x * e2z, e1x * e2y - e1y * e2x
            ln_new = math.sqrt(nnx * nnx + nny * nny + nnz * nnz)
            if ln_new < 1e-14:
                return False
            ln_old = math.sqrt(nox * nox + noy * noy + noz * noz)
            if ln_old > 1e-14 and (nnx * nox + nny * noy + nnz * noz) < fc * ln_new * ln_old:
                return False
            e3x, e3y, e3z = c[0] - b[0], c[1] - b[1], c[2] - b[2]
            s2 = (e1x * e1x + e1y * e1y + e1z * e1z + e2x * e2x + e2y * e2y + e2z * e2z
                  + e3x * e3x + e3y * e3y + e3z * e3z)
            if 3.4641016 * ln_new < mq * s2:
                return False
        return True

    def _ring_uv(self, x, fuv, uvmap):
        un = 0.0
        UV, P_ = self.UV, self.P
        for f in self.vf[x]:
            if f in fuv:
                continue
            fx, fw = self.F[f], self.FW[f]
            i = fx.index(x)
            uvn = uvmap.get(fw[i])
            if uvn is None:
                a, b, c = P_[fx[0]], P_[fx[1]], P_[fx[2]]
                e1x, e1y, e1z = b[0] - a[0], b[1] - a[1], b[2] - a[2]
                e2x, e2y, e2z = c[0] - a[0], c[1] - a[1], c[2] - a[2]
                nx, ny, nz = e1y * e2z - e1z * e2y, e1z * e2x - e1x * e2z, e1x * e2y - e1y * e2x
                un += 0.5 * math.sqrt(nx * nx + ny * ny + nz * nz)
                continue
            u0, u1, u2 = UV[fw[0]], UV[fw[1]], UV[fw[2]]
            a_old = (u1[0] - u0[0]) * (u2[1] - u0[1]) - (u1[1] - u0[1]) * (u2[0] - u0[0])
            if i == 0:
                u0 = uvn
            elif i == 1:
                u1 = uvn
            else:
                u2 = uvn
            a_new = (u1[0] - u0[0]) * (u2[1] - u0[1]) - (u1[1] - u0[1]) * (u2[0] - u0[0])
            if abs(a_old) > 1e-14 and a_old * a_new <= 0:
                return False, 0.0
            if self.isl_img is not None:
                isl = self.wisland[fw[0]]
                img, R = self.isl_img, self.isl_res
                for b0, b1, b2 in _ISL_SAMPLES:
                    uu = b0 * u0[0] + b1 * u1[0] + b2 * u2[0]
                    vv = b0 * u0[1] + b1 * u1[1] + b2 * u2[1]
                    xi, yi = int(uu * R), int(vv * R)
                    if 0 <= xi < R and 0 <= yi < R:
                        if img[yi, xi] != isl:
                            return False, 0.0
                    else:
                        return False, 0.0
        return True, un

    def _island_uv(self, pn, faces):
        best_d, best_uv = None, None
        P_, UV = self.P, self.UV
        for f in faces:
            fx, fw = self.F[f], self.FW[f]
            w, d = _closest_bary(pn, P_[fx[0]], P_[fx[1]], P_[fx[2]])
            if best_d is None or d < best_d:
                u0, u1, u2 = UV[fw[0]], UV[fw[1]], UV[fw[2]]
                best_d = d
                best_uv = (u0[0] * w[0] + u1[0] * w[1] + u2[0] * w[2], u0[1] * w[0] + u1[1] * w[1] + u2[1] * w[2])
        return best_uv

    def _prepare(self, u, v, nu=None):
        """Legality + candidate positions sorted by quadric error, or None."""
        if self.frozen[u] and self.frozen[v]:
            return None
        vf = self.vf
        fuv = vf[u] & vf[v]
        nf = len(fuv)
        if nf == 0 or nf > 2:
            return None
        bu, bv = self.boundary[u], self.boundary[v]
        if bu and bv:
            if nf != 1:
                return None
        elif nf == 1:
            return None
        fixed = None
        if self.frozen[u]:
            fixed = {0.0}
        elif self.frozen[v]:
            fixed = {1.0}
        au, av = self.anchor[u], self.anchor[v]
        if au or av:
            if au and av:
                if au != av:
                    return None
                cand = {0.0, 1.0}
            else:
                cand = {0.0} if au else {1.0}
            fixed = cand if fixed is None else (fixed & cand)
        if bu and not bv:
            fixed = {0.0} if fixed is None else (fixed & {0.0})
        elif bv and not bu:
            fixed = {1.0} if fixed is None else (fixed & {1.0})
        if fixed is not None and not fixed:
            return None
        F, FW = self.F, self.FW
        opp = set()
        for f in fuv:
            for x in F[f]:
                if x != u and x != v:
                    opp.add(x)
        if nu is None:
            nu = self._neighbors(u)
        if (nu & self._neighbors(v)) != opp:  # link condition
            return None
        pairs, seen_u, seen_v, ambiguous = [], {}, {}, False
        for f in fuv:
            fx, fw = F[f], FW[f]
            wu = fw[fx.index(u)]
            wv = fw[fx.index(v)]
            if (wu in seen_u and seen_u[wu] != wv) or (wv in seen_v and seen_v[wv] != wu):
                ambiguous = True
            if wu not in seen_u and wv not in seen_v:
                pairs.append((wu, wv))
            seen_u.setdefault(wu, wv)
            seen_v.setdefault(wv, wu)
        if ambiguous:  # a UV seam ends on this edge: only u -> v without moving v
            fixed = {1.0} if fixed is None else (fixed & {1.0})
            if not fixed:
                return None
        pu, pv = self.P[u], self.P[v]
        qu, qv = self.Q[u], self.Q[v]
        Q = [qu[k] + qv[k] for k in range(10)]
        cands = []
        if fixed is None:
            ex, ey, ez = pv[0] - pu[0], pv[1] - pu[1], pv[2] - pu[2]
            d2 = ex * ex + ey * ey + ez * ez
            a00, a01, a02, a11, a12, a22 = Q[0], Q[1], Q[2], Q[4], Q[5], Q[7]
            c00 = a11 * a22 - a12 * a12
            c01 = a02 * a12 - a01 * a22
            c02 = a01 * a12 - a02 * a11
            det = a00 * c00 + a01 * c01 + a02 * c02
            scale = max(abs(a00), abs(a11), abs(a22), 1e-30)
            if abs(det) > 1e-9 * scale * scale * scale:
                c11 = a00 * a22 - a02 * a02
                c12 = a02 * a01 - a00 * a12
                c22 = a00 * a11 - a01 * a01
                b0, b1, b2 = -Q[3], -Q[6], -Q[8]
                inv = 1.0 / det
                x = ((c00 * b0 + c01 * b1 + c02 * b2) * inv, (c01 * b0 + c11 * b1 + c12 * b2) * inv,
                     (c02 * b0 + c12 * b1 + c22 * b2) * inv)
                tt = ((x[0] - pu[0]) * ex + (x[1] - pu[1]) * ey + (x[2] - pu[2]) * ez) / max(d2, 1e-30)
                tt = 0.0 if tt < 0 else (1.0 if tt > 1 else tt)
                ox, oy, oz = x[0] - pu[0] - tt * ex, x[1] - pu[1] - tt * ey, x[2] - pu[2] - tt * ez
                if ox * ox + oy * oy + oz * oz < 0.25 * d2:  # stay near the edge
                    cands.append((_qeval(Q, x), x))
            aex = a00 * ex + a01 * ey + a02 * ez
            aey = a01 * ex + a11 * ey + a12 * ez
            aez = a02 * ex + a12 * ey + a22 * ez
            qa = ex * aex + ey * aey + ez * aez
            qb = (ex * (a00 * pu[0] + a01 * pu[1] + a02 * pu[2] + Q[3]) + ey * (a01 * pu[0] + a11 * pu[1] + a12 * pu[2] + Q[6])
                  + ez * (a02 * pu[0] + a12 * pu[1] + a22 * pu[2] + Q[8]))
            t0 = (-qb / qa) if qa > 1e-30 else 0.5
            t0 = 0.0 if t0 < 0 else (1.0 if t0 > 1 else t0)
            for t in (t0, 1.0, 0.0):
                p = pv if t == 1.0 else (pu if t == 0.0 else (pu[0] + t * ex, pu[1] + t * ey, pu[2] + t * ez))
                cands.append((_qeval(Q, p), p))
        else:
            for t in fixed:
                p = pv if t == 1.0 else pu
                cands.append((_qeval(Q, p), p))
        cands.sort(key=lambda c: c[0])
        return cands, fuv, pairs

    def _finish(self, u, v, prep):
        cands, fuv, pairs = prep
        pu, pv = self.P[u], self.P[v]
        best = None
        for qerr, pn in cands:
            qerr = qerr if qerr > 0 else 0.0
            if best is not None and qerr >= best[0]:
                break
            if not self._ring_geom_ok(u, fuv, pn) or not self._ring_geom_ok(v, fuv, pn):
                continue
            uvmap_u, uvmap_v, plist = {}, {}, []
            if pn is pv or pn is pu:
                for wu, wv in pairs:
                    uvn = self.UV[wv] if pn is pv else self.UV[wu]
                    uvmap_u[wu] = uvn; uvmap_v[wv] = uvn; plist.append((wu, wv, uvn))
            else:
                ring = self.vf[u] | self.vf[v]
                F, FW = self.F, self.FW
                for wu, wv in pairs:
                    faces = [f for f in ring if (u in F[f] and FW[f][F[f].index(u)] == wu)
                             or (v in F[f] and FW[f][F[f].index(v)] == wv)]
                    uvn = self._island_uv(pn, faces)
                    uvmap_u[wu] = uvn; uvmap_v[wv] = uvn; plist.append((wu, wv, uvn))
            ok, un_u = self._ring_uv(u, fuv, uvmap_u)
            if not ok:
                continue
            ok, un_v = self._ring_uv(v, fuv, uvmap_v)
            if not ok:
                continue
            slide = 0.0
            if un_u > 0:
                dx, dy, dz = pn[0] - pu[0], pn[1] - pu[1], pn[2] - pu[2]
                slide += un_u * (dx * dx + dy * dy + dz * dz) / 6.0
            if un_v > 0:
                dx, dy, dz = pn[0] - pv[0], pn[1] - pv[1], pn[2] - pv[2]
                slide += un_v * (dx * dx + dy * dy + dz * dz) / 6.0
            cost = qerr + self.uv_weight * slide
            if best is None or cost < best[0]:
                best = (cost, pn, plist, slide)
        return best

    def evaluate(self, u, v):
        if not self.valive[u] or not self.valive[v]:
            return None
        prep = self._prepare(u, v)
        return None if prep is None else self._finish(u, v, prep)

    def _push_best(self, u):
        self.stamp[u] += 1
        if not self.valive[u]:
            return
        nu = self._neighbors(u)
        preps = []
        for v in nu:
            p = self._prepare(u, v, nu)
            if p is not None:
                lb = p[0][0][0]
                preps.append((lb if lb > 0 else 0.0, v, p))
        preps.sort(key=lambda x: x[0])
        best = None
        for lb, v, p in preps:
            if best is not None and lb >= best[0]:
                break
            r = self._finish(u, v, p)
            if r is not None and (best is None or r[0] < best[0]):
                best = (r[0], v)
        if best is not None:
            heapq.heappush(self.heap, (best[0], self.stamp[u], u, best[1]))

    def _collapse(self, u, v, pn, plist):
        fuv = self.vf[u] & self.vf[v]
        self.P[v] = pn
        map_u, map_v = {}, {}
        for wu, wv, uvn in plist:  # one new UV wedge per island touching the edge
            self.UV.append((uvn[0], uvn[1]))
            if self.wisland is not None:
                self.wisland.append(self.wisland[wu])
            map_u[wu] = map_v[wv] = len(self.UV) - 1
        for f in fuv:
            self.falive[f] = False
            for x in self.F[f]:
                self.vf[x].discard(f)
            self.nfaces -= 1
        for f in self.vf[v]:
            i = self.F[f].index(v)
            w = self.FW[f][i]
            if w in map_v:
                self.FW[f][i] = map_v[w]
        for f in list(self.vf[u]):
            fx = self.F[f]
            i = fx.index(u)
            fx[i] = v
            w = self.FW[f][i]
            if w in map_u:
                self.FW[f][i] = map_u[w]
            self.vf[v].add(f)
        self.vf[u].clear()
        self.valive[u] = False
        qv, qu = self.Q[v], self.Q[u]
        for i in range(10):
            qv[i] += qu[i]
        self.boundary[v] = self.boundary[v] or self.boundary[u]
        self.anchor[v] |= self.anchor[u]
        self.frozen[v] = self.frozen[v] or self.frozen[u]

    def run(self, target, min_faces=4):
        target = max(target, min_faces)
        if self.nfaces <= target:
            return self.nfaces
        self.heap = []
        for u in range(len(self.P)):
            if self.valive[u]:
                self._push_best(u)
        while self.nfaces > target and self.heap:
            c, st, u, v = heapq.heappop(self.heap)
            if st != self.stamp[u] or not self.valive[u] or not self.valive[v]:
                continue
            r = self.evaluate(u, v)
            if r is None or r[0] > c * 1.0000001 + 1e-18:
                self._push_best(u)
                continue
            affected = self._neighbors(u) | self._neighbors(v)
            self._collapse(u, v, r[1], r[2])
            if r[3] > 0:
                self.sliding += 1
            for x in affected:
                if x != u:
                    self._push_best(x)
        return self.nfaces

    def snapshot(self):
        idx = [i for i, a in enumerate(self.falive) if a]
        F = np.array([self.F[i] for i in idx], np.int64).reshape(-1, 3)
        W = np.array([self.FW[i] for i in idx], np.int64).reshape(-1, 3)
        return np.array(self.P, float), F, W, np.array(self.UV, float)


# ============================================================================ silhouettes (numpy)
def _sil_views():
    v = [(2 * math.pi * k / 6, 0.0) for k in range(6)]
    v += [(2 * math.pi * (k + 0.5) / 3, math.radians(35)) for k in range(3)]
    return v


def _project(P, az, el):
    ca, sa, ce, se = math.cos(az), math.sin(az), math.cos(el), math.sin(el)
    right = np.array([-sa, ca, 0.0])
    up = np.array([-ca * se, -sa * se, ce])
    return np.stack([P @ right, P @ up], 1)


def _coverage(xy, tri, origin, px, shape, chunk=20000):
    H, W = shape
    img = np.zeros(H * W, bool)
    q = (xy - origin) / px
    a, b, c = q[tri[:, 0]], q[tri[:, 1]], q[tri[:, 2]]
    mn = np.floor(np.minimum(np.minimum(a, b), c) - 0.5).astype(np.int64)
    mx = np.ceil(np.maximum(np.maximum(a, b), c) - 0.5).astype(np.int64)
    mn[:, 0] = np.clip(mn[:, 0], 0, W - 1); mx[:, 0] = np.clip(mx[:, 0], 0, W - 1)
    mn[:, 1] = np.clip(mn[:, 1], 0, H - 1); mx[:, 1] = np.clip(mx[:, 1], 0, H - 1)
    w = mx[:, 0] - mn[:, 0] + 1; h = mx[:, 1] - mn[:, 1] + 1
    idx_all = np.nonzero((w > 0) & (h > 0))[0]
    for s in range(0, len(idx_all), chunk):
        idx = idx_all[s:s + chunk]
        cnt = w[idx] * h[idx]
        t = np.repeat(idx, cnt)
        k = np.arange(cnt.sum()) - np.repeat(np.cumsum(cnt) - cnt, cnt)
        x = mn[t, 0] + k % w[t]; y = mn[t, 1] + k // w[t]
        cx, cy = x + 0.5, y + 0.5
        A, B, C = a[t], b[t], c[t]
        d = (B[:, 0] - A[:, 0]) * (C[:, 1] - A[:, 1]) - (B[:, 1] - A[:, 1]) * (C[:, 0] - A[:, 0])
        w0 = (B[:, 0] - cx) * (C[:, 1] - cy) - (B[:, 1] - cy) * (C[:, 0] - cx)
        w1 = (C[:, 0] - cx) * (A[:, 1] - cy) - (C[:, 1] - cy) * (A[:, 0] - cx)
        w2 = (A[:, 0] - cx) * (B[:, 1] - cy) - (A[:, 1] - cy) * (B[:, 0] - cx)
        sg = np.sign(d)
        inside = (d != 0) & (w0 * sg >= 0) & (w1 * sg >= 0) & (w2 * sg >= 0)
        img[(y * W + x)[inside]] = True
    return img.reshape(H, W)


class Silhouettes:
    """Reference silhouettes of a mesh from 9 views; deviation() = mean outline displacement
    (XOR area / reference perimeter) of another mesh, in model units."""

    def __init__(self, P, tri, px, pad=4):
        self.px = px
        self.views = _sil_views()
        self.ref = []
        for az, el in self.views:
            xy = _project(P, az, el)
            lo = xy.min(0) - pad * px
            hi = xy.max(0) + pad * px
            shape = (int(math.ceil((hi[1] - lo[1]) / px)) + 1, int(math.ceil((hi[0] - lo[0]) / px)) + 1)
            m = _coverage(xy, tri, lo, px, shape)
            er = m.copy()
            er[1:, :] &= m[:-1, :]; er[:-1, :] &= m[1:, :]; er[:, 1:] &= m[:, :-1]; er[:, :-1] &= m[:, 1:]
            self.ref.append((lo, shape, m, max(int((m & ~er).sum()), 1)))

    def removal(self):
        """Error charged for dropping the part: half its largest silhouette extent (mean over views).
        (Area / own perimeter would make a long thin spike look as cheap as its half-width.)"""
        ext = []
        for (_lo, _shape, m, _perim) in self.ref:
            ys, xs = np.nonzero(m)
            ext.append(0.5 * self.px * max(np.ptp(xs) + 1, np.ptp(ys) + 1) if len(xs) else 0.0)
        return float(np.mean(ext))

    def deviation(self, P, tri):
        if len(tri) == 0:
            return self.removal()
        devs = []
        for (az, el), (lo, shape, m, perim) in zip(self.views, self.ref):
            m2 = _coverage(_project(P, az, el), tri, lo, self.px, shape)
            devs.append(int((m ^ m2).sum()) * self.px / perim)
        return float(np.mean(devs))


# ============================================================================ Blender: build stage
if IN_BLENDER:
    import bmesh
    from mathutils import Matrix, Vector
    from mathutils.bvhtree import BVHTree


def _arr(coll, attr, n, dt=np.float64):
    a = np.zeros(len(coll) * n, dt)
    coll.foreach_get(attr, a)
    return a.reshape(len(coll), n) if n > 1 else a


def _tris(me):
    co = _arr(me.vertices, 'co', 3)
    lv = _arr(me.loops, 'vertex_index', 1, np.int64)
    ls = _arr(me.polygons, 'loop_start', 1, np.int64)
    L = ls[:, None] + np.arange(3)[None]
    return co, lv[L], L


def _select_only(obj):
    for o in bpy.context.selected_objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)


def _labels(nv, tri):
    parent = np.arange(nv)

    def find(a):
        r = a
        while parent[r] != r:
            r = parent[r]
        while parent[a] != r:
            parent[a], a = r, parent[a]
        return r
    for a, b, c in tri.tolist():
        ra, rb, rc = find(a), find(b), find(c)
        if rb != ra:
            parent[rb] = ra
        rc = find(rc)
        if rc != ra:
            parent[rc] = ra
    root = np.array([find(t[0]) for t in tri.tolist()])
    return np.unique(root, return_inverse=True)[1].ravel()


def import_glb_blender(glb):
    """Import, join, triangulate, weld seam copies. Returns (obj, per-vertex GLB normals)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=glb, merge_vertices=False, import_shading='NORMALS')
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if not meshes:
        raise SystemExit(f'{glb}: no mesh')
    for o in meshes:
        _select_only(o)
        o.data = o.data.copy() if o.data.users > 1 else o.data
        o.data.transform(o.matrix_world)
        o.matrix_world = Matrix.Identity(4)
    if len(meshes) > 1:
        log(f'{glb}: joining {len(meshes)} mesh objects')
        _select_only(meshes[0])
        for o in meshes[1:]:
            o.select_set(True)
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active if len(meshes) > 1 else meshes[0]
    for o in list(bpy.context.scene.objects):
        if o is not obj:
            bpy.data.objects.remove(o)
    me = obj.data
    if len(me.materials) > 1:
        log(f'warning: {len(me.materials)} materials; only the first material\'s textures are used')
    cn = _arr(me.corner_normals, 'vector', 3)
    lv = _arr(me.loops, 'vertex_index', 1, np.int64)
    co0 = _arr(me.vertices, 'co', 3)
    vn = np.zeros_like(co0)
    vn[lv] = cn
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
    diag = float(np.linalg.norm(co0.max(0) - co0.min(0)))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6 * diag)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(me)
    bm.free()
    with bpy.context.temp_override(object=obj, active_object=obj):
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    co = _arr(me.vertices, 'co', 3)
    # weld copies share one position; Tripo gives them identical normals (checked) -> look up by position
    q = 1.0 / (1e-6 * diag)
    lut = {}
    for k, n in zip(np.round(co0 * q).astype(np.int64).tolist(), vn.tolist()):
        lut.setdefault(tuple(k), n)
    glbn = np.array([lut.get(tuple(k), (0.0, 0.0, 1.0)) for k in np.round(co * q).astype(np.int64).tolist()])
    if not me.uv_layers:
        me.uv_layers.new(name='UV_src')
    me.uv_layers[0].name = 'UV_src'
    return obj, glbn


def orient_and_normalise(obj, normals, front_axis, facing):
    """Rotate about the vertical so the model's front faces Unity `facing`, scale to height 1,
    pivot at the base centre. Blender -Y becomes Unity +Z at export."""
    me = obj.data
    fb = {'+X': (1, 0), '-X': (-1, 0), '+Z': (0, -1), '-Z': (0, 1)}[front_axis]  # glTF axis -> Blender XY
    target = (0, -1) if facing == '+Z' else (0, 1)
    ang = math.atan2(target[1], target[0]) - math.atan2(fb[1], fb[0])
    R = Matrix.Rotation(ang, 4, 'Z')
    me.transform(R)
    normals = normals @ np.array(R.to_3x3()).T
    co = _arr(me.vertices, 'co', 3)
    z0, z1 = co[:, 2].min(), co[:, 2].max()
    s = 1.0 / (z1 - z0)
    co = (co - [0, 0, z0]) * s
    _, tri, _ = _tris(me)
    cx, cy = slice_center(co, tri, SLICE_PIVOT)
    co -= [cx, cy, 0]
    me.vertices.foreach_set('co', co.ravel())
    me.update()
    nrm = normals / np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-12)
    me.normals_split_custom_set_from_vertices(nrm.tolist())
    return dict(rotation_deg=round(math.degrees(ang), 3), scale=s, source_height=z1 - z0, pivot_shift=[cx, cy])


def clip_below(co, tri, h):
    """Triangles of the surface part with z <= h (clipped), as an (m,3,3) array."""
    p = co[tri]
    below = p[:, :, 2] <= h
    full = p[below.all(1)]
    out = [full]
    part = p[below.any(1) & ~below.all(1)]
    polys = []
    for t in part:
        poly = []
        for i in range(3):
            a, b = t[i], t[(i + 1) % 3]
            ina, inb = a[2] <= h, b[2] <= h
            if ina:
                poly.append(a)
            if ina != inb:
                s = (h - a[2]) / (b[2] - a[2])
                poly.append(a + s * (b - a))
        for k in range(1, len(poly) - 1):
            polys.append((poly[0], poly[k], poly[k + 1]))
    if polys:
        out.append(np.array(polys))
    return np.concatenate(out) if out else np.zeros((0, 3, 3))


def slice_center(co, tri, frac):
    """x/y bounding-box centre of the surface within the lowest `frac` of the height (z from 0)."""
    z0 = co[:, 2].min()
    t = clip_below(co - [0, 0, z0], tri, frac * (co[:, 2].max() - z0))
    pts = t.reshape(-1, 3)
    return 0.5 * (pts[:, 0].min() + pts[:, 0].max()), 0.5 * (pts[:, 1].min() + pts[:, 1].max())


def shrink_hidden_islands(me, hidden, factor=0.05):
    """Scale UV islands made only of never-visible faces down to `factor` so the visible surface
    gets the texture space (Tripo's glass stalk/pod has a full inner wall)."""
    bm = bmesh.new()
    bm.from_mesh(me)
    uvl = bm.loops.layers.uv['UVMap']
    bm.faces.ensure_lookup_table()
    parent = {f.index: f.index for f in bm.faces if hidden[f.index]}

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    for f in bm.faces:
        if not hidden[f.index]:
            continue
        for l in f.loops:
            for l2 in l.edge.link_loops:
                g = l2.face
                if g.index == f.index or not hidden[g.index]:
                    continue
                a1, a2 = l[uvl].uv, l.link_loop_next[uvl].uv
                if l2.vert == l.vert:
                    b1, b2 = l2[uvl].uv, l2.link_loop_next[uvl].uv
                else:
                    b1, b2 = l2.link_loop_next[uvl].uv, l2[uvl].uv
                if (a1 - b1).length < 1e-7 and (a2 - b2).length < 1e-7:
                    ra, rb = find(f.index), find(g.index)
                    if ra != rb:
                        parent[ra] = rb
    groups = {}
    for fi in parent:
        groups.setdefault(find(fi), []).append(fi)
    for faces in groups.values():
        loops = [l for fi in faces for l in bm.faces[fi].loops]
        c = Vector((0.0, 0.0))
        for l in loops:
            c += l[uvl].uv
        c /= len(loops)
        for l in loops:
            l[uvl].uv = c + (l[uvl].uv - c) * factor
    bm.to_mesh(me)
    bm.free()
    return len(groups)


def _uv_density(me, layer='UVMap'):
    co, tri, L = _tris(me)
    uv = _arr(me.uv_layers[layer].data, 'uv', 2)[L]
    p = co[tri]
    a3 = np.linalg.norm(np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0]), axis=1) / 2
    au = np.abs(np.cross(uv[:, 1] - uv[:, 0], uv[:, 2] - uv[:, 0])) / 2
    return au / np.maximum(a3, 1e-15), au


def _select_faces(me, mask):
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_mode(type='FACE')
    bme = bmesh.from_edit_mesh(me)
    bme.faces.ensure_lookup_table()
    for v in bme.verts:
        v.select_set(False)
    for f in bme.faces:
        f.select_set(bool(mask[f.index]))
    bme.select_flush_mode()
    bmesh.update_edit_mesh(me)


def make_atlas(obj, hidden=None, smooth_iters=60, angle=66.0, slim_iters=60, margin=0.003, min_coverage=0.25):
    """New UV map 'UVMap': charts from a smoothed copy (Smart UV Project), SLIM-flattened on the
    real surface, Smart UV fallback for faces SLIM leaves collapsed or blown up, uniform texel
    density, islands of never-visible faces shrunk, packed. SLIM is not deterministic and can
    occasionally blow a chart up, so the packed result is checked and the unwrap retried
    (SLIM, SLIM, then Smart UV only)."""
    me = obj.data
    proxy = obj.copy()
    proxy.data = me.copy()
    bpy.context.scene.collection.objects.link(proxy)
    pm = proxy.data
    bm = bmesh.new()
    bm.from_mesh(pm)
    for _ in range(smooth_iters):
        bmesh.ops.smooth_vert(bm, verts=bm.verts, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.to_mesh(pm)
    bm.free()
    pm.uv_layers.active = pm.uv_layers.new(name='UVMap')
    _select_only(proxy)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=0.0, area_weight=0.0,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.seams_from_islands()
    bpy.ops.object.mode_set(mode='OBJECT')
    seam = np.zeros(len(pm.edges), bool)
    pm.edges.foreach_get('use_seam', seam)
    bpy.data.objects.remove(proxy)
    bpy.data.meshes.remove(pm)
    bm = bmesh.new()
    bm.from_mesh(me)
    nm = np.array([not e.is_manifold for e in bm.edges])
    if hidden is not None:  # cut between visible and never-visible faces
        for e in bm.edges:
            fs = [hidden[f.index] for f in e.link_faces]
            if any(fs) and not all(fs):
                nm[e.index] = True
    bm.free()
    me.edges.foreach_set('use_seam', seam | nm)
    _select_only(obj)
    result = None
    for attempt, method in enumerate(('SLIM', 'SLIM', 'SMART')):
        if 'UVMap' in me.uv_layers:
            me.uv_layers.remove(me.uv_layers['UVMap'])
        me.uv_layers.active = me.uv_layers.new(name='UVMap')
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.select_all(action='SELECT')
        if method == 'SLIM':
            bpy.ops.uv.unwrap(method='MINIMUM_STRETCH', margin=0.0, fill_holes=True, iterations=slim_iters, no_flip=True)
        else:
            bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=0.0, area_weight=0.0,
                                     correct_aspect=True, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode='OBJECT')
        dens, _ = _uv_density(me)
        good = np.isfinite(dens) & (dens > 0)
        med = float(np.median(dens[good])) if good.any() else 1.0
        bad = ~good | (dens < 0.02 * med) | (dens > 30.0 * med)
        if bad.any():
            _select_faces(me, bad)
            bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=0.0, area_weight=0.0,
                                     correct_aspect=True, scale_to_bounds=False)
            bpy.ops.object.mode_set(mode='OBJECT')
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.select_all(action='SELECT')
        bpy.ops.uv.average_islands_scale()
        bpy.ops.object.mode_set(mode='OBJECT')
        shrunk = shrink_hidden_islands(me, hidden) if hidden is not None and hidden.any() else 0
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.select_all(action='SELECT')
        bpy.ops.uv.pack_islands(rotate=True, rotate_method='ANY', scale=True, margin_method='FRACTION',
                                margin=margin, shape_method='CONCAVE')
        bpy.ops.object.mode_set(mode='OBJECT')
        dens, au = _uv_density(me)
        cov = float(au.sum())
        visible = ~hidden if hidden is not None else np.ones(len(au), bool)
        dv = dens[visible]
        spread = float(np.percentile(dv, 95) / max(np.percentile(dv, 5), 1e-12))
        result = dict(method=method, attempt=attempt + 1, fallback_faces=int(bad.sum()), uv_coverage=round(cov, 4),
                      hidden_islands=shrunk, density_spread_p95_p5=round(spread, 2))
        if cov >= min_coverage and spread < 20:
            break
        log(f'  atlas attempt {attempt + 1} ({method}) rejected: coverage {cov:.1%}, density spread {spread:.1f}')
    return result


def export_corners(obj, path):
    me = obj.data
    co, tri, L = _tris(me)

    def tangents(layer):
        me.calc_tangents(uvmap=layer)
        t = _arr(me.loops, 'tangent', 3)
        s = _arr(me.loops, 'bitangent_sign', 1)
        return np.concatenate([t, s[:, None]], 1)
    to = tangents('UV_src')
    tn = tangents('UVMap')
    cn = _arr(me.corner_normals, 'vector', 3)
    np.savez(path, uv_old=_arr(me.uv_layers['UV_src'].data, 'uv', 2)[L], uv_new=_arr(me.uv_layers['UVMap'].data, 'uv', 2)[L],
             normal=cn[L], tan_old=to[L], tan_new=tn[L])


def hidden_faces(co, tri, n_rays=64, piece=None, persistent=None):
    """Faces that cannot be seen from outside: no ray from the face centre into its normal
    hemisphere (Fibonacci directions) escapes. Only persistent geometry counts as an occluder:
    rays pass through pieces that a LOD may drop (persistent[piece] False), so a face covered
    only by a leaf or a drop stays. Catches parts buried inside bigger parts (leaf roots, drop
    tops) and inner walls of hollow shells (Tripo's glassy stalk/pod)."""
    tree = BVHTree.FromPolygons(co.tolist(), tri.tolist(), all_triangles=True)
    p = co[tri]
    n = np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0])
    n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-20)
    cen = p.mean(1)
    eps = 1e-5 * float(np.linalg.norm(co.max(0) - co.min(0)))
    i = np.arange(n_rays) + 0.5
    z = i / n_rays
    r = np.sqrt(1 - z * z)
    phi = i * math.pi * (3 - math.sqrt(5))
    dirs = np.stack([r * np.cos(phi), r * np.sin(phi), z], 1)
    solid = None if piece is None else persistent[piece]
    hidden = np.zeros(len(tri), bool)
    for f in range(len(tri)):
        nz = n[f]
        a = np.array([1.0, 0, 0]) if abs(nz[0]) < 0.9 else np.array([0, 1.0, 0])
        tx = np.cross(nz, a)
        tx /= np.linalg.norm(tx)
        ty = np.cross(nz, tx)
        start = Vector((cen[f] + nz * eps).tolist())
        world = dirs @ np.stack([tx, ty, nz])
        hidden[f] = True
        for d in world.tolist():
            dv = Vector(d)
            o = start
            blocked = False
            for _ in range(64):
                hit, _nrm, idx, _dd = tree.ray_cast(o, dv)
                if hit is None:
                    break
                if solid is None or solid[idx]:
                    blocked = True
                    break
                o = hit + dv * eps
            if not blocked:
                hidden[f] = False
                break
    return hidden


def prune_hidden(tri, hidden, piece, persistent, area, min_share=0.1):
    """On pieces that every LOD keeps, only drop never-visible regions that are a sizeable share
    of the piece (inner walls, bases buried in other parts). Small hidden patches (deep crevices)
    stay: cutting them out would leave holes whose rims block the decimation."""
    hidden = hidden.copy()
    e = np.concatenate([tri[:, [0, 1]], tri[:, [1, 2]], tri[:, [2, 0]]])
    e.sort(1)
    fid = np.tile(np.arange(len(tri)), 3)
    order = np.lexsort((e[:, 1], e[:, 0]))
    e, fid = e[order], fid[order]
    same = np.all(e[1:] == e[:-1], axis=1)
    parent = np.arange(len(tri))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    for a, b in zip(fid[:-1][same].tolist(), fid[1:][same].tolist()):
        if hidden[a] and hidden[b] and piece[a] == piece[b]:
            ra, rb = find(a), find(b)
            if ra != rb:
                parent[ra] = rb
    piece_area = np.bincount(piece, weights=area)
    regions = {}
    for f in np.nonzero(hidden)[0].tolist():
        regions.setdefault(find(f), []).append(f)
    for faces in regions.values():
        pc = piece[faces[0]]
        if persistent[pc] and area[faces].sum() < min_share * piece_area[pc]:
            hidden[faces] = False
    return hidden


def loose_pieces(me):
    """Face -> loose piece id after splitting non-manifold edges (same split the LOD builder uses)."""
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.split_edges(bm, edges=[e for e in bm.edges if not e.is_manifold and not e.is_boundary])
    dm = bpy.data.meshes.new('split_tmp')
    bm.to_mesh(dm)
    bm.free()
    co, tri, _ = _tris(dm)
    bpy.data.meshes.remove(dm)
    return _labels(len(co), tri)


def persistent_pieces(co, tri, piece, removable_share=0.02, removable_size=0.15):
    """Pieces big enough that no LOD drops them (mirrors the removable rule in lod_tables)."""
    H = float(np.ptp(co[:, 2]))
    out = np.zeros(piece.max() + 1, bool)
    for c in range(piece.max() + 1):
        f = tri[piece == c]
        v = co[np.unique(f)]
        out[c] = len(f) >= removable_share * len(tri) or np.linalg.norm(v.max(0) - v.min(0)) >= removable_size * H
    return out


def _sample_surface(co, tri, n, rng):
    p = co[tri]
    a = np.linalg.norm(np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0]), axis=1) / 2
    if len(tri) == 0 or a.sum() <= 0:
        return co[:1]
    f = rng.choice(len(tri), size=n, p=a / a.sum())
    r1 = np.sqrt(rng.random(n))
    r2 = rng.random(n)
    return (1 - r1)[:, None] * p[f, 0] + (r1 * (1 - r2))[:, None] * p[f, 1] + (r1 * r2)[:, None] * p[f, 2]


def _bvh(co, tri):
    return BVHTree.FromPolygons(co.tolist(), tri.tolist(), all_triangles=True)


def _nearest(tree, pts):
    out = np.empty(len(pts))
    for i, p in enumerate(pts.tolist()):
        r = tree.find_nearest(p)
        out[i] = r[3] if r[0] is not None else 1e9
    return out


def island_map(tri, FW, UVw, res=2048, grow=2):
    """UV islands (faces joined across edges whose two corners share UV wedges) rasterised into an
    id image [v, u] at the texture resolution, grown by `grow` px into the gutters."""
    e = np.concatenate([np.stack([tri[:, a], tri[:, b], FW[:, a], FW[:, b]], 1) for a, b in ((0, 1), (1, 2), (2, 0))])
    fid = np.tile(np.arange(len(tri)), 3)
    lo = np.minimum(e[:, 0], e[:, 1]); hi = np.maximum(e[:, 0], e[:, 1])
    wlo = np.where(e[:, 0] < e[:, 1], e[:, 2], e[:, 3]); whi = np.where(e[:, 0] < e[:, 1], e[:, 3], e[:, 2])
    key = np.stack([lo, hi, wlo, whi], 1)
    order = np.lexsort(key.T[::-1])
    key, fid = key[order], fid[order]
    same = np.all(key[1:] == key[:-1], axis=1)
    parent = np.arange(len(tri))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    for a, b in zip(fid[:-1][same].tolist(), fid[1:][same].tolist()):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb
    isl = np.array([find(f) for f in range(len(tri))])
    _, isl = np.unique(isl, return_inverse=True)
    isl = isl.ravel()
    wisland = np.full(len(UVw), -1, np.int64)
    wisland[FW.ravel()] = np.repeat(isl, 3)
    img = np.full((res, res), -1, np.int64)
    U = UVw[FW] * res  # (T,3,2) in pixels, x = u, y = v
    for s_ in range(0, len(tri), 4000):
        idx = np.arange(s_, min(len(tri), s_ + 4000))
        a, b, c = U[idx, 0], U[idx, 1], U[idx, 2]
        mn = np.clip(np.floor(np.minimum(np.minimum(a, b), c)).astype(np.int64), 0, res - 1)
        mx = np.clip(np.ceil(np.maximum(np.maximum(a, b), c)).astype(np.int64), 0, res - 1)
        w = mx[:, 0] - mn[:, 0] + 1; h = mx[:, 1] - mn[:, 1] + 1
        cnt = w * h
        t = np.repeat(np.arange(len(idx)), cnt)
        k = np.arange(cnt.sum()) - np.repeat(np.cumsum(cnt) - cnt, cnt)
        x = mn[t, 0] + k % w[t]; y = mn[t, 1] + k // w[t]
        cx, cy = x + 0.5, y + 0.5
        A, B, C = a[t], b[t], c[t]
        d = (B[:, 0] - A[:, 0]) * (C[:, 1] - A[:, 1]) - (B[:, 1] - A[:, 1]) * (C[:, 0] - A[:, 0])
        w0 = (B[:, 0] - cx) * (C[:, 1] - cy) - (B[:, 1] - cy) * (C[:, 0] - cx)
        w1 = (C[:, 0] - cx) * (A[:, 1] - cy) - (C[:, 1] - cy) * (A[:, 0] - cx)
        w2 = (A[:, 0] - cx) * (B[:, 1] - cy) - (A[:, 1] - cy) * (B[:, 0] - cx)
        sg = np.sign(d)
        inside = (d != 0) & (w0 * sg >= 0) & (w1 * sg >= 0) & (w2 * sg >= 0)
        img[y[inside], x[inside]] = isl[idx[t[inside]]]
    for _ in range(grow):  # grow islands into empty gutter pixels (first come)
        empty = img < 0
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            src = np.roll(np.roll(img, dy, 0), dx, 1)
            fill = empty & (src >= 0) & (img < 0)
            img[fill] = src[fill]
    return img, wisland


def lod_tables(co, tri, FW, UV, lab, pinned, sil_px, step=0.8, min_faces=8, geo_frac=0.25,
               removable_share=0.02, removable_size=0.15, face_weight=None, islands=None):
    """Progressive decimation of every connected part, snapshotted at geometric steps.
    error = mean silhouette displacement (9 views) + geo_frac * p95 symmetric surface distance.
    Small parts (< removable_share of all faces and < removable_size across) may also be dropped;
    their error is then the silhouette they leave behind."""
    p = co[tri]
    total_area = (np.linalg.norm(np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0]), axis=1) / 2).sum()
    comps = []
    for c in np.unique(lab):
        fm = lab == c
        F = tri[fm]
        used = np.unique(F)
        remap = -np.ones(len(co), np.int64)
        remap[used] = np.arange(len(used))
        Fc, Pc = remap[F], co[used]
        n = len(Fc)
        pa = Pc[Fc]
        area = (np.linalg.norm(np.cross(pa[:, 1] - pa[:, 0], pa[:, 2] - pa[:, 0]), axis=1) / 2).sum()
        ns = int(np.clip(area / total_area * 30000, 150, 4000))
        rng = np.random.default_rng(0)
        tree0 = _bvh(Pc, Fc)
        s0 = _sample_surface(Pc, Fc, ns, rng)
        S = Silhouettes(Pc, Fc, sil_px)
        D = Decimator(Pc, Fc, FW[fm], UV, pinned_groups=[g[used] for g in pinned], islands=islands)
        rows = [dict(n=n, err=0.0, geo=0.0, sil=0.0, P=Pc, F=Fc, W=FW[fm], UV=UV)]
        k = 1
        while True:
            want = int(math.floor(n * step ** k))
            k += 1
            if want < min_faces:
                break
            got = D.run(want)
            if got >= rows[-1]['n']:
                break
            Ps, Fs, Ws, UVs = D.snapshot()
            tb = _bvh(Ps, Fs)
            g = float(max(np.percentile(_nearest(tb, s0), 95), np.percentile(_nearest(tree0, _sample_surface(Ps, Fs, ns, rng)), 95)))
            sl = S.deviation(Ps, Fs)
            rows.append(dict(n=got, err=sl + geo_frac * g, geo=g, sil=sl, P=Ps, F=Fs, W=Ws, UV=UVs))
            if got > want:
                break
        wpart = 1.0
        if face_weight is not None:
            fa = np.linalg.norm(np.cross(pa[:, 1] - pa[:, 0], pa[:, 2] - pa[:, 0]), axis=1)
            wpart = float((face_weight[fm] * fa).sum() / max(fa.sum(), 1e-20))
        for r in rows:
            r['raw'] = r['err']
            r['err'] = r['err'] * wpart
        size = float(np.linalg.norm(Pc.max(0) - Pc.min(0)))
        if n < removable_share * len(tri) and size < removable_size * float(np.ptp(co[:, 2])):
            sl = S.removal()
            rows.append(dict(n=0, err=sl * wpart, raw=sl, geo=sl, sil=sl, P=Pc, F=np.zeros((0, 3), np.int64),
                             W=np.zeros((0, 3), np.int64), UV=UV))
        best = 0.0
        for r in rows:
            best = max(best, r['err'])
            r['err_m'] = best
        comps.append(dict(label=int(c), used=used, P=Pc, F0=Fc, FW=FW[fm], n=n, rows=rows, weight=wpart))
    return comps


def allocate(comps, target):
    """Smallest common error level whose per-part triangle counts fit the budget."""
    errs = sorted({r['err_m'] for c in comps for r in c['rows']})

    def pick(eps):
        return [[r for r in c['rows'] if r['err_m'] <= eps][-1] for c in comps]
    lo, hi = 0, len(errs) - 1
    while lo < hi:
        mid = (lo + hi) // 2
        if sum(r['n'] for r in pick(errs[mid])) <= target:
            hi = mid
        else:
            lo = mid + 1
    return errs[lo], pick(errs[lo])


def face_luminance_weight(UVf, tex_path, floor=0.3):
    """Per-face weight from the albedo under it: errors on bright parts show more in a dark scene.
    w = floor + (1-floor) * min(1, L / L_ref), L_ref = 90th percentile of the face luminance."""
    if not tex_path or not os.path.exists(tex_path):
        return None
    im = bpy.data.images.load(tex_path)
    W, H = im.size
    px = np.zeros(W * H * 4, np.float32)
    im.pixels.foreach_get(px)
    bpy.data.images.remove(im)
    px = px.reshape(H, W, 4)  # row 0 = bottom (v = 0)
    c = UVf.mean(1)
    x = np.clip((c[:, 0] % 1.0 * W).astype(int), 0, W - 1)
    y = np.clip((c[:, 1] % 1.0 * H).astype(int), 0, H - 1)
    rgb = px[y, x, :3]
    lum = rgb @ np.array([0.2126, 0.7152, 0.0722])
    ref = max(float(np.percentile(lum, 90)), 1e-3)
    return floor + (1 - floor) * np.minimum(1.0, lum / ref)


def build_lods(lod0, ratios, uv_layer='UVMap', tex_path=None):
    """Returns [(P, F, UVcorner, info)] for each ratio (LOD1, LOD2, ...)."""
    me = lod0.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.split_edges(bm, edges=[e for e in bm.edges if not e.is_manifold and not e.is_boundary])
    dm = bpy.data.meshes.new('lod_src')
    bm.to_mesh(dm)
    bm.free()
    co, tri, L = _tris(dm)
    uv = _arr(dm.uv_layers[uv_layer].data, 'uv', 2)
    lv = _arr(dm.loops, 'vertex_index', 1, np.int64)
    _, wid = np.unique(np.c_[lv, np.round(uv * 1e7).astype(np.int64)], axis=0, return_inverse=True)
    wid = wid.ravel()
    UVw = np.zeros((wid.max() + 1, 2))
    UVw[wid] = uv
    FW = wid[L]
    lab = _arr(dm.attributes['comp'].data, 'value', 1, np.int64)
    hid = _arr(dm.attributes['hidden'].data, 'value', 1, bool)
    bpy.data.meshes.remove(dm)
    keep0 = ~hid
    lab = np.full(len(tri), -1, np.int64)
    lab[keep0] = _labels(len(co), tri[keep0])  # loose pieces after the split: each can shrink or go
    n0 = len(tri)
    keep = keep0
    tri_k, FW_k, lab_k = tri[keep], FW[keep], lab[keep]
    z = co[:, 2]
    pinned = [z <= z.min() + PIN_BAND, z >= z.max() - PIN_BAND]
    t0 = time.time()
    wface = face_luminance_weight(UVw[FW_k], tex_path)
    islands = island_map(tri, FW, UVw)
    comps = lod_tables(co, tri_k, FW_k, UVw, lab_k, pinned, sil_px=(z.max() - z.min()) / 1024, face_weight=wface,
                       islands=islands)
    pa = co[tri]
    area = np.linalg.norm(np.cross(pa[:, 1] - pa[:, 0], pa[:, 2] - pa[:, 0]), axis=1)
    hidden_share = float(area[hid].sum() / area.sum())
    log(f'  LOD tables: {len(comps)} parts, never-visible faces left out of LOD1+ ({time.time() - t0:.1f}s)')
    out = []
    big = int(np.argmax([c['n'] for c in comps]))
    for ratio in ratios:
        target = int(round(n0 * ratio))
        eps, sel = allocate(comps, target)
        total = sum(r['n'] for r in sel)
        parts = []
        removed = sum(1 for r in sel if r['n'] == 0)
        for i, (c, r) in enumerate(zip(comps, sel)):
            if r['n'] == 0:
                continue
            P_, F_, W_, UV_ = r['P'], r['F'], r['W'], r['UV']
            if i == big and total != target:
                want = r['n'] + (target - total)
                D = Decimator(c['P'], c['F0'], c['FW'], UVw, pinned_groups=[g[c['used']] for g in pinned], islands=islands)
                D.run(want)
                P_, F_, W_, UV_ = D.snapshot()
            parts.append((P_, F_, UV_[W_].reshape(-1, 2)))
        Ps, Fs, UVs, off = [], [], [], 0
        for P_, F_, UVc in parts:
            used = np.unique(F_)
            remap = -np.ones(len(P_), np.int64)
            remap[used] = np.arange(len(used))
            Ps.append(P_[used]); Fs.append(remap[F_] + off); UVs.append(UVc); off += len(used)
        order = np.argsort([-c['n'] for c in comps])[:4]
        log('    biggest parts (faces -> kept, weighted error, weight): ' + ', '.join(
            f"{comps[i]['n']}->{sel[i]['n']} ({sel[i]['err_m']:.4f}, w {comps[i]['weight']:.2f})" for i in order))
        info = dict(target=target, error=float(max(r['raw'] for r in sel)), weighted_error=float(eps),
                    hidden_faces=int(hid.sum()), hidden_area=round(hidden_share, 4),
                    parts=len(comps), parts_removed=removed,
                    geo=float(max(r['geo'] for r in sel)), sil=float(max(r['sil'] for r in sel)))
        out.append((np.concatenate(Ps), np.concatenate(Fs), np.concatenate(UVs), info))
    return out


def mesh_from_arrays(name, P, F, UVc):
    p = P[F]
    area2 = np.linalg.norm(np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0]), axis=1)
    key = np.sort(F, axis=1)
    _, first = np.unique(key, axis=0, return_index=True)
    ok = np.zeros(len(F), bool)
    ok[first] = True
    ok &= (area2 > 1e-12) & (F[:, 0] != F[:, 1]) & (F[:, 1] != F[:, 2]) & (F[:, 0] != F[:, 2])
    if not ok.all():
        log(f'  {name}: dropped {int((~ok).sum())} degenerate/duplicate triangles')
        F, UVc = F[ok], UVc.reshape(-1, 3, 2)[ok].reshape(-1, 2)
    me = bpy.data.meshes.new(name)
    me.from_pydata(P.tolist(), [], F.tolist())
    uvl = me.uv_layers.new(name='UVMap')
    uvl.data.foreach_set('uv', UVc.reshape(-1).astype(np.float32))
    me.polygons.foreach_set('use_smooth', np.ones(len(me.polygons), bool))
    me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def mark_creases(me, angle_deg=60.0):
    """Sharp edges where two LOD faces meet at more than angle_deg, so a coarse triangle that wraps
    round a thin rim gets its own normal instead of the average of both sides (Blender keeps one
    custom normal per smooth fan)."""
    bm = bmesh.new()
    bm.from_mesh(me)
    lim = math.cos(math.radians(angle_deg))
    n = 0
    for e in bm.edges:
        lf = e.link_faces
        if len(lf) == 2 and lf[0].normal.dot(lf[1].normal) < lim:
            e.smooth = False
            n += 1
    bm.to_mesh(me)
    bm.free()
    return n


def transfer_normals(src, dst):
    """Custom normals for a LOD: interpolate LOD0's normals at the nearest LOD0 point whose face
    points the same way as the LOD face (a plain nearest-point transfer grabs inner walls / the
    far side of thin parts and flips the shading)."""
    me0 = src.data
    co0, tri0, L0 = _tris(me0)
    cn0 = _arr(me0.corner_normals, 'vector', 3)
    tree = BVHTree.FromPolygons(co0.tolist(), tri0.tolist(), all_triangles=True)
    me = dst.data
    co, tri, L = _tris(me)
    p = co[tri]
    fn = np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0])
    fn /= np.maximum(np.linalg.norm(fn, axis=1, keepdims=True), 1e-20)
    out = np.zeros((len(me.loops), 3))
    reach = 0.02 * float(np.ptp(co0[:, 2]))
    fallback = 0
    for f in range(len(tri)):
        nf = Vector(fn[f].tolist())
        for k in range(3):
            v = Vector(co[tri[f][k]].tolist())
            loc, nrm, idx, d = tree.find_nearest(v)
            if nrm.dot(nf) <= 0.2:
                best = None
                for loc2, nrm2, idx2, d2 in tree.find_nearest_range(v, d + reach):
                    if nrm2.dot(nf) > 0.2 and (best is None or d2 < best[3]):
                        best = (loc2, nrm2, idx2, d2)
                if best is not None:
                    loc, nrm, idx, d = best
            a, b, c = (Vector(x) for x in co0[tri0[idx]].tolist())
            v0, v1, v2 = b - a, c - a, loc - a
            d00, d01, d11, d20, d21 = v0.dot(v0), v0.dot(v1), v1.dot(v1), v2.dot(v0), v2.dot(v1)
            den = d00 * d11 - d01 * d01
            w1 = (d11 * d20 - d01 * d21) / den if den else 1 / 3
            w2 = (d00 * d21 - d01 * d20) / den if den else 1 / 3
            w0 = 1 - w1 - w2
            nl = w0 * cn0[L0[idx][0]] + w1 * cn0[L0[idx][1]] + w2 * cn0[L0[idx][2]]
            ln = np.linalg.norm(nl)
            if ln < 1e-12 or np.dot(nl, fn[f]) <= 0:
                nl, ln = fn[f], 1.0
                fallback += 1
            out[L[f][k]] = nl / ln
    me.normals_split_custom_set(out.tolist())
    return fallback


# ---------------------------------------------------------------- analysis (Unity space = (-x, z, -y) of Blender)
def to_unity(co):
    return np.stack([-co[:, 0], co[:, 2], -co[:, 1]], 1)


def _dilate(m, r):
    out = m.copy()
    for d in range(1, r + 1):
        out[d:, :] |= m[:-d, :]; out[:-d, :] |= m[d:, :]
    m2 = out.copy()
    for d in range(1, r + 1):
        out[:, d:] |= m2[:, :-d]; out[:, :-d] |= m2[:, d:]
    return out


def footprint(co_u, tri, slice_h=SLICE_FOOT, px=0.0025, close=0.02):
    """Bottom-slice occupancy -> radius around the pivot + circles on a coarse grid. Anything the
    slice encloses is solid (a hollow trunk's ring counts as a filled disc); gaps narrower than
    2*close are bridged first, so a ring with a crack still encloses its inside."""
    from collections import deque
    co_b = np.stack([co_u[:, 0], co_u[:, 2], co_u[:, 1]], 1)  # x, z, y(up) for clipping
    t = clip_below(co_b, tri, slice_h)
    xy = t[:, :, :2].reshape(-1, 2)
    lo = xy.min(0) - 3 * px
    hi = xy.max(0) + 3 * px
    shape = (int(math.ceil((hi[1] - lo[1]) / px)) + 1, int(math.ceil((hi[0] - lo[0]) / px)) + 1)
    pts = t[:, :, :2].reshape(-1, 2)
    m = _coverage(pts, np.arange(len(pts)).reshape(-1, 3), lo, px, shape)
    r = max(1, int(round(close / px)))
    pad = r + 2
    mp = np.pad(m, pad)
    closed = ~_dilate(~_dilate(mp, r), r)[pad:-pad, pad:-pad]
    m = m | closed
    # fill interior holes: flood the outside from the border
    H, W = m.shape
    outside = np.zeros_like(m)
    dq = deque()
    for y in range(H):
        for x in (0, W - 1):
            if not m[y, x] and not outside[y, x]:
                outside[y, x] = True; dq.append((y, x))
    for x in range(W):
        for y in (0, H - 1):
            if not m[y, x] and not outside[y, x]:
                outside[y, x] = True; dq.append((y, x))
    while dq:
        y, x = dq.popleft()
        for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= yy < H and 0 <= xx < W and not m[yy, xx] and not outside[yy, xx]:
                outside[yy, xx] = True
                dq.append((yy, xx))
    filled = ~outside
    ys, xs = np.nonzero(filled)
    cx = lo[0] + (xs + 0.5) * px
    cz = lo[1] + (ys + 0.5) * px
    half = px * 0.7072
    radius = float(np.sqrt(cx ** 2 + cz ** 2).max() + half)
    extent = max(np.ptp(cx), np.ptp(cz)) + px
    cell = max(0.05, extent / 6.0)
    gx = np.floor(cx / cell).astype(int)
    gz = np.floor(cz / cell).astype(int)
    circles = []
    for key in sorted(set(zip(gx.tolist(), gz.tolist()))):
        sel = (gx == key[0]) & (gz == key[1])
        if sel.sum() * px * px < 0.03 * cell * cell:  # ignore cells with a sliver of occupancy
            continue
        mx, mz = cx[sel].mean(), cz[sel].mean()
        r = float(np.sqrt((cx[sel] - mx) ** 2 + (cz[sel] - mz) ** 2).max() + half)
        circles.append([round(float(mx), 4), round(float(mz), 4), round(r, 4)])
    area = float(filled.sum() * px * px)
    covered = 0
    if circles:
        cc = np.array(circles)
        d = np.sqrt((cx[:, None] - cc[None, :, 0]) ** 2 + (cz[:, None] - cc[None, :, 1]) ** 2) <= cc[None, :, 2]
        covered = float(d.any(1).mean())
    return dict(radius=round(radius, 4), slice_height=slice_h, cell=round(cell, 4), area=round(area, 4),
                circles=circles, circle_coverage=round(covered, 4))


def glow_ranges(co_u, tri, lab, rules):
    """Evaluate the per-plant glow rules (plants_to_unity.json) on LOD0 in Unity space.
    Returns ({part: [y0, y1]} in rule order - consumers that support one band take the first -,
    {part: how it was found})."""
    out, how = {}, {}
    y = co_u[:, 1]
    comps = []
    for c in np.unique(lab):
        f = tri[lab == c]
        v = co_u[np.unique(f)]
        comps.append(dict(id=int(c), faces=len(f), ymin=float(v[:, 1].min()), ymax=float(v[:, 1].max()),
                          xz_area=float(np.ptp(v[:, 0]) * np.ptp(v[:, 2])), height=float(np.ptp(v[:, 1]))))
    total = len(tri)
    for part, rule in rules.items():
        kind = rule.get('rule')
        rng = None
        if 'range' in rule:
            rng = rule['range']
            how[part] = 'fixed in plants_to_unity.json'
        elif kind == 'widest_component':
            c = max(comps, key=lambda c: c['xz_area'])
            rng = [c['ymin'], c['ymax']]
            how[part] = f'y extent of the widest connected part ({c["faces"]} faces)'
        elif kind == 'small_components':
            share = rule.get('max_share', 0.02)
            sm = [c for c in comps if c['faces'] < share * total and c['ymin'] >= rule.get('min_y', 0.0)]
            if sm:
                rng = [min(c['ymin'] for c in sm), max(c['ymax'] for c in sm)]
                how[part] = f'y extent of the {len(sm)} small separate parts (< {share:.0%} of faces each)'
        elif kind == 'above_base_component':
            grounded = [c for c in comps if c['ymin'] < 0.01]
            base = max(grounded, key=lambda c: c['xz_area'])
            others = [c for c in comps if c['id'] != base['id']]
            tall = max(others, key=lambda c: c['height'])
            rng = [max(base['ymax'], tall['ymin']), tall['ymax']]
            how[part] = (f'tallest part ({tall["faces"]} faces) above the top of the widest grounded part '
                         f'({base["faces"]} faces, ends at y={base["ymax"]:.3f})')
        elif kind == 'bulb_top':
            factor = rule.get('factor', 2.0)
            rng_ = np.random.default_rng(0)
            pts = _sample_surface(co_u, tri, 200000, rng_)
            bins = np.linspace(0, 1, 101)
            r = np.full(100, np.nan)
            for i in range(100):
                s = pts[(pts[:, 1] >= bins[i]) & (pts[:, 1] < bins[i + 1])]
                if len(s) > 20:
                    cx, cz = np.median(s[:, 0]), np.median(s[:, 2])
                    r[i] = np.percentile(np.hypot(s[:, 0] - cx, s[:, 2] - cz), 95)
            top = np.nanargmax(np.where(np.arange(100) >= 60, r, np.nan))
            r_min = np.nanmin(r[20:top])
            i = top
            while i > 0 and r[i - 1] > factor * r_min:
                i -= 1
            # interpolate the crossing inside bin i-1 .. i
            lo_r, hi_r = r[i - 1], r[i]
            frac = (factor * r_min - lo_r) / (hi_r - lo_r) if hi_r != lo_r else 0.0
            yb = (i - 1 + 0.5) / 100 + frac / 100
            rng = [float(yb), float(y.max())]
            how[part] = (f'radius profile: bulb from where the radius first exceeds {factor:g}x the thinnest '
                         f'stalk radius ({r_min:.4f}) below the bulb to the top')
        if rng is not None:
            out[part] = [round(float(rng[0]), 3), round(float(rng[1]), 3)]
    return out, how


# ---------------------------------------------------------------- FBX export
FBX_EXPORT_SETTINGS = dict(
    object_types={'EMPTY', 'MESH'},
    use_selection=False,
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',   # Blender metres -> FBX UnitScaleFactor 100 => Unity file scale 1
    axis_forward='-Z',
    axis_up='Y',
    use_space_transform=False,             # data is already pre-rotated into FBX axes; no rotations written
    bake_space_transform=False,
    use_mesh_modifiers=True,
    mesh_smooth_type='EDGE',               # write the crease edges too, so importers rebuild the same split normals
    use_triangles=True,
    use_tspace=False,                      # Unity recomputes MikkTSpace tangents
    use_custom_props=False,
    colors_type='NONE',
    add_leaf_bones=False,
    bake_anim=False,
    path_mode='STRIP',
    embed_textures=False,
    use_metadata=True,
)


def export_fbx(path, name, lod_objs):
    """Root empty <name> + the LOD meshes. Mesh data are rotated Blender -> FBX axes (x, z, -y)
    so every object transform written to the file is identity."""
    for o in list(bpy.context.scene.objects):
        if o not in lod_objs:
            bpy.data.objects.remove(o)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    mat = bpy.data.materials.new(f'{name}_Mat')
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    conv = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))  # Blender (x,y,z) -> FBX (x, z, -y)
    for o in lod_objs:
        o.data.materials.clear()
        o.data.materials.append(mat)
        o.data.transform(conv)
        o.data.update()
        o.parent = root
        o.matrix_world = Matrix.Identity(4)
    bpy.ops.export_scene.fbx(filepath=path, **FBX_EXPORT_SETTINGS)


def build_stage(args):
    glb = os.path.abspath(args.glb)
    name = args.name or plant_name(glb)
    out_dir = os.path.join(args.out, name)
    work = os.path.join(args.work, name)
    os.makedirs(out_dir, exist_ok=True)
    os.makedirs(work, exist_ok=True)
    t0 = time.time()
    log(f'{name}: build from {glb}')
    obj, glbn = import_glb_blender(glb)
    info = orient_and_normalise(obj, glbn, args.front_axis, args.facing)
    obj.name = obj.data.name = f'{name}_LOD0'
    me = obj.data
    co, tri, _ = _tris(me)
    lab = _labels(len(co), tri)
    att = me.attributes.new('comp', 'INT', 'FACE')
    att.data.foreach_set('value', lab.astype(np.int32))
    me.polygons.foreach_set('use_smooth', np.ones(len(me.polygons), bool))
    t1 = time.time()
    piece = loose_pieces(me)
    persist = persistent_pieces(co, tri, piece)
    hidden = hidden_faces(co, tri, n_rays=128, piece=piece, persistent=persist)
    pa = co[tri]
    fa = np.linalg.norm(np.cross(pa[:, 1] - pa[:, 0], pa[:, 2] - pa[:, 0]), axis=1)
    hidden = prune_hidden(tri, hidden, piece, persist, fa)
    log(f'{name}: {int(hidden.sum())} faces ({fa[hidden].sum() / fa.sum():.1%} of the area) cannot be seen from outside '
        f'({time.time() - t1:.1f}s)')
    att = me.attributes.new('hidden', 'BOOLEAN', 'FACE')
    att.data.foreach_set('value', hidden)
    atlas = make_atlas(obj, hidden)
    log(f'{name}: new atlas ({atlas["method"]}, attempt {atlas["attempt"]}): {atlas["uv_coverage"]:.1%} of the texture '
        f'used, texel density spread p95/p5 {atlas["density_spread_p95_p5"]}, fallback faces {atlas["fallback_faces"]}, '
        f'never-visible islands shrunk {atlas["hidden_islands"]}')
    npz = os.path.join(work, 'corners.npz')
    export_corners(obj, npz)
    r = subprocess.run([args.python, SCRIPT, 'textures', '--npz', npz, '--glb', glb, '--out-dir', out_dir,
                        '--name', name, '--size', str(args.tex)], capture_output=True, text=True)
    sys.stdout.write(r.stdout)
    if r.returncode != 0:
        sys.stdout.write(r.stderr)
        raise SystemExit(f'{name}: texture transfer failed')
    me.uv_layers.remove(me.uv_layers['UV_src'])
    lods = build_lods(obj, args.ratios, tex_path=os.path.join(out_dir, f'{name}_BaseColor.png'))
    objs = [obj]
    for k, (P, F, UVc, linfo) in enumerate(lods, start=1):
        o = mesh_from_arrays(f'{name}_LOD{k}', P, F, UVc)
        linfo['crease_edges'] = mark_creases(o.data)
        nfb = transfer_normals(obj, o)
        linfo['normal_fallbacks'] = nfb
        objs.append(o)
        log(f'{name}: LOD{k} {len(o.data.polygons)} tris (target {linfo["target"]}), error {linfo["error"]:.4f} '
            f'(silhouette {linfo["sil"]:.4f}, surface p95 {linfo["geo"]:.4f}); {linfo["parts_removed"]} of '
            f'{linfo["parts"]} loose parts dropped; {nfb} corners kept the face normal')
    # analysis in Unity space
    co, tri, _ = _tris(me)
    co_u = to_unity(co)
    size = (co_u.max(0) - co_u.min(0)).tolist()
    foot = footprint(co_u, tri)
    rules = plant_config(name).get('glow', {})
    glow, glow_how = glow_ranges(co_u, tri, lab, rules)
    entry = dict(
        name=name,
        fbx=f'{name}/{name}.fbx',
        material=f'{name}_Mat',
        textures=dict(baseColor=f'{name}/{name}_BaseColor.png', normal=f'{name}/{name}_Normal.png', orm=f'{name}/{name}_ORM.png'),
        lods=[o.name for o in objs],
        tris=[len(o.data.polygons) for o in objs],
        lod_error=[0.0] + [round(l[3]['error'], 5) for l in lods],
        size=[round(v, 4) for v in size],
        footprint=dict(radius=foot['radius'], slice_height=foot['slice_height'], cell=foot['cell'],
                       circles=foot['circles']),
        facing=args.facing,
        glow=glow,
        glow_source=glow_how,
        source=dict(glb=glb, front_axis_gltf=args.front_axis, front_detection=args.front_how,
                    source_height=round(info['source_height'], 5), rotation_deg_about_up=info['rotation_deg']),
    )
    fbx = os.path.join(out_dir, f'{name}.fbx')
    export_fbx(fbx, name, objs)
    with open(os.path.join(work, 'entry.json'), 'w') as f:
        json.dump(dict(entry=entry, footprint_check=dict(area=foot['area'], circle_coverage=foot['circle_coverage'])), f, indent=1)
    log(f'{name}: wrote {fbx} ({time.time() - t0:.1f}s)')


# ============================================================================ Blender: verify stage
def fbx_header(path):
    """Read the FBX file itself (independent of the importer): axes, unit scale, every Model's
    local transform, and whether any Texture / Video (embedded or referenced) exists."""
    from io_scene_fbx import parse_fbx
    root, version = parse_fbx.parse(path)

    def child(e, key):
        for c in e.elems:
            if c.id == key:
                return c
        return None

    def props70(e):
        out = {}
        p70 = child(e, b'Properties70') if e is not None else None
        for p in (p70.elems if p70 else []):
            if p.id == b'P':
                k = p.props[0].decode(errors='replace')
                out[k] = list(p.props[4:])
        return out
    gs = props70(child(root, b'GlobalSettings'))
    objs = child(root, b'Objects')
    models, kinds = {}, {}
    for e in objs.elems:
        kinds[e.id.decode()] = kinds.get(e.id.decode(), 0) + 1
        if e.id == b'Model':
            nm = e.props[1].split(b'\x00\x01')[0].decode()
            pr = props70(e)
            models[nm] = dict(type=e.props[2].decode(), translation=pr.get('Lcl Translation', [0, 0, 0]),
                              rotation=pr.get('Lcl Rotation', [0, 0, 0]), scaling=pr.get('Lcl Scaling', [1, 1, 1]))
    return dict(version=version, UpAxis=gs.get('UpAxis', [None])[0], UpAxisSign=gs.get('UpAxisSign', [None])[0],
                FrontAxis=gs.get('FrontAxis', [None])[0], FrontAxisSign=gs.get('FrontAxisSign', [None])[0],
                CoordAxis=gs.get('CoordAxis', [None])[0], CoordAxisSign=gs.get('CoordAxisSign', [None])[0],
                UnitScaleFactor=gs.get('UnitScaleFactor', [None])[0], models=models, object_kinds=kinds)


def _inward_fraction(co, tri, lab):
    """Share of face area whose normal points into its own closed part (ray parity along the normal)."""
    tree = BVHTree.FromPolygons(co.tolist(), tri.tolist(), all_triangles=True)
    p = co[tri]
    n = np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0])
    area = np.linalg.norm(n, axis=1)
    n = n / np.maximum(area[:, None], 1e-20)
    cen = p.mean(1)
    eps = 1e-5 * float(np.linalg.norm(co.max(0) - co.min(0)))
    inward = np.zeros(len(tri), bool)
    for i in range(len(tri)):
        if area[i] < 1e-14:
            continue
        o = Vector(cen[i] + n[i] * eps)
        d = Vector(n[i])
        cnt = 0
        for _ in range(64):
            hit, _n, idx, _dd = tree.ray_cast(o, d)
            if hit is None:
                break
            if lab[idx] == lab[i]:
                cnt += 1
            o = hit + d * eps
        inward[i] = cnt & 1
    return float((area * inward).sum() / area.sum())


def _render_setup(res):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x = sc.render.resolution_y = res
    sc.render.film_transparent = True
    sc.view_settings.view_transform = 'Standard'
    world = bpy.data.worlds.new('w')
    sc.world = world
    bgn = world.node_tree.nodes['Background']
    bgn.inputs[0].default_value = (0.55, 0.57, 0.6, 1)
    bgn.inputs[1].default_value = 1.0
    sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN'))
    sc.collection.objects.link(sun)
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    sc.collection.objects.link(cam)
    cam.data.type = 'ORTHO'
    sc.camera = cam
    return cam


def _textured_material(name, out_dir):
    mat = bpy.data.materials.new(f'{name}_preview')
    nt = mat.node_tree
    bsdf = nt.nodes['Principled BSDF']
    tex = {}
    for k, f in (('color', 'BaseColor'), ('normal', 'Normal'), ('orm', 'ORM')):
        p = os.path.join(out_dir, f'{name}_{f}.png')
        if os.path.exists(p):
            n = nt.nodes.new('ShaderNodeTexImage')
            n.image = bpy.data.images.load(p)
            if k != 'color':
                n.image.colorspace_settings.name = 'Non-Color'
            tex[k] = n
    if 'color' in tex:
        nt.links.new(tex['color'].outputs['Color'], bsdf.inputs['Base Color'])
    if 'normal' in tex:
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nm.uv_map = 'UVMap'
        nt.links.new(tex['normal'].outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    if 'orm' in tex:
        sep = nt.nodes.new('ShaderNodeSeparateColor')
        nt.links.new(tex['orm'].outputs['Color'], sep.inputs['Color'])
        nt.links.new(sep.outputs['Green'], bsdf.inputs['Roughness'])
        nt.links.new(sep.outputs['Blue'], bsdf.inputs['Metallic'])
    return mat


def _backface_material():
    mat = bpy.data.materials.new('backface_check')
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    em = nt.nodes.new('ShaderNodeEmission')
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    mix = nt.nodes.new('ShaderNodeMix')
    mix.data_type = 'RGBA'
    mix.inputs['A'].default_value = (1, 1, 1, 1)
    mix.inputs['B'].default_value = (1, 0, 0, 1)
    nt.links.new(geo.outputs['Backfacing'], mix.inputs['Factor'])
    nt.links.new(mix.outputs['Result'], em.inputs['Color'])
    nt.links.new(em.outputs['Emission'], out.inputs['Surface'])
    return mat


def _aim(cam, az_deg, el_deg, pts, margin=1.06):
    """Orthographic camera from azimuth/elevation (azimuth 0 = Blender -Y = the model's front =
    Unity +Z side), framed on the points `pts` (same framing for every LOD of a view)."""
    a, e = math.radians(az_deg), math.radians(el_deg)
    d = np.array([math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)])
    right = np.array([math.cos(a), math.sin(a), 0.0])
    up = np.cross(right, -d)
    up /= np.linalg.norm(up)
    x, y = pts @ right, pts @ up
    cx, cy = 0.5 * (x.min() + x.max()), 0.5 * (y.min() + y.max())
    centre = right * cx + up * cy + d * float((pts @ d).mean())
    cam.location = Vector((centre + d * 20.0).tolist())
    cam.rotation_euler = Vector((-d).tolist()).to_track_quat('-Z', 'Y').to_euler()
    cam.data.ortho_scale = margin * max(np.ptp(x), np.ptp(y))
    cam.data.clip_end = 100.0


def verify_stage(args):
    name = args.name
    out_dir = os.path.join(args.out, name)
    work = os.path.join(args.work, name)
    fbx = os.path.join(out_dir, f'{name}.fbx')
    with open(os.path.join(work, 'entry.json')) as f:
        entry = json.load(f)['entry']
    bpy.ops.wm.read_factory_settings(use_empty=True)
    res = dict(name=name, problems=[])
    hdr = fbx_header(fbx)
    res['fbx_header'] = {k: v for k, v in hdr.items() if k != 'models'}
    res['fbx_models'] = hdr['models']
    exp_names = [name] + entry['lods']
    if sorted(hdr['models']) != sorted(exp_names):
        res['problems'].append(f'FBX models {sorted(hdr["models"])} != {sorted(exp_names)}')
    for mn, m in hdr['models'].items():
        if any(abs(v) > 1e-6 for v in m['translation']) or any(abs(v) > 1e-6 for v in m['rotation']) or \
                any(abs(v - 1) > 1e-6 for v in m['scaling']):
            res['problems'].append(f'{mn}: non-identity transform in file {m}')
    if (hdr['UpAxis'], hdr['UpAxisSign'], hdr['FrontAxis'], hdr['FrontAxisSign'], hdr['CoordAxis'], hdr['CoordAxisSign']) != (1, 1, 2, 1, 0, 1):
        res['problems'].append('FBX axis system is not Y-up / +Z front / +X right')
    if abs((hdr['UnitScaleFactor'] or 0) - 100.0) > 1e-6:
        res['problems'].append(f'UnitScaleFactor {hdr["UnitScaleFactor"]} (expected 100 = metres, Unity scale 1)')
    for k in ('Texture', 'Video'):
        if hdr['object_kinds'].get(k):
            res['problems'].append(f'FBX contains {hdr["object_kinds"][k]} {k} object(s)')
    # Blender import in this fresh session
    bpy.ops.import_scene.fbx(filepath=fbx)
    objs = {o.name: o for o in bpy.context.scene.objects}
    res['imported_objects'] = sorted(objs)
    root = objs.get(name)
    if root is None or root.type != 'EMPTY':
        res['problems'].append('root empty missing')
    lods = []
    for i, ln in enumerate(entry['lods']):
        o = objs.get(ln)
        if o is None or o.type != 'MESH':
            res['problems'].append(f'{ln} missing')
            continue
        if o.parent is not root:
            res['problems'].append(f'{ln} is not a child of {name}')
        lods.append(o)
    bpy.context.view_layer.update()
    checks = []
    for o, expected in zip(lods, entry['tris']):
        me = o.data
        M = np.array(o.matrix_world)
        co = _arr(me.vertices, 'co', 3)
        co_w = co @ M[:3, :3].T + M[:3, 3]  # Blender world (Z up, front -Y)
        me.calc_loop_triangles()
        tri = _arr(me.loop_triangles, 'vertices', 3, np.int64)
        co_u = to_unity(co_w)
        lab = _labels(len(co), tri)
        uvs = me.uv_layers
        uv = _arr(uvs[0].data, 'uv', 2) if len(uvs) else np.zeros((0, 2))
        # normals: corner normals vs face winding
        cn = _arr(me.corner_normals, 'vector', 3) @ M[:3, :3].T
        ls = _arr(me.polygons, 'loop_start', 1, np.int64)
        lt = _arr(me.polygons, 'loop_total', 1, np.int64)
        fn = _arr(me.polygons, 'normal', 3) @ M[:3, :3].T
        fa = _arr(me.polygons, 'area', 1)
        agree = np.array([np.dot(fn[i], cn[ls[i]:ls[i] + lt[i]].mean(0)) for i in range(len(ls))])
        worst = np.array([np.min(cn[ls[i]:ls[i] + lt[i]] @ fn[i]) for i in range(len(ls))])
        bm = bmesh.new()
        bm.from_mesh(me)
        nb = sum(1 for e in bm.edges if e.is_boundary)
        bm.free()
        if len(uv):
            lt_ = np.zeros(len(me.loop_triangles) * 3, np.int64)
            me.loop_triangles.foreach_get('loops', lt_)
            U = uv[lt_.reshape(-1, 3)]
            uv_cov = float((np.abs(np.cross(U[:, 1] - U[:, 0], U[:, 2] - U[:, 0])) / 2).sum())
        else:
            uv_cov = 0.0
        c = dict(name=o.name, tris=len(tri), expected_tris=expected, uv_layers=len(uvs), uv_coverage=round(uv_cov, 4),
                 uv_min=[float(v) for v in uv.min(0)] if len(uv) else None, uv_max=[float(v) for v in uv.max(0)] if len(uv) else None,
                 height=float(np.ptp(co_u[:, 1])), y_min=float(co_u[:, 1].min()), y_max=float(co_u[:, 1].max()),
                 size=[float(v) for v in np.ptp(co_u, 0)],
                 base_centre=[float(v) for v in slice_center(np.stack([co_u[:, 0], co_u[:, 2], co_u[:, 1]], 1), tri, SLICE_PIVOT)],
                 normals_disagree_area=float((fa * (agree < 0)).sum() / fa.sum()),
                 corner_normal_away_area=float((fa * (worst < 0)).sum() / fa.sum()),
                 normals_inward_area=_inward_fraction(co_w, tri, lab),
                 boundary_edges=nb)
        checks.append(c)
        if c['tris'] != expected:
            res['problems'].append(f'{o.name}: {c["tris"]} tris, expected {expected}')
        if not c['uv_layers']:
            res['problems'].append(f'{o.name}: no UVs')
        elif o.name.endswith('_LOD0') and c['uv_coverage'] < 0.2:
            res['problems'].append(f'{o.name}: UVs cover only {c["uv_coverage"]:.1%} of the texture')
        if abs(c['y_min']) > 1e-4 or (o.name.endswith('LOD0') and abs(c['height'] - 1) > 1e-4) or abs(c['height'] - 1) > 0.01:
            res['problems'].append(f'{o.name}: y range {c["y_min"]:.5f}..{c["y_max"]:.5f}')
        tol = 0.001 if o.name.endswith('_LOD0') else 0.02
        if max(abs(v) for v in c['base_centre']) > tol:
            res['problems'].append(f'{o.name}: base centre off pivot {c["base_centre"]}')
        if c['normals_disagree_area'] > 0.01:
            res['problems'].append(f'{o.name}: {c["normals_disagree_area"]:.1%} of the area has normals opposite to the faces')
    res['lods'] = checks
    if checks:
        base = checks[0]['corner_normal_away_area']
        for c in checks[1:]:
            if c['corner_normal_away_area'] > base + 0.005:
                res['problems'].append(f'{c["name"]}: {c["corner_normal_away_area"]:.1%} of the area has a corner normal '
                                       f'facing away from its face (LOD0 {base:.1%}) - dark shading')
    # textures
    res['textures'] = {}
    for f_, cs in (('BaseColor', 'sRGB'), ('Normal', 'Non-Color'), ('ORM', 'Non-Color')):
        p_ = os.path.join(out_dir, f'{name}_{f_}.png')
        if not os.path.exists(p_):
            res['problems'].append(f'missing {p_}')
            continue
        im = bpy.data.images.load(p_)
        res['textures'][f_] = dict(size=list(im.size), channels=im.channels, depth=im.depth)
        if tuple(im.size) != (args.tex, args.tex):
            res['problems'].append(f'{f_}: {tuple(im.size)} instead of {args.tex}x{args.tex}')
        bpy.data.images.remove(im)
    # renders
    for o in lods:
        o.data.materials.clear()
    tex_mat = _textured_material(name, out_dir)
    bf_mat = _backface_material()
    cam = _render_setup(args.res)
    sc = bpy.context.scene
    allco = np.concatenate([np.array([o.matrix_world @ v.co for v in o.data.vertices]) for o in lods])
    views = [('front', 0.0, 4.0), ('3/4 view', 45.0, 18.0)]
    tiles = [[None] * len(lods) for _ in views]
    os.makedirs(os.path.join(work, 'tiles'), exist_ok=True)
    for li, o in enumerate(lods):
        for x in lods:
            x.hide_render = x is not o
        o.data.materials.clear()
        o.data.materials.append(tex_mat)
        for vi, (vn, az, el) in enumerate(views):
            _aim(cam, az, el, allco)
            sc.render.filepath = os.path.join(work, 'tiles', f'tile_{vi}_{li}.png')
            bpy.ops.render.render(write_still=True)
            tiles[vi][li] = sc.render.filepath
    # back-face visibility from above-ground views (open shells / inside-out faces / holes)
    sc.render.resolution_x = sc.render.resolution_y = 320
    bf = {}
    for o in lods:
        for x in lods:
            x.hide_render = x is not o
        o.data.materials.clear()
        o.data.materials.append(bf_mat)
        red = tot = 0
        for az, el in ((0, 10), (90, 10), (180, 10), (270, 10), (45, 45), (225, 45)):
            _aim(cam, az, el, allco)
            sc.render.filepath = os.path.join(work, 'tiles', 'bf.png')
            bpy.ops.render.render(write_still=True)
            im = bpy.data.images.load(sc.render.filepath)
            px = np.array(im.pixels[:]).reshape(-1, 4)
            bpy.data.images.remove(im)
            cov = px[:, 3] > 0.5
            red += int((cov & (px[:, 1] < 0.5)).sum())
            tot += int(cov.sum())
        bf[o.name] = round(red / max(tot, 1), 4)
    res['backface_visible'] = bf
    meta = dict(tiles=tiles, views=[v[0] for v in views],
                lods=[dict(name=c['name'], tris=c['tris']) for c in checks],
                title=f'{name}  -  LOD0 / LOD1 / LOD2   (re-imported FBX, textures from the PNGs)',
                subtitle=(f'size w {entry["size"][0]:.3f}  h {entry["size"][1]:.3f}  d {entry["size"][2]:.3f}   '
                          f'front faces Unity {entry["facing"]}   footprint radius {entry["footprint"]["radius"]:.3f}'),
                footer='front view = the source image\'s view (camera on Unity +Z looking back at the plant); '
                       '3/4 view = 45 deg to the side, 18 deg up',
                out=os.path.join(args.preview_dir, f'{name}_lods_preview.png'))
    mp = os.path.join(work, 'preview_meta.json')
    with open(mp, 'w') as f:
        json.dump(meta, f)
    r = subprocess.run([args.python, SCRIPT, 'compose', '--meta', mp], capture_output=True, text=True)
    if r.returncode != 0:
        res['problems'].append('preview composition failed: ' + r.stderr[-400:])
    else:
        res['silhouette_iou'] = json.loads(r.stdout.strip().splitlines()[-1])
        for k, v in res['silhouette_iou'].items():
            if v < 0.9:
                res['problems'].append(f'{k}: silhouette IoU vs LOD0 only {v:.3f}')
    res['preview'] = meta['out']
    lod0_bf = bf.get(entry['lods'][0], 0)
    for k, v in bf.items():
        if v > lod0_bf + 0.01:
            res['problems'].append(f'{k}: {v:.1%} of pixels show back faces (LOD0 {lod0_bf:.1%}) - holes or flipped faces')
    with open(os.path.join(work, 'verify.json'), 'w') as f:
        json.dump(res, f, indent=1)
    log(f'{name}: verify done, {len(res["problems"])} problem(s)')
    for p in res['problems']:
        log(f'  PROBLEM {p}')


# ============================================================================ orchestration (system python)
def _dump_json(obj):
    """json.dumps with indent, but lists of numbers (and lists of short number lists) on one line."""
    txt = json.dumps(obj, indent=2)
    num = r'-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?'
    flat = re.compile(r'\[\s*(' + num + r'(?:,\s*' + num + r')*)\s*\]')
    txt = flat.sub(lambda m: '[' + ', '.join(x.strip() for x in m.group(1).split(',')) + ']', txt)
    return txt


def merge_json(out, entries, verifies):
    path = os.path.join(out, 'plants.json')
    data = dict(version=1, plants=[])
    if os.path.exists(path):
        try:
            with open(path) as f:
                data = json.load(f)
        except ValueError:
            pass
    by = {p['name']: p for p in data.get('plants', [])}
    for e in entries:
        v = verifies.get(e['name'], {})
        e = dict(e)
        bf = v.get('backface_visible', {})
        if bf:
            e['double_sided'] = bool(bf.get(e['lods'][0], 0) > 0.01)
            e['backface_visible_lod0'] = bf.get(e['lods'][0], 0)
        by[e['name']] = e
    data = dict(
        version=1,
        generator='Tools/plants_to_unity.py',
        units=('metres; every plant is exactly 1.0 tall (scale per placement); pivot = base centre at y=0; '
               'Unity axes: Y up, footprint circles are [x, z, r] in the prefab\'s local space, the model front '
               '(the Tripo source image view) faces "facing"; glow = {part: [y_min, y_max]} on the 0..1 height'),
        lod_note='tris = triangles per LOD (LOD0 = Tripo original); lod_error = silhouette+surface error in model units',
        plants=[by[k] for k in sorted(by)],
    )
    with open(path, 'w') as f:
        f.write(_dump_json(data) + '\n')
    return path


def run_stage(args):
    glbs = [os.path.abspath(g) for g in args.glbs]
    os.makedirs(args.work, exist_ok=True)
    jobs, entries, verifies = [], [], {}
    names = []
    for g in glbs:
        name = plant_name(g) if len(glbs) > 1 or not args.name else args.name
        names.append(name)
        cfg = plant_config(name)
        if cfg.get('front'):
            front, how = cfg['front'], 'plants_to_unity.json'
        elif args.front != 'auto':
            front, how = args.front, 'command line'
        else:
            img, _task = tripo_source_image(g)
            if img:
                front, scores = detect_front(g, img)
                how = f'auto vs {os.path.basename(img)}: ' + ', '.join(f'{k} {v:.3f}' for k, v in scores.items())
            else:
                front, how = '+X', 'default (Tripo P1 puts the input view on glTF +X); no source image found'
        log(f'{name}: front = glTF {front} ({how})')
        cmd = [args.blender, '-b', '--factory-startup', '-P', SCRIPT, '--', 'build', '--glb', g, '--name', name,
               # '=' form: a value like '-X' would otherwise be read as an option and fail to parse
               '--out', args.out, '--work', args.work, '--python', sys.executable, f'--front-axis={front}',
               f'--front-how={how}', f'--facing={args.facing}', '--tex', str(args.tex),
               '--ratios', ','.join(str(r) for r in args.ratios)]
        jobs.append((name, cmd, os.path.join(args.work, f'{name}_build.log')))

    def run_parallel(jobs, n):
        running, failed = [], []
        queue = list(jobs)
        while queue or running:
            while queue and len(running) < n:
                nm, cmd, lg = queue.pop(0)
                fh = open(lg, 'w')
                running.append((nm, subprocess.Popen(cmd, stdout=fh, stderr=subprocess.STDOUT), fh, lg))
            time.sleep(0.5)
            for item in list(running):
                nm, p, fh, lg = item
                if p.poll() is not None:
                    fh.close()
                    running.remove(item)
                    with open(lg) as f:
                        txt = f.read()
                    for line in txt.splitlines():
                        if line.startswith('[plants_to_unity]'):
                            print(line, flush=True)
                    if p.returncode != 0 or 'Traceback' in txt:
                        failed.append(nm)
                        print('\n'.join(txt.splitlines()[-25:]), flush=True)
        return failed
    t0 = time.time()
    failed = run_parallel(jobs, args.jobs)
    if failed:
        raise SystemExit(f'build failed for: {", ".join(failed)} (logs in {args.work})')
    if not args.no_verify:
        vjobs = []
        for g, name in zip(glbs, names):
            pdir = args.preview_dir or os.path.dirname(g)
            cmd = [args.blender, '-b', '--factory-startup', '-P', SCRIPT, '--', 'verify', '--name', name,
                   '--out', args.out, '--work', args.work, '--python', sys.executable, '--preview-dir', pdir,
                   '--tex', str(args.tex)]
            vjobs.append((name, cmd, os.path.join(args.work, f'{name}_verify.log')))
        failed = run_parallel(vjobs, args.jobs)
        if failed:
            raise SystemExit(f'verify failed for: {", ".join(failed)} (logs in {args.work})')
    for name in names:
        with open(os.path.join(args.work, name, 'entry.json')) as f:
            entries.append(json.load(f)['entry'])
        vp = os.path.join(args.work, name, 'verify.json')
        if os.path.exists(vp):
            with open(vp) as f:
                verifies[name] = json.load(f)
    path = merge_json(args.out, entries, verifies)
    log(f'plants.json updated: {path}')
    print()
    print(f'{"plant":14s} {"LOD0":>6s} {"LOD1":>6s} {"LOD2":>6s}  {"w x h x d":22s} {"radius":>7s}  glow')
    for e in entries:
        g = ', '.join(f'{k} {v[0]:.3f}-{v[1]:.3f}' for k, v in e['glow'].items())
        sz = ' x '.join(f'{v:.3f}' for v in e['size'])
        print(f'{e["name"]:14s} {e["tris"][0]:6d} {e["tris"][1]:6d} {e["tris"][2]:6d}  {sz:22s} {e["footprint"]["radius"]:7.3f}  {g}')
    probs = {n: v.get('problems', []) for n, v in verifies.items()}
    print()
    for n, p in probs.items():
        print(f'verify {n}: ' + ('OK' if not p else f'{len(p)} problem(s): ' + '; '.join(p)))
    log(f'done in {time.time() - t0:.0f}s')


def main(argv):
    ap = argparse.ArgumentParser(prog='plants_to_unity.py')
    sub = ap.add_subparsers(dest='cmd', required=True)
    r = sub.add_parser('run', help='convert GLBs (system python3; launches Blender)')
    r.add_argument('glbs', nargs='+')
    r.add_argument('--out', default=DEFAULT_OUT)
    r.add_argument('--preview-dir', default=None, help='default: the folder of each GLB')
    r.add_argument('--work', default=os.path.join(os.environ.get('TMPDIR', '/tmp'), 'plants_to_unity_work'))
    r.add_argument('--blender', default=os.environ.get('BLENDER', DEFAULT_BLENDER))
    r.add_argument('--front', default='auto', choices=['auto', '+X', '-X', '+Z', '-Z'], help='glTF axis the model front faces')
    r.add_argument('--facing', default='+Z', choices=['+Z', '-Z'], help='Unity axis the front should face')
    r.add_argument('--tex', type=int, default=2048)
    r.add_argument('--ratios', type=lambda s: [float(x) for x in s.split(',')], default=[0.30, 0.08])
    r.add_argument('--name', default=None, help='output name (single GLB only)')
    r.add_argument('--jobs', type=int, default=max(1, min(5, (os.cpu_count() or 4) // 3)))
    r.add_argument('--no-verify', action='store_true')
    b = sub.add_parser('build')
    for k in ('--glb', '--name', '--out', '--work', '--python', '--front-axis', '--front-how', '--facing'):
        b.add_argument(k, required=k not in ('--front-how',), default='')
    b.add_argument('--tex', type=int, default=2048)
    b.add_argument('--ratios', type=lambda s: [float(x) for x in s.split(',')], default=[0.30, 0.08])
    v = sub.add_parser('verify')
    for k in ('--name', '--out', '--work', '--python', '--preview-dir'):
        v.add_argument(k, required=True)
    v.add_argument('--res', type=int, default=640)
    v.add_argument('--tex', type=int, default=2048)
    t = sub.add_parser('textures')
    for k in ('--npz', '--glb', '--out-dir', '--name'):
        t.add_argument(k, required=True)
    t.add_argument('--size', type=int, default=2048)
    c = sub.add_parser('compose')
    c.add_argument('--meta', required=True)
    args = ap.parse_args(argv)
    if args.cmd == 'run':
        run_stage(args)
    elif args.cmd == 'build':
        build_stage(args)
    elif args.cmd == 'verify':
        verify_stage(args)
    elif args.cmd == 'textures':
        transfer_textures(args.npz, args.glb, args.out_dir, args.name, args.size)
    elif args.cmd == 'compose':
        print(json.dumps(compose_preview(args.meta)))


if __name__ == '__main__':
    if IN_BLENDER:
        argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
        try:
            main(argv)
        except SystemExit as e:
            if e.code not in (0, None):
                print(f'[plants_to_unity] ERROR: {e}', flush=True)
                sys.stdout.flush()
                os._exit(1)
        except Exception:
            import traceback
            traceback.print_exc()
            sys.stdout.flush()
            os._exit(1)
    else:
        main(sys.argv[1:])
