// The particle look (ParticleLook.cs): glowing points sampled from the plants', props' and ground's surfaces,
// drawn additively as soft round dots that face the camera, one quad (six vertices) per point, read from a
// buffer of { position, packed colour }. Colour bytes are gamma-space rgb; the fourth byte marks a point in a
// plant's glowing part (brighter, breathing). Every point stays at least about a pixel wide, so the far forest
// thins into dust instead of vanishing.
Shader "AntGarden/ParticleLook"
{
    Properties
    {
        _Size ("Point radius (units)", Float) = 0.016
        _Gain ("Brightness", Float) = 0.9
        _GlowGain ("Extra brightness of glowing points", Float) = 3
        _MinPixels ("Smallest point (pixels)", Float) = 1.2
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

            struct Point { float3 pos; uint col; };
            StructuredBuffer<Point> _Points;
            float _Size, _Gain, _GlowGain, _MinPixels;
            float4 _TrailPlayer;   // global (GuideTrail): points near the ant brighten a little

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 col : COLOR;
                UNITY_FOG_COORDS(1)
            };

            static const float2 corners[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };

            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                uint i = id / 6;
                Point p = _Points[i];
                float2 c = corners[id % 6];
                float3 rgb = GammaToLinearSpace(float3(p.col & 255, (p.col >> 8) & 255, (p.col >> 16) & 255) / 255.0);
                float kind = ((p.col >> 24) & 255) / 255.0;
                float glow = step(0.75, kind), ground = step(0.25, kind) * (1 - glow);
                float h = frac(sin(i * 12.9898 + 0.5) * 43758.5453);
                float dist = distance(p.pos, _WorldSpaceCameraPos);
                float pixel = 2 * dist / (unity_CameraProjection._m11 * _ScreenParams.y);
                float want = _Size * (0.6 + 0.8 * h) * (1 + glow * 0.8) * (1 - ground * 0.35);
                float size = max(want, pixel * _MinPixels);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 w = p.pos + (right * c.x + up * c.y) * size;
                o.pos = mul(UNITY_MATRIX_VP, float4(w, 1));
                o.uv = c;
                float breathe = glow > 0.5 ? 0.7 + 0.3 * sin(_Time.y * (0.8 + h * 1.6) + h * 40) : 1;
                // a point drawn bigger than it wants (far away) spreads the same light over more pixels: dim it
                float spread = saturate(want / size);
                float near = 1 + 0.6 * (1 - smoothstep(0.5, 3.5, length(p.pos.xz - _TrailPlayer.xz)));
                o.col = rgb * (_Gain + glow * _GlowGain) * (1 - ground * 0.55) * breathe * lerp(0.3, 1, spread) * near;
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
