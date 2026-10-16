import sys; sys.path.insert(0, sys.argv[sys.argv.index("--")+1])
from scene_lib import *
args = sys.argv[sys.argv.index("--")+1:]
NAME, OUT = args[1], args[2]
mat = reset()
ANT = lambda i: f"{A}/Ants/Ant_00{i}/Ant_00{i}.fbx"
PL = lambda p: f"{A}/Plants/{p}/{p}.fbx"
def box(loc, size, bevel=0.0, c="main", rot=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rot); o = bpy.context.active_object
    o.scale = (size[0]/2, size[1]/2, size[2]/2); bpy.ops.object.transform_apply(scale=True)
    if bevel: m = o.modifiers.new("b", "BEVEL"); m.width = bevel; m.segments = 4
    whiten(o, mat); move_to(o, coll(c)); return o
def sphere(loc, r, c="main"):
    bpy.ops.mesh.primitive_uv_sphere_add(location=loc, radius=r, segments=32, ring_count=16); o = bpy.context.active_object
    bpy.ops.object.shade_smooth(); whiten(o, mat); move_to(o, coll(c)); return o
def rod(a, b, r, c="main"):
    a = mathutils.Vector(a); b = mathutils.Vector(b); d = b - a
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=d.length, location=(a+b)/2, vertices=24); o = bpy.context.active_object
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler(); bpy.ops.object.shade_smooth(); whiten(o, mat); move_to(o, coll(c)); return o
def dome(loc, r, c="main"):
    bpy.ops.mesh.primitive_uv_sphere_add(location=loc, radius=r, segments=48, ring_count=24); o = bpy.context.active_object
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -1e-4], context="VERTS"); bm.to_mesh(o.data); bm.free()
    bpy.ops.object.shade_smooth(); whiten(o, mat); move_to(o, coll(c)); return o
def model(path, x, y, z, h, rot=0, lod="LOD1", action=None, frame=1, c="main"):
    r, objs = import_model(path, lod, action, frame, mat, c); place(r, objs, x, y, z, h, rot); return r, objs
def path_pt(tile, k):
    T = TILES[tile]; S = T["S"]; i = int(k * (len(S) - 1)); a, b = S[i]
    d = b - a; ang = math.atan2(d[1], d[0]); return float(a[0]), float(a[1]), ang
def tile_path_line(tile, z, w=1.6, dash=(10, 8)):
    S = TILES[tile]["S"]
    for a, b in S: add_line([(a[0], a[1], z), (b[0], b[1], z)], w, dash)
def card_lines(x0, y0, z, w, h):
    for i in range(3):
        add_line([(x0, y0 - i*h, z), (x0 + w*(1 if i < 2 else 0.6), y0 - i*h, z)], 1.6)

if NAME == "01_capture":
    o, Z = hex_tile("nest", "path-013", 0, 0, 1.0, mat, depth=0.1, thick=0.22, width=0.07, chamber=0.12)
    T = TILES["nest"]
    for k in (0.15, 0.45, 0.7, 0.9):
        x, y, a = path_pt("nest", k); model(ANT(1), x, y, T["floor"], 0.07, a - math.pi/2, "LOD1")
    # glass lid (dashed outline) + light strip
    add_line([(math.cos(math.radians(60*i+30))*1.0, math.sin(math.radians(60*i+30))*1.0, 0.06) for i in range(7)], 1.6, (8, 7))
    # phone
    PR = (math.radians(35), 0, math.radians(40))
    ph = box((0.95, 1.0, 1.45), (0.46, 0.86, 0.05), 0.04, rot=PR)
    sc_ = box((0.95, 1.0, 1.45), (0.38, 0.72, 0.056), 0.0, rot=PR)
    for p in ((-0.5, -0.4), (0.6, 0.3), (0.1, 0.7)):
        add_line([(0.9, 0.95, 1.35), (p[0], p[1], 0.0)], 1.5, (6, 8))
    setup_render((2.8, -3.2, 3.0), (0.45, 0.25, 0.6), ortho=4.0, res=(1600, 1200))
elif NAME == "02_translate":
    # photo card with traced lines
    box((-0.9, 0, 0), (1.1, 1.45, 0.03), 0.0)
    segs, junc = load_net("path-013"); allp = segs.reshape(-1, 2); ctr = (allp.min(0) + allp.max(0)) / 2
    sc0 = 0.6 / np.max(np.abs(allp - ctr))
    for a, b in segs: add_line([(-0.9 + (a[0]-ctr[0])*sc0, -(a[1]-ctr[1])*sc0, 0.017), (-0.9 + (b[0]-ctr[0])*sc0, -(b[1]-ctr[1])*sc0, 0.017)], 3.2)
    # tablet with node graph
    box((0.95, 0, 0), (1.25, 1.6, 0.06), 0.05); box((0.95, 0, 0.01), (1.1, 1.42, 0.06), 0.0)
    d = json.load(open(f"{NET}/path-013.json")); nodes = {n["id"]: n for n in d["nodes"]}
    def tp(n): return (0.95 + (n["x"]-ctr[0])*sc0*0.95, -(n["y"]-ctr[1])*sc0*0.95, 0.06)
    for e in d["edges"]: rod(tp(nodes[e["source"]]), tp(nodes[e["target"]]), 0.012)
    for n in d["nodes"]: sphere(tp(n), 0.045 if n["type"] == "junction" else 0.032)
    for a, b in segs: add_line([(0.95 + (a[0]-ctr[0])*sc0*0.95, -(a[1]-ctr[1])*sc0*0.95, 0.045), (0.95 + (b[0]-ctr[0])*sc0*0.95, -(b[1]-ctr[1])*sc0*0.95, 0.045)], 1.4, (5, 6))
    add_line([(-0.25, -0.05, 0.05), (0.22, -0.05, 0.05)], 2.6); add_line([(0.12, 0.05, 0.05), (0.22, -0.05, 0.05), (0.12, -0.15, 0.05)], 2.6)
    setup_render((0.3, -2.4, 3.2), (0.05, 0, 0), ortho=3.5, res=(1600, 1200))
elif NAME == "03_create":
    o, Z = hex_tile("g", "path-011", 0, 0, 1.0, mat)
    T = TILES["g"]
    for (p, x, y, h, r) in (("fan_tree", -0.55, 0.45, 0.6, 0.3), ("spore_stalk", 0.5, 0.55, 0.32, 0), ("curl_glow", 0.62, -0.25, 0.3, 1.0), ("sponge_mound", -0.35, -0.6, 0.14, 0)):
        model(PL(p), x, y, 0.0, h, r)
    x, y, a = path_pt("g", 0.55); ring(x, y, T["floor"], 0.09, mat); ring(x, y, T["floor"], 0.12, mat)
    model(PL("lantern_pods"), x, y, T["floor"], 0.36, 0.4, c="dashed")
    # photo + story card floating
    CR = (math.radians(62), 0, math.radians(40))
    box((x + 0.8, y - 0.35, 1.05), (0.46, 0.34, 0.015), 0.0, rot=CR)
    box((x + 0.95, y - 0.5, 0.68), (0.46, 0.26, 0.015), 0.0, rot=CR)
    add_line([(x + 0.6, y - 0.2, 0.55), (x + 0.3, y - 0.1, 0.35), (x + 0.05, y, 0.2)], 1.6, (6, 7))
    setup_render((2.6, -3.0, 2.6), (0.15, 0, 0.05), ortho=3.3, res=(1600, 1200))
elif NAME == "04_connect":
    cs = [(-1.15, 0.6, "path-001", 0), (1.15, 0.6, "path-002", 1.0), (0.0, -1.35, "path-003", 2.0)]
    for i, (cx, cy, net, rot) in enumerate(cs):
        hex_tile(f"t{i}", net, cx, cy, 0.85, mat, depth=0.12, thick=0.2, rot=rot)
    for (a, b) in ((0, 1), (1, 2), (2, 0)):
        A_, B_ = mathutils.Vector(cs[a][:2] + (0.25,)), mathutils.Vector(cs[b][:2] + (0.25,))
        add_line([A_.lerp(B_, 0.3), A_.lerp(B_, 0.5) + mathutils.Vector((0, 0, 0.35)), A_.lerp(B_, 0.7)], 1.8, (8, 8))
    model(PL("lantern_pods"), -1.15, 0.95, 0.0, 0.25, 0); model(PL("fan_tree"), 1.45, 0.75, 0.0, 0.35, 0); model(PL("spore_stalk"), 0.3, -1.1, 0.0, 0.2, 0)
    setup_render((3.2, -4.2, 4.2), (0, -0.05, 0), ortho=4.6, res=(1600, 1200))
elif NAME == "05_explore":
    o, Z = hex_tile("c", "path-013", 0, 0, 3.0, mat, depth=0.09, width=0.06, chamber=0.11)
    T = TILES["c"]; fl = T["floor"]
    x, y, a = path_pt("c", 0.12); xb, yb, ab = path_pt("c", 0.2)
    d = mathutils.Vector((xb - x, yb - y)).normalized()
    model(ANT(1), x + d.x*0.12, y + d.y*0.12, fl, 0.075, math.atan2(d.y, d.x) + math.pi/2, "LOD0", "Crawl_InPlace", 8)
    gx, gy = x + d.x*0.55, y + d.y*0.55
    ring(gx, gy, fl, 0.12, mat, minor=0.006); ring(gx, gy, fl, 0.16, mat, minor=0.006)
    model(PL("lantern_pods"), gx, gy, fl, 0.24, 0.6)
    n = mathutils.Vector((-d.y, d.x))
    for s_, k, p, h in ((1, 0.6, "fan_tree", 0.5), (-1, 0.75, "spore_stalk", 0.28), (1, 1.1, "curl_glow", 0.25), (-1, 0.35, "bracket_shelf", 0.2), (-1, 1.3, "fan_tree", 0.45)):
        px, py = x + d.x*k + n.x*s_*0.42, y + d.y*k + n.y*s_*0.42
        model(PL(p), px, py, 0.0, h, k)
    cam = mathutils.Vector((x - d.x*0.35, y - d.y*0.35, fl + 0.2))
    setup_render(tuple(cam), (gx, gy, fl + 0.02), res=(1600, 1000), lens=26)
elif NAME == "06_collect":
    o, Z = hex_tile("src", "path-006", -0.9, 0, 0.8, mat, depth=0.12, thick=0.2)
    o2, Z2 = hex_tile("home", "path-010", 1.25, 0.1, 0.55, mat, depth=0.12, thick=0.2)
    T = TILES["src"]; x, y, a = path_pt("src", 0.5)
    ring(x, y, T["floor"], 0.13, mat); model(PL("lantern_pods"), x, y, T["floor"], 0.5, 0.5)
    model(PL("lantern_pods"), 0.25, -0.15, 0.55, 0.5, 0.5, c="dashed")
    H = TILES["home"]; hx_, hy_, _ = path_pt("home", 0.5)
    ring(hx_, hy_, H["floor"], 0.08, mat)
    add_line([(x + 0.12, y, 0.4), (-0.05, -0.1, 0.75), (0.3, -0.15, 0.95)], 1.6, (6, 7))
    add_line([(0.55, -0.1, 0.8), (0.95, 0.0, 0.7), (hx_, hy_, 0.35)], 2.2)
    add_line([(hx_ - 0.07, hy_ + 0.05, 0.45), (hx_, hy_, 0.35), (hx_ + 0.08, hy_ + 0.04, 0.44)], 2.2)
    box((0.7, -0.6, 1.15), (0.4, 0.24, 0.012), 0.0, rot=(math.radians(62), 0, math.radians(28)))
    setup_render((2.0, -3.6, 2.6), (0.15, 0, 0.3), ortho=3.6, res=(1600, 1100))
elif NAME == "07_meet":
    box((0, 0, -0.01), (3, 3, 0.02), c="hidden").hide_render = True
    model(ANT(1), -0.15, 0, 0, 0.12, math.pi/2, "LOD0", "AntennaTalk", 40)
    model(ANT(4), 0.15, 0, 0, 0.13, -math.pi/2, "LOD0", "AntennaTalk", 40)
    setup_render((0.15, -1.6, 0.55), (0, 0, 0.05), ortho=0.95, res=(1600, 1000))
elif NAME == "08_light_gate":
    o, Z = hex_tile("a", "path-013", 0, 0, 2.0, mat, depth=0.07, width=0.07, chamber=0.12)
    T = TILES["a"]; x, y, a = path_pt("a", 0.03); xb, yb, _ = path_pt("a", 0.1)
    d = mathutils.Vector((xb - x, yb - y)).normalized()
    dome((x, y, T["floor"]), 0.32)
    model(ANT(1), x + d.x*0.75, y + d.y*0.75, T["floor"], 0.08, math.atan2(-d.y, -d.x) + math.pi/2, "LOD0", "Crawl_InPlace", 4)
    model(PL("spore_stalk"), x + d.x*0.3 + 0.3, y + d.y*0.3 + 0.1, 0.0, 0.25, 0)
    model(PL("lantern_pods"), x + d.x*0.2 - 0.32, y + d.y*0.2 - 0.1, 0.0, 0.22, 2)
    cam = mathutils.Vector((x + d.x*1.5, y + d.y*1.5, T["floor"] + 0.3))
    setup_render(tuple(cam), (x, y, T["floor"] + 0.12), res=(1600, 1000), lens=32)
elif NAME == "09_map":
    hr = 0.8; dx = hr*math.sqrt(3)
    for i, (cx, cy, net) in enumerate(((0, 0, "path-001"), (dx, 0, "path-002"), (dx/2, -1.5*hr, "path-003"))):
        hex_tile(f"m{i}", net, cx, cy, hr, mat, depth=0.08, thick=0.12)
    hex_tile("amb", "path-010", 3.3, -0.4, hr, mat, depth=0.08, thick=0.12)
    add_line([(dx + 0.55, -0.2, 0.05), (2.3, -0.1, 0.4), (3.3 - 0.55, -0.35, 0.05)], 1.8, (8, 8))
    # pin
    rod((dx/2, -0.25, 0.0), (dx/2, -0.25, 0.55), 0.018); sphere((dx/2, -0.25, 0.62), 0.1)
    setup_render((0.3, -3.0, 8.0), (1.5, -0.55, 0), ortho=5.4, res=(1600, 1000))
elif NAME == "10_flyer":
    o, Z = hex_tile("f", "path-006", 0, 0, 1.6, mat, depth=0.08, width=0.07, chamber=0.12)
    T = TILES["f"]; x, y, a = path_pt("f", 0.4)
    model(ANT(3), x + 0.25, y - 0.35, 0.55, 0.3, a, "LOD0", "Fly_InPlace", 6)
    model(ANT(2), x + 0.25, y - 0.2, T["floor"], 0.04, a + 2.4, "LOD1", "Crawl_InPlace", 10)
    add_line([(x - 0.9, y - 0.4, 0.05), (x - 0.5, y - 0.2, 0.3), (x - 0.15, y, 0.38)], 1.5, (6, 8))
    model(PL("fan_tree"), x - 0.9, y + 0.7, 0.0, 0.45, 0); model(PL("coral_fungus"), x + 0.6, y + 0.3, 0.0, 0.18, 0)
    setup_render((x + 1.6, y - 2.2, 1.4), (x, y, 0.2), ortho=2.6, res=(1600, 1100))
render(OUT); export_contours(OUT + ".json")
