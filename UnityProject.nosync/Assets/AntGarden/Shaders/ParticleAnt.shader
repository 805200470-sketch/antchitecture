// The particle nest's NPC ants (ParticleAnts.cs): each ant drawn as glowing points spread over its surface
// (AssetBuilder.SaveAntPoints), skinned here with its own skeleton: every point follows the two bones that move
// it most (_Skin: bone × bindpose matrices, this ant's from _SkinBase on). Soft round dots facing the camera,
// one triangle each, drawn additively, as in ParticleLook.shader. Colour bytes are gamma-space rgb from the
// ant's texture, turned by the ant's own hue (_Hue, _Sat) and lifted toward a pale glow (_Pale); the fourth
// byte says what the point is (0 shell, 1 eye, 2 wing). The dots shimmer a little, so the ant looks alive.
Shader "AntGarden/ParticleAnt"
{
    Properties
    {
        _Size ("Point radius (units)", Float) = 0.008
        _Gain ("Brightness", Float) = 0.6
        _MinPixels ("Smallest point (pixels)", Float) = 0.9
        _Pale ("How far colours go toward a pale glow", Range(0, 1)) = 0.45
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

            struct AntPoint { float3 pos; uint col; uint slots; float w; };
            StructuredBuffer<AntPoint> _AntPoints;
            StructuredBuffer<float4x4> _Skin;
            float _SkinBase, _Size, _Gain, _MinPixels, _Pale, _Hue, _Sat;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 col : COLOR;
                UNITY_FOG_COORDS(1)
            };

            static const float2 corners[3] = { float2(0, 2), float2(-1.7320508, -1), float2(1.7320508, -1) };

            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                uint i = id / 3;
                AntPoint p = _AntPoints[i];
                float2 c = corners[id % 3];
                uint b = (uint)_SkinBase;
                float3 w0 = mul(_Skin[b + (p.slots & 0xFFFF)], float4(p.pos, 1)).xyz;
                float3 w1 = mul(_Skin[b + (p.slots >> 16)], float4(p.pos, 1)).xyz;
                float3 pos = lerp(w1, w0, p.w);
                float h = frac(sin(i * 12.9898 + 0.5) * 43758.5453);
                float t = _Time.y;
                pos += 0.004 * float3(sin(t * (1.1 + h * 1.3) + h * 17.0), sin(t * (0.9 + h * 1.1) + h * 29.0), sin(t * (1.0 + h * 1.7) + h * 41.0));
                float dist = distance(pos, _WorldSpaceCameraPos);
                float pixel = 2 * dist / (unity_CameraProjection._m11 * _ScreenParams.y);
                float want = _Size * (0.6 + 0.8 * h);
                float size = max(want, pixel * _MinPixels);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                o.pos = mul(UNITY_MATRIX_VP, float4(pos + (right * c.x + up * c.y) * size, 1));
                o.uv = c;
                float3 rgb = GammaToLinearSpace(float3(p.col & 255, (p.col >> 8) & 255, (p.col >> 16) & 255) / 255.0);
                uint kind = (p.col >> 24) & 255;
                rgb = AG_HueShift(rgb, _Hue, _Sat);
                float lum = dot(rgb, float3(0.3, 0.55, 0.15));
                rgb = lerp(rgb * 1.5, float3(0.75, 0.88, 1.0) * (0.25 + 1.4 * sqrt(lum)), _Pale);
                float breathe = 0.85 + 0.15 * sin(t * (0.6 + h) + h * 23);
                float spread = saturate(want / size);
                o.col = rgb * _Gain * breathe * lerp(0.3, 1, spread) * (kind == 1 ? 0.25 : kind == 2 ? 0.6 : 1);
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
