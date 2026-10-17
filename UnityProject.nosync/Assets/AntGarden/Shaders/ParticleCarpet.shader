// The particle nest's floor round the ant (ParticleLook.cs, "the carpet"): a dense grid of points made here on
// the GPU, so the ground underfoot is a field of points as fine as the plants', not a few scattered stars. Point
// i of an N × N grid sits in a cell fixed to the world (the grid moves with the ant by whole cells, each point
// jittered in its cell by a hash of the cell), on the terrain's height (_HeightMap, read with manual bilinear) and
// only where the terrain is floor (_LayerMap: floor, bank, plateau, open; the walls are the shell's). Its colour is
// the floor texture as the terrain shader paints it (top projection, tint, hue), its light and dark deciding how
// bright the point is and whether it is drawn at all, like a halftone (_Grain), and a share of its own colour
// (_TexMix) over the pale floor tint. Each point is drawn only within its own distance of the ant, between
// _Fade.x and _Fade.y, so the carpet thins out at its edge; round the ant itself the ground brightens a little.
Shader "AntGarden/ParticleCarpet"
{
    Properties
    {
        _Size ("Point radius (units)", Float) = 0.0055
        _Gain ("Brightness", Float) = 0.1
        _MinPixels ("Smallest point (pixels)", Float) = 0.8
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
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

            sampler2D _HeightMap, _LayerMap, _FloorTex;
            float4 _HeightMap_TexelSize, _FloorTex_TexelSize;
            float4 _HeightRect;     // the height map's corner (x, z) and size (x, z)
            float4 _TrailPlayer;    // global: the player's ant
            float4 _Fade;           // x: thinning out from, y: gone by (units from the ant)
            float4 _FloorTint, _TintFloor, _TintBank;   // the floor texture's tint; the pale point tints (linear)
            float _Step, _N, _Size, _Gain, _MinPixels, _FloorScale, _HueShift, _Saturation, _MeanLum, _Grain, _TexMix;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 col : COLOR;
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
                if (d > lerp(_Fade.x, _Fade.y, Hash(cell + 104729))) return o;
                float2 uv = (xz - _HeightRect.xy) / _HeightRect.zw;
                if (any(uv < 0) || any(uv > 1)) return o;
                float4 lw = tex2Dlod(_LayerMap, float4(uv, 0, 0));   // floor, bank, plateau, open
                if (lw.r < 0.25) return o;
                // the floor texture, as the terrain paints it, and its light and dark
                float mip = log2(max(1, _Step * _FloorScale * _FloorTex_TexelSize.z));
                float3 albedo = AG_HueShift(tex2Dlod(_FloorTex, float4(xz * _FloorScale, 0, mip)).rgb * _FloorTint.rgb, _HueShift, _Saturation);
                float al = max(1e-4, dot(albedo, float3(0.299, 0.587, 0.114)));
                float rel = al / _MeanLum;
                if (Hash(cell + 15485863) > lerp(1, saturate(0.05 + 0.95 * pow(rel, 2)), _Grain)) return o;
                float lum = (0.3 * lw.r + 0.75 * lw.g) / max(1e-3, lw.r + lw.g) * (0.55 + 0.45 * lw.a);
                lum *= lerp(1, clamp(0.06 + 0.94 * pow(rel, 2.4), 0.03, 3.5), _Grain) * (0.9 + 0.2 * Hash(cell + 3571));
                float3 tint = (_TintFloor.rgb * lw.r + _TintBank.rgb * lw.g) / max(1e-3, lw.r + lw.g);
                float3 rgb = lerp(tint * lum, albedo * (lum / al), _TexMix) * _Gain;
                rgb *= 1 + 1.4 * (1 - smoothstep(0.5, 3.0, d));   // round the ant, as if its antennae felt the ground
                float3 pos = float3(xz.x, Height(uv) + 0.04, xz.y);
                float dist = distance(pos, _WorldSpaceCameraPos);
                float pixel = 2 * dist / (unity_CameraProjection._m11 * _ScreenParams.y);
                float h = Hash(cell + 7);
                float want = _Size * (0.6 + 0.8 * h);
                float size = max(want, pixel * _MinPixels);
                float2 c = corners[id % 3];
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                o.pos = mul(UNITY_MATRIX_VP, float4(pos + (right * c.x + up * c.y) * size, 1));
                o.uv = c;
                o.col = rgb * lerp(0.3, 1, saturate(want / size)) * (0.85 + 0.15 * sin(_Time.y * (0.5 + h) + h * 23));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r2 = dot(i.uv, i.uv);
                float g = exp(-r2 * 4.0) * (1 - smoothstep(0.8, 1.0, r2));
                fixed4 col = fixed4(i.col * g, 0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
                return col;
            }
            ENDCG
        }
    }
}
