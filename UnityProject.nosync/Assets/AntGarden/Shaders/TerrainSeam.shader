// The seam between two honeycomb cells with different ground (blue moss / amber soil), without a hard line:
// a copy of the terrain triangles near the seam, drawn over them in the other cell's ground, with holes cut by
// a noise pattern so the two grounds interleave in patches that thin out away from the seam.
// Mesh uv: x = the other ground's share at that vertex (0.5 on the seam, 0 a few units in), y = 1 on the
// second cell of the pair: there the pattern is inverted, so both sides cut the same patches and they meet.
// Same look as Terrain.shader (TerrainCore.cginc); slightly pulled toward the camera so it wins over the
// terrain below it.
Shader "AntGarden/TerrainSeam"
{
    Properties
    {
        _FloorTex ("Floor", 2D) = "gray" {}
        _BankTex ("Bank", 2D) = "gray" {}
        _PlateauTex ("Plateau", 2D) = "gray" {}
        _FloorNormal ("Floor normal", 2D) = "bump" {}
        _FloorTex2 ("Floor variant 2", 2D) = "gray" {}
        _FloorTex3 ("Floor variant 3", 2D) = "gray" {}
        _FloorNormal2 ("Floor variant 2 normal", 2D) = "bump" {}
        _FloorNormal3 ("Floor variant 3 normal", 2D) = "bump" {}
        _FloorVariety ("Floor variants on (0 = one floor texture)", Range(0, 1)) = 0
        _VarietyScale ("Size of the floor variant patches (1 / units)", Float) = 0.06
        _BankNormal ("Bank normal", 2D) = "bump" {}
        _PlateauNormal ("Plateau normal", 2D) = "bump" {}
        _DetailScale ("Close-up detail scale (x floor scale)", Float) = 3.7
        _DetailMix ("Close-up detail amount", Range(0, 1)) = 0.45
        _ParallaxDepth ("Floor relief depth (world units, 0 = off)", Range(0, 0.4)) = 0.12
        _ParallaxRange ("Relief fades out beyond this distance", Float) = 22
        _HeightBlend ("Layers meet along their heights", Range(0, 1)) = 0.6
        _Cavity ("Crevice shading from the height map", Range(0, 1)) = 0.2
        _NormalStrength ("Normal strength", Range(0, 3)) = 1.4
        _FloorTint ("Floor tint", Color) = (0.85, 1.05, 1.08, 1)
        _BankTint ("Bank tint", Color) = (0.75, 0.98, 0.92, 1)
        _PlateauTint ("Plateau tint", Color) = (0.55, 0.72, 0.76, 1)
        _HueShift ("Hue shift (degrees)", Range(-180, 180)) = 0
        _Saturation ("Saturation", Range(0, 2)) = 1
        _FloorScale ("Floor texture scale (per unit)", Float) = 0.11
        _BankScale ("Bank texture scale (per unit)", Float) = 0.08
        _PlateauScale ("Plateau texture scale (per unit)", Float) = 0.05
        _FloorSmooth ("Floor smoothness", Range(0, 1)) = 0.38
        _BankSmooth ("Bank smoothness", Range(0, 1)) = 0.12
        _DetailStrength ("Large-scale variation", Range(0, 1)) = 0.35
        _GlowSpecks ("Glow specks on the floor", Range(0, 2)) = 0.6
        _GlowColor ("Glow colour", Color) = (0.55, 1.0, 0.92, 1)
        _SeamNoise ("Size of the interleaving patches (1 / units)", Float) = 0.55
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest-50" }
        LOD 300
        Offset -1, -1

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.5
        #define TERRAIN_SEAM
        #include "TerrainCore.cginc"

        float _SeamNoise;

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // patches: two octaves of value noise, stretched so their shares follow the weight closely
            float2 q = IN.wpos.xz * _SeamNoise;
            float n = vnoise(q) * 0.65 + vnoise(q * 3.1 + 17.3) * 0.35;
            n = saturate((n - 0.5) * 1.8 + 0.5);
            n = IN.seam.y > 0.5 ? 1 - n : n;
            clip(IN.seam.x - n);
            TerrainSurf(IN, o);
        }
        ENDCG
    }
    FallBack Off
}
