import bpy, bmesh, math, json, mathutils, numpy as np
LINES = []
TILES = {}
def add_line(pts, w=2.0, dash=None):
    LINES.append({'p': [list(p) for p in pts], 'w': w, 'dash': dash})
A = "/Users/xujiafeng/Desktop/AntGarden/UnityProject.nosync/Assets/AntGarden/Art"
NET = "/Users/xujiafeng/Desktop/AntGarden/04_Paths/Networks"
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    m = bpy.data.materials.new("white"); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    e = nt.nodes.new("ShaderNodeEmission"); e.inputs[0].default_value = (1,1,1,1)
    o = nt.nodes.new("ShaderNodeOutputMaterial"); nt.links.new(e.outputs[0], o.inputs[0])
    return m
def coll(name):
    c = bpy.data.collections.get(name)
    if not c:
        c = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(c)
    return c
def move_to(o, c):
    for x in list(o.users_collection): x.objects.unlink(o)
    c.objects.link(o)
def whiten(o, mat):
    if o.type == "MESH":
        o.data.materials.clear(); o.data.materials.append(mat)
def import_model(path, lod="LOD1", action=None, frame=1, mat=None, c="main"):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == "MESH"]
    keep = [o for o in meshes if lod.lower() in o.name.lower()] or meshes
    drop = [o for o in meshes if o not in keep]
    new = [o for o in new if o not in drop]
    for o in drop: bpy.data.objects.remove(o, do_unlink=True)
    arm = [o for o in new if o.type == "ARMATURE"]
    if arm and action:
        acts = [a for a in bpy.data.actions if a.name.endswith("|"+action) and a.name.startswith(arm[0].name)]
        if acts:
            arm[0].animation_data.action = acts[0]
            try: arm[0].animation_data.action_slot = acts[0].slots[0]
            except Exception: pass
    root = bpy.data.objects.new("root_"+path.split("/")[-1], None)
    bpy.context.scene.collection.objects.link(root)
    for o in new:
        if o.parent is None: o.parent = root
        if mat: whiten(o, mat)
        move_to(o, coll(c))
    move_to(root, coll(c))
    bpy.context.scene.frame_set(frame)
    return root, new
def model_bounds(objs):
    dg = bpy.context.evaluated_depsgraph_get(); pts = []
    for o in objs:
        if o.type != "MESH": continue
        e = o.evaluated_get(dg); m = e.to_mesh(); pts += [e.matrix_world @ v.co for v in m.vertices]; e.to_mesh_clear()
    mn = mathutils.Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = mathutils.Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return mn, mx
def place(root, objs, x, y, z, height=None, rotz=0, ground=True):
    """scale so model height == height, put base at (x,y,z)"""
    bpy.context.view_layer.update()
    mn, mx = model_bounds(objs)
    s = (height / (mx.z - mn.z)) if height else 1.0
    root.scale = (s, s, s); root.rotation_euler = (0, 0, rotz)
    bpy.context.view_layer.update()
    mn, mx = model_bounds(objs)
    c = (mn + mx) / 2
    root.location += mathutils.Vector((x - c.x, y - c.y, z - mn.z if ground else z - c.z))
    bpy.context.view_layer.update()
def load_net(name):
    d = json.load(open(f"{NET}/{name}.json"))
    segs = []
    for e in d["edges"]:
        pts = e["points"][::max(1, len(e["points"]) // 60)] + [e["points"][-1]]
        for a, b in zip(pts, pts[1:]): segs.append((a, b))
    junc = [(n["x"], n["y"]) for n in d["nodes"] if n["type"] == "junction"]
    return np.array(segs, float), np.array(junc, float).reshape(-1, 2)
def hex_tile(name, net, cx, cy, R, mat, depth=0.13, thick=0.3, width=0.085, chamber=0.16, res=300, c="main", rot=0):
    segs, junc = load_net(net)
    allp = segs.reshape(-1, 2)
    ctr = (allp.min(0) + allp.max(0)) / 2
    sc = 0.98 * R / np.max(np.linalg.norm(allp - ctr, axis=1))
    ca, sa = math.cos(rot), math.sin(rot)
    def tf(p):
        q = (p - ctr) * sc; q[..., 1] *= -1
        return np.stack([q[..., 0]*ca - q[..., 1]*sa, q[..., 0]*sa + q[..., 1]*ca], -1)
    S = tf(segs.reshape(-1, 2)).reshape(-1, 2, 2); J = tf(junc) if len(junc) else junc
    bm = bmesh.new()
    cverts = [bm.verts.new((0, 0, 0))] + [bm.verts.new((R*math.cos(math.radians(60*i+30)), R*math.sin(math.radians(60*i+30)), 0)) for i in range(6)]
    for i in range(6): bm.faces.new((cverts[0], cverts[1+i], cverts[1+(i+1)%6]))
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=int(res/3), use_grid_fill=True)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    P = np.array([(v.co.x, v.co.y) for v in bm.verts])
    def heights(P):
        a = S[:, 0]; b = S[:, 1]; ab = b - a; L2 = (ab**2).sum(1) + 1e-9
        dmin = np.full(len(P), 1e9)
        for i in range(0, len(S), 48):
            A_ = a[i:i+48]; AB = ab[i:i+48]; l2 = L2[i:i+48]
            t_ = np.clip(((P[:, None, :] - A_[None]) * AB[None]).sum(2) / l2[None], 0, 1)
            q = A_[None] + t_[..., None] * AB[None]
            dmin = np.minimum(dmin, np.linalg.norm(P[:, None, :] - q, axis=2).min(1))
        w = width * R
        for j in J: dmin = np.minimum(dmin, np.linalg.norm(P - j, axis=1) - (chamber - width) * R)
        t_ = np.clip((dmin - w) / (0.035 * R), 0, 1)
        return ((t_ * t_ * (3 - 2 * t_)) - 1.0) * depth * R
    h = heights(P)
    for v, z in zip(bm.verts, h): v.co.z = z; v.co.x += cx; v.co.y += cy
    bm.edges.ensure_lookup_table()
    bnd = [e for e in bm.edges if len(e.link_faces) == 1]
    zb = -thick * R
    low = {}
    for e in bnd:
        for v in e.verts:
            if v not in low: low[v] = bm.verts.new((v.co.x, v.co.y, zb))
    for e in bnd:
        v1, v2 = e.verts
        f = e.link_faces[0]
        # keep winding outward
        loop = [l for l in f.loops if l.edge == e][0]
        a_, b_ = (loop.vert, loop.link_loop_next.vert)
        bm.faces.new((b_, a_, low[a_], low[b_]))
    bottom_edges = []
    for e in bnd:
        v1, v2 = e.verts
        bottom_edges.append(bm.edges.get((low[v1], low[v2])) or bm.edges.new((low[v1], low[v2])))
    bmesh.ops.triangle_fill(bm, edges=bottom_edges, use_beauty=True)
    mats = []
    for nm in ("top", "wall", "floor", "side"):
        m = bpy.data.materials.get("t_"+nm) or mat.copy(); m.name = "t_"+nm; mats.append(m)
    for f in bm.faces:
        zs = [v.co.z for v in f.verts]
        if min(zs) <= zb + 1e-6: f.material_index = 3
        else:
            zc = sum(zs) / len(zs)
            f.material_index = 0 if zc > -0.12 * depth * R else (2 if zc < -0.88 * depth * R else 1)
    marked = set()
    for e in bnd: marked.add(e)
    for v, lv in low.items():
        if abs(abs(math.atan2(v.co.y-cy, v.co.x-cx)) % (math.pi/3) - math.pi/6) < 1e-3 and abs(math.hypot(v.co.x-cx, v.co.y-cy) - R) < 1e-4:
            e = bm.edges.get((v, lv));  marked.add(e) if e else None
    for e in bottom_edges: marked.add(e)
    mark_idx = None
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    mlist = []
    o = bpy.data.objects.new(name, me); coll("terrain").objects.link(o)
    bm2 = bmesh.new(); bm2.from_mesh(me); fsl = bm2.edges.layers.int.new("fs"); bm2.edges.ensure_lookup_table(); bm2.faces.ensure_lookup_table()
    for e in bm2.edges:
        lf = e.link_faces
        if len(lf) == 2 and ((lf[0].material_index == 3) != (lf[1].material_index == 3)): e.smooth = False; mark_e = True
        else: mark_e = False
        if len(lf) == 2 and lf[0].material_index == 3 and lf[1].material_index == 3:
            n0, n1 = lf[0].normal, lf[1].normal
            if n0.dot(n1) < 0.9: mark_e = True
        e[fsl] = 1 if mark_e else 0
    marks = [e[fsl] for e in bm2.edges]
    bm2.free()
    for e, mk_ in zip(me.edges, marks):
        try: e.use_freestyle_mark = bool(mk_)
        except Exception:
            pass
    if not hasattr(me.edges[0], "use_freestyle_mark"):
        attr = me.attributes.get("freestyle_edge") or me.attributes.new("freestyle_edge", "BOOLEAN", "EDGE")
        for i, mk_ in enumerate(marks): attr.data[i].value = bool(mk_)
    # contours (rim + floor) in 3D
    G = 420; gx = np.linspace(-R, R, G); GX, GY = np.meshgrid(gx, gx)
    GP = np.stack([GX.ravel(), GY.ravel()], 1)
    a_ = S[:, 0]; ab_ = S[:, 1] - a_; L2_ = (ab_**2).sum(1) + 1e-9
    dm = np.full(len(GP), 1e9)
    for i in range(0, len(S), 48):
        A_ = a_[i:i+48]; AB = ab_[i:i+48]; l2 = L2_[i:i+48]
        tt = np.clip(((GP[:, None, :] - A_[None]) * AB[None]).sum(2) / l2[None], 0, 1)
        dm = np.minimum(dm, np.linalg.norm(GP[:, None, :] - (A_[None] + tt[..., None] * AB[None]), axis=2).min(1))
    for j in J: dm = np.minimum(dm, np.linalg.norm(GP - j, axis=1) - (chamber - width) * R)
    D = dm.reshape(G, G)
    inside = lambda x, y: all((x*math.cos(math.radians(60*k)) + y*math.sin(math.radians(60*k))) <= R*math.cos(math.radians(30)) - 0.004*R for k in range(6))
    segs3 = []
    for lvl, zz in ((width*R + 0.035*R*0.98, 0.0), (width*R + 0.035*R*0.02, -depth*R)):
        F = D - lvl
        for i in range(G-1):
            for j in range(G-1):
                c = [F[i,j], F[i,j+1], F[i+1,j+1], F[i+1,j]]
                if (min(c) > 0) or (max(c) < 0): continue
                P4 = [(gx[j],gx[i]),(gx[j+1],gx[i]),(gx[j+1],gx[i+1]),(gx[j],gx[i+1])]
                pts = []
                for k in range(4):
                    c1, c2 = c[k], c[(k+1)%4]
                    if (c1 < 0) != (c2 < 0):
                        t3 = c1 / (c1 - c2); p1, p2 = P4[k], P4[(k+1)%4]
                        pts.append((p1[0] + t3*(p2[0]-p1[0]), p1[1] + t3*(p2[1]-p1[1])))
                for k in range(0, len(pts) - 1, 2):
                    (x1, y1), (x2, y2) = pts[k], pts[k+1]
                    if inside(x1, y1) and inside(x2, y2):
                        segs3.append(((x1+cx, y1+cy, zz + 0.002*R), (x2+cx, y2+cy, zz + 0.002*R)))
    for a_, b_ in segs3: LINES.append({"p": [list(a_), list(b_)], "w": 1.9, "dash": None})
    TILES[name] = dict(S=S + np.array([cx, cy]), J=(J + np.array([cx, cy])) if len(J) else J, cx=cx, cy=cy, R=R, floor=-depth*R, top=0.0)
    xs = np.linspace(-R, R, 200); X, Y = np.meshgrid(xs, xs)
    Z = heights(np.stack([X.ravel(), Y.ravel()], 1)).reshape(200, 200)
    return o, Z
def floor_z(Z, R, x, y, cx=0, cy=0):
    res = Z.shape[0]; i = int((y - cy + R) / (2 * R) * (res - 1)); j = int((x - cx + R) / (2 * R) * (res - 1))
    return float(Z[min(max(i, 0), res-1), min(max(j, 0), res-1)])
def ring(x, y, z, r, mat, c="main", minor=0.012):
    bpy.ops.mesh.primitive_torus_add(major_radius=r, minor_radius=minor, location=(x, y, z + minor), major_segments=96, minor_segments=8)
    o = bpy.context.active_object; whiten(o, mat); move_to(o, coll(c)); return o
SCALE = 1.5; TH = 1.9  # v2: larger render, heavier and darker lines
def setup_render(cam_loc, target, ortho=None, res=(1600, 1200), lens=50, crease=140, thick=(4.0, 1.4), dashed=()):
    res = (int(res[0] * SCALE), int(res[1] * SCALE)); thick = (thick[0] * TH, thick[1] * TH)
    sc = bpy.context.scene
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam)
    cam.location = cam_loc
    d = (mathutils.Vector(target) - mathutils.Vector(cam_loc))
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    if ortho: cam.data.type = "ORTHO"; cam.data.ortho_scale = ortho
    else: cam.data.lens = lens
    sc.camera = cam
    sc.render.resolution_x, sc.render.resolution_y = res
    names = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    sc.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in names else "BLENDER_EEVEE_NEXT"
    sc.render.film_transparent = True; sc.view_settings.view_transform = "Standard"
    sc.render.use_freestyle = True; sc.render.line_thickness_mode = "ABSOLUTE"
    vl = sc.view_layers[0]; vl.use_freestyle = True; fs = vl.freestyle_settings
    fs.crease_angle = math.radians(crease)
    for ls in list(fs.linesets): fs.linesets.remove(ls)
    def mk(name, th, col, collection=None, exclude=False, dash=None, **sel):
        ls = fs.linesets.new(name); ls.select_by_visibility = True; ls.visibility = "VISIBLE"; ls.select_by_edge_types = True
        for k in ("select_silhouette","select_border","select_crease","select_external_contour","select_contour","select_material_boundary","select_suggestive_contour","select_ridge_valley"): setattr(ls, k, False)
        for k, v in sel.items(): setattr(ls, k, v)
        if collection is not None:
            ls.select_by_collection = True; ls.collection = collection; ls.collection_negation = "EXCLUSIVE" if exclude else "INCLUSIVE"
        st = ls.linestyle; st.thickness = th; st.color = col; st.caps = "ROUND"
        if dash:
            st.use_dashed_line = True; st.dash1 = dash[0]; st.gap1 = dash[1]
        return ls
    dc = coll("dashed"); mc = coll("main"); tc = coll("terrain")
    mk("outer", thick[0], (0.0,0.0,0.0), dc, True, select_external_contour=True)
    mk("inner", thick[1], (0.04,0.04,0.04), mc, False, select_silhouette=True, select_crease=True, select_border=True, select_contour=True)
    mk("terrain", thick[1]*1.1, (0.05,0.05,0.05), tc, False, select_edge_mark=True)
    mk("d_outer", thick[1]*1.2, (0.05,0.05,0.05), dc, False, (21, 15), select_silhouette=True, select_border=True, select_external_contour=True)
    return cam
def render(out):
    sc = bpy.context.scene; sc.render.filepath = out
    sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
    bpy.ops.render.render(write_still=True)
def export_contours(path):
    from bpy_extras.object_utils import world_to_camera_view
    sc = bpy.context.scene; cam = sc.camera; dg = bpy.context.evaluated_depsgraph_get()
    W_, H_ = sc.render.resolution_x, sc.render.resolution_y
    fwd = (cam.matrix_world.to_quaternion() @ mathutils.Vector((0, 0, -1))).normalized()
    def vis(p):
        p = mathutils.Vector(p)
        if cam.data.type == "ORTHO": org = p - fwd * 50; d = fwd
        else: org = cam.matrix_world.translation; d = (p - org).normalized()
        hit, loc, *_ = sc.ray_cast(dg, org, d)
        return (not hit) or (loc - org).length >= (p - org).length - 0.006
    def pr(p):
        q = world_to_camera_view(sc, cam, mathutils.Vector(p)); return [q.x * W_, (1 - q.y) * H_]
    out = []
    for L in LINES:
        pts = [mathutils.Vector(p) for p in L["p"]]
        # densify long polylines for visibility tests
        dense = [pts[0]]
        for a, b in zip(pts, pts[1:]):
            n = max(1, int((b - a).length / 0.01))
            dense += [a.lerp(b, (k+1)/n) for k in range(n)]
        runs, cur = [], []
        for a, b in zip(dense, dense[1:]):
            qa = world_to_camera_view(sc, cam, a); qb = world_to_camera_view(sc, cam, b)
            if qa.z > 0.02 and qb.z > 0.02 and vis((a + b) / 2):
                if not cur: cur = [pr(a)]
                cur.append(pr(b))
            elif cur: runs.append(cur); cur = []
        if cur: runs.append(cur)
        for r in runs: out.append({"pts": r, "w": L["w"], "dash": L["dash"]})
    json.dump(out, open(path, "w"))
