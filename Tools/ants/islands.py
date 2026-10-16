# Per-island topology report (read-only): bone, material, V/E/F, Euler characteristic, boundary/non-manifold edges, size.
# usage: Blender -b --factory-startup <blend> -P islands.py -- Ant_00N
import bpy, sys, numpy as np, json
from collections import Counter
AID = sys.argv[sys.argv.index("--") + 1]
S = "/Users/xujiafeng/Desktop/AntGarden/Tools/ants/work"
body = bpy.data.objects[f"{AID}_Body"]
me = body.data
nv = len(me.vertices)
vg = [g.name for g in body.vertex_groups]
dom = np.full(nv, -1)
for v in me.vertices:
    if len(v.groups):
        dom[v.index] = max(v.groups, key=lambda g: g.weight).group
ed = np.empty(len(me.edges) * 2, np.int32); me.edges.foreach_get("vertices", ed); ed = ed.reshape(-1, 2)
lab = np.arange(nv)
while True:
    m = np.minimum(lab[ed[:, 0]], lab[ed[:, 1]]); old = lab.copy()
    np.minimum.at(lab, ed[:, 0], m); np.minimum.at(lab, ed[:, 1], m); lab = lab[lab]
    if (lab == old).all():
        break
uniq, inv = np.unique(lab, return_inverse=True)
n = len(uniq)
# edge -> face count
ce = np.empty(len(me.loops), np.int32); me.loops.foreach_get("edge_index", ce)
efc = np.bincount(ce, minlength=len(me.edges))
pl = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get("loop_start", pl)
pv0 = np.empty(len(me.loops), np.int32); me.loops.foreach_get("vertex_index", pv0)
fisl = inv[pv0[pl]]
mi = np.empty(len(me.polygons), np.int32); me.polygons.foreach_get("material_index", mi)
eisl = inv[ed[:, 0]]
V = np.bincount(inv, minlength=n); E = np.bincount(eisl, minlength=n); F = np.bincount(fisl, minlength=n)
B = np.bincount(eisl[efc == 1], minlength=n); NM = np.bincount(eisl[efc > 2], minlength=n)
co = np.empty(nv * 3); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
rows = []
order = np.argsort(inv); st = np.searchsorted(inv[order], np.arange(n)); en = np.append(st[1:], nv)
for i in range(n):
    vs = order[st[i]:en[i]]
    c = co[vs]
    fm = Counter(mi[fisl == i].tolist()).most_common(1)[0][0] if F[i] else -1
    rows.append({"i": i, "bone": vg[dom[vs[0]]], "mat": int(fm), "V": int(V[i]), "E": int(E[i]), "F": int(F[i]),
                 "chi": int(V[i] - E[i] + F[i]), "boundary_e": int(B[i]), "nonmanifold_e": int(NM[i]),
                 "size": [round(float(x), 4) for x in (c.max(0) - c.min(0))], "center": [round(float(x), 4) for x in c.mean(0)]})
json.dump(rows, open(f"{S}/islands_{AID}.json", "w"), indent=0)
cnt = Counter((r["chi"], r["boundary_e"] > 0, r["nonmanifold_e"] > 0) for r in rows)
print("[ISL]", AID, "islands", n, "(chi, has_boundary, has_nonmanifold) counts:", dict(cnt))
for r in rows:
    if r["bone"].endswith(".R"):
        continue
    print("[ISL]", r["bone"], "mat", r["mat"], "F", r["F"], "chi", r["chi"], "bnd", r["boundary_e"], "nm", r["nonmanifold_e"], "size", r["size"], "c", r["center"])
