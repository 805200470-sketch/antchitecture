// The particle nest's floor round the ant (ParticleLook.cs, "the carpet"): a dense grid of splats made here on the
// GPU, so the ground underfoot reads as a surface made of points, the way a Gaussian-splat capture covers its
// surfaces: opaque dabs of different sizes (most near the middle size, some smaller, a few much larger), each a
// little irregular in outline and longer one way than the other, the large ones carrying the floor's broad colour (a
// coarser look at its texture) and the fine ones its detail, set a little above them.
// Splat i of an N × N grid sits in a cell fixed to the world (the grid moves with the ant by whole cells, each splat
// jittered in its cell by a hash of the cell), on the terrain's height (_HeightMap, read with manual bilinear) and
// only where the terrain is floor (_LayerMap: floor, bank, plateau, open). Each lies tilted halfway between the
// ground and the camera, so the floor foreshortens like a surface without turning to slivers. Its colour is the
// floor texture as the terrain shader paints it (top projection, tint, hue), its light and dark exaggerated
// (_Grain; the darkest parts left as gaps) and a share of its own colour (_TexMix) over the floor's tint. Each is
// drawn only within its own distance of the ant, between _Fade.x and _Fade.y, shrinking and darkening into the
// night over its last stretch, so the carpet dissolves at its edge; round the ant the ground brightens a little,
// and the guiding pulses running ahead swell and light the splats they pass.
Shader "AntGarden/ParticleCarpet"
{
    Properties
    {
        _Size ("Splat radius (units)", Float) = 0.0075
        _Gain ("Brightness", Float) = 0.22
        _MinPixels ("Smallest splat (pixels)", Float) = 0.8
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+20" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        ZWrite On
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "AntGardenCommon.cginc"
            #include "Chevron.cginc"
            #include "NestField.cginc"

            sampler2D _HeightMap, _LayerMap, _FloorTex;
            float4 _HeightMap_TexelSize, _FloorTex_TexelSize;
            float4 _HeightRect;     // the height map's corner (x, z) and size (x, z)
            float4 _TrailPlayer;    // global: the player's ant
            float4 _Fade;           // x: thinning out from, y: gone by (units from the ant)
            float4 _FloorTint, _TintFloor, _TintBank;   // the floor texture's tint; the floor's own tints (linear)
            float _Step, _N, _Size, _Gain, _MinPixels, _FloorScale, _HueShift, _Saturation, _MeanLum, _Grain, _TexMix;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 col : COLOR;
                float shape : TEXCOORD2;    // a per-splat seed for its outline
                UNITY_FOG_COORDS(1)
            };

            static const float2 corners[3] = { float2(0, 2), float2(-1.7320508, -1), float2(1.7320508, -1) };

            float Hash(float2 p)
            {
                uint2 q = (uint2)(int2)p;
                uint h = q.x * 73856093u ^ q.y * 19349663u;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216.0;
            }

            // smooth value noise (0..1) on a grid of the given size, for patches across the floor
            float Patch(float2 xz, float scale, float seed)
            {
                float2 g = xz / scale + seed, f = frac(g), b = floor(g);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(b), Hash(b + float2(1, 0)), f.x), lerp(Hash(b + float2(0, 1)), Hash(b + float2(1, 1)), f.x), f.y);
            }

            float Height(float2 uv)
            {
                float2 t = uv * _HeightMap_TexelSize.zw - 0.5;
                float2 f = frac(t), b = (floor(t) + 0.5) * _HeightMap_TexelSize.xy;
                float h00 = tex2Dlod(_HeightMap, float4(b, 0, 0)).r;
                float h10 = tex2Dlod(_HeightMap, float4(b + float2(_HeightMap_TexelSize.x, 0), 0, 0)).r;
                float h01 = tex2Dlod(_HeightMap, float4(b + float2(0, _HeightMap_TexelSize.y), 0, 0)).r;
                float h11 = tex2Dlod(_HeightMap, float4(b + _HeightMap_TexelSize.xy, 0, 0)).r;
                return lerp(lerp(h00, h10, f.x), lerp(h01, h11, f.x), f.y);
            }

            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                o.pos = float4(0, 0, 0, 1);
                uint i = id / 3, n = (uint)_N;
                float2 cell = floor(_TrailPlayer.xz / _Step) - floor(_N * 0.5) + float2(i % n, i / n);
                float2 xz = (cell + float2(Hash(cell), Hash(cell + 7919))) * _Step;
                float d = length(xz - _TrailPlayer.xz);
                float reach = lerp(_Fade.x, _Fade.y, Hash(cell + 104729));
                if (d > reach) return o;
                float soft = saturate((reach - d) / 0.9);   // over its last stretch it shrinks and darkens away
                float2 uv = (xz - _HeightRect.xy) / _HeightRect.zw;
                if (any(uv < 0) || any(uv > 1)) return o;
                float4 lw = tex2Dlod(_LayerMap, float4(uv, 0, 0));   // floor, bank, plateau, open
                if (lw.r < 0.25) return o;
                // its size, as in a capture: most near the middle, some smaller, a few much larger (the large ones
                // carry the broad colour and lie a little lower, the fine ones the detail)
                float gN = (Hash(cell + 911) + Hash(cell + 912) + Hash(cell + 913) - 1.5) * 1.6;
                float grow = clamp(exp(gN * 0.5), 0.45, 3.0);
                float big = saturate((grow - 1) / 2);
                // the floor texture, as the terrain paints it (the large splats see it blurred), and its light and dark
                float mip = log2(max(1, _Step * _FloorScale * _FloorTex_TexelSize.z)) + big * 2.5;
                float3 albedo = AG_HueShift(tex2Dlod(_FloorTex, float4(xz * _FloorScale, 0, mip)).rgb * _FloorTint.rgb, _HueShift, _Saturation);
                float al = max(1e-4, dot(albedo, float3(0.299, 0.587, 0.114)));
                float rel = al / _MeanLum;
                // the darkest parts of the texture thin out (the fine splats only)
                if (Hash(cell + 15485863) > lerp(1, saturate(0.45 + 0.55 * pow(rel, 1.5)), _Grain * (1 - big))) return o;
                float lum = (0.3 * lw.r + 0.75 * lw.g) / max(1e-3, lw.r + lw.g) * (0.55 + 0.45 * lw.a);
                lum *= lerp(1, clamp(0.08 + 0.92 * pow(rel, 2.0), 0.04, 3.2), _Grain) * (0.85 + 0.3 * Hash(cell + 3571));
                float3 tint = (_TintFloor.rgb * lw.r + _TintBank.rgb * lw.g) / max(1e-3, lw.r + lw.g);
                float3 rgb = lerp(tint * lum, albedo * (lum / al), _TexMix) * _Gain;
                // patches across the floor, some leaning teal-green, some blue-violet, lighter and darker; and a
                // little drift from splat to splat, as in a capture
                float pa = Patch(xz, 2.8, 17.3), pb = Patch(xz, 1.1, 41.9);
                rgb = AG_HueShift(rgb, (pa - 0.5) * 22 + (Hash(cell + 2741) - 0.5) * 12, 0.85 + 0.3 * pb);
                rgb *= 0.55 + 0.9 * pb * pa;
                // dimmer toward the banks: the floor's edge fades into the walls' points
                rgb *= smoothstep(0.25, 0.75, lw.r);
                rgb *= 1 + 0.8 * (1 - smoothstep(0.5, 3.0, d));   // round the ant, as if its antennae felt the ground
                float3 pos = float3(xz.x, Height(uv) + 0.007 - 0.005 * big, xz.y);
                float chev = Chevron(pos, Hash(cell + 7));       // a guiding pulse passing: the splats swell, lift and light up
                pos.y += 0.04 * chev;
                // rings from the ant's steps lift and light the floor as they pass; a slow breath runs across it; the
                // guiding threads' pools of light
                float rip = NestRipple(xz);
                pos.y += 0.025 * rip;
                rgb *= (1 + 1.4 * rip) * (1 + 0.16 * sin(dot(xz, float2(0.21, 0.13)) - _Time.y * 0.5));
                rgb += NestBeacon(pos) * _BeaconColor.rgb * 0.26 * soft;
                float dist = distance(pos, _WorldSpaceCameraPos);
                float pixel = 2 * dist / (unity_CameraProjection._m11 * _ScreenParams.y);
                float want = _Size * grow * (1 + 0.6 * chev) * lerp(0.35, 1, soft);
                float size = max(want, pixel * _MinPixels);
                // tilted halfway between lying on the ground and facing the camera
                float3 V = normalize(_WorldSpaceCameraPos - pos);
                float3 N = normalize(float3(0, 1, 0) * 0.55 + V * 0.45);
                float3 R = UNITY_MATRIX_V[0].xyz;
                float3 B = normalize(cross(N, R)), T = cross(B, N);
                // a dab a little longer one way than the other, turned at random
                float turn = Hash(cell + 2742) * 6.2831853, cs = cos(turn), sn = sin(turn), stretch = 1 + 0.55 * Hash(cell + 104);
                float3 A1 = T * cs + B * sn, A2 = -T * sn + B * cs;
                float2 c = corners[id % 3];
                o.pos = mul(UNITY_MATRIX_VP, float4(pos + (A1 * (c.x * stretch) + A2 * (c.y / stretch)) * size, 1));
                o.uv = c;
                o.shape = Hash(cell + 13);
                o.col = rgb * soft;
                o.col = lerp(o.col, _ChevronColor.rgb * (_ChevronColor.a * 0.16), saturate(chev));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // a dab with a slightly irregular outline
                float r2 = dot(i.uv, i.uv);
                float ang = atan2(i.uv.y, i.uv.x);
                clip(1 + 0.12 * sin(ang * 3 + i.shape * 31) + 0.07 * sin(ang * 5 + i.shape * 57) - r2);
                // a splat: flat, like a dab of paint, its very edge a touch darker so neighbours stay apart
                fixed4 col = fixed4(i.col * (1 - 0.12 * smoothstep(0.75, 1.0, r2)), 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
