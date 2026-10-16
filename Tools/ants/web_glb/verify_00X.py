# (adapted from glb_export_npc/verify_npc.py: output folder glb_export_<NUM>)
# Verify an NPC ant GLB: parse JSON/BIN directly, re-import into a fresh empty scene, sample bones, render previews.
# usage: Blender -b --factory-startup --python verify_npc.py -- <glb> <preview.png> <NUM e.g. 002>
import bpy, json, struct, os, sys, math, numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
GLB, PREVIEW, NUM = argv[0], argv[1], argv[2]
S = "/Users/xujiafeng/Desktop/AntGarden/Tools/ants/work_" + NUM
src_stats = json.load(open(os.path.join(S, f"export_stats_{NUM}.json")))
out = {}

def log(*a):
    print("[VERIFY]", *a, flush=True)

# ------------------------------------------------------------------ 1. raw GLB parse
data = open(GLB, "rb").read()
magic, ver, length = struct.unpack_from("<III", data, 0)
assert magic == 0x46546C67 and ver == 2 and length == len(data)
off, chunks = 12, {}
while off < length:
    clen, ctype = struct.unpack_from("<II", data, off); off += 8
    chunks[ctype] = data[off:off + clen]; off += clen
js = json.loads(chunks[0x4E4F534A]); binb = chunks[0x004E4942]
COMP = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
NC = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}
def acc(i):
    a = js["accessors"][i]; bv = js["bufferViews"][a["bufferView"]]
    dt = np.dtype(COMP[a["componentType"]]); n = NC[a["type"]]
    o = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    stride = bv.get("byteStride", dt.itemsize * n)
    if stride == dt.itemsize * n:
        return np.frombuffer(binb, dtype=dt, count=a["count"] * n, offset=o).reshape(a["count"], n)
    raw = np.frombuffer(binb, dtype=np.uint8, count=stride * a["count"], offset=o).reshape(a["count"], stride)
    return raw[:, :dt.itemsize * n].copy().view(dt).reshape(a["count"], n)

out["file_bytes"] = len(data)
out["extensionsUsed"] = js.get("extensionsUsed", [])
tris = 0; prim_info = []; pmin = np.full(3, 1e9); pmax = np.full(3, -1e9)
for m in js["meshes"]:
    for p in m["primitives"]:
        nidx = js["accessors"][p["indices"]]["count"]
        assert p.get("mode", 4) == 4
        tris += nidx // 3
        pa = js["accessors"][p["attributes"]["POSITION"]]
        pmin = np.minimum(pmin, pa["min"]); pmax = np.maximum(pmax, pa["max"])
        info = {"mesh": m["name"], "material": js["materials"][p["material"]]["name"], "tris": nidx // 3,
                "verts": pa["count"], "attributes": sorted(p["attributes"].keys())}
        if "COLOR_0" in p["attributes"]:
            ca = js["accessors"][p["attributes"]["COLOR_0"]]
            c = acc(p["attributes"]["COLOR_0"]).astype(np.float64)
            if ca.get("normalized"):
                c = c / np.iinfo(COMP[ca["componentType"]]).max
            info["COLOR_0"] = {"type": ca["type"], "componentType": ca["componentType"], "mean": c.mean(0).round(4).tolist(),
                               "min": c.min(0).round(4).tolist(), "max": c.max(0).round(4).tolist()}
        prim_info.append(info)
out["triangles"] = tris; out["primitives"] = prim_info
out["position_bbox_min"] = pmin.tolist(); out["position_bbox_max"] = pmax.tolist()
out["materials"] = js["materials"]
out["images"] = [{"name": im.get("name"), "mime": im.get("mimeType"), "bytes": js["bufferViews"][im["bufferView"]]["byteLength"]} for im in js.get("images", [])]
skin = js["skins"][0]
node_names = [n.get("name") for n in js["nodes"]]
out["skins"] = len(js["skins"])
out["joints"] = [node_names[j] for j in skin["joints"]]
out["scene_roots"] = [node_names[n] for n in js["scenes"][js.get("scene", 0)]["nodes"]]
out["skinned_mesh_nodes"] = [(n.get("name"), n.get("skin")) for n in js["nodes"] if "mesh" in n]

anims = {}
for a in js.get("animations", []):
    tmax, tmin, nkeys = 0.0, 1e9, set()
    loop_err = 0.0; worst = None; root_t = None; nonconst_step = 0
    for ch in a["channels"]:
        smp = a["samplers"][ch["sampler"]]
        ia = js["accessors"][smp["input"]]
        tmax = max(tmax, ia["max"][0]); tmin = min(tmin, ia["min"][0]); nkeys.add(ia["count"])
        vals = acc(smp["output"]).astype(np.float64)
        path = ch["target"]["path"]; nn = node_names[ch["target"]["node"]]
        if smp.get("interpolation") == "STEP" and np.abs(vals - vals[0]).max() > 1e-6:
            nonconst_step += 1
        if ia["count"] > 1:
            if path == "rotation":
                e = min(np.abs(vals[0] - vals[-1]).max(), np.abs(vals[0] + vals[-1]).max())
            else:
                e = np.abs(vals[0] - vals[-1]).max()
            if e > loop_err:
                loop_err, worst = float(e), f"{nn}.{path}"
        if nn == "root" and path == "translation":
            root_t = {"keys": int(ia["count"]), "first": vals[0].round(4).tolist(), "last": vals[-1].round(4).tolist(),
                      "range": (vals.max(0) - vals.min(0)).round(4).tolist()}
    anims[a["name"]] = {"duration_s": round(tmax, 5), "frames_at_24": round(tmax * 24, 3), "start_s": tmin, "channels": len(a["channels"]),
                        "loop_max_err": loop_err, "loop_worst": worst, "root_translation": root_t, "nonconst_STEP": nonconst_step}
out["animations"] = anims
for k, v in out.items():
    if k not in ("materials",):
        log(k, json.dumps(v)[:2500])
log("materials", json.dumps(out["materials"]))

# ------------------------------------------------------------------ 2. re-import into a fresh scene
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 24
bpy.ops.import_scene.gltf(filepath=GLB)
arm = [o for o in scene.objects if o.type == 'ARMATURE'][0]
meshes = [o for o in scene.objects if o.type == 'MESH' and len(o.vertex_groups) > 0]
log("imported objects", [(o.name, o.type, o.parent.name if o.parent else None) for o in scene.objects])
log("actions", [(a.name, tuple(a.frame_range)) for a in bpy.data.actions])
ad = arm.animation_data
if ad:
    for t in ad.nla_tracks:
        t.mute = True

def use_clip(clip):
    acts = [a for a in bpy.data.actions if a.name == clip or a.name.startswith(clip + "_") or a.name.startswith(clip + ".")]
    acts = [a for a in acts if any(s.target_id_type == 'OBJECT' for s in a.slots)]
    a = acts[0]
    arm.animation_data.action = a
    slot = [s for s in a.slots if s.target_id_type == 'OBJECT']
    if arm.animation_data.action_slot is None or arm.animation_data.action_slot.identifier not in [s.identifier for s in slot]:
        arm.animation_data.action_slot = slot[0]
    return a

def head_g(bone, tail=False):
    pb = arm.pose.bones[bone]
    v = arm.matrix_world @ (pb.tail if tail else pb.head)
    return np.array([v.x, v.z, -v.y])

body = [o for o in meshes if any("tarsus" in g.name for g in o.vertex_groups) and any(s.material and s.material.name.endswith("Shell") for s in o.material_slots)][0]
wings = [o for o in meshes if o is not body]
wings = wings[0] if wings else None
log("body", body.name, "wings", wings.name if wings else None)
tars_groups = {g.index for g in body.vertex_groups if "tarsus" in g.name}
vg_dom = np.full(len(body.data.vertices), -1)
for v in body.data.vertices:
    if len(v.groups):
        vg_dom[v.index] = max(v.groups, key=lambda g: g.weight).group
tars_mask = np.isin(vg_dom, list(tars_groups))

def deformed_coords_g(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg); m = ev.to_mesh()
    co = np.empty(len(m.vertices) * 3); m.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
    ev.to_mesh_clear()
    M = np.array(ob.matrix_world); co = co @ M[:3, :3].T + M[:3, 3]
    return np.stack([co[:, 0], co[:, 2], -co[:, 1]], 1)

arm.animation_data.action = None
for pb in arm.pose.bones:
    pb.matrix_basis.identity()
scene.frame_set(0)
rest = deformed_coords_g(body)
out["reimport_rest_foot_min_y"] = float(rest[tars_mask, 1].min())
allrest = np.vstack([rest] + ([deformed_coords_g(wings)] if wings else []))
out["reimport_rest_bbox_gltf"] = {"min": allrest.min(0).tolist(), "max": allrest.max(0).tolist()}
log("rest bbox", out["reimport_rest_bbox_gltf"], "rest foot min y", out["reimport_rest_foot_min_y"])

ref = src_stats["source_reference_positions_gltf"]
clips = {}
clip_names = [a["name"] for a in js["animations"]]
for clip in clip_names:
    a = use_clip(clip)
    f0, f1 = int(round(a.frame_range[0])), int(round(a.frame_range[1]))
    roots, feet_min, bb_min, bb_max, wing_y = [], [], np.full(3, 1e9), np.full(3, -1e9), []
    step = 1 if (f1 - f0) <= 30 else 3
    for f in list(range(f0, f1 + 1, step)) + [f1]:
        scene.frame_set(f)
        roots.append(head_g("root"))
        co = deformed_coords_g(body)
        feet_min.append(co[tars_mask, 1].min()); bb_min = np.minimum(bb_min, co.min(0)); bb_max = np.maximum(bb_max, co.max(0))
        if wings:
            wco = deformed_coords_g(wings)
            bb_min = np.minimum(bb_min, wco.min(0)); bb_max = np.maximum(bb_max, wco.max(0))
            wing_y.append(float(head_g("wing_fore.L", tail=True)[1] - head_g("wing_fore.L")[1]))
    roots = np.array(roots)
    c = {"frames": [f0, f1], "root_first": roots[0].round(4).tolist(), "root_last": roots[-1].round(4).tolist(),
         "root_range": (roots.max(0) - roots.min(0)).round(4).tolist(),
         "feet_min_y_over_clip": round(float(min(feet_min)), 4), "feet_min_y_first_frame": round(float(feet_min[0]), 4),
         "anim_bbox_min": bb_min.round(3).tolist(), "anim_bbox_max": bb_max.round(3).tolist()}
    if wing_y:
        c["wing_fore.L_tip_rel_height_range"] = [round(min(wing_y), 4), round(max(wing_y), 4)]
    srcname = {"Crawl": "Crawl_InPlace", "FastCrawl": "FastCrawl_InPlace", "Fly": "Fly_InPlace", "TakeOff": "TakeOff", "Land": "Land"}.get(clip)
    if srcname and srcname in ref:
        errs, samples = [], {}
        for sf, bones in ref[srcname].items():
            scene.frame_set(f0 + int(sf) - 1)
            for b, p in bones.items():
                cur = head_g(b[:-5], tail=True) if b.endswith("_tail") else head_g(b)
                errs.append(float(np.linalg.norm(cur - np.array(p))))
            samples[sf] = head_g("leg_front_tarsus.L").round(4).tolist()
        c["max_err_vs_source_rig"] = max(errs)
        c["leg_front_tarsus.L_samples"] = samples
        fr = sorted(samples, key=int)
        c["tarsus_moved"] = float(np.linalg.norm(np.array(samples[fr[0]]) - np.array(samples[fr[1]])))
    clips[clip] = c
    log("clip", clip, json.dumps(c))
out["reimport_clips"] = clips

# ------------------------------------------------------------------ 3. preview renders
eng = None
for e in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT", "BLENDER_WORKBENCH"):
    try:
        scene.render.engine = e; eng = e; break
    except TypeError:
        continue
log("render engine", eng)
try:
    scene.eevee.taa_render_samples = 32
except Exception:
    pass
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
scene.render.resolution_x, scene.render.resolution_y = 1280, 800
scene.render.film_transparent = False
world = bpy.data.worlds.new("W"); scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
bg.inputs["Color"].default_value = (0.22, 0.22, 0.24, 1.0); bg.inputs["Strength"].default_value = 1.0
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN')); scene.collection.objects.link(sun)
sun.data.energy = 3.0
sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
for o in scene.objects:
    if o.type == "MESH" and not o.vertex_groups:
        o.hide_render = True   # importer bone-shape icosphere
for m in bpy.data.materials:   # report what the importer made of the wing materials
    log("reimported material", m.name, "render_method", getattr(m, "surface_render_method", None), "backface_culling", m.use_backface_culling)

def frame_bounds():
    co = np.vstack([deformed_coords_g(body)] + ([deformed_coords_g(wings)] if wings else []))
    cb = np.stack([co[:, 0], -co[:, 2], co[:, 1]], 1)  # back to Blender coords
    cmin, cmax = cb.min(0), cb.max(0)
    return Vector(((cmin + cmax) / 2).tolist()), float(np.linalg.norm(cmax - cmin)) / 2, cmin

a = use_clip("Crawl")
mid = int(round((a.frame_range[0] + a.frame_range[1]) / 2))
scene.frame_set(mid)
center, radius, cmin = frame_bounds()
bpy.ops.mesh.primitive_plane_add(size=14, location=(center.x, center.y, float(cmin[2])))
plane = bpy.context.active_object
pm = bpy.data.materials.new("Ground"); pm.use_nodes = True
pm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.55, 0.52, 0.47, 1)
plane.data.materials.append(pm)
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); scene.collection.objects.link(cam); scene.camera = cam

def shoot(direction, dist_mul, target, path, lens=50):
    cam.data.lens = lens
    d = Vector(direction).normalized()
    fov = 2 * math.atan(36 / 2 / lens)
    dist = radius * dist_mul / math.sin(fov / 2)
    cam.location = target + d * dist
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    log("rendered", path, os.path.getsize(path))

# ant faces Blender -Y; 3/4 front view from front-left, above (higher for the winged ant so the folded wings read)
main_dir = (0.9, -1.15, 1.0) if wings else (0.9, -1.25, 0.75)
shoot(main_dir, 1.05, center, PREVIEW)
head_c = arm.matrix_world @ arm.pose.bones["head"].head
shoot((0.9, -1.0, 0.55), 0.30, Vector(((head_c.x + center.x) / 2 + 0.25, head_c.y - 0.25, head_c.z - 0.3)), os.path.join(S, f"close_{NUM}.png"))
if wings:
    shoot((0.6, 1.0, 1.3), 0.55, center + Vector((0, 0.6, 0.2)), os.path.join(S, f"wings_rest_{NUM}.png"))
    a = use_clip("Fly")
    scene.frame_set(int(a.frame_range[0]) + 2)
    plane.hide_render = True
    center, radius, cmin = frame_bounds()
    shoot((0.9, -1.0, 0.8), 1.0, center, os.path.join(S, f"fly_{NUM}.png"))
    a = use_clip("TakeOff")
    scene.frame_set(int(a.frame_range[0]) + 40)
    center, radius, cmin = frame_bounds()
    shoot((1.2, -0.6, 0.5), 1.0, center, os.path.join(S, f"takeoff_{NUM}.png"))

json.dump(out, open(os.path.join(S, f"verify_stats_{NUM}.json"), "w"), indent=1)
log("VERIFY DONE")
