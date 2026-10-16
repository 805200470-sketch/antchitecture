// Ant Garden core — the `seed`, `path`, `frame`, `field`, `ground` and `stitch` sections of
// src/config.js that the path → canyon logic reads, with the same default values.
// World units: 1 unit ≈ 1 ant body length. Image pixels map onto the XZ plane.
namespace AntGarden.Core
{
    /// <summary>
    /// Every tunable the canyon generator reads. <see cref="Default"/> returns the numbers in the web
    /// prototype's config.js (CONFIG.seed / path / field / frame / ground / stitch). A few values
    /// that the core never reads are carried along for the Unity side; they are marked as such.
    /// </summary>
    public sealed class PathConfig
    {
        // ---- seed ----------------------------------------------------------------------------
        /// <summary>CONFIG.seed. The field noise uses Seed + 11, the per-path widths Seed + 77.</summary>
        public int Seed = 20261002;

        // ---- path ----------------------------------------------------------------------------
        /// <summary>path.worldSize: the source image's longest side spans this many world units (0 = use <see cref="PxToWorld"/>).</summary>
        public double WorldSize = 106;
        /// <summary>path.pxToWorld: used only when <see cref="WorldSize"/> is 0.</summary>
        public double PxToWorld = 0.11;
        /// <summary>path.widthMode: "designed" | "measured" | "auto".</summary>
        public string WidthMode = "designed";
        /// <summary>path.lineDrawingMaxWidth ("auto" only): narrower recognized widths count as a line drawing.</summary>
        public double LineDrawingMaxWidth = 1.5;
        /// <summary>path.measuredInfluence ("designed"): how strongly the photo's relative widths carry over (0 = ignore).</summary>
        public double MeasuredInfluence = 1.5;
        /// <summary>path.fixedWidth: "designed" canyon floor width before per-path variation.</summary>
        public double FixedWidth = 12.0;
        /// <summary>path.edgeWidthRange: each path's base width multiplier is drawn from [min, max].</summary>
        public double[] EdgeWidthRange = { 0.8, 1.4 };
        /// <summary>path.widthVariation: ± fraction, swelling and narrowing along each path.</summary>
        public double WidthVariation = 0.35;
        /// <summary>path.widthFrequency: how often the width swells (per world unit).</summary>
        public double WidthFrequency = 0.05;
        /// <summary>path.pinch: occasional squeeze points.</summary>
        public double Pinch = 0.3;
        /// <summary>path.minHalfWidth.</summary>
        public double MinHalfWidth = 3.2;
        /// <summary>path.maxHalfWidth.</summary>
        public double MaxHalfWidth = 10;
        /// <summary>path.chamberNoise: irregular chamber outlines (rounded lobes).</summary>
        public double ChamberNoise = 0.3;
        /// <summary>path.chamberMin: minimum chamber radius (world units).</summary>
        public double ChamberMin = 9;
        /// <summary>path.chamberMax: maximum chamber radius (world units).</summary>
        public double ChamberMax = 18;
        /// <summary>path.pondChance (not read by the core): junction chambers that get a still pond.</summary>
        public double PondChance = 0.7;
        /// <summary>path.widthScale ("measured"): corridor width = meanWidthPx * scale * widthScale.</summary>
        public double WidthScale = 1.35;
        /// <summary>path.minWidth ("measured").</summary>
        public double MinWidth = 3.2;
        /// <summary>path.maxWidth ("measured").</summary>
        public double MaxWidth = 12;
        /// <summary>path.junctionClearing: junction chamber radius = this * widest incident half-width.</summary>
        public double JunctionClearing = 2.2;
        /// <summary>path.endpointClearing: inner dead ends become rounded chambers.</summary>
        public double EndpointClearing = 1.7;
        /// <summary>path.resampleStep: polyline resampling step (world units).</summary>
        public double ResampleStep = 0.8;
        /// <summary>path.defaultSample (not read by the core).</summary>
        public string DefaultSample = "path-013";

        // ---- field ---------------------------------------------------------------------------
        /// <summary>field.cell: distance-field grid cell size.</summary>
        public double FieldCell = 0.5;

        // ---- frame ---------------------------------------------------------------------------
        /// <summary>frame.fill: the starting hexagon is this fraction of the one inscribed in the image.</summary>
        public double FrameFill = 0.985;
        /// <summary>frame.borderMargin: path points this close to the image edge count as exit tails.</summary>
        public double FrameBorderMargin = 0.035;
        /// <summary>frame.innerPad: room between inner features (loops, junctions) and the frame.</summary>
        public double FrameInnerPad = 11;
        /// <summary>frame.wallMargin: the ant stops this far inside the frame.</summary>
        public double FrameWallMargin = 0.9;
        /// <summary>frame.sideDepth (not read by the core): depth of the soil cross-section below the plateau.</summary>
        public double FrameSideDepth = 7;
        /// <summary>frame.exitRadius (not read by the core): how close to an exit the "next module" prompt appears.</summary>
        public double FrameExitRadius = 3.2;
        /// <summary>frame.forceR: minimum hexagon circumradius (set by the stitcher so modules share one size).</summary>
        public double? FrameForceR = null;

        // ---- ground (height profile) -----------------------------------------------------------
        /// <summary>ground.undulation: gentle floor noise amplitude.</summary>
        public double GroundUndulation = 0.3;
        /// <summary>ground.bankHeight: the canyon walls, a mossy bank rising to the plateau.</summary>
        public double GroundBankHeight = 5.0;
        /// <summary>ground.bankWidth.</summary>
        public double GroundBankWidth = 4.5;
        /// <summary>ground.bankLumps.</summary>
        public double GroundBankLumps = 0.7;
        /// <summary>ground.plateauNoise.</summary>
        public double GroundPlateauNoise = 1.1;

        // ---- stitch --------------------------------------------------------------------------
        /// <summary>stitch.portInset: how far inside the frame a tail is cut back before the connector canyon takes over.</summary>
        public double StitchPortInset = 9;
        /// <summary>stitch.linkBulge: how much wider the connector is at the seam (0.3 = +30%).</summary>
        public double StitchLinkBulge = 0.3;

        /// <summary>A fresh config with the config.js defaults.</summary>
        public static PathConfig Default()
        {
            return new PathConfig();
        }

        /// <summary>Deep copy.</summary>
        public PathConfig Clone()
        {
            var c = (PathConfig)MemberwiseClone();
            c.EdgeWidthRange = EdgeWidthRange == null ? null : (double[])EdgeWidthRange.Clone();
            return c;
        }
    }
}
