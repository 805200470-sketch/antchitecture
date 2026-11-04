#!/usr/bin/env python3
"""pack_k19.py <after out> <before out> <v15 dir> <round dir> <keys dir> [neighbour name=dir ...]

Blind review packages for the kiln's k19 round (richness): three sets under neutral letters, the same views of each,
so a reviewer judges without knowing which is the new one. The letters' meaning goes to <keys dir>/specs/k19.json only.
- blind_content: after, before (the user, 11-04: "太空了"), v15 (the user's approved template), game and ant's-eye views
  of the way in, halfway and the way out, and the aerial.
- blind_plants: the same three sets' plant close-ups (photo_/eye_ views of the nest's plants and landmarks).
- blind_colour: after and before (colours accepted at k18 but the family), the same views; neighbours copied as named.
"""
import json, os, random, shutil, sys

after, before, v15, out, keys = sys.argv[1:6]
neigh = dict(a.split("=", 1) for a in sys.argv[6:])
MAIN = ["r0_1_play_lab_way_in", "r0_1_eye_lab_way_in", "r0_2_play_lab_halfway", "r0_2_eye_lab_halfway",
        "r0_3_play_lab_heart", "r0_3_eye_lab_heart", "r0_9_aerial"]


def views(d, prefixes):
    return sorted(f[:-4] for f in os.listdir(d) if f.endswith(".png") and any(f.startswith(p) for p in prefixes))


def copy_set(src, names, dst, letter):
    os.makedirs(dst, exist_ok=True)
    n = 0
    for name in names:
        p = os.path.join(src, name + ".png")
        if os.path.exists(p):
            shutil.copy(p, os.path.join(dst, f"{letter}_{name}.png"))
            n += 1
    return n


rng = random.Random(1904)
spec = {"round": "k19", "sets": {}}
for pkg, sets, names in [
    ("blind_content", {"after": after, "before": before, "v15": v15}, MAIN),
    ("blind_colour", {"after": after, "before": before}, MAIN),
]:
    letters = ["甲", "乙", "丙"][:len(sets)]
    order = list(sets)
    rng.shuffle(order)
    spec["sets"][pkg] = {}
    for letter, which in zip(letters, order):
        n = copy_set(sets[which], names, os.path.join(out, pkg), letter)
        spec["sets"][pkg][letter] = which
        print(f"{pkg}: {letter} {n} stills")
# plants: every photo_ and eye_ view of the nest's own plants and landmarks, after and before; v15's as the scale
order = ["after", "before", "v15"]
rng.shuffle(order)
spec["sets"]["blind_plants"] = {}
for letter, which in zip(["甲", "乙", "丙"], order):
    src = {"after": after, "before": before, "v15": v15}[which]
    names = [v for v in views(src, ["r0_"]) if ("_photo_" in v or "_eye_lab_" in v) and not v.startswith(("r0_1_", "r0_2_", "r0_3_"))]
    n = copy_set(src, names, os.path.join(out, "blind_plants"), letter)
    spec["sets"]["blind_plants"][letter] = which
    print(f"blind_plants: {letter} {n} stills")
for name, d in neigh.items():
    dst = os.path.join(out, "neighbours", name)
    os.makedirs(dst, exist_ok=True)
    for f in sorted(os.listdir(d)):
        if f.endswith((".png", ".jpg")):
            shutil.copy(os.path.join(d, f), dst)
    print(f"neighbours: {name} {len(os.listdir(dst))}")
os.makedirs(os.path.join(keys, "specs"), exist_ok=True)
with open(os.path.join(keys, "specs", "k19.json"), "w") as f:
    json.dump(spec, f, ensure_ascii=False, indent=1)
print("key:", os.path.join(keys, "specs", "k19.json"))
