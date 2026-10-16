# Verify Ant_00N.fbx in a FRESH Blender session (factory settings): raw FBX parse + re-import + pose checks + renders.
# usage: Blender -b --factory-startup -P verify_unity.py -- Ant_00N
import bpy, sys, os, json, math, time, numpy as np
from mathutils import Vector, Matrix

AID = sys.argv[sys.argv.index("--") + 1]
NUM = AID.split("_")[1]
SCR = "/Users/xujiafeng/Desktop/AntGarden/Tools/ants/work"
OUT = f"/Users/xujiafeng/Desktop/AntGarden/UnityProject.nosync/Assets/AntGarden/Art/Ants/{AID}"
PREVIEW = f"/Users/xujiafeng/Desktop/AntGarden/03_Models/Ants/{AID}/unity_lods_preview.png"
QA = f"{SCR}/qa_{AID}"
os.makedirs(QA, exist_ok=True)
FBX = f"{OUT}/{AID}.fbx"
ex = json.load(open(f"{SCR}/export_stats_{AID}.json"))
BL = ex["body"]["bodyLength"]
T0 = time.time()
out = {"ant": AID}
def log(*a):
    print("[VER %6.1fs]" % (time.time() - T0), *a, flush=True)

# ============================================================================== 1. raw FBX structure
from io_scene_fbx import parse_fbx
root, version = parse_fbx.parse(FBX)
def name_of(e):
    return e.props[1].split(b"\x00\x01")[0].decode()
def child(e, eid):
    return [c for c in e.elems if c.id == eid]
def p70(e):
    d = {}
    for pp in child(e, b"Properties70"):
        for p in pp.elems:
            d[p.props[0].decode()] = p.props[4:] if len(p.props) > 4 else None
    return d
gs = p70(child(root, b"GlobalSettings")[0])
out["fbx_version"] = version
out["global_settings"] = {k: (gs[k][0] if gs.get(k) else None) for k in ("UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign", "CoordAxis", "CoordAxisSign", "UnitScaleFactor", "OriginalUnitScaleFactor", "TimeMode", "CustomFrameRate")}
objs = child(root, b"Objects")[0]
models = {e.props[0]: (name_of(e), e.props[2].decode()) for e in objs.elems if e.id == b"Model"}
geoms = {e.props[0]: e for e in objs.elems if e.id == b"Geometry"}
mats = {e.props[0]: name_of(e) for e in objs.elems if e.id == b"Material"}
stacks = [e for e in objs.elems if e.id == b"AnimationStack"]
conns = [(c.props[0].decode(), c.props[1], c.props[2]) for c in child(root, b"Connections")[0].elems]
KT = 46186158000
take_info = []
for st in stacks:
    pr = p70(st)
    t0 = pr.get("LocalStart", [0])[0]; t1 = pr.get("LocalStop", [0])[0]
    take_info.append({"name": name_of(st), "start_s": t0 / KT, "stop_s": t1 / KT, "frames_at_24": round((t1 - t0) / KT * 24, 4)})
out["takes_raw"] = take_info
mesh_models = {uid: n for uid, (n, t) in models.items() if t == "Mesh"}
geo_of = {}; mats_of = {}
for kind, a, b in conns:
    if kind == "OO" and a in geoms and b in mesh_models:
        geo_of[b] = a
    if kind == "OO" and a in mats and b in mesh_models:
        mats_of.setdefault(b, []).append(mats[a])
raw_meshes = {}
for uid, n in mesh_models.items():
    g = geoms[geo_of[uid]]
    pvi = np.array(child(g, b"PolygonVertexIndex")[0].props[0])
    ends = np.nonzero(pvi < 0)[0]
    sizes = np.diff(np.concatenate([[-1], ends]))
    nverts = len(child(g, b"Vertices")[0].props[0]) // 3
    layers = sorted({c.id.decode() for c in g.elems if c.id.startswith(b"LayerElement")})
    raw_meshes[n] = {"triangles": int((sizes == 3).sum()), "non_tri_polys": int((sizes != 3).sum()), "control_points": nverts,
                     "materials": mats_of.get(uid, []), "layers": layers}
out["meshes_raw"] = raw_meshes
# raw LOD0 control points are in FBX world space (top-level nodes are identity): Unity = (-x, y, z)
for uid, n in mesh_models.items():
    if n.endswith("_LOD0"):
        v = np.array(child(geoms[geo_of[uid]], b"Vertices")[0].props[0]).reshape(-1, 3)
        umin = np.array([-v[:, 0].max(), v[:, 1].min(), v[:, 2].min()]); umax = np.array([-v[:, 0].min(), v[:, 1].max(), v[:, 2].max()])
        rb0 = np.array(ex["body"]["rest_bounds_unity_min"]); rb1 = np.array(ex["body"]["rest_bounds_unity_max"])
        out["lod0_raw_bounds_unity"] = {"min": umin.round(4).tolist(), "max": umax.round(4).tolist(),
                                        "max_diff_vs_full_mesh_rest_bounds": float(max(np.abs(umin - rb0).max(), np.abs(umax - rb1).max())),
                                        "faces_plus_z": bool(umax[2] > 0.5 and umax[2] > abs(ex["body"]["gasterTipZ"]) * 0.5), "feet_at_y0": bool(abs(umin[1]) < 0.05)}
        log("LOD0 raw bounds (Unity axes)", out["lod0_raw_bounds_unity"])
limbs = [n for n, t in models.values() if t == "LimbNode"]
nulls = [n for n, t in models.values() if t == "Null"]
out["bones_raw"] = {"count": len(limbs), "names": limbs, "leaf_end_bones": [n for n in limbs if n.endswith("_end")], "null_nodes": nulls}
clusters = [e for e in objs.elems if e.id == b"Deformer" and e.props[2] == b"Cluster"]
skins = [e for e in objs.elems if e.id == b"Deformer" and e.props[2] == b"Skin"]
out["skin_raw"] = {"skins": len(skins), "clusters": len(clusters)}
# local transforms of top-level nodes (what Unity sees on the GameObjects)
top = {}
parents = {a: b for k, a, b in conns if k == "OO"}
for e in objs.elems:
    if e.id == b"Model" and parents.get(e.props[0]) == 0:
        pr = p70(e)
        top[name_of(e)] = {k: list(pr[k][:3]) for k in ("Lcl Translation", "Lcl Rotation", "Lcl Scaling") if k in pr}
out["top_level_nodes"] = top
for e in objs.elems:   # default (prefab) pose of the root bone: must be the rest pose (0,0,0)
    if e.id == b"Model" and name_of(e) in ("root", "COG"):
        pr = p70(e)
        out.setdefault("default_pose", {})[name_of(e)] = {k: [round(x, 5) for x in pr[k][:3]] for k in ("Lcl Translation", "Lcl Rotation") if k in pr}
log("default pose (FBX node transforms)", out.get("default_pose"))
log("global settings", out["global_settings"])
log("takes (raw AnimStack names)", [(t["name"], t["frames_at_24"]) for t in take_info])
log("meshes (raw)", raw_meshes)
log("bones (raw)", out["bones_raw"]["count"], "leaf _end bones", out["bones_raw"]["leaf_end_bones"], "nulls", nulls)
log("top-level nodes", top)

# ============================================================================== 2. re-import (fresh empty scene)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 24
bpy.ops.import_scene.fbx(filepath=FBX, use_anim=True)
arms = [o for o in scene.objects if o.type == 'ARMATURE']
meshes = sorted([o for o in scene.objects if o.type == 'MESH' and len(o.vertex_groups)], key=lambda o: o.name)
assert len(arms) == 1, arms
arm = arms[0]
log("imported", [(o.name, o.type, o.parent.name if o.parent else None) for o in scene.objects])
lod_names = [f"{AID}_LOD{i}" for i in range(3)]
assert [o.name for o in meshes] == lod_names, [o.name for o in meshes]
imp = {}
for o in meshes:
    o.data.calc_loop_triangles()
    mods = [(m.type, m.object.name if getattr(m, "object", None) else None) for m in o.modifiers]
    imp[o.name] = {"tris": len(o.data.loop_triangles), "verts": len(o.data.vertices), "materials": [s.material.name if s.material else None for s in o.material_slots],
                   "vertex_groups": len(o.vertex_groups), "modifiers": mods, "uv_layers": [u.name for u in o.data.uv_layers],
                   "scale": [round(x, 6) for x in o.matrix_world.to_scale()]}
out["reimport_meshes"] = imp
out["reimport_armature"] = {"name": arm.name, "bones": len(arm.data.bones), "scale": [round(x, 6) for x in arm.matrix_world.to_scale()],
                            "dimensions": [round(x, 4) for x in arm.dimensions]}
log("reimport meshes", imp)
log("armature", out["reimport_armature"])
acts = {}
for a in bpy.data.actions:
    acts[a.name.split("|")[-1]] = a
out["reimport_actions"] = {k: [a.name, list(a.frame_range)] for k, a in acts.items()}
log("actions", out["reimport_actions"])
if arm.animation_data:
    for t in arm.animation_data.nla_tracks:
        t.mute = True

def use(take):
    a = acts[take]
    arm.animation_data.action = a
    slots = [s for s in a.slots if s.target_id_type == 'OBJECT']
    if arm.animation_data.action_slot is None or arm.animation_data.action_slot.identifier not in [s.identifier for s in slots]:
        arm.animation_data.action_slot = slots[0]
    return a

def deformed(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg); m = ev.to_mesh()
    c = np.empty(len(m.vertices) * 3); m.vertices.foreach_get("co", c); ev.to_mesh_clear()
    M = np.array(ob.matrix_world)
    return c.reshape(-1, 3) @ M[:3, :3].T + M[:3, 3]

def doms(ob):
    d = np.full(len(ob.data.vertices), -1)
    for v in ob.data.vertices:
        if len(v.groups):
            d[v.index] = max(v.groups, key=lambda g: g.weight).group
    return d
vg = {o.name: [g.name for g in o.vertex_groups] for o in meshes}
dom = {o.name: doms(o) for o in meshes}
weights_ok = {}
for o in meshes:
    ng = np.array([len([g for g in v.groups if g.weight > 0]) for v in o.data.vertices])
    mw = np.array([max([g.weight for g in v.groups], default=0) for v in o.data.vertices])
    weights_ok[o.name] = {"unweighted": int((ng == 0).sum()), "multi": int((ng > 1).sum()), "w_not_1": int((~np.isclose(mw, 1.0, atol=1e-4)).sum())}
out["reimport_weights"] = weights_ok
log("weights", weights_ok)

# ---------------- rest
arm.animation_data.action = None
for pb in arm.pose.bones:
    pb.matrix_basis.identity()
scene.frame_set(1)
rest = {o.name: deformed(o) for o in meshes}
c0 = np.array([o.data.vertices[0].co for o in meshes[:1]])
rest_bb = {n: [r.min(0).round(4).tolist(), r.max(0).round(4).tolist()] for n, r in rest.items()}
out["rest_bbox_blender"] = rest_bb

# ---------------- foot tips vs the original full mesh (Crawl_InPlace frames 1/8/15)
# tip = the full mesh's claw-tip vertex (farthest from the tarsus head at rest); LOD deviation = distance from that
# point to the nearest vertex of the same tarsus part in the LOD (posed). Also the sole height (lowest foot point).
tars = [b for b in vg[lod_names[0]] if "tarsus" in b]
reft = ex["reference"]["foot_tips_crawl_inplace"]["Crawl_InPlace"]
use("Crawl_InPlace")
foot = {o.name: [] for o in meshes}; sole = {o.name: [] for o in meshes}
for f in (1, 8, 15):
    scene.frame_set(f)
    for o in meshes:
        d = deformed(o)
        for tb in tars:
            vs = d[dom[o.name] == vg[o.name].index(tb)]
            tip = np.array(reft[str(f)]["tips"][tb])
            foot[o.name].append((f, tb, float(np.linalg.norm(vs - tip, axis=1).min())))
            sole[o.name].append((f, tb, float(vs[:, 2].min() - reft[str(f)]["sole_z"][tb])))
footsum = {}
for n in foot:
    worst = max(foot[n], key=lambda x: x[2]); ws = max(sole[n], key=lambda x: abs(x[2]))
    footsum[n] = {"tip_max_dev": worst[2], "tip_max_dev_BL": worst[2] / BL, "tip_mean_dev_BL": float(np.mean([x[2] for x in foot[n]])) / BL,
                  "tip_worst": [worst[0], worst[1]], "sole_height_max_dev_BL": abs(ws[2]) / BL, "sole_worst": [ws[0], ws[1], ws[2]],
                  "per_frame_tip_max_BL": {f: max(x[2] for x in foot[n] if x[0] == f) / BL for f in (1, 8, 15)}}
out["foot_tip_deviation_crawl_inplace_f1_8_15"] = footsum
log("foot tips vs full mesh", footsum)

# ---------------- baked bones vs the source rig (every take, start/mid/end + loop-closing frame)
bref = ex["reference"]["bones"]
bake = {}
for take, frames in bref.items():
    if take not in acts:
        bake[take] = "MISSING"; continue
    use(take)
    errs = []
    for f, bones in frames.items():
        scene.frame_set(int(f))
        for b, (h, t) in bones.items():
            pb = arm.pose.bones[b]
            # FBX stores joints, not tails: compare the head and a virtual tail = head + bone Y axis * source length
            M = arm.matrix_world @ pb.matrix
            hh = np.array(M.translation); yax = np.array(M.col[1][:3]); yax /= np.linalg.norm(yax)
            L = float(np.linalg.norm(np.array(t) - np.array(h)))
            errs.append(float(np.linalg.norm(hh - np.array(h))))
            errs.append(float(np.linalg.norm(hh + yax * L - np.array(t))))
    bake[take] = {"max_err": max(errs), "max_err_BL": max(errs) / BL}
out["bake_vs_source_rig"] = bake
log("bake vs source rig", {k: (round(v["max_err_BL"], 7) if isinstance(v, dict) else v) for k, v in bake.items()})

# ---------------- clips: frame ranges, loop closure, root travel
clips = {}
for c in ex["clips"]:
    take = c["take"]
    a = use(take)
    f0, f1 = int(round(a.frame_range[0])), int(round(a.frame_range[1]))
    def pose(fr):
        scene.frame_set(fr)
        rootm = np.array(arm.matrix_world @ arm.pose.bones["root"].matrix)
        rel = {b.name: np.linalg.inv(rootm) @ np.array(arm.matrix_world @ b.matrix) for b in arm.pose.bones}
        return rootm, rel
    r0, p0 = pose(f0); r1, p1 = pose(f1)
    loop_err = max(float(np.abs(p0[k] - p1[k]).max()) for k in p0)
    clips[take] = {"frames": [f0, f1], "n_frames": f1 - f0, "loop": c["loop"], "pose_first_vs_last_maxdiff": loop_err,
                   "root_travel": (r1[:3, 3] - r0[:3, 3]).round(5).tolist()}
out["clips"] = clips
log("clips", {k: (v["frames"], round(v["pose_first_vs_last_maxdiff"], 6), v["root_travel"]) for k, v in clips.items()})

# ---------------- all LODs deform together: per-bone part centroid consistency + bbox agreement
together = {}
checks = [("Crawl", None), ("FastCrawl_InPlace", None), ("AntennaTalk", None), ("Idle", None)] + ([("Fly", None), ("TakeOff", None), ("Land", None)] if "Fly" in acts else [])
rest_cent = {o.name: {} for o in meshes}
for o in meshes:
    for gi, b in enumerate(vg[o.name]):
        sel = dom[o.name] == gi
        if sel.any():
            rest_cent[o.name][b] = rest[o.name][sel].mean(0)
worst_all = 0.0
for take, _ in checks:
    if take not in acts:
        continue
    a = use(take)
    f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
    w = 0.0; bbd = 0.0
    for fr in (f0, (f0 + f1) // 2, f1):
        scene.frame_set(fr)
        d = {o.name: deformed(o) for o in meshes}
        for b in rest_cent[lod_names[0]]:
            pb = arm.pose.bones[b]
            M = np.array(arm.matrix_world @ pb.matrix @ pb.bone.matrix_local.inverted() @ arm.matrix_world.inverted())
            for o in meshes[1:]:
                gi = vg[o.name].index(b) if b in vg[o.name] else None
                if gi is None or b not in rest_cent[o.name]:
                    continue
                sel = dom[o.name] == gi
                pred = M[:3, :3] @ rest_cent[o.name][b] + M[:3, 3]
                w = max(w, float(np.linalg.norm(d[o.name][sel].mean(0) - pred)))
                # and LOD0 too
                gi0 = vg[lod_names[0]].index(b); sel0 = dom[lod_names[0]] == gi0
                pred0 = M[:3, :3] @ rest_cent[lod_names[0]][b] + M[:3, 3]
                w = max(w, float(np.linalg.norm(d[lod_names[0]][sel0].mean(0) - pred0)))
        bbs = np.array([[d[n].min(0), d[n].max(0)] for n in lod_names])
        bbd = max(bbd, float(np.abs(bbs - bbs[0]).max()))
    together[take] = {"part_rigidity_err_BL": w / BL, "bbox_max_diff_vs_LOD0_BL": bbd / BL}
    worst_all = max(worst_all, w)
out["lods_deform_together"] = together
log("LODs deform together", together)
json.dump(out, open(f"{SCR}/verify_stats_{AID}.json", "w"), indent=1)

# ============================================================================== 3. renders
eng = None
for e in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
    try:
        scene.render.engine = e; eng = e; break
    except TypeError:
        continue
try:
    scene.eevee.taa_render_samples = 48
except Exception:
    pass
scene.view_settings.view_transform = 'Standard'
scene.render.film_transparent = False
world = bpy.data.worlds.new("W"); scene.world = world
world.use_nodes = True
bgn = world.node_tree.nodes["Background"]; bgn.inputs["Color"].default_value = (0.20, 0.21, 0.23, 1); bgn.inputs["Strength"].default_value = 0.9
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN')); scene.collection.objects.link(sun)
sun.data.energy = 3.2; sun.data.angle = math.radians(3)
sun.rotation_euler = (math.radians(48), 0, math.radians(-38))
fill = bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", 'SUN')); scene.collection.objects.link(fill)
fill.data.energy = 0.8; fill.rotation_euler = (math.radians(60), 0, math.radians(140))
for o in scene.objects:
    if o.type == 'MESH' and not o.vertex_groups:
        o.hide_render = True

def img(kind, cs):
    i = bpy.data.images.load(f"{OUT}/{AID}_{kind}.png", check_existing=True); i.colorspace_settings.name = cs; return i
I_bc, I_nm, I_rg = img("BaseColor", "sRGB"), img("Normal", "Non-Color"), img("Roughness", "Non-Color")
def shader(name, alpha=1.0, rough=None):
    m = bpy.data.materials.new("R_" + name); m.use_nodes = True; nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    o_ = nt.nodes.new("ShaderNodeOutputMaterial"); bs = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bs.outputs[0], o_.inputs[0])
    tb = nt.nodes.new("ShaderNodeTexImage"); tb.image = I_bc; nt.links.new(tb.outputs[0], bs.inputs["Base Color"])
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = I_nm; nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tn.outputs[0], nm.inputs["Color"]); nt.links.new(nm.outputs[0], bs.inputs["Normal"])
    if rough is None:
        tr = nt.nodes.new("ShaderNodeTexImage"); tr.image = I_rg; nt.links.new(tr.outputs[0], bs.inputs["Roughness"])
    else:
        bs.inputs["Roughness"].default_value = rough
    bs.inputs["Alpha"].default_value = alpha
    if alpha < 1:
        m.surface_render_method = 'BLENDED'; m.use_backface_culling = False
    return m
rmats = {}
for o in meshes:
    for s in o.material_slots:
        base = s.material.name.split(".")[0]
        if base not in rmats:
            if "WingMembrane" in base:
                rmats[base] = shader(base, 0.34, 0.12)
            elif "WingVein" in base:
                rmats[base] = shader(base, 0.9, 0.3)
            elif base.endswith("_Eye"):
                rmats[base] = shader(base, 1.0, 0.2)
            else:
                rmats[base] = shader(base)
        s.material = rmats[base]

def bounds_all():
    co = np.vstack([deformed(o) for o in meshes])
    return co.min(0), co.max(0)
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); scene.collection.objects.link(cam); scene.camera = cam
ground = None

def setup_ground(zmin, center):
    global ground
    if ground is None:
        bpy.ops.mesh.primitive_plane_add(size=40)
        ground = bpy.context.active_object
        gm = bpy.data.materials.new("G"); gm.use_nodes = True
        gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.42, 0.40, 0.37, 1)
        gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
        ground.data.materials.append(gm)
    ground.location = (center[0], center[1], zmin)

def aim(direction, center, radius, lens=60, mul=1.08):
    cam.data.lens = lens
    d = Vector(direction).normalized()
    fov = 2 * math.atan(36 / 2 / lens)
    cam.location = Vector(center) + d * (radius * mul / math.sin(fov / 2))
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()

def label(text, size=0.06):
    cu = bpy.data.curves.new("L", 'FONT'); cu.body = text; cu.size = size; cu.align_x = 'CENTER'
    t = bpy.data.objects.new("Label", cu); scene.collection.objects.link(t)
    half_w = (cam.data.sensor_width / 2) / cam.data.lens
    half_h = half_w * scene.render.resolution_y / scene.render.resolution_x
    t.parent = cam; t.location = (0, -0.86 * half_h, -1.0)
    cu.size = 0.075 * half_h
    lm = bpy.data.materials.new("LM"); lm.use_nodes = True
    nt = lm.node_tree; [nt.nodes.remove(n) for n in list(nt.nodes)]
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = (1, 1, 1, 1); em.inputs[1].default_value = 1.0
    oo = nt.nodes.new("ShaderNodeOutputMaterial"); nt.links.new(em.outputs[0], oo.inputs[0])
    cu.materials.append(lm)
    return t

def render_each(path_tmpl, res=(900, 760), labels=True):
    scene.render.resolution_x, scene.render.resolution_y = res
    files = []
    for i, o in enumerate(meshes):
        for oo in meshes:
            oo.hide_render = oo is not o
        lb = label(f"LOD{i}  {imp[o.name]['tris']:,} tris") if labels else None
        scene.render.filepath = path_tmpl.format(i)
        bpy.ops.render.render(write_still=True)
        files.append(scene.render.filepath)
        if lb:
            bpy.data.objects.remove(lb)
    for oo in meshes:
        oo.hide_render = False
    return files

def stitch(files, dst):
    ims = []
    for f in files:
        im = bpy.data.images.load(f, check_existing=False)
        w, h = im.size
        px = np.empty(w * h * 4, np.float32); im.pixels.foreach_get(px)
        ims.append(px.reshape(h, w, 4)); bpy.data.images.remove(im)
    gap = np.ones((ims[0].shape[0], 6, 4), np.float32) * np.array([0.1, 0.1, 0.1, 1], np.float32)
    row = np.concatenate(sum([[im, gap] for im in ims], [])[:-1], axis=1)
    H, W = row.shape[:2]
    o = bpy.data.images.new("sheet", W, H, alpha=False)
    o.pixels.foreach_set(row.ravel())
    o.filepath_raw = dst; o.file_format = 'PNG'
    o.save()
    bpy.data.images.remove(o)
    for f in files:
        os.remove(f)
    log("wrote", dst, os.path.getsize(dst))

# main contact sheet: Crawl, mid-cycle, 3/4 front view
a = use("Crawl")
fr = int(round((a.frame_range[0] + a.frame_range[1]) / 2))
scene.frame_set(fr)
mn, mx = bounds_all()
center = (mn + mx) / 2; radius = float(np.linalg.norm(mx - mn)) / 2
setup_ground(float(mn[2]) - 0.002, center)
aim((0.95, -1.2, 0.85), center, radius)
os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
stitch(render_each(QA + "/_p{}.png"), PREVIEW)
# QA extras (scratch only): side, top, front-low close, legs close, plus flight poses for the winged ant
views = [("side", (1, 0, 0.12), 1.02, 50), ("top", (0.001, 0.0, 1), 1.02, 50), ("front_low", (0.25, -1, 0.25), 0.62, 70), ("back", (-0.8, 1.2, 0.7), 1.0, 50)]
for nm_, dirv, mul, lens in views:
    aim(dirv, center, radius, lens=lens, mul=mul)
    stitch(render_each(QA + "/_q{}.png", res=(700, 560)), f"{QA}/{nm_}.png")
head = np.array(arm.matrix_world @ arm.pose.bones["head"].head)
aim((0.9, -1.1, 0.45), head + np.array([0.25, -0.15, -0.35]), radius * 0.42, lens=70, mul=1.0)
stitch(render_each(QA + "/_q{}.png", res=(700, 560)), f"{QA}/closeup.png")
if "Fly" in acts:
    for take, frx in (("Fly", 3), ("TakeOff", 40), ("Land", 60), ("FastFly", 5)):
        a = use(take); scene.frame_set(int(a.frame_range[0]) + frx)
        mn, mx = bounds_all(); center = (mn + mx) / 2; radius = float(np.linalg.norm(mx - mn)) / 2
        ground.hide_render = True
        aim((0.9, -1.0, 0.8), center, radius)
        stitch(render_each(QA + "/_q{}.png", res=(700, 560)), f"{QA}/{take.lower()}.png")
log("VERIFY DONE")
