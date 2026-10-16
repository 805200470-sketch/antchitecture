# Ant_001 -> web GLB. Runs on the opened Ant_001_Resource.blend, modifies ONLY in memory, never saves.
# Writes only into the scratchpad (GLB, downscaled textures, stats JSON).
import bpy, numpy as np, json, os, time, math

S = "/Users/xujiafeng/Desktop/AntGarden/Tools/ants/work"
OUT = os.path.join(S, "ant_001.glb")
TEX_SRC = "/Users/xujiafeng/Desktop/AntGarden/03_Models/Ants_Source.nosync/Ant_001_Resource/textures"
FBX = "/Users/xujiafeng/Desktop/AntGarden/03_Models/Ants_Source.nosync/Ant_001_Resource/Ant_001.fbx"
TEX_RES = 1024
TRI_BUDGET = 150000
stats = {}
T0 = time.time()

def log(*a):
    print("[EXPORT %6.1fs]" % (time.time() - T0), *a, flush=True)

scene = bpy.context.scene
rig = bpy.data.objects["Ant_001_Rig"]
body = bpy.data.objects["Ant_001_Body"]
fps = scene.render.fps / scene.render.fps_base
stats["fps"] = fps
B2G = lambda v: [float(v[0]), float(v[2]), float(-v[1])]  # Blender Z-up -> glTF Y-up (export_yup)

def set_action(name):
    act = bpy.data.actions[name]
    rig.animation_data.action = act
    if rig.animation_data.action_slot is None:
        rig.animation_data.action_slot = act.slots[0]
    return act

def head_w(bone):
    return np.array(rig.matrix_world @ rig.pose.bones[bone].head)

# ---------------------------------------------------------------- 1. root travel (original actions)
travel = {}
for name, period in (("Ant_001_Crawl", 28), ("Ant_001_FastCrawl", 12),
                     ("Ant_001_Crawl_InPlace", 28), ("Ant_001_FastCrawl_InPlace", 12)):
    set_action(name)
    pos = []
    for f in range(1, period + 2):
        scene.frame_set(f)
        pos.append(head_w("root"))
    pos = np.array(pos)
    d = pos[-1] - pos[0]
    lin = pos[0] + np.outer(np.arange(period + 1) / period, d)
    dev = np.linalg.norm(pos - lin, axis=1).max()
    travel[name] = {"travel_blender": d.tolist(), "travel_gltf": B2G(d), "dist": float(np.linalg.norm(d)),
                    "period_frames": period, "secs": period / fps,
                    "speed_per_s": float(np.linalg.norm(d)) / (period / fps),
                    "max_dev_from_linear": float(dev),
                    "root_range": (pos.max(0) - pos.min(0)).tolist()}
    log("root", name, travel[name])
stats["root_travel"] = travel

# reference leg positions from the SOURCE rig (to compare with the GLB re-import later)
ref = {}
for name, frames in (("Ant_001_Crawl_InPlace", (1, 8, 15)), ("Ant_001_FastCrawl_InPlace", (1, 4, 7))):
    set_action(name)
    ref[name] = {}
    for f in frames:
        scene.frame_set(f)
        ref[name][f] = {b: B2G(head_w(b)) for b in ("leg_front_tarsus.L", "leg_hind_tarsus.R", "head", "root")}
stats["source_reference_positions_gltf"] = ref

# ---------------------------------------------------------------- 2. UVs from the FBX (identical topology)
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
uv = np.empty(len(me.loops) * 2, np.float32)
fbx_mesh.uv_layers["UVMap"].data.foreach_get("uv", uv)
nm_strength = 1.0
for m in [m for m in bpy.data.materials if m not in mats_before]:
    if m.node_tree:
        for n in m.node_tree.nodes:
            if n.type == 'NORMAL_MAP':
                nm_strength = n.inputs["Strength"].default_value
log("FBX normal map strength (import artifact, overridden to 1.0)", nm_strength)
stats["fbx_normal_strength"] = nm_strength
nm_strength = 1.0
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

# drop vertex colours (would become COLOR_0 and tint the textures in three.js)
for ca in list(me.color_attributes):
    me.color_attributes.remove(ca)

# ---------------------------------------------------------------- 3. weights / rigidity diagnostics
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

dom, ng, mw = weight_info(me)
ed = np.empty(len(me.edges) * 2, np.int32); me.edges.foreach_get("vertices", ed); ed = ed.reshape(-1, 2)
cross = int((dom[ed[:, 0]] != dom[ed[:, 1]]).sum())
orig_rigid = bool((ng == 1).all() and np.allclose(mw, 1.0))
stats["orig_weights"] = {"verts": int(len(dom)), "unweighted": int((ng == 0).sum()), "multi_group": int((ng > 1).sum()),
                         "max_w_not_1": int((~np.isclose(mw, 1.0)).sum()), "cross_part_edges": cross, "rigid": orig_rigid}
log("orig weights", stats["orig_weights"])

me.calc_loop_triangles()
tri_before = len(me.loop_triangles)
stats["tris_before"] = tri_before

# ---------------------------------------------------------------- 4. decimate (collapse; parts are separate islands)
ratio = (TRI_BUDGET * 0.98) / tri_before
dec = body.modifiers.new("Decimate", 'DECIMATE')
dec.decimate_type = 'COLLAPSE'
dec.use_collapse_triangulate = True
body.modifiers.move(body.modifiers.find("Decimate"), 0)
arm_mod = [m for m in body.modifiers if m.type == 'ARMATURE'][0]
arm_mod.show_viewport = False  # evaluate tri count on the undeformed mesh
for attempt in range(4):
    dec.ratio = ratio
    dg = bpy.context.evaluated_depsgraph_get()
    ev = body.evaluated_get(dg); em = ev.to_mesh(); em.calc_loop_triangles(); t = len(em.loop_triangles); ev.to_mesh_clear()
    log(f"decimate ratio {ratio:.4f} -> {t} tris")
    if t <= TRI_BUDGET:
        break
    ratio *= (TRI_BUDGET * 0.98) / t
arm_mod.show_viewport = True
with bpy.context.temp_override(object=body, active_object=body, selected_objects=[body], selected_editable_objects=[body]):
    bpy.ops.object.modifier_apply(modifier="Decimate")
me = body.data
me.calc_loop_triangles()
tri_after = len(me.loop_triangles)
stats["tris_after"] = tri_after
stats["decimate_ratio"] = ratio
mi = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get("material_index", mi)
stats["eye_faces_after"] = int((mi == 1).sum())
log("after decimate tris", tri_after, "verts", len(me.vertices), "eye faces", stats["eye_faces_after"])

# re-rigidify (only if the source was rigid): each vertex -> its dominant group at 1.0
dom2, ng2, mw2 = weight_info(me)
fixed = 0
if orig_rigid:
    groups = body.vertex_groups
    for vi in np.nonzero((ng2 != 1) | (~np.isclose(mw2, 1.0)))[0]:
        g = int(dom2[vi])
        if g < 0:
            continue
        for gg in [x.group for x in me.vertices[vi].groups]:
            groups[gg].remove([int(vi)])
        groups[g].add([int(vi)], 1.0, 'REPLACE')
        fixed += 1
dom2, ng2, mw2 = weight_info(me)
stats["post_weights"] = {"verts": int(len(dom2)), "unweighted": int((ng2 == 0).sum()), "multi_group": int((ng2 > 1).sum()),
                         "re_rigidified": fixed, "groups": len(body.vertex_groups)}
log("post weights", stats["post_weights"])

# ---------------------------------------------------------------- 5. rest-pose measurements (exported mesh)
co = np.empty(len(me.vertices) * 3, np.float64); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
M = np.array(body.matrix_world); co = co @ M[:3, :3].T + M[:3, 3]
g = lambda names: np.isin(dom2, [vg_names.index(n) for n in names if n in vg_names])
front_mask = g(["mandible.L", "mandible.R", "head"]); mand_mask = g(["mandible.L", "mandible.R"])
gaster_mask = g(["gaster_2"]); tarsus_mask = g([n for n in vg_names if "tarsus" in n])
front_y = co[front_mask, 1].min(); mand_y = co[mand_mask, 1].min(); back_y = co[gaster_mask, 1].max()
mn, mx = co.min(0), co.max(0)
gmin = np.array(B2G(mn)); gmax = np.array(B2G(mx))
stats["rest_bbox_gltf"] = {"min": np.minimum(gmin, gmax).tolist(), "max": np.maximum(gmin, gmax).tolist(),
                           "size": np.abs(gmax - gmin).tolist()}
stats["body_length"] = {"mandible_tip_y_blender": float(mand_y), "head_front_y_blender": float(front_y),
                        "gaster_tip_y_blender": float(back_y), "length": float(back_y - min(front_y, mand_y)),
                        "mandible_tip_gltf_z": float(-mand_y), "gaster_tip_gltf_z": float(-back_y)}
stats["rest_foot_min_gltf_y"] = float(co[tarsus_mask, 2].min())
stats["head_dir_blender"] = (np.array(rig.data.bones["head"].tail_local) - np.array(rig.data.bones["head"].head_local)).tolist()
log("rest bbox gltf", stats["rest_bbox_gltf"]); log("body length", stats["body_length"]); log("rest foot min Y", stats["rest_foot_min_gltf_y"])

# ---------------------------------------------------------------- 6. textures (downscaled copies in scratch) + materials
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

img_bc = downscale("Ant_001_BaseColor.png", "sRGB")
img_rg = downscale("Ant_001_Roughness.png", "Non-Color")
img_nm = downscale("Ant_001_Normal.png", "Non-Color")

# eye colour: representative value of the source A001_Eye ramp (voronoi F1 distance mostly 0..0.6)
src_eye = bpy.data.materials["A001_Eye"]
ramp = [n for n in src_eye.node_tree.nodes if n.type == 'VALTORGB'][0].color_ramp
log("eye ramp stops", [(round(e.position, 3), tuple(round(c, 4) for c in e.color)) for e in ramp.elements])
samples = np.array([ramp.evaluate(float(t))[:3] for t in np.linspace(0.0, 0.6, 13)])
eye_rgb = samples.mean(0).tolist()
src_eye_bsdf = [n for n in src_eye.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'][0]
eye_rough = float(src_eye_bsdf.inputs["Roughness"].default_value)
stats["eye_material"] = {"base_color_linear": eye_rgb, "roughness": eye_rough}
log("eye colour (linear)", eye_rgb, "roughness", eye_rough)

for m in ("A001_Shell", "A001_Eye"):
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

shell, nt, bsdf = new_mat("A001_Shell")
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
nmap.inputs["Strength"].default_value = nm_strength
nt.links.new(t_nm.outputs["Color"], nmap.inputs["Color"])
nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])

eye, nt2, bsdf2 = new_mat("A001_Eye")
bsdf2.inputs["Base Color"].default_value = (*eye_rgb, 1.0)
bsdf2.inputs["Roughness"].default_value = eye_rough

body.material_slots[0].material = shell
body.material_slots[1].material = eye

# ---------------------------------------------------------------- 7. actions: keep 4, rename, include loop-closing frame
for name in ("Ant_001_Crawl", "Ant_001_FastCrawl"):
    bpy.data.actions.remove(bpy.data.actions[name])
rename = {"Ant_001_Idle": "Idle", "Ant_001_Crawl_InPlace": "Crawl",
          "Ant_001_FastCrawl_InPlace": "FastCrawl", "Ant_001_AntennaTalk": "AntennaTalk"}
for old, newname in rename.items():
    a = bpy.data.actions[old]
    a.name = newname
    a.use_frame_range = True
    a.frame_start = 1
    a.frame_end = int(round(a.curve_frame_range[1]))  # 97 / 29 / 13 / 97: last sample == first -> seamless loop
    log("action", newname, tuple(a.frame_range), "cyclic", a.use_cyclic)
stats["actions"] = {a.name: list(a.frame_range) for a in bpy.data.actions}
assert sorted(stats["actions"]) == ["AntennaTalk", "Crawl", "FastCrawl", "Idle"], stats["actions"]

rig.animation_data.action = None
for pb in rig.pose.bones:
    pb.matrix_basis.identity()
scene.frame_set(1)

# keep root / COG / prop (non-deform) as joints; IK, pole and MCH bones are dropped by export_def_bones
for b in ("root", "COG", "prop"):
    rig.data.bones[b].use_deform = True
stats["exported_bones"] = [b.name for b in rig.data.bones if b.use_deform]

# ---------------------------------------------------------------- 8. export
me = body.data
nv0, ne0, nf0 = len(me.vertices), len(me.edges), len(me.polygons)
fixed_any = me.validate(verbose=True, clean_customdata=False)
me.calc_loop_triangles()
stats["validate"] = {"fixed": bool(fixed_any), "verts": [nv0, len(me.vertices)], "edges": [ne0, len(me.edges)],
                     "faces": [nf0, len(me.polygons)], "tris_final": len(me.loop_triangles)}
stats["tris_after"] = len(me.loop_triangles)
log("validate", stats["validate"])
for o in scene.objects:
    o.select_set(False)
rig.select_set(True); body.select_set(True)
bpy.context.view_layer.objects.active = rig
if os.path.exists(OUT):
    os.remove(OUT)
bpy.ops.export_scene.gltf(
    filepath=OUT, export_format='GLB', use_selection=True,
    export_yup=True, export_apply=False,
    export_texcoords=True, export_normals=True, export_tangents=False,
    export_materials='EXPORT', export_image_format='AUTO',
    export_vertex_color='NONE', export_all_vertex_colors=False, export_active_vertex_color_when_no_material=False,
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
with open(os.path.join(S, "export_stats.json"), "w") as f:
    json.dump(stats, f, indent=1)
log("EXPORT DONE (source .blend not saved)")
