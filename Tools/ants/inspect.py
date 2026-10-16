# Read-only inspection of Ant_00N_Resource.blend (+ its FBX) for the Unity LOD export. Never saves.
# usage: Blender -b --factory-startup <blend> -P inspect.py -- Ant_00N
import bpy, sys, json, numpy as np, time
from collections import Counter, defaultdict

AID = sys.argv[sys.argv.index("--") + 1]
RES = f"/Users/xujiafeng/Desktop/AntGarden/03_Models/Ants_Source.nosync/{AID}_Resource"
T0 = time.time()
def log(*a):
    print("[INSP %5.1fs]" % (time.time() - T0), *a, flush=True)

sc = bpy.context.scene
log("fps", sc.render.fps, sc.render.fps_base, "units", sc.unit_settings.system, sc.unit_settings.scale_length, "frames", sc.frame_start, sc.frame_end)
for ob in bpy.data.objects:
    log("OBJ", ob.name, ob.type, "parent", ob.parent.name if ob.parent else None, ob.parent_type,
        "mods", [(m.name, m.type) for m in getattr(ob, "modifiers", [])], "loc", tuple(round(v, 4) for v in ob.matrix_world.to_translation()),
        "rot", tuple(round(v, 4) for v in ob.matrix_world.to_euler()), "scale", tuple(round(v, 4) for v in ob.matrix_world.to_scale()))

rig = bpy.data.objects[f"{AID}_Rig"]
body = bpy.data.objects[f"{AID}_Body"]
me = body.data
me.calc_loop_triangles()
log("BODY verts", len(me.vertices), "faces", len(me.polygons), "tris", len(me.loop_triangles), "uv", [u.name for u in me.uv_layers],
    "colors", [(c.name, c.domain, c.data_type) for c in me.color_attributes], "attrs", [(a.name, a.domain, a.data_type) for a in me.attributes],
    "mats", [s.material.name if s.material else None for s in body.material_slots])
mi = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get("material_index", mi)
log("faces per material", dict(Counter(mi.tolist())))
pv = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get("loop_total", pv)
log("face sizes", dict(Counter(pv.tolist())))
if "sharp_face" in me.attributes:
    sf = np.empty(len(me.polygons), bool); me.attributes["sharp_face"].data.foreach_get("value", sf)
    log("sharp faces", int(sf.sum()), "of", len(sf))
if "sharp_edge" in me.attributes:
    se = np.empty(len(me.edges), bool); me.attributes["sharp_edge"].data.foreach_get("value", se)
    log("sharp edges", int(se.sum()))
log("has custom normals", me.has_custom_normals)

# ------------------------------------------------------------ bones / weights
vg_names = [g.name for g in body.vertex_groups]
log("vgroups", len(vg_names), vg_names)
nv = len(me.vertices)
dom = np.full(nv, -1, np.int32); ng = np.zeros(nv, np.int32); mw = np.zeros(nv, np.float32)
for v in me.vertices:
    bg, bw, c = -1, 0.0, 0
    for g in v.groups:
        if g.weight > 0:
            c += 1
            if g.weight > bw:
                bg, bw = g.group, g.weight
    dom[v.index] = bg; ng[v.index] = c; mw[v.index] = bw
log("weights: unweighted", int((ng == 0).sum()), "multi", int((ng > 1).sum()), "w!=1", int((~np.isclose(mw, 1)).sum()))

bones = rig.data.bones
log("BONES", len(bones), "deform", sum(b.use_deform for b in bones))
for b in bones:
    pb = rig.pose.bones[b.name]
    log("  bone", b.name, "parent", b.parent.name if b.parent else None, "deform", b.use_deform,
        "cons", [(c.type, getattr(c, "subtarget", None), c.owner_space if hasattr(c, "owner_space") else None, c.target_space if hasattr(c, "target_space") else None) for c in pb.constraints],
        "rotmode", pb.rotation_mode, "inherit_scale", b.inherit_scale, "len", round(b.length, 4))

# ------------------------------------------------------------ islands (numpy label propagation)
ed = np.empty(len(me.edges) * 2, np.int32); me.edges.foreach_get("vertices", ed); ed = ed.reshape(-1, 2)
lab = np.arange(nv)
for it in range(10000):
    a = lab[ed[:, 0]]; b = lab[ed[:, 1]]
    m = np.minimum(a, b)
    old = lab.copy()
    np.minimum.at(lab, ed[:, 0], m); np.minimum.at(lab, ed[:, 1], m)
    lab = lab[lab]
    if (lab == old).all():
        break
uniq, inv = np.unique(lab, return_inverse=True)
nis = len(uniq)
log("islands", nis, "iterations", it)
co = np.empty(nv * 3); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
lt = np.empty(len(me.loop_triangles) * 3, np.int32); me.loop_triangles.foreach_get("vertices", lt); lt = lt.reshape(-1, 3)
tri_isl = inv[lt[:, 0]]
tris_per_isl = np.bincount(tri_isl, minlength=nis)
vert_per_isl = np.bincount(inv, minlength=nis)
# island -> bone
isl_bone = {}
mixed = 0
for i in range(nis):
    pass
bone_of_isl = np.full(nis, -1)
order = np.argsort(inv)
starts = np.searchsorted(inv[order], np.arange(nis))
ends = np.append(starts[1:], nv)
ext = np.zeros((nis, 3))
isl_mat = np.full(nis, -1)
tri_mat = mi[me.loop_triangles[0].polygon_index] if False else None
ltp = np.empty(len(me.loop_triangles), np.int32); me.loop_triangles.foreach_get("polygon_index", ltp)
tri_mi = mi[ltp]
for i in range(nis):
    vs = order[starts[i]:ends[i]]
    d = np.unique(dom[vs])
    if len(d) > 1:
        mixed += 1
    bone_of_isl[i] = d[0]
    c = co[vs]
    ext[i] = c.max(0) - c.min(0)
log("islands with mixed bones", mixed)
isl_size = np.linalg.norm(ext, axis=1)
per_bone = defaultdict(lambda: [0, 0, []])
for i in range(nis):
    bn = vg_names[bone_of_isl[i]] if bone_of_isl[i] >= 0 else "NONE"
    per_bone[bn][0] += 1; per_bone[bn][1] += int(tris_per_isl[i]); per_bone[bn][2].append((int(tris_per_isl[i]), round(float(isl_size[i]), 4)))
for bn, (n, t, lst) in per_bone.items():
    lst.sort(reverse=True)
    log("  bone", bn, "islands", n, "tris", t, "top islands (tris,size)", lst[:12])
hist = np.histogram(tris_per_isl, bins=[0, 8, 16, 32, 64, 128, 256, 512, 1024, 4096, 16384, 1e9])
log("tris per island hist", list(zip(hist[1][:-1].tolist(), hist[0].tolist())))
hs = np.histogram(isl_size, bins=[0, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 5])
log("island size hist", list(zip(hs[1][:-1].tolist(), hs[0].tolist())))
# material per island
mat_isl = defaultdict(set)
for t_i in range(0, len(tri_isl), 1):
    pass
isl_mats = np.zeros((nis, 4), np.int64)
np.add.at(isl_mats, (tri_isl, tri_mi), 1)
log("islands per material (any tri)", [(m, int((isl_mats[:, m] > 0).sum())) for m in range(isl_mats.shape[1])])
log("islands with >1 material", int(((isl_mats > 0).sum(1) > 1).sum()))
bbmin, bbmax = co.min(0), co.max(0)
log("rest bbox blender", bbmin.round(4).tolist(), bbmax.round(4).tolist())

# ------------------------------------------------------------ actions
for a in bpy.data.actions:
    info = {"frame_range": tuple(a.frame_range), "curve_range": tuple(a.curve_frame_range), "manual": a.use_frame_range, "cyclic": a.use_cyclic,
            "slots": [(s.identifier, s.target_id_type) for s in a.slots], "users": a.users}
    fcs = []
    for layer in a.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                fcs += list(cb.fcurves)
    mods = Counter((m.type, getattr(m, "mode_before", None), getattr(m, "mode_after", None)) for fc in fcs for m in fc.modifiers)
    interp = Counter(k.interpolation for fc in fcs for k in fc.keyframe_points)
    targets = sorted({fc.data_path.split('"')[1] for fc in fcs if fc.data_path.startswith("pose.bones")})
    root_loc = [fc for fc in fcs if fc.data_path == 'pose.bones["root"].location']
    rl = [(fc.array_index, fc.keyframe_points[0].co[1], fc.keyframe_points[-1].co[1]) for fc in root_loc]
    log("ACTION", a.name, info, "fcurves", len(fcs), "mods", dict(mods), "interp", dict(interp), "targets", len(targets), "root loc first/last", rl)
log("rig action", rig.animation_data.action.name if rig.animation_data and rig.animation_data.action else None,
    "nla tracks", [t.name for t in rig.animation_data.nla_tracks] if rig.animation_data else None)

# ------------------------------------------------------------ materials in blend
for m in bpy.data.materials:
    if not m.node_tree:
        log("MAT", m.name, "no nodes"); continue
    nodes = Counter(n.type for n in m.node_tree.nodes)
    bs = [n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED']
    extra = {}
    if bs:
        b = bs[0]
        for k in ("Base Color", "Roughness", "Alpha", "Metallic"):
            inp = b.inputs[k]
            extra[k] = ("linked" if inp.is_linked else (tuple(round(x, 4) for x in inp.default_value) if hasattr(inp.default_value, "__len__") else round(inp.default_value, 4)))
    log("MAT", m.name, "users", m.users, dict(nodes), extra, "method", getattr(m, "surface_render_method", None), "blend", getattr(m, "blend_method", None))

# ------------------------------------------------------------ FBX comparison
before = set(bpy.data.objects); mats_before = set(bpy.data.materials)
bpy.ops.import_scene.fbx(filepath=f"{RES}/{AID}.fbx", use_anim=True, use_image_search=False)
new = [o for o in bpy.data.objects if o not in before]
for o in new:
    log("FBX OBJ", o.name, o.type, "parent", o.parent.name if o.parent else None, "scale", tuple(round(v, 4) for v in o.matrix_world.to_scale()), "rot", tuple(round(v, 4) for v in o.matrix_world.to_euler()))
    if o.type == 'MESH':
        fm = o.data
        log("  FBX mesh verts", len(fm.vertices), "polys", len(fm.polygons), "uv", [u.name for u in fm.uv_layers], "colors", [(c.name, c.domain, c.data_type) for c in fm.color_attributes],
            "mats", [s.material.name if s.material else None for s in o.material_slots], "vgroups", len(o.vertex_groups))
        same = len(fm.loops) == len(me.loops) and len(fm.polygons) == len(me.polygons)
        if same:
            la = np.empty(len(me.loops), np.int32); lb = np.empty(len(me.loops), np.int32)
            me.loops.foreach_get("vertex_index", la); fm.loops.foreach_get("vertex_index", lb)
            mb = np.empty(len(me.polygons), np.int32); fm.polygons.foreach_get("material_index", mb)
            fco = np.empty(len(fm.vertices) * 3); fm.vertices.foreach_get("co", fco); fco = fco.reshape(-1, 3)
            M = np.array(o.matrix_world)
            fco_w = fco @ M[:3, :3].T + M[:3, 3]
            log("  topology identical", bool((la == lb).all()), "mat idx identical", bool((mi == mb).all()), "max pos diff (world)", float(np.abs(fco_w - co).max()) if len(fco) == nv else None)
            uv = np.empty(len(fm.loops) * 2, np.float32); fm.uv_layers[0].data.foreach_get("uv", uv); uv = uv.reshape(-1, 2)
            log("  uv range", uv.min(0).tolist(), uv.max(0).tolist())
            # per material uv bbox
            lp = np.empty(len(fm.polygons), np.int32); fm.polygons.foreach_get("loop_start", lp)
            loop_mat = np.repeat(mi, pv)
            for mm in np.unique(mi):
                u = uv[loop_mat == mm]
                log("   mat", mm, "uv bbox", u.min(0).round(4).tolist(), u.max(0).round(4).tolist(), "loops", len(u))
        for s in o.material_slots:
            m = s.material
            if m and m.node_tree:
                b = [n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED']
                texs = [(n.image.name if n.image else None, [(l.to_node.name, l.to_socket.name) for l in n.outputs[0].links]) for n in m.node_tree.nodes if n.type == 'TEX_IMAGE']
                vcol = [n.type for n in m.node_tree.nodes if n.type in ('VERTEX_COLOR', 'ATTRIBUTE')]
                extra = {}
                if b:
                    for k in ("Base Color", "Roughness", "Alpha"):
                        inp = b[0].inputs[k]
                        extra[k] = ("linked" if inp.is_linked else (tuple(round(x, 4) for x in inp.default_value) if hasattr(inp.default_value, "__len__") else round(inp.default_value, 4)))
                log("  FBX MAT", m.name, extra, "tex", texs, "vcol nodes", vcol, "blend", getattr(m, "surface_render_method", None))
    if o.type == 'ARMATURE':
        log("  FBX bones", len(o.data.bones))
log("FBX actions", [(a.name, tuple(a.frame_range)) for a in bpy.data.actions if a.name not in [x.name for x in []]][:40])
log("DONE (not saved)")
