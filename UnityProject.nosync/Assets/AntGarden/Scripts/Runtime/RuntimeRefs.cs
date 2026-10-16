using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace AntGarden
{
    /// <summary>
    /// Asset manifest the runtime loads from Resources/AntGarden/RuntimeRefs.
    /// Filled by the editor AssetBuilder (AntGarden → Build Assets) from plants.json and ants.json.
    /// </summary>
    public class RuntimeRefs : ScriptableObject
    {
        public PostProcessResources postResources;
        public PostProcessProfile postProfile;
        public Material terrain;
        public Material soilWall;
        public Material sky;
        public Material spores;
        public Material slotRing;
        public Material gateCurtain;
        public Material roots;          // hanging tunnel roots
        public Material amber;          // translucent amber (the ancestor's shrine)
        public List<PlantRef> plants = new List<PlantRef>();
        public List<AntRef> ants = new List<AntRef>();
        public List<GroundSet> groundSets = new List<GroundSet>();

        public PlantRef Plant(string name) => plants.Find(p => p.name == name);
        public GroundSet Ground(string id) => string.IsNullOrEmpty(id) ? null : groundSets.Find(g => g.id == id);
        public AntRef Ant(string id) => ants.Find(a => a.id == id);

        public static RuntimeRefs Load() => Resources.Load<RuntimeRefs>("AntGarden/RuntimeRefs");
    }

    /// <summary>One style's ground textures (Art/Ground/&lt;id&gt;/floor|bank|plateau.png and their normal maps).</summary>
    [Serializable]
    public class GroundSet
    {
        public string id;
        public Texture2D floor, bank, plateau, floorNormal, bankNormal, plateauNormal;
        public Texture2D floor2, floor3, floor2Normal, floor3Normal;   // optional sister floors, mixed in patches
    }

    /// <summary>A plant prefab normalized to 1 unit tall with its pivot at the base.</summary>
    [Serializable]
    public class PlantRef
    {
        public string name;
        public GameObject prefab;
        public Vector3 size = Vector3.one;     // normalized (height = 1)
        public float footprintRadius = 0.3f;   // at height 1
        public Vector3[] circles;              // (x, z, r) collision circles at height 1
    }

    /// <summary>An ant prefab (LODGroup + Animator) with its clips and gait measurements.</summary>
    [Serializable]
    public class AntRef
    {
        public string id;
        public GameObject prefab;
        public AnimationClip idle, crawl, fast, talk, takeoff, fly, land;
        public float modelBodyLength = 1f;     // mandible tip to gaster tip, model units
        public float modelCenterZ;             // body centre along +Z, model units
        public float crawlCycleSpeed = 0.15f;  // ground speed the Crawl clip was authored for (body lengths / s)
        public float fastCycleSpeed = 0.45f;
        public float takeoffForward = 0.45f;   // flyers: how far TakeOff carries the body forward (body lengths)
        public float takeoffRise = 1.0f;       // and how high it ends above the floor (body lengths)
        public float landForward = 0.5f;       // how far Land carries the body forward (body lengths)
    }
}
