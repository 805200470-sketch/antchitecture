"""turntable_mesh.py - process film P02, the first phase: a plant's model (its Tripo mesh with its textures, as Unity has
it: Art/Plants/<name>/<name>.fbx, LOD0) rendered from the very camera the game filmed the plant with (ProcessFilms.cs
-turntablefilm: film.json), bent, twisted and stretched as the game bends its splats (ParticleLook.Shape with this plant's
numbers), so it can be faded into the points and the game's frames. Run through Blender:

  Blender -b --factory-startup -P Tools/process_film/turntable_mesh.py -- <film dir> <plant> <out dir> [from] [to] [bg r,g,b]

Lit like a turntable: the lights turn with the camera (a key from the upper left, a fill, a rim from behind), on the
film's dark ground. Frames f00000.jpg ... at 1920x1080. Coordinates: Blender (x, y, z) = Unity (-x, -z, y)
(plants_to_unity.to_unity's inverse), Unity's plant set at the origin.
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index('--') + 1:]
film_dir, subject, out = argv[0], argv[1], argv[2]
f0 = int(argv[3]) if len(argv) > 3 else 0
f1 = int(argv[4]) if len(argv) > 4 else -1
bg = [float(v) / 255.0 for v in (argv[5] if len(argv) > 5 else '3,3,4').split(',')]
ART = os.path.expanduser('~/Desktop/AntGarden/UnityProject.nosync/Assets/AntGarden/Art/Plants/')

film = json.load(open(os.path.join(film_dir, 'film.json')))
sub = next(s for s in film['subjects'] if s['name'] == subject)
name = sub['name']
os.makedirs(out, exist_ok=True)


def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def to_blender(p):
    return np.stack([-p[..., 0], -p[..., 2], p[..., 1]], -1)


def shape(p, s):
    p = p.copy()
    top = max(s['top'], 1e-4)
    y01 = np.clip(p[:, 1] / top, 0, 1)
    if not s.get('rigid') and not (s['bend'] == 0 and s['twist'] == 0 and s['sy'] == 1 and s['sxz'] == 1):
        y = np.maximum(0, p[:, 1])
        p[:, 0] *= s['sxz']; p[:, 2] *= s['sxz']; p[:, 1] *= s['sy']
        a = s['twist'] * y; ca, sa = np.cos(a), np.sin(a)
        px, pz = p[:, 0] * ca - p[:, 2] * sa, p[:, 0] * sa + p[:, 2] * ca
        p[:, 0], p[:, 2] = px, pz
        yy = y * s['sy']; bx, bz = s['bendDir']
        p[:, 0] += bx * s['bend'] * yy * yy; p[:, 2] += bz * s['bend'] * yy * yy
    if s.get('sunk', 0) > 0:
        p[:, 1] -= np.where(y01 < s['sunk'] + 0.03, 1 + s['sunk'], s['sunk']) * top * s['sy']
    return p


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(ART, name, name + '.fbx'))
objs = list(bpy.context.scene.objects)
lod0 = next(o for o in objs if o.type == 'MESH' and o.name.endswith('_LOD0'))
for o in objs:
    if o is not lod0 and o.type == 'MESH':
        bpy.data.objects.remove(o, do_unlink=True)
bpy.context.view_layer.update()
me = lod0.data
M = np.array(lod0.matrix_world)
co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
cw = co @ M[:3, :3].T + M[:3, 3]
u = np.stack([-cw[:, 0], cw[:, 2], -cw[:, 1]], 1)            # Unity: the plant's own space (plants_to_unity.to_unity)
Mu = np.array(sub['m'], np.float64)
O = Mu[:3, 3]
w = shape(u, sub) @ Mu[:3, :3].T                            # where the game puts it, relative to its foot O
lod0.parent = None
lod0.matrix_world = Matrix.Identity(4)
me.vertices.foreach_set('co', to_blender(w).astype(np.float64).ravel())
me.update()
bpy.context.view_layer.objects.active = lod0
lod0.select_set(True)
try:
    bpy.ops.mesh.customdata_custom_splitnormals_clear()
except Exception as e:
    print('turntable_mesh: custom normals kept', e)
for poly in me.polygons:
    poly.use_smooth = True

# its textures (plants_to_unity's preview material)
mat = bpy.data.materials.new(name + '_film')
mat.use_nodes = True
nt = mat.node_tree
bsdf = nt.nodes['Principled BSDF']
tex = {}
for k, f in (('color', 'BaseColor'), ('normal', 'Normal'), ('orm', 'ORM')):
    p = os.path.join(ART, name, f'{name}_{f}.png')
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
    nt.links.new(tex['normal'].outputs['Color'], nm.inputs['Color'])
    nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
if 'orm' in tex:
    sep = nt.nodes.new('ShaderNodeSeparateColor')
    nt.links.new(tex['orm'].outputs['Color'], sep.inputs['Color'])
    nt.links.new(sep.outputs['Green'], bsdf.inputs['Roughness'])
me.materials.clear()
me.materials.append(mat)

sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE'
sc.render.resolution_x, sc.render.resolution_y = film['width'], film['height']
sc.render.resolution_percentage = 100
sc.render.film_transparent = False
sc.view_settings.view_transform = 'Standard'
sc.view_settings.look = 'None'
try:
    sc.eevee.taa_render_samples = 48
except Exception:
    pass
sc.render.image_settings.file_format = 'JPEG'
sc.render.image_settings.quality = 95
world = bpy.data.worlds.new('w')
sc.world = world
world.use_nodes = True
bgn = world.node_tree.nodes['Background']
bgn.inputs[0].default_value = (srgb_to_lin(bg[0]), srgb_to_lin(bg[1]), srgb_to_lin(bg[2]), 1)
bgn.inputs[1].default_value = 1.0

cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.sensor_fit = 'VERTICAL'
cam.data.sensor_height = 24.0
cam.data.lens = 12.0 / math.tan(math.radians(sub['fov']) / 2)
cam.data.clip_start = 0.02
cam.data.clip_end = 200.0

# lights that turn with the camera: (direction toward the light in the camera's own axes: x right, y up, z back), strength, colour
LIGHTS = [((-0.55, 0.65, 0.5), 3.2, (1.0, 0.97, 0.92)),   # key, upper left, in front
          ((0.75, 0.1, 0.45), 0.9, (0.82, 0.9, 1.0)),      # fill, right
          ((0.2, 0.45, -0.85), 2.4, (0.85, 0.95, 1.0))]    # rim, behind
suns = []
for i, (d, e, c) in enumerate(LIGHTS):
    L = bpy.data.objects.new(f'sun{i}', bpy.data.lights.new(f'sun{i}', 'SUN'))
    L.data.energy = e
    L.data.color = c
    L.data.angle = math.radians(6)
    sc.collection.objects.link(L)
    suns.append((L, Vector(d).normalized()))


def quat_rows(q):
    x, y, z, w_ = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w_), 2 * (x * z + y * w_)],
                     [2 * (x * y + z * w_), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w_)],
                     [2 * (x * z - y * w_), 2 * (y * z + x * w_), 1 - 2 * (x * x + y * y)]])


cams = sub['cams']
f1 = len(cams) - 1 if f1 < 0 else min(f1, len(cams) - 1)
for f in range(f0, f1 + 1):
    c = cams[f]
    R = quat_rows(c[3:7])                                   # Unity: columns right, up, forward
    right, up, fwd = to_blender(R[:, 0]), to_blender(R[:, 1]), to_blender(R[:, 2])
    pos = to_blender(np.array(c[:3]) - O)
    m = Matrix(((right[0], up[0], -fwd[0], pos[0]), (right[1], up[1], -fwd[1], pos[1]),
                (right[2], up[2], -fwd[2], pos[2]), (0, 0, 0, 1)))
    cam.matrix_world = m
    rot = m.to_3x3()
    for L, d in suns:
        toward = rot @ d                                    # toward the light, in the world
        L.rotation_euler = (-toward).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(out, f'f{f:05d}.jpg')
    bpy.ops.render.render(write_still=True)
    if f % 30 == 0:
        print(f'turntable_mesh: {name} frame {f}', flush=True)
print('turntable_mesh: done', name, f0, f1)
