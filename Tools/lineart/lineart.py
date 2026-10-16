# blender -b -P lineart.py -- <fbx> <out.png> <view: hero|side|front> [lod]
import bpy, sys, math, mathutils
argv = sys.argv[sys.argv.index("--")+1:]
src, out, view = argv[0], argv[1], argv[2]
lodpick = argv[3] if len(argv) > 3 else "LOD1"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
names = [o.name for o in meshes]
lods = [o for o in meshes if lodpick.lower() in o.name.lower()]
keep = lods if lods else meshes
for o in meshes:
    if o not in keep: bpy.data.objects.remove(o, do_unlink=True)
meshes = keep
act = argv[5] if len(argv) > 5 else ""
frm = int(argv[6]) if len(argv) > 6 else 1
for o in bpy.context.scene.objects:
    if o.type == "ARMATURE" and act:
        a = [x for x in bpy.data.actions if x.name.endswith("|"+act)]
        if a:
            o.animation_data.action = a[0]
            try:
                o.animation_data.action_slot = a[0].slots[0]
            except Exception as ex: print("slot", ex)
bpy.context.scene.frame_set(frm)
# flat white material
mat = bpy.data.materials.new("white"); mat.use_nodes = True
nt = mat.node_tree; nt.nodes.clear()
em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = (1,1,1,1)
o_ = nt.nodes.new("ShaderNodeOutputMaterial"); nt.links.new(em.outputs[0], o_.inputs[0])
for o in meshes:
    o.data.materials.clear(); o.data.materials.append(mat)
# bounds (evaluated, world space)
dg = bpy.context.evaluated_depsgraph_get()
pts = []
for o in meshes:
    e = o.evaluated_get(dg); m = e.to_mesh()
    pts += [e.matrix_world @ v.co for v in m.vertices]
    e.to_mesh_clear()
mn = mathutils.Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
mx = mathutils.Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
c = (mn+mx)/2; size = (mx-mn).length
# camera
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); bpy.context.scene.collection.objects.link(cam)
cam.data.type = "ORTHO"
up = "Z" if (mx.z-mn.z) >= 0 else "Z"
dirs = {"hero": (1.0, -1.25, 0.75), "side": (1, 0, 0.15), "front": (0, -1, 0.15), "top": (0.001, -0.3, 1)}
d = mathutils.Vector(dirs[view] if view in dirs else [float(x) for x in view.split(",")]).normalized()
cam.location = c + d * size * 3
cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
# ortho scale from projected extents
inv = cam.matrix_world.inverted() if False else None
bpy.context.view_layer.update()
mw = cam.matrix_world.inverted()
pp = [mw @ p for p in pts]
w = max(p.x for p in pp)-min(p.x for p in pp); h = max(p.y for p in pp)-min(p.y for p in pp)
sc = bpy.context.scene
sc.render.resolution_x = 1600; sc.render.resolution_y = 1600
cam.data.ortho_scale = max(w, h) * 1.12
cx = (max(p.x for p in pp)+min(p.x for p in pp))/2; cy = (max(p.y for p in pp)+min(p.y for p in pp))/2
cam.data.shift_x = cx / cam.data.ortho_scale; cam.data.shift_y = cy / cam.data.ortho_scale
sc.camera = cam
# render settings
sc.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE_NEXT"
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
sc.render.use_freestyle = True
sc.render.line_thickness_mode = "ABSOLUTE"
vl = sc.view_layers[0]
vl.use_freestyle = True
fs = vl.freestyle_settings
fs.crease_angle = math.radians(float(argv[4]) if len(argv) > 4 else 125)
for ls in list(fs.linesets): fs.linesets.remove(ls)
def lineset(name, thick, color, **sel):
    ls = fs.linesets.new(name)
    ls.select_by_visibility = True; ls.visibility = "VISIBLE"
    ls.select_by_edge_types = True
    for k in ("select_silhouette","select_border","select_crease","select_external_contour","select_contour","select_material_boundary","select_suggestive_contour","select_ridge_valley"):
        setattr(ls, k, False)
    for k, v in sel.items(): setattr(ls, k, v)
    st = ls.linestyle; st.thickness = thick; st.color = color
    st.caps = "ROUND"; st.chaining = "PLAIN"
    return ls
lineset("outer", 4.2, (0.08,0.08,0.08), select_external_contour=True, select_border=True)
lineset("inner", 1.4, (0.18,0.18,0.18), select_silhouette=True, select_crease=True)
sc.render.filepath = out
sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
bpy.ops.render.render(write_still=True)
print("MESHES", names, "KEPT", [o.name for o in meshes])
