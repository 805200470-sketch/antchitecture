# Ant_00N_Resource.blend -> Unity FBX with 3 skinned LODs (Ant_00N_LOD0/1/2) on one armature, bare-named takes.
# Method reused from glb_export_npc/export_npc.py: in-memory edits only, the .blend is NEVER saved;
# UVs copied from the delivered FBX (identical topology, verified); rigid per-part skinning kept.
# Writes: <Unity>/Assets/AntGarden/Art/Ants/Ant_00N/Ant_00N.fbx (atomic rename) + stats JSON in the scratch folder.
# usage: Blender -b --factory-startup <Ant_00N_Resource.blend> -P export_unity.py -- Ant_00N [nofbx]
import bpy, bmesh, numpy as np, json, os, sys, time, math
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
AID = argv[0]
OPTS = set(argv[1:])
NUM = AID.split("_")[1]
MP = "A" + NUM
SCR = "/Users/xujiafeng/Desktop/AntGarden/Tools/ants/work"
OUT = f"/Users/xujiafeng/Desktop/AntGarden/UnityProject.nosync/Assets/AntGarden/Art/Ants/{AID}"
RES = f"/Users/xujiafeng/Desktop/AntGarden/03_Models/Ants_Source.nosync/{AID}_Resource"
FBX_SRC = f"{RES}/{AID}.fbx"
LODS = [("LOD0", 60000), ("LOD1", 15000), ("LOD2", 4000)]
T0 = time.time()
stats = {"ant": AID, "blender": bpy.app.version_string}

def log(*a):
    print("[EXP %6.1fs]" % (time.time() - T0), *a, flush=True)

scene = bpy.context.scene
rig = bpy.data.objects[f"{AID}_Rig"]
body = bpy.data.objects[f"{AID}_Body"]
fps = scene.render.fps / scene.render.fps_base
stats["fps"] = fps
assert abs(fps - 24) < 1e-6, fps
assert abs(scene.unit_settings.scale_length - 1.0) < 1e-9 and scene.unit_settings.system == 'METRIC'
assert rig.matrix_world == Matrix.Identity(4) and body.matrix_world == Matrix.Identity(4)
B2U = lambda v: [-float(v[0]), float(v[2]), -float(v[1])]   # Blender (Z-up, ant faces -Y) -> Unity (Y-up, ant faces +Z)
acts = {a.name for a in bpy.data.actions}
A = lambda s: f"{AID}_{s}"
HAS_WINGS = any(s.material and "Wing" in s.material.name for s in body.material_slots)
stats["has_wings"] = HAS_WINGS

def set_action(name):
    act = bpy.data.actions[name]
    rig.animation_data.action = act
    if rig.animation_data.action_slot is None:
        rig.animation_data.action_slot = act.slots[0]
    return act

def bone_head(b):
    return np.array(rig.matrix_world @ rig.pose.bones[b].head)

def period(short):
    a = bpy.data.actions[A(short)]
    return int(round(a.frame_range[1] - a.frame_range[0] + 1))

# =====================================================================================================  1. source rig
log("actions", sorted(acts))
orig_action = rig.animation_data.action
travel = {}
for short in ("Crawl", "FastCrawl", "Crawl_InPlace", "FastCrawl_InPlace", "Fly", "Fly_InPlace", "FastFly", "FastFly_InPlace"):
    if A(short) not in acts:
        continue
    T = period(short)
    set_action(A(short))
    pos = []
    for f in range(1, T + 2):
        scene.frame_set(f)
        pos.append(bone_head("root"))
    pos = np.array(pos)
    d = pos[-1] - pos[0]
    travel[short] = {"cycle_frames": T, "cycle_seconds": T / fps, "root_travel_blender": d.round(6).tolist(),
                     "root_travel_unity": [round(x, 6) for x in B2U(d)], "travel_per_cycle": float(np.linalg.norm(d)),
                     "speed_units_per_s": float(np.linalg.norm(d)) / (T / fps),
                     "root_height_range": [float(pos[:, 2].min()), float(pos[:, 2].max())]}
    log("cycle", short, travel[short])
for short in ("TakeOff", "Land"):
    if A(short) not in acts:
        continue
    a = set_action(A(short))
    f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
    pos, cog = [], []
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        pos.append(bone_head("root")); cog.append(bone_head("COG"))
    pos = np.array(pos); cog = np.array(cog)
    travel[short] = {"frames": [f0, f1], "root_start_unity": B2U(pos[0]), "root_end_unity": B2U(pos[-1]),
                     "root_displacement_unity": B2U(pos[-1] - pos[0]), "forward_travel": float(-(pos[-1, 1] - pos[0, 1])),
                     "root_height_start": float(pos[0, 2]), "root_height_end": float(pos[-1, 2]), "root_height_max": float(pos[:, 2].max()),
                     "cog_height_start": float(cog[0, 2]), "cog_height_end": float(cog[-1, 2])}
    log("one-shot", short, travel[short])
stats["root_motion"] = travel

# ---------- rest-pose geometry of the FULL source mesh (bodyLength etc.), foot-tip vertices
rig.animation_data.action = None
for pb in rig.pose.bones:
    pb.matrix_basis.identity()
scene.frame_set(1)
me = body.data
vg_names = [g.name for g in body.vertex_groups]
nv = len(me.vertices)
dom = np.full(nv, -1, np.int32)
for v in me.vertices:
    if len(v.groups):
        dom[v.index] = max(v.groups, key=lambda g: g.weight).group
co = np.empty(nv * 3); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
gmask = lambda names: np.isin(dom, [vg_names.index(n) for n in names if n in vg_names])
mand = gmask(["mandible.L", "mandible.R"]); gast = gmask(["gaster_2"])
mand_y = float(co[mand, 1].min()); gast_y = float(co[gast, 1].max())
BL = gast_y - mand_y
nonwing = ~gmask([n for n in vg_names if n.startswith("wing")])
noant = nonwing & ~gmask([n for n in vg_names if n.startswith("antenna")])
stats["body"] = {"bodyLength": BL, "mandibleTipZ": -mand_y, "gasterTipZ": -gast_y, "bodyCenterZ": -(mand_y + gast_y) / 2,
                 "height": float(co[:, 2].max()), "height_without_antennae_wings": float(co[noant, 2].max()),
                 "rest_bounds_unity_min": np.minimum(B2U(co.min(0)), B2U(co.max(0))).tolist(),
                 "rest_bounds_unity_max": np.maximum(B2U(co.min(0)), B2U(co.max(0))).tolist(),
                 "rest_lowest_point_y": float(co[:, 2].min()), "antenna_tip_z": float(-co[gmask([n for n in vg_names if n.startswith('antenna')]), 1].min())}
log("body", stats["body"])
TARSI = [n for n in vg_names if "tarsus" in n]
tips = {}
for tb in TARSI:
    vs = np.nonzero(dom == vg_names.index(tb))[0]
    h = np.array(rig.data.bones[tb].head_local)
    i = vs[np.argmax(np.linalg.norm(co[vs] - h, axis=1))]
    tips[tb] = int(i)
stats["foot_tip_vertex_full"] = tips
part_centroid_rest = {vg_names[g]: co[dom == g].mean(0).tolist() for g in np.unique(dom)}

def deformed(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg); m = ev.to_mesh()
    c = np.empty(len(m.vertices) * 3); m.vertices.foreach_get("co", c)
    ev.to_mesh_clear()
    M = np.array(ob.matrix_world)
    return c.reshape(-1, 3) @ M[:3, :3].T + M[:3, 3]

# reference foot tips + bone heads at Crawl_InPlace frames 1/8/15 (blender coords) from the FULL mesh
ref = {"Crawl_InPlace": {}}
set_action(A("Crawl_InPlace"))
for f in (1, 8, 15):
    scene.frame_set(f)
    d = deformed(body)
    ref["Crawl_InPlace"][f] = {"tips": {tb: d[i].tolist() for tb, i in tips.items()},
                               "sole_z": {tb: float(d[dom == vg_names.index(tb), 2].min()) for tb in TARSI},
                               "parts": {vg_names[g]: d[dom == g].mean(0).tolist() for g in np.unique(dom)}}
# bone reference for the baked takes (compared after re-import)
bone_ref = {}
REF_BONES = ["root", "COG", "thorax", "head", "gaster_2", "leg_front_tarsus.L", "leg_mid_tarsus.R", "leg_hind_tarsus.L", "antenna_2b.R", "mandible.L", "prop"] + \
            (["wing_fore.L", "wing_hind.R"] if HAS_WINGS else [])
for short in ("Idle", "Crawl", "Crawl_InPlace", "FastCrawl", "FastCrawl_InPlace", "AntennaTalk", "TakeOff", "Fly", "Fly_InPlace", "FastFly", "FastFly_InPlace", "Land"):
    if A(short) not in acts:
        continue
    a = set_action(A(short))
    f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
    frames = sorted({f0, (f0 + f1) // 2, f1, f1 + (1 if a.use_cyclic else 0)})
    bone_ref[short] = {}
    for f in frames:
        scene.frame_set(f)
        bone_ref[short][f] = {b: (np.array(rig.matrix_world @ rig.pose.bones[b].head).tolist(), np.array(rig.matrix_world @ rig.pose.bones[b].tail).tolist()) for b in REF_BONES}
rig.animation_data.action = None
for pb in rig.pose.bones:
    pb.matrix_basis.identity()
scene.frame_set(1)
log("reference poses recorded")

# =====================================================================================================  2. UVs from the FBX
before = set(bpy.data.objects)
snap = {k: set(getattr(bpy.data, k)) for k in ("meshes", "armatures", "materials", "images", "actions")}
bpy.ops.import_scene.fbx(filepath=FBX_SRC, use_anim=False, use_image_search=False)
new_objs = [o for o in bpy.data.objects if o not in before]
fm = [o for o in new_objs if o.type == 'MESH'][0].data
assert len(fm.loops) == len(me.loops) and len(fm.polygons) == len(me.polygons)
la = np.empty(len(me.loops), np.int32); lb = np.empty(len(me.loops), np.int32)
me.loops.foreach_get("vertex_index", la); fm.loops.foreach_get("vertex_index", lb)
assert (la == lb).all(), "loop order differs"
pa = np.empty(len(me.polygons), np.int32); pb_ = np.empty(len(me.polygons), np.int32)
me.polygons.foreach_get("material_index", pa); fm.polygons.foreach_get("material_index", pb_)
assert (pa == pb_).all()
uv = np.empty(len(me.loops) * 2, np.float32); fm.uv_layers["UVMap"].data.foreach_get("uv", uv)
for o in new_objs:
    bpy.data.objects.remove(o, do_unlink=True)
for k, old in snap.items():
    coll = getattr(bpy.data, k)
    for idb in [i for i in coll if i not in old]:
        coll.remove(idb)
for l in list(me.uv_layers):
    me.uv_layers.remove(l)
uvl = me.uv_layers.new(name="UVMap"); uvl.data.foreach_set("uv", uv)
me.uv_layers.active = uvl
for ca in list(me.color_attributes):
    me.color_attributes.remove(ca)          # Unity does not need them (wing colour now lives in the texture)
log("UVMap copied from FBX", len(uv) // 2, "loops")

# =====================================================================================================  3. LOD meshes
def tri_count(m):
    m.calc_loop_triangles(); return len(m.loop_triangles)

def islands(m):
    n = len(m.vertices)
    ed = np.empty(len(m.edges) * 2, np.int32); m.edges.foreach_get("vertices", ed); ed = ed.reshape(-1, 2)
    lab = np.arange(n)
    while True:
        mm = np.minimum(lab[ed[:, 0]], lab[ed[:, 1]]); old = lab.copy()
        np.minimum.at(lab, ed[:, 0], mm); np.minimum.at(lab, ed[:, 1], mm); lab = lab[lab]
        if (lab == old).all():
            break
    return np.unique(lab, return_inverse=True)[1]

def drop_islands(ob, pred, why):
    m = ob.data
    inv = islands(m)
    c = np.empty(len(m.vertices) * 3); m.vertices.foreach_get("co", c); c = c.reshape(-1, 3)
    pl = np.empty(len(m.polygons), np.int32); m.polygons.foreach_get("loop_start", pl)
    lv = np.empty(len(m.loops), np.int32); m.loops.foreach_get("vertex_index", lv)
    fis = inv[lv[pl]]
    nf = np.bincount(fis, minlength=inv.max() + 1)
    kill = []
    for i in range(inv.max() + 1):
        vs = inv == i
        ext = c[vs].max(0) - c[vs].min(0)
        if pred(ext, int(nf[i])):
            kill.append(i)
    if not kill:
        return 0
    bm = bmesh.new(); bm.from_mesh(m)
    bm.verts.ensure_lookup_table()
    killv = set(np.nonzero(np.isin(inv, kill))[0].tolist())
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in killv], context='VERTS')
    bm.to_mesh(m); bm.free(); m.update()
    log(f"  {ob.name}: dropped {len(kill)} islands ({why})")
    return len(kill)

def weight_dom(m, ob):
    n = len(m.vertices)
    d = np.full(n, -1, np.int32); ng = np.zeros(n, np.int32); mw = np.zeros(n, np.float32)
    for v in m.vertices:
        bg, bw, k = -1, 0.0, 0
        for g in v.groups:
            if g.weight > 0:
                k += 1
                if g.weight > bw:
                    bg, bw = g.group, g.weight
        d[v.index] = bg; ng[v.index] = k; mw[v.index] = bw
    return d, ng, mw

def decimate_object(ob, target, tag):
    """Collapse-decimate ob (Armature modifier disabled while counting) to <= target tris, as close as possible."""
    t0 = tri_count(ob.data)
    if t0 <= target:
        return
    dec = ob.modifiers.new("Decimate", 'DECIMATE')
    dec.decimate_type = 'COLLAPSE'; dec.use_collapse_triangulate = True
    dec.use_symmetry = False   # L/R parts are not vertex-exact mirrors: symmetric collapse stalls at ~8.6k
    ob.modifiers.move(ob.modifiers.find("Decimate"), 0)
    arms = [m for m in ob.modifiers if m.type == 'ARMATURE']
    for a in arms:
        a.show_viewport = False
    ratio = target / t0
    best = None
    for attempt in range(8):
        dec.ratio = ratio
        dg = bpy.context.evaluated_depsgraph_get()
        ev = ob.evaluated_get(dg); em = ev.to_mesh(); t = tri_count(em); ev.to_mesh_clear()
        log(f"  {tag} ratio {ratio:.5f} -> {t} tris (target {target})")
        if t <= target and (best is None or t > best[1]):
            best = (ratio, t)
        if 0.985 * target <= t <= target:
            break
        ratio *= target / t * (0.997 if t > target else 1.0)
    dec.ratio = best[0] if best else ratio
    for a in arms:
        a.show_viewport = True
    with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob], selected_editable_objects=[ob]):
        bpy.ops.object.modifier_apply(modifier="Decimate")

def keep_bones(ob, pred):
    """Delete every face whose (rigid) bone does not satisfy pred."""
    names = [g.name for g in ob.vertex_groups]
    bm = bmesh.new(); bm.from_mesh(ob.data)
    dl = bm.verts.layers.deform.active
    dead = []
    for f in bm.faces:
        d = f.verts[0][dl]
        if not pred(names[max(d.items(), key=lambda kv: kv[1])[0]]):
            dead.append(f)
    bmesh.ops.delete(bm, geom=dead, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(ob.data); bm.free(); ob.data.update()

# LOD2 budget split: global QEM spends most of a 4k budget on the big smooth body plates and flattens the domed ends
# of the thin leg/antenna segments (visible joint gaps). LOD2 therefore decimates limbs, body and wings separately.
THIN = lambda b: b.startswith(("leg", "antenna", "mandible"))
WING = lambda b: b.startswith("wing")
BODY = lambda b: not THIN(b) and not WING(b)
def lod_split(label):
    if label != "LOD2":
        return None
    if HAS_WINGS:
        return [("limbs", THIN, 0.88 * 0.55), ("body", BODY, 0.88 * 0.45), ("wings", WING, 0.12)]
    return [("limbs", THIN, 0.55), ("body", BODY, 0.45)]

def make_lod(label, target):
    name = f"{AID}_{label}"
    split = lod_split(label)
    parts = []
    for gname, pred, frac in (split or [("all", lambda b: True, 1.0)]):
        ob = clean.copy(); ob.data = clean.data.copy()
        ob.name = f"{name}_{gname}"; ob.data.name = ob.name
        for c in clean.users_collection:
            c.objects.link(ob)
        if split:
            keep_bones(ob, pred)
        parts.append((ob, int(round(target * frac)), gname))
    dropped = 0
    t0 = sum(tri_count(o.data) for o, _, _ in parts)
    for ob, tgt, gname in parts:
        dropped += drop_islands(ob, lambda ext, nf: ext.max() < 1e-3, "degenerate, < 1 mm")
        if label == "LOD2":   # Ant_005's 64+64 single-triangle mandible teeth cannot be decimated; sub-pixel at LOD2 range
            dropped += drop_islands(ob, lambda ext, nf: nf <= 2, "1-2 triangle islands (LOD2)")
        decimate_object(ob, tgt, f"{name}/{gname}")
    ob = parts[0][0]
    if len(parts) > 1:
        objs = [p[0] for p in parts]
        with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=objs, selected_editable_objects=objs):
            bpy.ops.object.join()
    ob.name = name; ob.data.name = name
    # remove loose edges / vertices left by the collapse (they would become unused FBX control points)
    bm = bmesh.new(); bm.from_mesh(ob.data)
    le = [e for e in bm.edges if not e.link_faces]
    if le:
        bmesh.ops.delete(bm, geom=le, context='EDGES')
    lv = [v for v in bm.verts if not v.link_faces]
    if lv:
        bmesh.ops.delete(bm, geom=lv, context='VERTS')
    bm.to_mesh(ob.data); bm.free(); ob.data.update()
    loose_removed = (len(le), len(lv))
    best = (round(tri_count(ob.data) / t0, 6), None)
    m = ob.data
    # rigid weights: every vertex -> its dominant group at 1.0
    d, ng, mw = weight_dom(m, ob)
    fixed = 0
    for vi in np.nonzero((ng != 1) | (~np.isclose(mw, 1.0)))[0]:
        g = int(d[vi])
        for gg in [x.group for x in m.vertices[vi].groups]:
            ob.vertex_groups[gg].remove([int(vi)])
        ob.vertex_groups[g].add([int(vi)], 1.0, 'REPLACE'); fixed += 1
    m.validate(verbose=False, clean_customdata=False)
    d, ng, mw = weight_dom(m, ob)
    inv = islands(m)
    pl = np.empty(len(m.polygons), np.int32); m.polygons.foreach_get("loop_start", pl)
    lv = np.empty(len(m.loops), np.int32); m.loops.foreach_get("vertex_index", lv)
    mixed = sum(len(np.unique(d[inv == i])) > 1 for i in range(inv.max() + 1))
    mi = np.empty(len(m.polygons), np.int32); m.polygons.foreach_get("material_index", mi)
    # degenerate triangles
    m.calc_loop_triangles()
    tv = np.empty(len(m.loop_triangles) * 3, np.int32); m.loop_triangles.foreach_get("vertices", tv); tv = tv.reshape(-1, 3)
    c = np.empty(len(m.vertices) * 3); m.vertices.foreach_get("co", c); c = c.reshape(-1, 3)
    area = 0.5 * np.linalg.norm(np.cross(c[tv[:, 1]] - c[tv[:, 0]], c[tv[:, 2]] - c[tv[:, 0]]), axis=1)
    per_bone = {}
    tdom = d[tv[:, 0]]
    for g in np.unique(tdom):
        per_bone[ob.vertex_groups[int(g)].name] = int((tdom == g).sum())
    info = {"target": target, "tris": len(m.loop_triangles), "verts": len(m.vertices), "ratio": best[0], "split": [(g, t) for _, t, g in parts] if split else None, "loose_removed_edges_verts": loose_removed,
            "src_tris": t0, "islands": int(inv.max() + 1), "islands_mixed_bones": int(mixed), "unweighted": int((ng == 0).sum()),
            "multi_group": int((ng > 1).sum()), "rigidified": fixed, "dropped_islands": dropped,
            "faces_per_material": {ob.material_slots[k].name: int((mi == k).sum()) for k in range(len(ob.material_slots))},
            "degenerate_tris(<1e-9)": int((area < 1e-9).sum()), "tris_per_bone": per_bone}
    log(f"  {name}", {k: v for k, v in info.items() if k != "tris_per_bone"})
    return ob, info

# ---------- clean decimation source: weld at 0.1 mm INSIDE each island removes the ~4k micro-triangles (edges ~5e-5)
# that the collapse decimator cannot remove. Per island (not per bone): welding touching plates of one bone created
# non-manifold edges on Ant_004's thorax and stalled the decimator.
from collections import defaultdict
clean = body.copy(); clean.data = body.data.copy(); clean.name = f"{AID}_clean"
for c in body.users_collection:
    c.objects.link(clean)
isl_src = islands(clean.data)
bm = bmesh.new(); bm.from_mesh(clean.data); bm.verts.ensure_lookup_table()
groups = defaultdict(list)
for v in bm.verts:
    groups[int(isl_src[v.index])].append(v)
nv0 = len(bm.verts)
for g, vs in groups.items():
    bmesh.ops.remove_doubles(bm, verts=vs, dist=1e-4)
bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=bm.edges[:])
bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
nv1 = len(bm.verts)
bm.to_mesh(clean.data); bm.free(); clean.data.update()
def edge_topo(m):
    _bm = bmesh.new(); _bm.from_mesh(m)
    r = {"boundary": sum(1 for e in _bm.edges if len(e.link_faces) == 1), "over2faces": sum(1 for e in _bm.edges if len(e.link_faces) > 2),
         "wire": sum(1 for e in _bm.edges if len(e.link_faces) == 0)}
    _bm.free(); return r
topo0, topo1 = edge_topo(body.data), edge_topo(clean.data)
stats["weld"] = {"verts_before": nv0, "verts_after": nv1, "tris_after": tri_count(clean.data), "edges_source": topo0, "edges_welded": topo1}
assert topo1["over2faces"] - topo0["over2faces"] < 50 and topo1["boundary"] - topo0["boundary"] < 50, (topo0, topo1)
log("weld", stats["weld"])

lods = []
stats["lods"] = {}
for label, target in LODS:
    ob, info = make_lod(label, target)
    lods.append(ob)
    stats["lods"][ob.name] = info
log("LODs built")

# =====================================================================================================  4. materials (Unity-facing names)
def load_img(kind, colorspace):
    img = bpy.data.images.load(f"{OUT}/{AID}_{kind}.png", check_existing=False)
    img.colorspace_settings.name = colorspace
    img.alpha_mode = 'NONE'
    return img
img_bc, img_nm, img_rg = load_img("BaseColor", "sRGB"), load_img("Normal", "Non-Color"), load_img("Roughness", "Non-Color")

src_mats = [s.material for s in body.material_slots]
src_names = [m.name for m in src_mats]
src_info = {}
for m in src_mats:
    bs = [n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'][0]
    src_info[m.name] = {"alpha": float(bs.inputs["Alpha"].default_value),
                        "roughness": None if bs.inputs["Roughness"].is_linked else float(bs.inputs["Roughness"].default_value)}
for m in src_mats:
    m.name = m.name + "_src"

def make_mat(name, alpha=1.0, rough_const=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial"); bs = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bs.outputs["BSDF"], out.inputs["Surface"])
    bs.inputs["Base Color"].default_value = (1, 1, 1, 1)      # FBX DiffuseColor = white, so Unity does not tint the texture
    bs.inputs["Metallic"].default_value = 0.0
    uvn = nt.nodes.new("ShaderNodeUVMap"); uvn.uv_map = "UVMap"
    def tex(img):
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = img
        nt.links.new(uvn.outputs["UV"], t.inputs["Vector"]); return t
    tb = tex(img_bc); nt.links.new(tb.outputs["Color"], bs.inputs["Base Color"])
    tn = tex(img_nm); nmap = nt.nodes.new("ShaderNodeNormalMap"); nmap.uv_map = "UVMap"
    nt.links.new(tn.outputs["Color"], nmap.inputs["Color"]); nt.links.new(nmap.outputs["Normal"], bs.inputs["Normal"])
    if rough_const is None:
        tr = tex(img_rg); nt.links.new(tr.outputs["Color"], bs.inputs["Roughness"])
    else:
        bs.inputs["Roughness"].default_value = rough_const
    bs.inputs["Alpha"].default_value = alpha
    if alpha < 1.0:
        m.surface_render_method = 'BLENDED'
        m.use_backface_culling = False
    return m

new_mats = []
for base in src_names:
    kind = base.split("_", 1)[1]           # Shell / Eye / WingMembrane / WingVein
    si = src_info[base]
    if kind == "Shell":
        nm = make_mat(f"{MP}_Baked")
    elif kind == "Eye":
        nm = make_mat(f"{MP}_Eye")         # the baked atlas carries the eye colour/facets (checked: tex_probe)
    else:
        nm = make_mat(base, alpha=si["alpha"], rough_const=si["roughness"])
    new_mats.append(nm)
for ob in lods:
    for k, nm in enumerate(new_mats):
        ob.material_slots[k].material = nm
stats["materials"] = [{"slot": k, "name": nm.name, "source": src_names[k], "alpha": float(nm.node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value)}
                      for k, nm in enumerate(new_mats)]
log("materials", stats["materials"])

# =====================================================================================================  5. bones + takes
rig.data.bones["prop"].use_deform = True     # keep the mouth socket; IK / pole / MCH bones are dropped (not deform, not ancestors)
exported_bones = []
for b in rig.data.bones:
    if b.use_deform:
        chain = [b]
        p = b.parent
        while p:
            chain.append(p); p = p.parent
        for x in chain:
            if x.name not in exported_bones:
                exported_bones.append(x.name)
exported_bones = [b.name for b in rig.data.bones if b.name in exported_bones]
stats["exported_bones"] = exported_bones
stats["dropped_bones"] = [b.name for b in rig.data.bones if b.name not in exported_bones]
log("exported bones", len(exported_bones), "dropped", len(stats["dropped_bones"]))

# ---------- bake the Z-up -> Y-up conversion into the armature + mesh data so the FBX nodes (and Unity GameObjects)
# get identity rotations: data rotated by Rx(-90), objects by Rx(+90) -> world space unchanged (checked below).
R = Matrix.Rotation(-math.pi / 2, 4, 'X')
probe = ["root", "thorax", "head", "leg_front_tarsus.L", "leg_hind_tarsus.R", "antenna_2b.L", "gaster_2"] + (["wing_fore.L"] if HAS_WINGS else [])
def probe_pose(take, fr):
    set_action(A(take)); scene.frame_set(fr)
    r = np.array([np.array(rig.matrix_world @ rig.pose.bones[b].tail) for b in probe])
    v = {ob.name: deformed(ob)[::97] for ob in lods}
    return r, v
pre = {(t, f): probe_pose(t, f) for t, f in (("Crawl", 9), ("Idle", 40)) + ((("Land", 30),) if HAS_WINGS else ())}
for ob in lods:
    assert ob.parent == rig and ob.matrix_parent_inverse == Matrix.Identity(4) and ob.matrix_basis == Matrix.Identity(4)
rig.data.transform(R)
for ob in lods:
    ob.data.transform(R)
rig.matrix_world = R.inverted()
bpy.context.view_layer.update()
axis_err = 0.0
for (t, f), (r0, v0) in pre.items():
    r1, v1 = probe_pose(t, f)
    axis_err = max(axis_err, float(np.abs(r1 - r0).max()), *[float(np.abs(v1[k] - v0[k]).max()) for k in v0])
stats["axis_bake_world_error"] = axis_err
log("axis baked into data: max world-space change", axis_err)
assert axis_err < 1e-4, axis_err
rig.animation_data.action = None
for pb in rig.pose.bones:
    pb.matrix_basis.identity()

TAKES = ["Idle", "Crawl", "Crawl_InPlace", "FastCrawl", "FastCrawl_InPlace", "AntennaTalk",
         "TakeOff", "Fly", "Fly_InPlace", "FastFly", "FastFly_InPlace", "Land"]
ad = rig.animation_data
ad.action = None
for tr in list(ad.nla_tracks):
    ad.nla_tracks.remove(tr)
clips = []
for take in TAKES:
    if A(take) not in acts:
        continue
    act = bpy.data.actions[A(take)]
    f0, f1 = int(round(act.frame_range[0])), int(round(act.frame_range[1]))
    loop = bool(act.use_cyclic)
    end = f1 + 1 if loop else f1                   # loops carry the closing frame (== first pose) so Unity loops seamlessly
    tr = ad.nla_tracks.new(); tr.name = take
    st = tr.strips.new(take, f0, act)
    st.name = take                                 # strips.new() ignores its name argument; the FBX take = strip name
    if getattr(st, "action_slot", "n/a") is None:
        st.action_slot = act.slots[0]
    st.action_frame_start = f0
    st.action_frame_end = end
    st.extrapolation = 'NOTHING'
    st.blend_type = 'REPLACE'
    st.use_auto_blend = False
    st.influence = 1.0
    assert abs(st.frame_start - f0) < 1e-6 and abs(st.frame_end - end) < 1e-6, (take, st.frame_start, st.frame_end)
    cyc = (end - f0) if loop else None
    clips.append({"take": take, "source_action": act.name, "loop": loop, "blender_frames": [f0, end], "samples": end - f0 + 1,
                  "frames": end - f0, "duration_s": (end - f0) / fps, "cycle_frames": cyc,
                  "root_motion": take in ("Crawl", "FastCrawl", "Fly", "FastFly", "TakeOff", "Land")})
stats["clips"] = clips
log("takes", [(c["take"], c["blender_frames"], c["loop"]) for c in clips])

stats["reference"] = {"foot_tips_crawl_inplace": ref, "bones": bone_ref, "part_centroid_rest": part_centroid_rest}

# =====================================================================================================  6. export
if "nofbx" not in OPTS:
    os.makedirs(OUT, exist_ok=True)
    for o in scene.objects:
        o.select_set(False)
    rig.select_set(True)
    for ob in lods:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = rig
    rig.data.pose_position = 'POSE'
    for pb in rig.pose.bones:
        pb.matrix_basis.identity()
    # The FBX node (default) pose is whatever is evaluated now. Frame 0 precedes every strip (extrapolation NOTHING),
    # so it is the rest pose -> Unity's prefab default pose = rest, not the last NLA track's first frame.
    scene.frame_set(0)
    dg = bpy.context.evaluated_depsgraph_get()
    assert all(np.allclose(np.array(pb.matrix), np.array(pb.bone.matrix_local), atol=1e-5) for pb in rig.pose.bones), "default pose is not rest"
    tmp = os.path.join(OUT, f".{AID}.tmp.fbx")
    final = os.path.join(OUT, f"{AID}.fbx")
    settings = dict(
        filepath=tmp, check_existing=False, use_selection=True, use_visible=False, use_active_collection=False,
        global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        use_space_transform=True, bake_space_transform=False, axis_forward='-Z', axis_up='Y',
        object_types={'ARMATURE', 'MESH'}, use_mesh_modifiers=False, use_mesh_modifiers_render=False,
        mesh_smooth_type='FACE', colors_type='NONE', prioritize_active_color=False, use_subsurf=False,
        use_mesh_edges=False, use_tspace=False, use_triangles=True, use_custom_props=False,
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        use_armature_deform_only=True, armature_nodetype='NULL',
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
        path_mode='STRIP', embed_textures=False, batch_mode='OFF', use_batch_own_dir=False, use_metadata=True)
    t = time.time()
    res = bpy.ops.export_scene.fbx(**settings)
    assert res == {'FINISHED'}, res
    os.replace(tmp, final)
    stats["fbx"] = {"path": final, "bytes": os.path.getsize(final), "seconds": round(time.time() - t, 1),
                    "settings": {k: (sorted(v) if isinstance(v, set) else v) for k, v in settings.items() if k != "filepath"}}
    log("FBX written", final, stats["fbx"]["bytes"])
json.dump(stats, open(os.path.join(SCR, f"export_stats_{AID}.json"), "w"), indent=1)
log("EXPORT DONE (source .blend not saved)")
