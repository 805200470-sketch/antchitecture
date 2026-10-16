# Ant_002 / Ant_003 -> web GLB. Adapted from glb_export/export_ant.py (Ant_001).
# Runs on the opened Ant_00X_Resource.blend, modifies ONLY in memory, never saves the .blend.
# Writes only into the scratchpad (GLB, downscaled textures, stats JSON).
# usage: Blender -b <blend> --python export_npc.py -- Ant_002|Ant_003
import bpy, bmesh, numpy as np, json, os, sys, time

AID = sys.argv[sys.argv.index("--") + 1]          # "Ant_002" / "Ant_003"
NUM = AID.split("_")[1]                            # "002"
MP = "A" + NUM                                     # material prefix "A002"
S = "/Users/xujiafeng/Desktop/AntGarden/Tools/ants/work"
RES = f"/Users/xujiafeng/Desktop/AntGarden/03_Models/Ants_Source.nosync/{AID}_Resource"
OUT = os.path.join(S, f"ant_{NUM}.glb")
TEX_SRC = os.path.join(RES, "textures")
FBX = os.path.join(RES, f"{AID}.fbx")
TEX_RES = 1024
TRI_BUDGET = 150000
WING_TRI_TARGET = 24000        # wings are the hero feature of Ant_003: keep them denser than the shell
stats = {"ant": AID}
T0 = time.time()

def log(*a):
    print("[EXPORT %6.1fs]" % (time.time() - T0), *a, flush=True)

scene = bpy.context.scene
rig = bpy.data.objects[f"{AID}_Rig"]
body = bpy.data.objects[f"{AID}_Body"]
fps = scene.render.fps / scene.render.fps_base
stats["fps"] = fps
B2G = lambda v: [float(v[0]), float(v[2]), float(-v[1])]  # Blender Z-up -> glTF Y-up (export_yup)
HAS_WINGS = any(s.material and "Wing" in s.material.name for s in body.material_slots)
stats["has_wings"] = HAS_WINGS

def set_action(name):
    act = bpy.data.actions[name]
    rig.animation_data.action = act
    if rig.animation_data.action_slot is None:
        rig.animation_data.action_slot = act.slots[0]
    return act

def head_w(bone):
    return np.array(rig.matrix_world @ rig.pose.bones[bone].head)

A = lambda s: f"{AID}_{s}"
acts = {a.name for a in bpy.data.actions}
log("actions in file", sorted(acts))

# ---------------------------------------------------------------- 1. root motion measurements (original actions)
travel = {}
cyc = [("Crawl", 28), ("FastCrawl", 12), ("Crawl_InPlace", 28), ("FastCrawl_InPlace", 12)]
if HAS_WINGS:
    cyc += [("Fly", 24), ("Fly_InPlace", 24)]
for short, period in cyc:
    name = A(short)
    if name not in acts:
        continue
    set_action(name)
    pos, cog = [], []
    for f in range(1, period + 2):
        scene.frame_set(f)
        pos.append(head_w("root")); cog.append(head_w("COG"))
    pos = np.array(pos); cog = np.array(cog)
    d = pos[-1] - pos[0]
    travel[short] = {"travel_gltf": B2G(d), "dist": float(np.linalg.norm(d)), "period_frames": period,
                     "secs": period / fps, "speed_per_s": float(np.linalg.norm(d)) / (period / fps),
                     "root_range_blender": (pos.max(0) - pos.min(0)).tolist(),
                     "cog_height_range_blender_z": [float(cog[:, 2].min()), float(cog[:, 2].max())]}
    log("root", short, travel[short])
# one-shot clips: start -> end displacement
for short in ("TakeOff", "Land"):
    name = A(short)
    if name not in acts:
        continue
    a = set_action(name)
    f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
    pos, cog = [], []
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        pos.append(head_w("root")); cog.append(head_w("COG"))
    pos = np.array(pos); cog = np.array(cog)
    travel[short] = {"frames": [f0, f1], "root_start_gltf": B2G(pos[0]), "root_end_gltf": B2G(pos[-1]),
                     "root_disp_gltf": B2G(pos[-1] - pos[0]), "root_max_height_gltf": float(pos[:, 2].max()),
                     "cog_start_gltf": B2G(cog[0]), "cog_end_gltf": B2G(cog[-1])}
    log("root", short, travel[short])
stats["root_travel"] = travel

# reference leg positions from the SOURCE rig (compared against the GLB re-import later)
ref = {}
for short, frames in (("Crawl_InPlace", (1, 8, 15)), ("FastCrawl_InPlace", (1, 4, 7)), ("Fly_InPlace", (1, 5, 9)),
                      ("TakeOff", (1, 30, 60)), ("Land", (1, 30, 60))):
    if A(short) not in acts:
        continue
    set_action(A(short))
    ref[short] = {}
    bones = ["leg_front_tarsus.L", "leg_hind_tarsus.R", "head", "root"] + (["wing_fore.L", "wing_hind.R"] if HAS_WINGS else [])
    for f in frames:
        scene.frame_set(f)
        ref[short][f] = {b: B2G(head_w(b)) for b in bones}
        if HAS_WINGS:  # wing tips (tail) show the flap
            for b in ("wing_fore.L", "wing_hind.R"):
                ref[short][f][b + "_tail"] = B2G(np.array(rig.matrix_world @ rig.pose.bones[b].tail))
stats["source_reference_positions_gltf"] = ref

# ---------------------------------------------------------------- 2. UVs from the FBX (identical topology verified)
before = set(bpy.data.objects)
mats_before, imgs_before, meshes_before, arms_before = set(bpy.data.materials), set(bpy.data.images), set(bpy.data.meshes), set(bpy.data.armatures)
bpy.ops.import_scene.fbx(filepath=FBX, use_anim=False, use_image_search=False)
new_objs = [o for o in bpy.data.objects if o not in before]
fbx_mesh = [o for o in new_objs if o.type == 'MESH'][0].data
me = body.data
assert me.users == 1, "body mesh is multi-user"
assert len(fbx_mesh.loops) == len(me.loops) and len(fbx_mesh.polygons) == len(me.polygons)
la = np.empty(len(me.loops), np.int32); lb = np.empty(len(me.loops), np.int32)
me.loops.foreach_get("vertex_index", la); fbx_mesh.loops.foreach_get("vertex_index", lb)
assert (la == lb).all(), "loop order differs"
ma = np.empty(len(me.polygons), np.int32); mb = np.empty(len(me.polygons), np.int32)
me.polygons.foreach_get("material_index", ma); fbx_mesh.polygons.foreach_get("material_index", mb)
assert (ma == mb).all(), "material indices differ"
uv = np.empty(len(me.loops) * 2, np.float32)
fbx_mesh.uv_layers["UVMap"].data.foreach_get("uv", uv)
for o in new_objs:
    bpy.data.objects.remove(o, do_unlink=True)
for coll, old in ((bpy.data.meshes, meshes_before), (bpy.data.armatures, arms_before),
                  (bpy.data.materials, mats_before), (bpy.data.images, imgs_before)):
    for idb in [i for i in coll if i not in old]:
        coll.remove(idb)
uvl = me.uv_layers.new(name="UVMap")
uvl.data.foreach_set("uv", uv)
me.uv_layers.active = uvl
uvl.active_render = True
log("UVMap copied", len(uv) // 2, "loops")

# ---------------------------------------------------------------- 3. split wings into their own skinned mesh (Ant_003)
# Wing colour lives in the 'Col' point attribute (the baked atlas is wrong there), so the wings must
# carry COLOR_0 while the shell must not (three.js would multiply the shell texture by it).
# COLOR_0 is mesh-wide in the exporter -> separate object, same armature/skin.
objs = [body]
wings = None
if HAS_WINGS:
    slot_names = [s.material.name for s in body.material_slots]
    i_mem = slot_names.index(f"{MP}_WingMembrane"); i_vein = slot_names.index(f"{MP}_WingVein")
    assert (i_mem, i_vein) == (2, 3), slot_names
    wings = body.copy()
    wings.data = body.data.copy()
    wings.name = f"{AID}_Wings"; wings.data.name = f"{AID}_Wings"
    for c in body.users_collection:
        c.objects.link(wings)
    def keep_only(ob, keep, shift=0):
        bm = bmesh.new(); bm.from_mesh(ob.data)
        dead = [f for f in bm.faces if f.material_index not in keep]
        bmesh.ops.delete(bm, geom=dead, context='FACES')
        loose_e = [e for e in bm.edges if not e.link_faces]
        if loose_e:
            bmesh.ops.delete(bm, geom=loose_e, context='EDGES')
        loose_v = [v for v in bm.verts if not v.link_faces]
        if loose_v:
            bmesh.ops.delete(bm, geom=loose_v, context='VERTS')
        if shift:
            for f in bm.faces:
                f.material_index -= shift
        bm.to_mesh(ob.data); bm.free()
        ob.data.update()
    keep_only(body, (0, 1))
    keep_only(wings, (2, 3), shift=2)
    for _ in range(2):
        body.data.materials.pop()
        wings.data.materials.pop()
    # wings: keep only the colour attribute; drop the UV map (wing materials are untextured)
    wings.data.uv_layers.remove(wings.data.uv_layers["UVMap"])
    for ca in [c for c in wings.data.color_attributes if c.name != "Col"]:
        wings.data.color_attributes.remove(ca)
    wings.data.color_attributes.active_color = wings.data.color_attributes["Col"]
    wings.data.color_attributes.render_color_index = wings.data.color_attributes.find("Col")
    objs.append(wings)
    log("split: body verts", len(body.data.vertices), "faces", len(body.data.polygons), "| wings verts", len(wings.data.vertices), "faces", len(wings.data.polygons))

# body: drop vertex colours (would become COLOR_0 and tint the baked textures in three.js)
for ca in list(body.data.color_attributes):
    body.data.color_attributes.remove(ca)

# ---------------------------------------------------------------- 4. weights / rigidity diagnostics
vg_names = [g.name for g in body.vertex_groups]
def weight_info(mesh):
    n = len(mesh.vertices)
    dom = np.full(n, -1, np.int32); ng = np.zeros(n, np.int32); mw = np.zeros(n, np.float32)
    for v in mesh.vertices:
        best_g, best_w, c = -1, 0.0, 0
        for g in v.groups:
            if g.weight > 0.0:
                c += 1
                if g.weight > best_w:
                    best_g, best_w = g.group, g.weight
        dom[v.index] = best_g; ng[v.index] = c; mw[v.index] = best_w
    return dom, ng, mw

orig_rigid = {}
for ob in objs:
    dom, ng, mw = weight_info(ob.data)
    ed = np.empty(len(ob.data.edges) * 2, np.int32); ob.data.edges.foreach_get("vertices", ed); ed = ed.reshape(-1, 2)
    cross = int((dom[ed[:, 0]] != dom[ed[:, 1]]).sum())
    orig_rigid[ob.name] = bool((ng == 1).all() and np.allclose(mw, 1.0))
    stats.setdefault("orig_weights", {})[ob.name] = {"verts": int(len(dom)), "unweighted": int((ng == 0).sum()), "multi_group": int((ng > 1).sum()),
                                                    "max_w_not_1": int((~np.isclose(mw, 1.0)).sum()), "cross_part_edges": cross, "rigid": orig_rigid[ob.name]}
    log("orig weights", ob.name, stats["orig_weights"][ob.name])

# ---------------------------------------------------------------- 5. decimate (collapse; rigid parts are separate islands)
def tri_count(ob):
    ob.data.calc_loop_triangles()
    return len(ob.data.loop_triangles)

def decimate_to(ob, target):
    t0 = tri_count(ob)
    if t0 <= target:
        return t0, t0, 1.0
    ratio = (target * 0.99) / t0
    dec = ob.modifiers.new("Decimate", 'DECIMATE')
    dec.decimate_type = 'COLLAPSE'
    dec.use_collapse_triangulate = True
    ob.modifiers.move(ob.modifiers.find("Decimate"), 0)
    arm_mod = [m for m in ob.modifiers if m.type == 'ARMATURE'][0]
    arm_mod.show_viewport = False  # evaluate tri count on the undeformed mesh
    for attempt in range(5):
        dec.ratio = ratio
        dg = bpy.context.evaluated_depsgraph_get()
        ev = ob.evaluated_get(dg); em = ev.to_mesh(); em.calc_loop_triangles(); t = len(em.loop_triangles); ev.to_mesh_clear()
        log(f"  {ob.name} decimate ratio {ratio:.4f} -> {t} tris (target {target})")
        if t <= target:
            break
        ratio *= (target * 0.99) / t
    arm_mod.show_viewport = True
    with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob], selected_editable_objects=[ob]):
        bpy.ops.object.modifier_apply(modifier="Decimate")
    return t0, tri_count(ob), ratio

stats["tris_before"] = {ob.name: tri_count(ob) for ob in objs}
dec_info = {}
if wings is not None:
    tb, ta, r = decimate_to(wings, WING_TRI_TARGET); dec_info[wings.name] = {"before": tb, "after": ta, "ratio": r}
    body_target = int(TRI_BUDGET * 0.98) - ta
else:
    body_target = int(TRI_BUDGET * 0.98)
tb, ta, r = decimate_to(body, body_target); dec_info[body.name] = {"before": tb, "after": ta, "ratio": r}
stats["decimate"] = dec_info
log("decimate", dec_info)

# re-rigidify (only if the source was rigid): each vertex -> its dominant group at 1.0
post = {}
dom_by_obj = {}
for ob in objs:
    me_ = ob.data
    dom2, ng2, mw2 = weight_info(me_)
    fixed = 0
    if orig_rigid[ob.name]:
        groups = ob.vertex_groups
        for vi in np.nonzero((ng2 != 1) | (~np.isclose(mw2, 1.0)))[0]:
            g = int(dom2[vi])
            if g < 0:
                continue
            for gg in [x.group for x in me_.vertices[vi].groups]:
                groups[gg].remove([int(vi)])
            groups[g].add([int(vi)], 1.0, 'REPLACE')
            fixed += 1
    dom2, ng2, mw2 = weight_info(me_)
    dom_by_obj[ob.name] = dom2
    post[ob.name] = {"verts": int(len(dom2)), "unweighted": int((ng2 == 0).sum()), "multi_group": int((ng2 > 1).sum()), "re_rigidified": fixed}
stats["post_weights"] = post
log("post weights", post)

# ---------------------------------------------------------------- 6. validate + rest-pose measurements (exported meshes)
val = {}
for ob in objs:
    me_ = ob.data
    nv0, nf0 = len(me_.vertices), len(me_.polygons)
    fixed_any = me_.validate(verbose=False, clean_customdata=False)
    val[ob.name] = {"fixed": bool(fixed_any), "verts": [nv0, len(me_.vertices)], "faces": [nf0, len(me_.polygons)], "tris": tri_count(ob)}
    dom_by_obj[ob.name] = weight_info(me_)[0]
stats["validate"] = val
stats["tris_final"] = {k: v["tris"] for k, v in val.items()}
stats["tris_total"] = sum(stats["tris_final"].values())
log("validate", val, "TOTAL TRIS", stats["tris_total"])

def world_co(ob):
    co = np.empty(len(ob.data.vertices) * 3, np.float64); ob.data.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
    M = np.array(ob.matrix_world); return co @ M[:3, :3].T + M[:3, 3]
co = world_co(body); dom2 = dom_by_obj[body.name]
g = lambda names: np.isin(dom2, [vg_names.index(n) for n in names if n in vg_names])
head_mask = g(["head"]); mand_mask = g(["mandible.L", "mandible.R"])
gaster_mask = g(["gaster_2"]); tarsus_mask = g([n for n in vg_names if "tarsus" in n])
ant_mask = g([n for n in vg_names if n.startswith("antenna")])
head_y = co[head_mask, 1].min(); mand_y = co[mand_mask, 1].min(); back_y = co[gaster_mask, 1].max()
allco = np.vstack([world_co(o) for o in objs])
mn, mx = allco.min(0), allco.max(0)
gmin = np.array(B2G(mn)); gmax = np.array(B2G(mx))
stats["rest_bbox_gltf"] = {"min": np.minimum(gmin, gmax).tolist(), "max": np.maximum(gmin, gmax).tolist(), "size": np.abs(gmax - gmin).tolist()}
bmn, bmx = co.min(0), co.max(0)
bg0, bg1 = np.array(B2G(bmn)), np.array(B2G(bmx))
stats["rest_bbox_body_only_gltf"] = {"min": np.minimum(bg0, bg1).tolist(), "max": np.maximum(bg0, bg1).tolist(), "size": np.abs(bg1 - bg0).tolist()}
stats["body_length"] = {"mandible_tip_gltf_z": float(-mand_y), "head_front_gltf_z": float(-head_y), "gaster_tip_gltf_z": float(-back_y),
                        "mandible_to_gaster": float(back_y - mand_y), "front_to_gaster": float(back_y - min(head_y, mand_y)),
                        "antenna_front_gltf_z": float(-co[ant_mask, 1].min())}
if wings is not None:
    wco = world_co(wings)
    stats["wings_rest_gltf"] = {"z_min": float(-wco[:, 1].max()), "z_max": float(-wco[:, 1].min()), "span_x": float(wco[:, 0].max() - wco[:, 0].min()),
                                "y_min": float(wco[:, 2].min()), "y_max": float(wco[:, 2].max())}
stats["rest_foot_min_gltf_y"] = float(co[tarsus_mask, 2].min())
hb = rig.data.bones["head"]
stats["head_dir_gltf"] = B2G(np.array(hb.tail_local) - np.array(hb.head_local))
log("rest bbox gltf", stats["rest_bbox_gltf"]); log("body length", stats["body_length"]); log("rest foot min Y", stats["rest_foot_min_gltf_y"])
if wings is not None:
    log("wings rest", stats["wings_rest_gltf"])

# ---------------------------------------------------------------- 7. textures (downscaled copies in scratch) + materials
def downscale(fname, colorspace):
    src = bpy.data.images.load(os.path.join(TEX_SRC, fname), check_existing=False)
    src.colorspace_settings.name = colorspace
    src.scale(TEX_RES, TEX_RES)
    dst = os.path.join(S, fname.replace(".png", f"_{TEX_RES}.png"))
    src.file_format = 'PNG'
    src.save(filepath=dst)
    bpy.data.images.remove(src)
    img = bpy.data.images.load(dst, check_existing=False)
    img.colorspace_settings.name = colorspace
    log("texture", dst, tuple(img.size), os.path.getsize(dst))
    return img

img_bc = downscale(f"{AID}_BaseColor.png", "sRGB")
img_rg = downscale(f"{AID}_Roughness.png", "Non-Color")
img_nm = downscale(f"{AID}_Normal.png", "Non-Color")

src_eye = bpy.data.materials[f"{MP}_Eye"]
ramp = [n for n in src_eye.node_tree.nodes if n.type == 'VALTORGB'][0].color_ramp
samples = np.array([ramp.evaluate(float(t))[:3] for t in np.linspace(0.0, 0.6, 13)])
eye_rgb = samples.mean(0).tolist()
eye_rough = float([n for n in src_eye.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'][0].inputs["Roughness"].default_value)
stats["eye_material"] = {"base_color_linear": eye_rgb, "roughness": eye_rough}
log("eye colour (linear)", eye_rgb, "roughness", eye_rough)

wing_src = {}
if HAS_WINGS:
    for kind in ("WingMembrane", "WingVein"):
        m = bpy.data.materials[f"{MP}_{kind}"]
        b = [n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'][0]
        wing_src[kind] = {"alpha": float(b.inputs["Alpha"].default_value), "roughness": float(b.inputs["Roughness"].default_value)}
    stats["wing_material_src"] = wing_src
    log("wing material src", wing_src)

for m in [f"{MP}_Shell", f"{MP}_Eye"] + ([f"{MP}_WingMembrane", f"{MP}_WingVein"] if HAS_WINGS else []):
    bpy.data.materials[m].name = m + "_src"

def new_mat(name):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    bsdf.inputs["Metallic"].default_value = 0.0
    m.use_backface_culling = False
    return m, nt, bsdf

shell, nt, bsdf = new_mat(f"{MP}_Shell")
uvn = nt.nodes.new("ShaderNodeUVMap"); uvn.uv_map = "UVMap"
def tex(img):
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = img; t.interpolation = 'Linear'
    nt.links.new(uvn.outputs["UV"], t.inputs["Vector"])
    return t
t_bc, t_rg, t_nm = tex(img_bc), tex(img_rg), tex(img_nm)
nt.links.new(t_bc.outputs["Color"], bsdf.inputs["Base Color"])
sep = nt.nodes.new("ShaderNodeSeparateColor")
nt.links.new(t_rg.outputs["Color"], sep.inputs["Color"])
nt.links.new(sep.outputs["Green"], bsdf.inputs["Roughness"])
nmap = nt.nodes.new("ShaderNodeNormalMap"); nmap.space = 'TANGENT'; nmap.uv_map = "UVMap"
nmap.inputs["Strength"].default_value = 1.0
nt.links.new(t_nm.outputs["Color"], nmap.inputs["Color"])
nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])

eye, nt2, bsdf2 = new_mat(f"{MP}_Eye")
bsdf2.inputs["Base Color"].default_value = (*eye_rgb, 1.0)
bsdf2.inputs["Roughness"].default_value = eye_rough

body.material_slots[0].material = shell
body.material_slots[1].material = eye

if HAS_WINGS:
    def wing_mat(name, alpha, rough):
        m, ntw, bw = new_mat(name)
        vc = ntw.nodes.new("ShaderNodeVertexColor"); vc.layer_name = "Col"
        ntw.links.new(vc.outputs["Color"], bw.inputs["Base Color"])
        bw.inputs["Alpha"].default_value = alpha
        bw.inputs["Roughness"].default_value = rough
        m.surface_render_method = 'BLENDED'
        m.use_backface_culling = False
        return m
    mem = wing_mat(f"{MP}_WingMembrane", wing_src["WingMembrane"]["alpha"], wing_src["WingMembrane"]["roughness"])
    vein = wing_mat(f"{MP}_WingVein", wing_src["WingVein"]["alpha"], wing_src["WingVein"]["roughness"])
    wings.material_slots[0].material = mem
    wings.material_slots[1].material = vein
    # colour stats of what will be exported
    wc = np.empty(len(wings.data.vertices) * 4, np.float32); wings.data.color_attributes["Col"].data.foreach_get("color", wc)
    stats["wing_vcol_mean_linear"] = wc.reshape(-1, 4).mean(0).tolist()

# ---------------------------------------------------------------- 8. actions: keep the clip set, rename, include loop-closing frame
plan = [("Idle", "Idle", None), ("Crawl", "Crawl_InPlace", "Crawl"), ("FastCrawl", "FastCrawl_InPlace", "FastCrawl"),
        ("AntennaTalk", "AntennaTalk", None)]
if HAS_WINGS:
    plan += [("TakeOff", "TakeOff_InPlace", "TakeOff"), ("Fly", "Fly_InPlace", "Fly"), ("Land", "Land_InPlace", "Land")]
chosen, notes = {}, []
for clip, pref, fallback in plan:
    if A(pref) in acts:
        chosen[A(pref)] = clip
    elif fallback and A(fallback) in acts:
        chosen[A(fallback)] = clip
        notes.append(f"{clip}: no InPlace action, exported root-motion {A(fallback)}")
    else:
        notes.append(f"{clip}: MISSING")
stats["clip_sources"] = {v: k for k, v in chosen.items()}
stats["clip_notes"] = notes
log("clip sources", stats["clip_sources"], notes)
rig.animation_data.action = None
for a in list(bpy.data.actions):
    if a.name not in chosen:
        bpy.data.actions.remove(a)
for old, newname in chosen.items():
    a = bpy.data.actions[old]
    a.name = newname
    a.use_frame_range = True
    a.frame_start = 1
    a.frame_end = int(round(a.curve_frame_range[1]))  # cyclic: last sample == first -> seamless loop
    log("action", newname, tuple(a.frame_range), "cyclic", a.use_cyclic)
stats["actions"] = {a.name: list(a.frame_range) for a in bpy.data.actions}
for pb in rig.pose.bones:
    pb.matrix_basis.identity()
scene.frame_set(1)

# keep root / COG / prop (non-deform) as joints, same joint set as ant_001; IK, pole and MCH bones are dropped by export_def_bones
for b in ("root", "COG", "prop"):
    rig.data.bones[b].use_deform = True
stats["exported_bones"] = [b.name for b in rig.data.bones if b.use_deform]

# ---------------------------------------------------------------- 9. export
for o in scene.objects:
    o.select_set(False)
rig.select_set(True)
for ob in objs:
    ob.select_set(True)
bpy.context.view_layer.objects.active = rig
if os.path.exists(OUT):
    os.remove(OUT)
bpy.ops.export_scene.gltf(
    filepath=OUT, export_format='GLB', use_selection=True,
    export_yup=True, export_apply=False,
    export_texcoords=True, export_normals=True, export_tangents=False,
    export_materials='EXPORT', export_image_format='AUTO',
    export_vertex_color='MATERIAL', export_all_vertex_colors=False, export_active_vertex_color_when_no_material=False,
    export_cameras=False, export_lights=False, export_extras=False,
    export_skins=True, export_influence_nb=4, export_def_bones=True,
    export_rest_position_armature=True,
    export_animations=True, export_animation_mode='ACTIONS', export_anim_single_armature=True,
    export_force_sampling=True, export_frame_step=1, export_frame_range=False,
    export_anim_slide_to_zero=True, export_reset_pose_bones=True,
    export_optimize_animation_size=True,
    export_morph=False, export_draco_mesh_compression_enable=False,
    will_save_settings=False,
)
stats["glb_bytes"] = os.path.getsize(OUT)
log("GLB written", OUT, stats["glb_bytes"])
with open(os.path.join(S, f"export_stats_{NUM}.json"), "w") as f:
    json.dump(stats, f, indent=1)
log("EXPORT DONE (source .blend not saved)")
