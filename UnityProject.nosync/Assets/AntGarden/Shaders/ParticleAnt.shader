// The particle nest's NPC ants (ParticleAnts.cs): each ant drawn as points spread over its surface
// (AssetBuilder.SaveAntPoints), skinned here with its own skeleton: every point, and its normal, follows the two
// bones that move it most (_Skin: bone × bindpose matrices, this ant's from _SkinBase on).
// Two passes, so the ant reads as a body made of points rather than a see-through cloud:
//   1 · depth: each point a small opaque disc (a little larger than its dot) writes depth only, so the ant's
//       front surface hides its far side and whatever lies behind it;
//   2 · colour: soft round dots drawn additively, nudged toward the camera, so only the front surface's dots pass.
// Each dot is lit: a key light from above the camera, wrapped, and a rim along the outline, so the body's
// segments, joints and bumps show; its colour is the ant's texture (turned by the ant's own hue, _Hue / _Sat),
// with its light and dark a little exaggerated, lifted only slightly toward a pale glow (_Pale). The fourth
// colour byte says what the point is (0 shell, 1 eye, 2 wing). The dots shimmer a little, so the ant looks alive.
Shader "AntGarden/ParticleAnt"
{
    Properties
    {
        _Size ("Point radius (units)", Float) = 0.0045
        _Gain ("Brightness", Float) = 0.9
        _MinPixels ("Smallest point (pixels)", Float) = 0.8
        _Pale ("How far colours go toward a pale glow", Range(0, 1)) = 0.15
        _Contrast ("How much the texture's light and dark are exaggerated", Float) = 1.5
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    #include "AntGardenCommon.cginc"

    struct AntPoint { float3 pos; float3 nrm; uint col; uint slots; float w; };
    StructuredBuffer<AntPoint> _AntPoints;
    StructuredBuffer<float4x4> _Skin;
    float _SkinBase, _Size, _Gain, _MinPixels, _Pale, _Hue, _Sat, _Contrast;

    static const float2 corners[3] = { float2(0, 2), float2(-1.7320508, -1), float2(1.7320508, -1) };

    // the point's skinned position and normal, its random value, and its dot's radius
    void Skin(uint id, out AntPoint p, out float3 pos, out float3 nrm, out float h, out float size, out float want)
    {
        p = _AntPoints[id / 3];
        uint b = (uint)_SkinBase;
        float4x4 m0 = _Skin[b + (p.slots & 0xFFFF)], m1 = _Skin[b + (p.slots >> 16)];
        pos = lerp(mul(m1, float4(p.pos, 1)).xyz, mul(m0, float4(p.pos, 1)).xyz, p.w);
        nrm = normalize(lerp(mul((float3x3)m1, p.nrm), mul((float3x3)m0, p.nrm), p.w));
        h = frac(sin((id / 3) * 12.9898 + 0.5) * 43758.5453);
        float t = _Time.y;
        pos += 0.0025 * float3(sin(t * (1.1 + h * 1.3) + h * 17.0), sin(t * (0.9 + h * 1.1) + h * 29.0), sin(t * (1.0 + h * 1.7) + h * 41.0));
        float dist = distance(pos, _WorldSpaceCameraPos);
        float pixel = 2 * dist / (unity_CameraProjection._m11 * _ScreenParams.y);
        want = _Size * (0.7 + 0.6 * h);
        size = max(want, pixel * _MinPixels);
    }
    ENDCG

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        // 1 · depth: the front surface
        Pass
        {
            ZWrite On
            ColorMask 0
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                AntPoint p;
                float3 pos, nrm;
                float h, size, want;
                Skin(id, p, pos, nrm, h, size, want);
                float2 c = corners[id % 3];
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                o.pos = mul(UNITY_MATRIX_VP, float4(pos + (right * c.x + up * c.y) * size * 1.4, 1));
                o.uv = c;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                clip(1 - dot(i.uv, i.uv));
                return 0;
            }
            ENDCG
        }

        // 2 · colour: lit soft dots, the front surface's only
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend One One
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_fog

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 col : COLOR;
                UNITY_FOG_COORDS(1)
            };

            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                AntPoint p;
                float3 pos, nrm;
                float h, size, want;
                Skin(id, p, pos, nrm, h, size, want);
                float3 V = normalize(_WorldSpaceCameraPos - pos);
                // a little toward the camera, so the front surface's own dots pass the depth written above
                float3 drawn = pos + V * 0.03;
                float2 c = corners[id % 3];
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                o.pos = mul(UNITY_MATRIX_VP, float4(drawn + (right * c.x + up * c.y) * size, 1));
                o.uv = c;
                // the texture's colour, its light and dark a little exaggerated
                float3 rgb = GammaToLinearSpace(float3(p.col & 255, (p.col >> 8) & 255, (p.col >> 16) & 255) / 255.0);
                uint kind = (p.col >> 24) & 255;
                rgb = AG_HueShift(rgb, _Hue, _Sat);
                float lum = max(1e-3, dot(rgb, float3(0.3, 0.55, 0.15)));
                rgb *= pow(lum / 0.18, _Contrast - 1) ;
                rgb = lerp(rgb, float3(0.75, 0.88, 1.0) * lum * 2.2, _Pale);
                // lit: a key light from above the camera (wrapped), a cool fill, and a rim along the outline
                float3 L = normalize(UNITY_MATRIX_V[1].xyz * 0.9 - UNITY_MATRIX_V[2].xyz * 0.35 + UNITY_MATRIX_V[0].xyz * 0.25);
                float3 n = dot(nrm, V) < 0 ? -nrm : nrm;
                float key = saturate(dot(n, L) * 0.6 + 0.4);
                key *= key;
                float rim = pow(1 - saturate(dot(n, V)), 3);
                float3 lit = rgb * (0.55 + 1.5 * key) + float3(0.55, 0.8, 1.0) * rim * 0.12 * (0.4 + lum * 3);
                float breathe = 0.9 + 0.1 * sin(_Time.y * (0.6 + h) + h * 23);
                float spread = saturate(want / size);
                o.col = lit * _Gain * breathe * lerp(0.3, 1, spread) * (kind == 1 ? 0.3 : kind == 2 ? 0.55 : 1);
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
