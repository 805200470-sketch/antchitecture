# 4K -> 2K Unity textures for Ant_00N. Reads the source PNGs + source FBX (UVs, vertex colours); writes only to the
# Unity Art/Ants/Ant_00N folder (atomic rename) and the scratch folder. Never touches the sources.
#   BaseColor: 2x2 box filter in linear light, sRGB 8-bit RGB.  (Ant_003: wing-membrane charts repainted from the
#              'Col' vertex colours, because the source bake darkened them by the 0.34 alpha.)
#   Normal:    2x2 average of decoded vectors, renormalised, 8-bit RGB, OpenGL (+Y) kept (curl test, see normal_probe.py).
#   Roughness: 2x2 box filter, 8-bit RGB (R=G=B, as in the source).
#   Atlas background (BaseColor black + Roughness 0, ~55% of the atlas) is push-pull padded from the charts so mipmaps
#   and decimated LOD triangles that straddle UV charts never sample black / mirror-smooth texels.
# usage: Blender -b --factory-startup -P textures.py -- Ant_00N
import bpy, sys, os, json, time, zlib, struct, numpy as np

AID = sys.argv[sys.argv.index("--") + 1]
RES = f"/Users/xujiafeng/Desktop/AntGarden/03_Models/Ants_Source.nosync/{AID}_Resource"
SCR = "/Users/xujiafeng/Desktop/AntGarden/Tools/ants/work"
OUT = f"/Users/xujiafeng/Desktop/AntGarden/UnityProject.nosync/Assets/AntGarden/Art/Ants/{AID}"
os.makedirs(OUT, exist_ok=True)
T0 = time.time()
stats = {"ant": AID}
def log(*a):
    print("[TEX %5.1fs]" % (time.time() - T0), *a, flush=True)

def load_bytes(path):
    img = bpy.data.images.load(path, check_existing=False)
    img.colorspace_settings.name = 'Non-Color'          # raw byte values, no colour transform
    w, h = img.size
    px = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return np.round(px.reshape(h, w, 4) * 255.0).astype(np.uint8)[::-1, :, :3]   # row 0 = top (PNG order)

def write_png(path, arr):
    """8-bit RGB/Gray PNG writer (no gamma/ICC chunks), 'Up' filter, atomic rename."""
    arr = np.ascontiguousarray(arr)
    h, w = arr.shape[:2]
    c = 1 if arr.ndim == 2 else arr.shape[2]
    rows = arr.reshape(h, w * c).astype(np.int16)
    up = rows.copy(); up[1:] -= rows[:-1]
    filt = np.empty((h, w * c + 1), np.uint8)
    filt[:, 0] = 2; filt[:, 1:] = (up % 256).astype(np.uint8)
    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, {1: 0, 3: 2, 4: 6}[c], 0, 0, 0)) \
        + chunk(b"IDAT", zlib.compress(filt.tobytes(), 9)) + chunk(b"IEND", b"")
    d, b = os.path.split(path)
    tmp = os.path.join(d, "." + b + ".tmp")
    with open(tmp, "wb") as f:
        f.write(png)
    os.replace(tmp, path)
    return len(png)

def srgb_to_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
def lin_to_srgb(c):
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(np.clip(c, 0, None), 1 / 2.4) - 0.055)
def half(a):
    return 0.25 * (a[0::2, 0::2] + a[1::2, 0::2] + a[0::2, 1::2] + a[1::2, 1::2])

def half_w(a, w):
    """2x2 reduction averaging only valid texels (w in {0,1}); returns (avg, coverage)."""
    if a.ndim == 2:
        a = a[..., None]
    s = half(a * w[..., None]); c = half(w.astype(np.float64))
    return np.where(c[..., None] > 0, s / np.maximum(c[..., None], 1e-12), 0.0), c

def push_pull(img, w):
    """Fill texels with w == 0 from the surrounding valid texels (pyramid push-pull). img HxWxC float, w HxW in [0,1]."""
    levels = []
    c = img * w[..., None]; ww = w.astype(np.float64)
    while min(ww.shape) > 1:
        levels.append((c, ww))
        h2, w2 = ww.shape[0] // 2, ww.shape[1] // 2
        c = (c[0:2*h2:2, 0:2*w2:2] + c[1:2*h2:2, 0:2*w2:2] + c[0:2*h2:2, 1:2*w2:2] + c[1:2*h2:2, 1:2*w2:2])
        ww = (ww[0:2*h2:2, 0:2*w2:2] + ww[1:2*h2:2, 0:2*w2:2] + ww[0:2*h2:2, 1:2*w2:2] + ww[1:2*h2:2, 1:2*w2:2])
        k = np.maximum(ww, 1.0)
        c = c / k[..., None]; ww = ww / k            # premultiplied colour, weight clamped to 1
    filled = c / np.maximum(ww[..., None], 1e-12)
    for c_l, w_l in reversed(levels):
        up = np.repeat(np.repeat(filled, 2, 0), 2, 1)[:w_l.shape[0], :w_l.shape[1]]
        filled = c_l + (1.0 - w_l[..., None]) * up      # c_l is premultiplied by w_l
    return filled

# ------------------------------------------------------------------ base colour
bc4b = load_bytes(f"{RES}/textures/{AID}_BaseColor.png")
rg4b = load_bytes(f"{RES}/textures/{AID}_Roughness.png")
valid4 = ~(((bc4b == 0).all(2)) & (rg4b[..., 0] == 0))     # chart texels incl. the 6 px bake margin
stats["atlas_used_fraction_4k"] = round(float(valid4.mean()), 4)
bc_lin2, cov2 = half_w(srgb_to_lin(bc4b.astype(np.float64) / 255.0), valid4)
valid2 = cov2 > 0
bc2 = lin_to_srgb(bc_lin2)
bc2 = np.clip(np.round(bc2 * 255.0), 0, 255).astype(np.uint8)
H, W = bc2.shape[:2]
log("basecolor", bc4b.shape, "->", bc2.shape, "atlas used (4K)", stats["atlas_used_fraction_4k"])

# ------------------------------------------------------------------ Ant_003: repaint the wing-membrane charts from 'Col'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=f"{RES}/{AID}.fbx", use_anim=False, use_image_search=False)
ob = [o for o in bpy.data.objects if o.type == 'MESH'][0]
me = ob.data
mat_names = [s.material.name.split(".")[0] for s in ob.material_slots]
stats["fbx_material_slots"] = mat_names
if f"A{AID[4:]}_WingMembrane" in mat_names:
    k_mem = mat_names.index(f"A{AID[4:]}_WingMembrane")
    me.calc_loop_triangles()
    nt = len(me.loop_triangles)
    tl = np.empty(nt * 3, np.int32); me.loop_triangles.foreach_get("loops", tl); tl = tl.reshape(-1, 3)
    tp = np.empty(nt, np.int32); me.loop_triangles.foreach_get("polygon_index", tp)
    pmi = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get("material_index", pmi)
    tm = pmi[tp]
    uv = np.empty(len(me.loops) * 2, np.float32); me.uv_layers[0].data.foreach_get("uv", uv); uv = uv.reshape(-1, 2)
    col = np.empty(len(me.loops) * 4, np.float32); me.color_attributes["Col"].data.foreach_get("color_srgb", col); col = col.reshape(-1, 4)[:, :3]
    # pixel coordinates (x right, y down from the top row)
    P = np.stack([uv[:, 0] * W, (1.0 - uv[:, 1]) * H], 1)
    # 1) coverage of every OTHER material (point samples: corners, centroid, edge midpoints, inner points)
    other = np.zeros((H, W), bool)
    sel = tm != k_mem
    A, B, C = P[tl[sel][:, 0]], P[tl[sel][:, 1]], P[tl[sel][:, 2]]
    for wa, wb, wc in ((1, 0, 0), (0, 1, 0), (0, 0, 1), (1/3, 1/3, 1/3), (.5, .5, 0), (0, .5, .5), (.5, 0, .5), (.6, .2, .2), (.2, .6, .2), (.2, .2, .6)):
        p = wa * A + wb * B + wc * C
        other[np.clip(p[:, 1].astype(int), 0, H - 1), np.clip(p[:, 0].astype(int), 0, W - 1)] = True
    # 2) exact rasterisation of the membrane triangles with barycentric colour interpolation
    paint = np.zeros((H, W, 3), np.float64); mask = np.zeros((H, W), bool)
    idx = np.nonzero(tm == k_mem)[0]
    for t in idx:
        l = tl[t]
        a, b, c = P[l[0]], P[l[1]], P[l[2]]
        x0, x1 = int(np.floor(min(a[0], b[0], c[0]))), int(np.ceil(max(a[0], b[0], c[0])))
        y0, y1 = int(np.floor(min(a[1], b[1], c[1]))), int(np.ceil(max(a[1], b[1], c[1])))
        x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, W - 1), min(y1, H - 1)
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(d) < 1e-12:
            continue
        w0 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / d
        w1 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / d
        w2 = 1 - w0 - w1
        e = -1e-3
        ins = (w0 >= e) & (w1 >= e) & (w2 >= e)
        if not ins.any():
            # sliver smaller than a texel: stamp its centroid
            cx, cy = int((a[0] + b[0] + c[0]) / 3), int((a[1] + b[1] + c[1]) / 3)
            if 0 <= cx < W and 0 <= cy < H and not mask[cy, cx]:
                paint[cy, cx] = col[l].mean(0); mask[cy, cx] = True
            continue
        cc = w0[ins, None] * col[l[0]] + w1[ins, None] * col[l[1]] + w2[ins, None] * col[l[2]]
        yy = ys[ins].astype(int); xx = xs[ins].astype(int)
        paint[yy, xx] = cc; mask[yy, xx] = True
    # 3) dilate the painted colours 3 px into texels that belong to no other chart (bake gutter)
    protect = other.copy()
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        protect |= np.roll(other, (dy, dx), (0, 1))
    grown = mask.copy(); acc = paint.copy()
    for it in range(3):
        nsum = np.zeros_like(acc); ncnt = np.zeros((H, W))
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)):
            m = np.roll(grown, (dy, dx), (0, 1)); v = np.roll(acc, (dy, dx), (0, 1))
            nsum += v * m[..., None]; ncnt += m
        new = (~grown) & (ncnt > 0) & (~protect)
        acc[new] = nsum[new] / ncnt[new, None]; grown |= new
    before = bc2[mask].astype(float).mean(0)
    bc2[grown] = np.clip(np.round(acc[grown] * 255.0), 0, 255).astype(np.uint8)
    stats["membrane_repaint"] = {"texels_inside": int(mask.sum()), "texels_dilated": int((grown & ~mask).sum()),
                                 "old_mean_srgb": before.round(1).tolist(), "new_mean_srgb": bc2[mask].astype(float).mean(0).round(1).tolist(),
                                 "vcol_mean_srgb": (col[tl[idx]].reshape(-1, 3).mean(0) * 255).round(1).tolist()}
    log("membrane repaint", stats["membrane_repaint"])
    # mean vertex colour per wing material (sRGB 0-1) for the FBX material tint
    for k in range(len(mat_names)):
        sel = tm == k
        stats.setdefault("vcol_mean_srgb_per_material", {})[mat_names[k]] = col[tl[sel]].reshape(-1, 3).mean(0).round(4).tolist()

if "membrane_repaint" in stats:
    valid2 = valid2 | grown
pad = push_pull(srgb_to_lin(bc2.astype(np.float64) / 255.0), valid2.astype(np.float64))
bc2[~valid2] = np.clip(np.round(lin_to_srgb(pad[~valid2]) * 255.0), 0, 255).astype(np.uint8)
stats["padded_texels_2k"] = int((~valid2).sum())
log("padded background texels", stats["padded_texels_2k"], "of", valid2.size)
n_bc = write_png(f"{OUT}/{AID}_BaseColor.png", bc2)
log("wrote BaseColor", n_bc)

# ------------------------------------------------------------------ normal
nm4 = load_bytes(f"{RES}/textures/{AID}_Normal.png").astype(np.float64) / 255.0 * 2.0 - 1.0
nm2, _ = half_w(nm4, valid4)
nm2[~valid2] = (0.0, 0.0, 1.0)
nm2 /= np.maximum(np.linalg.norm(nm2, axis=2, keepdims=True), 1e-8)
nm2 = np.clip(np.round((nm2 * 0.5 + 0.5) * 255.0), 0, 255).astype(np.uint8)
n_nm = write_png(f"{OUT}/{AID}_Normal.png", nm2)
stats["normal_mean_rgb"] = nm2.reshape(-1, 3).mean(0).round(2).tolist()
log("wrote Normal", n_nm, "mean", stats["normal_mean_rgb"])

# ------------------------------------------------------------------ roughness
rg4 = rg4b.astype(np.float64)
assert (rg4[..., 0] == rg4[..., 1]).all() and (rg4[..., 1] == rg4[..., 2]).all()
rg_avg, _ = half_w(rg4[..., 0], valid4)
rg_pad = push_pull(rg_avg, valid2.astype(np.float64))
rg_full = np.where(valid2[..., None], rg_avg, rg_pad)[..., 0]
rg2 = np.clip(np.round(rg_full), 0, 255).astype(np.uint8)
n_rg = write_png(f"{OUT}/{AID}_Roughness.png", np.repeat(rg2[..., None], 3, 2))
stats["roughness_mean_charts"] = round(float(rg2[valid2].mean()) / 255.0, 4)
log("wrote Roughness", n_rg, "mean (charts)", stats["roughness_mean_charts"])

# ------------------------------------------------------------------ read back through Blender (independent decoder) to validate the files
for kind, ref in (("BaseColor", bc2), ("Normal", nm2), ("Roughness", np.repeat(rg2[..., None], 3, 2))):
    back = load_bytes(f"{OUT}/{AID}_{kind}.png")
    assert back.shape == ref.shape and (back == ref).all(), kind
stats["files"] = {k: os.path.getsize(f"{OUT}/{AID}_{k}.png") for k in ("BaseColor", "Normal", "Roughness")}
stats["size"] = [W, H]
json.dump(stats, open(f"{SCR}/tex_stats_{AID}.json", "w"), indent=1)
log("DONE", stats["files"])
