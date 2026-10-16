# Assemble Assets/AntGarden/Art/Ants/ants.json from export_stats_* + verify_stats_* (JsonUtility-friendly: arrays, x/y/z vectors).
import json, os, datetime
SCR = "/Users/xujiafeng/Desktop/AntGarden/Tools/ants/work"
DST = "/Users/xujiafeng/Desktop/AntGarden/UnityProject.nosync/Assets/AntGarden/Art/Ants/ants.json"
R = lambda x, n=4: round(float(x), n) + 0.0          # + 0.0 turns -0.0 into 0.0
V = lambda v, n=4: {"x": R(v[0], n), "y": R(v[1], n), "z": R(v[2], n)}
ants = []
for N in range(1, 6):
    aid = f"Ant_00{N}"
    ex = json.load(open(f"{SCR}/export_stats_{aid}.json"))
    ve = json.load(open(f"{SCR}/verify_stats_{aid}.json"))
    tx = json.load(open(f"{SCR}/tex_stats_{aid}.json"))
    b = ex["body"]; BL = b["bodyLength"]; rm = ex["root_motion"]
    raw = ve["meshes_raw"]
    lods = [{"name": f"{aid}_LOD{i}", "triangles": raw[f"{aid}_LOD{i}"]["triangles"], "vertices": raw[f"{aid}_LOD{i}"]["control_points"],
             "materials": raw[f"{aid}_LOD{i}"]["materials"]} for i in range(3)]
    takes = {t["name"]: t for t in ve["takes_raw"]}
    clips = []
    for c in ex["clips"]:
        t = takes[c["take"]]
        travel = rm.get(c["take"], {})
        entry = {"name": c["take"], "frames": int(round(t["frames_at_24"])), "sampledFrames": c["samples"], "durationSeconds": R(t["stop_s"] - t["start_s"], 5),
                 "loop": c["loop"], "rootMotion": c["root_motion"], "sourceAction": c["source_action"]}
        if "root_travel_unity" in travel:
            entry["rootTravel"] = V(travel["root_travel_unity"])
        elif "root_displacement_unity" in travel:
            entry["rootTravel"] = V(travel["root_displacement_unity"])
        else:
            entry["rootTravel"] = V((0, 0, 0))
        clips.append(entry)
    def cyc(k):
        r = rm[k]
        return {"cycleFrames": r["cycle_frames"], "cycleSeconds": R(r["cycle_seconds"], 5), "rootTravelPerCycle": R(r["travel_per_cycle"]),
                "speedUnitsPerSecond": R(r["speed_units_per_s"]), "speedBodyLengthsPerSecond": R(r["speed_units_per_s"] / BL)}
    tex_dir = aid + "/"
    textures = {"baseColor": tex_dir + f"{aid}_BaseColor.png", "normal": tex_dir + f"{aid}_Normal.png", "roughness": tex_dir + f"{aid}_Roughness.png",
                "size": 2048, "baseColorSRGB": True, "normalConvention": "OpenGL (+Y green), tangent space - import as Normal map",
                "roughnessSRGB": False, "roughnessChannel": "R (R=G=B); roughness, not smoothness: smoothness = 1 - R"}
    mats = []
    for m in ex["materials"]:
        k = m["source"].split("_", 1)[1]
        e = {"name": m["name"], "slot": m["slot"], "baseColor": textures["baseColor"], "normal": textures["normal"]}
        if k == "Shell":
            e.update({"roughness": textures["roughness"], "transparent": False, "opacity": 1.0, "note": "body shell"})
        elif k == "Eye":
            e.update({"roughness": textures["roughness"], "transparent": False, "opacity": 1.0,
                      "note": "eyes: same atlas (eye colour and facets are baked into it); glossy (baked roughness ~0.05)"})
        elif k == "WingMembrane":
            e.update({"roughness": "", "roughnessConstant": 0.12, "transparent": True, "opacity": R(m["alpha"], 3),
                      "note": "translucent wing membrane, closed thin shell: alpha-blend, ZWrite off, Cull Back (Cull Off doubles the layers)"})
        elif k == "WingVein":
            e.update({"roughness": "", "roughnessConstant": 0.3, "transparent": True, "opacity": R(m["alpha"], 3),
                      "note": "wing veins (closed thin tubes on the membrane): alpha-blend, draw after the membrane"})
        mats.append(e)
    a = {"id": aid, "fbx": f"{aid}/{aid}.fbx", "rigNode": f"{aid}_Rig", "rootBone": "root", "rootBonePath": f"{aid}_Rig/root",
         "boneCount": ve["bones_raw"]["count"], "socketBone": "prop",
         "lods": lods,
         "bodyLength": R(BL), "bodyCenterZ": R(b["bodyCenterZ"]), "mandibleTipZ": R(b["mandibleTipZ"]), "gasterTipZ": R(b["gasterTipZ"]),
         "height": R(b["height"]), "heightWithoutAntennae": R(b["height_without_antennae_wings"]),
         "restBoundsMin": V(b["rest_bounds_unity_min"]), "restBoundsMax": V(b["rest_bounds_unity_max"]), "restLowestPointY": R(b["rest_lowest_point_y"]),
         "crawlCycleSpeed": R(rm["Crawl"]["speed_units_per_s"] / BL), "fastCycleSpeed": R(rm["FastCrawl"]["speed_units_per_s"] / BL),
         "crawl": cyc("Crawl"), "fastCrawl": cyc("FastCrawl"),
         "clips": clips, "materials": mats, "textures": textures,
         "verification": {"footTipMaxDeviationLOD0_BL": R(ve["foot_tip_deviation_crawl_inplace_f1_8_15"][f"{aid}_LOD0"]["tip_max_dev_BL"], 5),
                          "footTipMaxDeviationLOD1_BL": R(ve["foot_tip_deviation_crawl_inplace_f1_8_15"][f"{aid}_LOD1"]["tip_max_dev_BL"], 5),
                          "footTipMaxDeviationLOD2_BL": R(ve["foot_tip_deviation_crawl_inplace_f1_8_15"][f"{aid}_LOD2"]["tip_max_dev_BL"], 5),
                          "bakedBoneMaxError_BL": float("%.2g" % max(x["max_err_BL"] for x in ve["bake_vs_source_rig"].values()))},
         "suggestedClips": {"idle": "Idle", "crawl": "Crawl_InPlace", "fast": "FastCrawl_InPlace", "talk": "AntennaTalk"}}
    if ex["has_wings"]:
        to, ld = rm["TakeOff"], rm["Land"]
        a["flight"] = {
            "takeOff": {"frames": int(round(takes["TakeOff"]["frames_at_24"])), "durationSeconds": R(takes["TakeOff"]["stop_s"], 5),
                        "forwardTravel": R(to["forward_travel"]), "forwardTravelBL": R(to["forward_travel"] / BL),
                        "rootRise": R(to["root_height_end"] - to["root_height_start"]), "rootRiseBL": R((to["root_height_end"] - to["root_height_start"]) / BL),
                        "rootEndHeight": R(to["root_height_end"]), "rootMaxHeight": R(to["root_height_max"])},
            "land": {"frames": int(round(takes["Land"]["frames_at_24"])), "durationSeconds": R(takes["Land"]["stop_s"], 5),
                     "forwardTravel": R(ld["forward_travel"]), "forwardTravelBL": R(ld["forward_travel"] / BL),
                     "rootDrop": R(ld["root_height_start"] - ld["root_height_end"]), "rootStartHeight": R(ld["root_height_start"])},
            "fly": dict(cyc("Fly"), cruiseRootHeight=R(sum(rm["Fly"]["root_height_range"]) / 2), cruiseRootHeightBL=R(sum(rm["Fly"]["root_height_range"]) / 2 / BL)),
            "fastFly": dict(cyc("FastFly"), cruiseRootHeight=R(sum(rm["FastFly"]["root_height_range"]) / 2)),
            "note": "TakeOff/Land have no InPlace variants: root (bone 'root') carries the climb/descent and forward travel. TakeOff ends in the Fly f1 pose; Land starts from it."}
        a["suggestedClips"].update({"takeoff": "TakeOff", "fly": "Fly_InPlace", "fastFly": "FastFly_InPlace", "land": "Land"})
        a["wingMembraneTextureNote"] = "membrane charts of the BaseColor atlas were repainted from the model's vertex colours (the source bake had them dark grey)"
    ants.append(a)
doc = {"version": 1, "generated": datetime.date.today().isoformat(),
       "source": "03_Models/Ants_Source.nosync/Ant_00N_Resource/Ant_00N_Resource.blend (Blender 5.0), exported by Tools/ants/export_unity.py",
       "units": "metres; FBX UnitScaleFactor = 100 -> Unity imports at scale 1.0 (keep 'Convert Units' on)",
       "axes": "Unity: +Y up, ant faces +Z, its right side is +X; every FBX node has identity rotation and scale 1",
       "fps": 24, "bodyLengthDefinition": "mandible tip to gaster tip along Z, full-resolution source mesh, rest pose",
       "speedDefinition": "root travel per cycle of the non-InPlace clip / cycle duration / bodyLength",
       "rootTravelDefinition": "clips[].rootTravel: root displacement per loop cycle (loops) or over the whole clip (TakeOff/Land), Unity axes",
       "ants": ants}
tmp = os.path.join(os.path.dirname(DST), ".ants.json.tmp")
with open(tmp, "w") as f:
    json.dump(doc, f, indent=2)
os.replace(tmp, DST)
print("wrote", DST, os.path.getsize(DST))
for a in ants:
    print(a["id"], "BL", a["bodyLength"], "centerZ", a["bodyCenterZ"], "height", a["height"], "crawl", a["crawlCycleSpeed"], "fast", a["fastCycleSpeed"],
          "lods", [l["triangles"] for l in a["lods"]])
