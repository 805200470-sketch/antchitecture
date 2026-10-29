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
        public Material microLife;      // the particle nest: the small life (springtails, mites, midges, seed fluff)
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
        [NonSerialized] public float[] pointDensity;  // how much denser than an even spread each point lies (null: even)
        [NonSerialized] public byte[] pointOpen;      // how open each point is, 0 (deep in a crevice) .. 255 (null: all open)
        // its growth map (Tools/plants/growth_map.py; null: none): how far up the plant each point is along its surface
        // from its foot (0..1 of the farthest), how far from the nearest growing tip (of its height), how much of a stem
        [NonSerialized] public float[] pointGrowth, pointTip;
        [NonSerialized] public byte[] pointStem;
        [NonSerialized] public byte[] pointRidge;   // a crystal's: how much each point lies on a ridge or a point (null: none found)
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
                if (head == 0x34504741)
                {
                    // "AGP4": as AGP3, and per point how dense the spread is there and how open it is
                    n = r.ReadInt32();
                    pointArea = r.ReadSingle();
                    var min = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    var size = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()) / 65535f;
                    points = new Vector3[n];
                    pointColors = new Color32[n];
                    pointNormals = new uint[n];
                    pointDensity = new float[n];
                    pointOpen = new byte[n];
                    var dens = new float[128];
                    for (int q = 0; q < 128; q++) dens[q] = Mathf.Pow(2f, (q - 64) / 16f);
                    for (int i = 0; i < n; i++)
                    {
                        points[i] = new Vector3(min.x + r.ReadUInt16() * size.x, min.y + r.ReadUInt16() * size.y, min.z + r.ReadUInt16() * size.z);
                        pointNormals[i] = r.ReadUInt32();
                        byte cr = r.ReadByte(), cg = r.ReadByte(), cb = r.ReadByte(), flags = r.ReadByte();
                        pointColors[i] = new Color32(cr, cg, cb, (byte)((flags & 128) != 0 ? 255 : 0));
                        pointDensity[i] = dens[flags & 127];
                        pointOpen[i] = r.ReadByte();
                    }
                }
                else if (head == 0x33504741)
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
            LoadGrowth(n, asset.bytes);
            return n > 0;
        }

        /// <summary>Its growth map, from Resources/AntGarden/Points/<name>_growth.bytes ("AGG2": n, the points file's mark,
        /// then per point g and t as 16-bit fractions, s a byte, a crystal's ridge a byte), if there is one made for these
        /// points.</summary>
        void LoadGrowth(int n, byte[] pointsFile)
        {
            var asset = Resources.Load<TextAsset>("AntGarden/Points/" + name + "_growth");
            if (asset == null || n <= 0) return;
            uint mark = 2166136261u;
            for (int i = 0; i < pointsFile.Length; i += 997) mark = (mark ^ pointsFile[i]) * 16777619u;
            using (var r = new System.IO.BinaryReader(new System.IO.MemoryStream(asset.bytes)))
            {
                if (r.ReadInt32() != 0x32474741 || r.ReadInt32() != n || r.ReadUInt32() != mark)
                {
                    Debug.LogWarning($"[AntGarden] {name}: its growth map was made for other points, left out (make it again: Tools/plants/growth_map.py)");
                    return;
                }
                pointGrowth = new float[n];
                pointTip = new float[n];
                pointStem = new byte[n];
                var ridge = new byte[n];
                bool any = false;
                for (int i = 0; i < n; i++)
                {
                    pointGrowth[i] = r.ReadUInt16() / 65535f;
                    pointTip[i] = r.ReadUInt16() / 65535f;
                    pointStem[i] = r.ReadByte();
                    any |= (ridge[i] = r.ReadByte()) != 0;
                }
                pointRidge = any ? ridge : null;
            }
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
