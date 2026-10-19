// The particle nest's NPC ants (ParticleAnts.cs): each ant drawn as points spread over its surface
// (AssetBuilder.SaveAntPoints), skinned here with its own skeleton: every point, and its normal, follows the two
// bones that move it most (_Skin: bone × bindpose matrices, this ant's from _SkinBase on).
// Each point is a small opaque bead facing the camera (a disc, a little brighter at its centre), so the ant is a
// body made of points, not a see-through cloud: near points hide far ones, and the ant's own dark mesh under
// them (AntOccluder) hides whatever lies behind it. Beads differ in size, most small, some middling, a few large;
// the large ones only where the surface faces the camera, so the outline stays crisp. Colours: the ant's
// texture (turned by its own hue, _Hue / _Sat; light and dark a little exaggerated, _Contrast), with grains of
// other colours among them: lighter and deeper ones of its own family, now and then a gold or rose fleck.
// Lit by a key light from above the camera, with a thin bright rim along the outline. They shimmer a little.
// Far ants show fewer points (_Count; the last ones shrink away), and an ant in the camera's way dissolves point
// by point (_Fade) instead of vanishing at once.
// The fourth colour byte says what the point is (0 shell, 1 eye, 2 wing: a third of its points, small, pale and a
// little iridescent, so it reads as a thin membrane).
Shader "AntGarden/ParticleAnt"
{
    Properties
    {
        _Size ("Point radius (units)", Float) = 0.0042
        _Gain ("Brightness", Float) = 1.3
        _MinPixels ("Smallest point (pixels)", Float) = 0.7
        _Pale ("How far colours go toward a pale glow", Range(0, 1)) = 0.12
        _Contrast ("How much the texture's light and dark are exaggerated", Float) = 1.5
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
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

            struct AntPoint { float3 pos; float3 nrm; uint col; uint slots; float w; };
            StructuredBuffer<AntPoint> _AntPoints;
            StructuredBuffer<float4x4> _Skin;
            float _SkinBase, _Size, _Gain, _MinPixels, _Pale, _Hue, _Sat, _Contrast;
            float _Count;   // how many of its points are shown (far ants fewer): the last ones shrink away
            float _Fade;    // how much of the ant is there: it dissolves point by point (out of the camera's way)

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 col : COLOR;
                UNITY_FOG_COORDS(1)
            };

            static const float2 corners[3] = { float2(0, 2), float2(-1.7320508, -1), float2(1.7320508, -1) };

            float Hash(uint n)
            {
                n = n * 747796405u + 2891336453u;
                n = ((n >> ((n >> 28u) + 4u)) ^ n) * 277803737u;
                n = (n >> 22u) ^ n;
                return (n & 0xFFFFFF) / 16777216.0;
            }

            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                uint i = id / 3;
                AntPoint p = _AntPoints[i];
                uint b = (uint)_SkinBase;
                float4x4 m0 = _Skin[b + (p.slots & 0xFFFF)], m1 = _Skin[b + (p.slots >> 16)];
                float3 pos = lerp(mul(m1, float4(p.pos, 1)).xyz, mul(m0, float4(p.pos, 1)).xyz, p.w);
                float3 nrm = normalize(lerp(mul((float3x3)m1, p.nrm), mul((float3x3)m0, p.nrm), p.w));
                float h = Hash(i), hs = Hash(i + 7919u), hc = Hash(i + 104729u);
                float t = _Time.y;
                pos += 0.002 * float3(sin(t * (1.1 + h * 1.3) + h * 17.0), sin(t * (0.9 + h * 1.1) + h * 29.0), sin(t * (1.0 + h * 1.7) + h * 41.0));
                float3 V = normalize(_WorldSpaceCameraPos - pos);
                float3 n = dot(nrm, V) < 0 ? -nrm : nrm;
                float facing = saturate(dot(n, V));
                uint kind = (p.col >> 24) & 255;
                // a wing is a thin membrane: a third of its points, small and pale
                if (kind == 2 && hc > 0.33) { o.pos = float4(0, 0, 0, 1); return o; }
                // dissolving, and far: fewer points, the last ones shrinking away
                float lodK = saturate((_Count - i) / max(1, _Count * 0.15));
                if (Hash(i + 31337u) > _Fade || lodK <= 0) { o.pos = float4(0, 0, 0, 1); return o; }
                // most beads small, some middling, a few large; large only where the surface faces the camera
                float grow = kind == 2 ? 0.55 : hs < 0.05 ? 1.9 : hs < 0.2 ? 1.35 : 0.65 + 0.5 * h;
                grow = lerp(min(grow, 0.9), grow, smoothstep(0.35, 0.75, facing));
                float dist = distance(pos, _WorldSpaceCameraPos);
                float pixel = 2 * dist / (unity_CameraProjection._m11 * _ScreenParams.y);
                float want = _Size * grow * lodK;
                float size = max(want, pixel * _MinPixels);
                float2 c = corners[id % 3];
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                o.pos = mul(UNITY_MATRIX_VP, float4(pos + n * 0.004 + (right * c.x + up * c.y) * size, 1));
                o.uv = c;
                // the texture's colour, its light and dark a little exaggerated
                float3 rgb = GammaToLinearSpace(float3(p.col & 255, (p.col >> 8) & 255, (p.col >> 16) & 255) / 255.0);
                rgb = AG_HueShift(rgb, _Hue, _Sat);
                float lum = max(1e-3, dot(rgb, float3(0.3, 0.55, 0.15)));
                rgb *= pow(lum / 0.18, _Contrast - 1);
                rgb = lerp(rgb, float3(0.80, 0.90, 0.86) * lum * 2.2, _Pale);
                // grains of other colours: lighter and deeper ones of its own family, now and then gold or rose
                if (kind == 0)
                {
                    float3 deep = AG_HueShift(rgb, 28, 1.15) * 0.6;
                    rgb = hc < 0.12 ? rgb * 1.45 + float3(0.05, 0.09, 0.1)
                        : hc < 0.2 ? deep
                        : hc < 0.245 ? float3(0.95, 0.66, 0.26) * (0.35 + lum * 1.6)
                        : hc < 0.27 ? float3(0.95, 0.45, 0.65) * (0.3 + lum * 1.4)
                        : rgb;
                }
                // lit: a key light from above the camera (wrapped), and a thin bright rim along the outline
                float3 L = normalize(UNITY_MATRIX_V[1].xyz * 0.9 - UNITY_MATRIX_V[2].xyz * 0.35 + UNITY_MATRIX_V[0].xyz * 0.25);
                float key = saturate(dot(n, L) * 0.6 + 0.4);
                key *= key;
                float rim = pow(1 - facing, 4);
                float3 lit = rgb * (0.45 + 1.4 * key) + float3(0.6, 0.85, 1.0) * rim * 0.5 * (0.3 + lum * 2);
                float twinkle = 0.9 + 0.1 * sin(t * (0.6 + h) + h * 23);
                // the wing's points: pale, a little iridescent
                if (kind == 2) lit = lerp(float3(0.55, 0.85, 1.0), float3(0.75, 0.65, 1.0), hc * 3) * (0.25 + 0.5 * key) + float3(0.6, 0.85, 1.0) * rim * 0.4;
                o.col = lit * _Gain * twinkle * (kind == 1 ? 0.35 : 1);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r2 = dot(i.uv, i.uv);
                clip(1 - r2);
                // a bead: a little brighter at its centre
                fixed4 col = fixed4(i.col * (1.15 - 0.45 * r2), 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
