// In a headset: the player's own ant seen through its eyes (VRSelf.cs), its points skinned as ParticleAnt's are, but
// see-through (the user, 11-01: "要变得更透明，可以直接调整透明度，能看出来蚂蚁造型的边界即可"): a point is most there
// where the surface turns away from the eyes, so the ant's outline shows and its face toward them is a faint glass;
// nothing of it shows close in front of the eyes (its face, the roots of its antennae), its eyes not at all. No depth
// of its own, so it hides nothing behind it; drawn after the solid nest (which still hides a leg sunk in the floor),
// before the way's veil, what glows and the screen's words. In a Resources folder so the build keeps it.
Shader "AntGarden/VRGhostAnt"
{
    Properties
    {
        _Size ("Point radius (units)", Float) = 0.003
        _Gain ("Brightness", Float) = 0.4
        _MinPixels ("Smallest point (pixels)", Float) = 0.7
        _Pale ("How far colours go toward a pale glow", Range(0, 1)) = 0.06
        _Contrast ("How much the texture's light and dark are exaggerated", Float) = 1.2
        _Alpha ("Opacity along its outline", Range(0, 1)) = 0.35
        _Glass ("Where it faces the eyes, this much of that", Range(0, 1)) = 0.12
        _Near ("Nothing of it nearer the eyes than this (units)", Float) = 0.08
        _Whole ("And all of it from here on (units)", Float) = 0.22
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-20" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        ZTest LEqual
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Assets/AntGarden/Shaders/AntGardenCommon.cginc"
            #include "Assets/AntGarden/Shaders/AGClock.cginc"

            struct AntPoint { float3 pos; float3 nrm; uint col; uint slots; float w; };
            StructuredBuffer<AntPoint> _AntPoints;
            StructuredBuffer<float4x4> _Skin;
            float _Size, _Gain, _MinPixels, _Pale, _Hue, _Sat, _Contrast, _Alpha, _Glass, _Near, _Whole;
            float4 _AntRimColor, _AntPaleColor, _AntTint;   // global (ParticleAnts, from the nest's theme)

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 col : COLOR;
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
                uint kind = (p.col >> 24) & 255;
                float h = Hash(i), hs = Hash(i + 7919u);
                // its eyes are where the player's are; a wing is a thin membrane (a third of its points)
                if (kind == 1 || (kind == 2 && Hash(i + 104729u) > 0.33)) { o.pos = float4(0, 0, 0, 1); return o; }
                float4x4 m0 = _Skin[p.slots & 0xFFFF], m1 = _Skin[p.slots >> 16];
                float3 pos = lerp(mul(m1, float4(p.pos, 1)).xyz, mul(m0, float4(p.pos, 1)).xyz, p.w);
                float3 nrm = normalize(lerp(mul((float3x3)m1, p.nrm), mul((float3x3)m0, p.nrm), p.w));
                float t = AG_TIME;
                pos += 0.002 * float3(sin(t * (1.1 + h * 1.3) + h * 17.0), sin(t * (0.9 + h * 1.1) + h * 29.0), sin(t * (1.0 + h * 1.7) + h * 41.0));
                float3 V = _WorldSpaceCameraPos - pos;
                float dist = length(V);
                V /= max(dist, 1e-5);
                float3 n = dot(nrm, V) < 0 ? -nrm : nrm;
                float facing = saturate(dot(n, V));
                // its outline the most of it, its face toward the eyes a glass; nothing close in front of the eyes
                float edge = smoothstep(0.1, 0.6, 1 - facing);
                float a = _Alpha * lerp(_Glass, 1, edge) * smoothstep(_Near, _Whole, dist);
                if (a < 0.006) { o.pos = float4(0, 0, 0, 1); return o; }
                float grow = kind == 2 ? 0.55 : hs < 0.08 ? 1.2 : 0.8 + 0.4 * h;
                float pixel = 2 * dist / (unity_CameraProjection._m11 * _ScreenParams.y);
                float size = max(_Size * grow, pixel * _MinPixels);
                float2 c = corners[id % 3];
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                o.pos = mul(UNITY_MATRIX_VP, float4(pos + (right * c.x + up * c.y) * size, 1));
                o.uv = c;
                // its own colours, as ParticleAnt has them, the outline lit pale by the nest's rim light
                float3 rgb = GammaToLinearSpace(float3(p.col & 255, (p.col >> 8) & 255, (p.col >> 16) & 255) / 255.0);
                rgb = AG_HueShift(rgb, _Hue, _Sat);
                float lum = max(1e-3, dot(rgb, float3(0.3, 0.55, 0.15)));
                rgb = lerp(rgb, _AntTint.rgb * (lum / max(1e-3, dot(_AntTint.rgb, float3(0.3, 0.55, 0.15)))), _AntTint.a);
                rgb *= pow(lum / 0.18, _Contrast - 1);
                rgb = lerp(rgb, _AntPaleColor.rgb * lum * 2.2, _Pale);
                float3 L = normalize(UNITY_MATRIX_V[1].xyz * 0.9 - UNITY_MATRIX_V[2].xyz * 0.35 + UNITY_MATRIX_V[0].xyz * 0.25);
                float key = saturate(dot(n, L) * 0.6 + 0.4);
                float3 lit = rgb * (0.45 + 1.4 * key * key) + _AntRimColor.rgb * edge * 1.2;
                o.col = half4(lit * _Gain, a);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // a round flake, soft at its rim
                float r2 = dot(i.uv, i.uv);
                clip(1 - r2);
                fixed4 col = fixed4(i.col.rgb, i.col.a * (1 - smoothstep(0.45, 1.0, r2)));
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
