// The particle look (ParticleLook.cs): glowing points sampled from the plants', props' and ground's surfaces,
// drawn additively as soft round dots that face the camera. One triangle (three vertices) per point, big
// enough to hold the dot, read from a buffer of { position, packed colour }. Colour bytes are gamma-space rgb;
// in the fourth, the top two bits say what the point is (0 plant, 1 dust floating in the air, 2 ground, 3 a
// plant's glowing part: brighter, breathing) and the low six how big it is (× size/8; 0: its own size).
// Every point stays at least about a pixel wide, so the far forest thins into dust instead of vanishing, and
// each one trembles a little (_Shimmer), so the forms look alive. Dust wanders slowly and now and then
// twinkles; the ground lights up round the ant as it passes. The shell (_Shell = 1, ParticleLook.MakeShell) is a
// second draw of dense ground points round the ant: each is drawn only within its own distance of the ant,
// between _ShellFade.x and .y, so they thin out toward the edge and fade in and out one by one as it walks.
// A point's random values come from its position, so they stay the same when the shell is made again.
Shader "AntGarden/ParticleLook"
{
    Properties
    {
        _Size ("Point radius (units)", Float) = 0.016
        _Gain ("Brightness", Float) = 0.9
        _GlowGain ("Extra brightness of glowing points", Float) = 3
        _MinPixels ("Smallest point (pixels)", Float) = 1.2
        _Shimmer ("How far points tremble (units)", Float) = 0
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
            float _Size, _Gain, _GlowGain, _MinPixels, _Shimmer, _Shell;
            float4 _ShellFade;
            float4 _TrailPlayer;   // global (GuideTrail): points near the ant brighten a little

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 col : COLOR;
                UNITY_FOG_COORDS(1)
            };

            // a triangle round the unit circle
            static const float2 corners[3] = { float2(0, 2), float2(-1.7320508, -1), float2(1.7320508, -1) };

            uint Hash(uint3 v)
            {
                uint x = v.x * 73856093u ^ v.y * 19349663u ^ v.z * 83492791u;
                x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16;
                return x;
            }

            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                uint i = id / 3;
                Point p = _Points[i];
                float h = (Hash(asuint(p.pos)) & 0xFFFFFF) / 16777216.0;
                if (_Shell > 0.5)
                {
                    // the shell: only within this point's own distance of the ant
                    float reach = lerp(_ShellFade.x, _ShellFade.y, frac(h * 7.31 + 0.17));
                    if (length(p.pos.xz - _TrailPlayer.xz) > reach) { o.pos = float4(0, 0, 0, 1); return o; }
                }
                float2 c = corners[id % 3];
                float3 rgb = GammaToLinearSpace(float3(p.col & 255, (p.col >> 8) & 255, (p.col >> 16) & 255) / 255.0);
                uint b = (p.col >> 24) & 255, kind = b >> 6, sz = b & 63;
                float glow = kind == 3 ? 1 : 0, ground = kind == 2 ? 1 : 0, dust = kind == 1 ? 1 : 0;
                float scale = sz == 0 ? 1 : sz * 0.125;
                float t = _Time.y;
                float3 pos = p.pos + _Shimmer * (1 - ground) * float3(sin(t * (0.9 + h * 1.3) + h * 17.0), sin(t * (0.7 + h * 1.1) + h * 29.0), sin(t * (0.8 + h * 1.7) + h * 41.0));
                // dust wanders slowly through the air
                float pace = 0.35 + 0.65 * h;
                pos += dust * float3(0.45 * sin(t * 0.21 * pace + h * 17.0), 0.22 * sin(t * 0.17 * pace + h * 29.0), 0.45 * cos(t * 0.19 * pace + h * 41.0));
                float dist = distance(pos, _WorldSpaceCameraPos);
                float pixel = 2 * dist / (unity_CameraProjection._m11 * _ScreenParams.y);
                float want = _Size * scale * (0.6 + 0.8 * h) * (1 + glow * 0.8) * (1 - ground * 0.35);
                float size = max(want, pixel * _MinPixels);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 w = pos + (right * c.x + up * c.y) * size;
                o.pos = mul(UNITY_MATRIX_VP, float4(w, 1));
                o.uv = c;
                float breathe = glow > 0.5 ? 0.7 + 0.3 * sin(t * (0.8 + h * 1.6) + h * 40) : 0.85 + 0.15 * sin(t * (0.5 + h) + h * 23);
                // dust twinkles now and then
                float tw = 0.5 + 0.5 * sin(t * (0.5 + 1.2 * h) + h * 61);
                breathe = lerp(breathe, 0.2 + 1.6 * tw * tw * tw * tw, dust);
                // a point drawn bigger than it wants (far away) spreads the same light over more pixels: dim it
                float spread = saturate(want / size);
                // round the ant the world lights up a little; the ground most, as if its antennae felt it
                float da = length(pos.xz - _TrailPlayer.xz);
                float near = 1 + (0.6 + 1.0 * ground) * (1 - smoothstep(0.5, 3.5 + 1.5 * ground, da));
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
