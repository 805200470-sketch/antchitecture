# A/B: render the SOURCE (undecimated) Ant_003 wings with the same camera as verify_npc.py's wings_rest shot. Never saves.
import bpy, math, numpy as np
from mathutils import Vector
S = "/Users/xujiafeng/Desktop/AntGarden/Tools/ants/work"
scene = bpy.context.scene
rig = bpy.data.objects["Ant_003_Rig"]; body = bpy.data.objects["Ant_003_Body"]
act = bpy.data.actions["Ant_003_Crawl_InPlace"]
rig.animation_data.action = act
if rig.animation_data.action_slot is None:
    rig.animation_data.action_slot = act.slots[0]
scene.frame_set(15)
dg = bpy.context.evaluated_depsgraph_get()
ev = body.evaluated_get(dg); m = ev.to_mesh()
co = np.empty(len(m.vertices) * 3); m.vertices.foreach_get("co", co); co = co.reshape(-1, 3); ev.to_mesh_clear()
cmin, cmax = co.min(0), co.max(0); center = Vector(((cmin + cmax) / 2).tolist()); radius = float(np.linalg.norm(cmax - cmin)) / 2
for o in scene.objects:
    if o.type == 'LIGHT' or (o.type == 'MESH' and o is not body):
        o.hide_render = True
scene.render.engine = 'BLENDER_EEVEE'
scene.eevee.taa_render_samples = 32
scene.view_settings.view_transform = 'Standard'; scene.view_settings.look = 'None'
scene.render.resolution_x, scene.render.resolution_y = 1280, 800
scene.render.resolution_percentage = 100
world = bpy.data.worlds.new("W"); scene.world = world; world.use_nodes = True
bg = [n for n in world.node_tree.nodes if n.type == "BACKGROUND"][0]; bg.inputs["Color"].default_value = (0.22, 0.22, 0.24, 1.0)
sun = bpy.data.objects.new("SunAB", bpy.data.lights.new("SunAB", 'SUN')); scene.collection.objects.link(sun)
sun.data.energy = 3.0; sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
bpy.ops.mesh.primitive_plane_add(size=14, location=(center.x, center.y, float(cmin[2])))
cam = bpy.data.objects.new("CamAB", bpy.data.cameras.new("CamAB")); scene.collection.objects.link(cam); scene.camera = cam
cam.data.lens = 50
d = Vector((0.6, 1.0, 1.3)).normalized(); fov = 2 * math.atan(36 / 2 / 50)
target = center + Vector((0, 0.6, 0.2))
cam.location = target + d * (radius * 0.55 / math.sin(fov / 2))
cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
scene.render.filepath = f"{S}/ab_wings_source_003.png"
bpy.ops.render.render(write_still=True)
print("AB DONE radius", radius, "center", tuple(center))
