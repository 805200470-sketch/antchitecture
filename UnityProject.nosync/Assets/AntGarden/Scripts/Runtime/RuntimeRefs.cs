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
        public Material guideTrail;     // small glowing particles: the way through a ruin, the motes along the seams
        public Material terrainSeam;    // ground interleaving across a seam between honeycomb cells
        public Material particleLook;   // the particle look: plants and ground drawn as glowing points (P)
        public Material particleAnt;    // the particle nest's NPC ants: points skinned with their skeletons
        public Material particleCarpet; // the particle nest: the dense floor of points round the ant, made on the GPU
        public Material antOccluder;    // the particle nest's NPC ants: their meshes as a dark body under their points
        public Material pheromone;      // the particle nest: arrows of light leading the ant
        public Material particleSolid;  // the particle nest: the floor's things as opaque lit splats
        public Material lightThread;    // the particle nest: the guiding threads of light
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
        // the particle look: points on the plant's surface (at height 1, in random order, so any first n are an
        // even sample) and their colours from its texture; alpha 255 marks a point in the glowing part. Read on
        // first use from Resources/AntGarden/Points/<name>.bytes (written by AssetBuilder)
        [NonSerialized] public Vector3[] points;
        [NonSerialized] public Color32[] pointColors;
        [NonSerialized] public uint[] pointNormals;   // octahedral (ThingPen.EncodeNormal); null in older files
        [NonSerialized] public float pointArea;       // the surface the points cover (model units²; 0: not known)
        [NonSerialized] bool pointsTried;

        public bool LoadPoints()
        {
            if (pointsTried) return points != null && points.Length > 0;
            pointsTried = true;
            var asset = Resources.Load<TextAsset>("AntGarden/Points/" + name);
            if (asset == null) return false;
            int n;
            using (var r = new System.IO.BinaryReader(new System.IO.MemoryStream(asset.bytes)))
            {
                int head = r.ReadInt32();
                if (head == 0x33504741)
                {
                    // "AGP3": 16-bit fractions of the bounds, normals, and the surface's area
                    n = r.ReadInt32();
                    pointArea = r.ReadSingle();
                    var min = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    var size = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()) / 65535f;
                    points = new Vector3[n];
                    pointColors = new Color32[n];
                    pointNormals = new uint[n];
                    for (int i = 0; i < n; i++)
                    {
                        points[i] = new Vector3(min.x + r.ReadUInt16() * size.x, min.y + r.ReadUInt16() * size.y, min.z + r.ReadUInt16() * size.z);
                        pointNormals[i] = r.ReadUInt32();
                        pointColors[i] = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                    }
                }
                else if (head == 0x32504741)
                {
                    // "AGP2": 16-bit fractions of the bounds
                    n = r.ReadInt32();
                    var min = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    var size = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()) / 65535f;
                    points = new Vector3[n];
                    pointColors = new Color32[n];
                    for (int i = 0; i < n; i++)
                    {
                        points[i] = new Vector3(min.x + r.ReadUInt16() * size.x, min.y + r.ReadUInt16() * size.y, min.z + r.ReadUInt16() * size.z);
                        pointColors[i] = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                    }
                }
                else
                {
                    n = head;
                    points = new Vector3[n];
                    pointColors = new Color32[n];
                    for (int i = 0; i < n; i++)
                    {
                        points[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        pointColors[i] = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                    }
                }
            }
            return n > 0;
        }
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
