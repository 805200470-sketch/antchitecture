// The particle look (ParticleLook.cs): glowing points sampled from the plants', props' and ground's surfaces,
// drawn additively as soft round dots that face the camera. One triangle (three vertices) per point, big
// enough to hold the dot, read from a buffer of { position, packed colour }. Colour bytes are gamma-space rgb;
// in the fourth, the top two bits say what the point is (0 plant, 1 dust floating in the air, 2 ground, 3 a
// plant's glowing part: brighter, breathing) and the low six how big it is (× size/8; 0: its own size).
// Every point stays at least about a pixel wide, so the far forest thins into dust instead of vanishing, and
// each one trembles a little (_Shimmer), so the forms look alive. Plants breathe slowly, each at its own pace and
// to its own brightness, their glowing parts dim most of the time; one an antenna has lit flares far brighter. Dust wanders slowly and now and then
// twinkles; the ground lights up round the ant as it passes. The shell (_Shell = 1, ParticleLook.MakeShell) is a
// second draw of dense ground points round the ant: each is drawn only within its own distance of the ant,
// between _ShellFade.x and .y, so they thin out toward the edge and fade in and out one by one as it walks.
// A point's random values come from its position, so they stay the same when the shell is made again.
// Plants and their glowing parts can be tinted from two small palettes (_UsePalette): for them the fourth byte's
// low six bits are not a size but the tints' places, glow << 3 | body (_GlowPal, _BodyPal).
// Where the shell makes the plants many times denser (round the ant), each of their points is dimmer
// (_NearDim: from .x to .y units away the gain goes from .z to 1), so the detail grows, not the glare.
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
            #include "Chevron.cginc"
            #include "NestField.cginc"

            struct Point { float3 pos; uint col; };
            StructuredBuffer<Point> _Points;
            float _Size, _Gain, _GlowGain, _MinPixels, _Shimmer, _Shell;
            float4 _ShellFade, _NearDim;
            float _ShellGain;                    // the shell's brightness (a new one fades in over the old)
            float4 _CarpetFade;                  // the carpet round the ant (x..y units): plain ground points give way to it
            float _CoverGain;                    // the floor's small things (ground points of sizes 1-9): their brightness
            float _FlareCount;                   // plants an antenna has lit: where, and how far / high / bright
            float4 _Flares[16], _FlareInfo[16];
            float _UsePalette;
            float4 _BodyPal[8], _GlowPal[8];
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
                float soft = 1;
                if (_Shell > 0.5)
                {
                    // the shell: only within this point's own distance of the ant, fading in over the last unit of it
                    float reach = lerp(_ShellFade.x, _ShellFade.y, frac(h * 7.31 + 0.17));
                    float dShell = length(p.pos.xz - _TrailPlayer.xz);
                    if (dShell > reach) { o.pos = float4(0, 0, 0, 1); return o; }
                    soft = saturate((reach - dShell) / 1.2) * _ShellGain;
                }
                float2 c = corners[id % 3];
                float3 rgb = GammaToLinearSpace(float3(p.col & 255, (p.col >> 8) & 255, (p.col >> 16) & 255) / 255.0);
                uint b = (p.col >> 24) & 255, kind = b >> 6, sz = b & 63;
                float glow = kind == 3 ? 1 : 0, ground = kind == 2 ? 1 : 0, dust = kind == 1 ? 1 : 0;
                // plain ground points (sizes 10, 20, 40; the floor's small things use others) give way to the carpet
                // round the ant, each at its own distance, so the two dissolve into each other
                if (kind == 2 && (sz == 10 || sz == 20 || sz == 40) && _CarpetFade.y > 0
                    && length(p.pos.xz - _TrailPlayer.xz) < lerp(_CarpetFade.x - 0.6, _CarpetFade.y - 0.4, frac(h * 13.7 + 0.31)))
                    { o.pos = float4(0, 0, 0, 1); return o; }
                bool plantKind = kind == 0 || kind == 3;
                bool tinted = _UsePalette > 0.5 && plantKind && sz != 63;   // 63: a thing keeping its own colours
                float scale = sz == 0 || plantKind ? 1 : sz * 0.125;
                if (tinted) rgb *= kind == 3 ? _GlowPal[(sz >> 3) & 7].rgb : _BodyPal[sz & 7].rgb;
                float t = _Time.y;
                float3 pos = p.pos + _Shimmer * (1 - ground) * float3(sin(t * (0.9 + h * 1.3) + h * 17.0), sin(t * (0.7 + h * 1.1) + h * 29.0), sin(t * (0.8 + h * 1.7) + h * 41.0));
                // dust wanders slowly through the air, and round the ant it is drawn into a slow swirl
                float pace = 0.35 + 0.65 * h;
                pos += dust * float3(0.45 * sin(t * 0.21 * pace + h * 17.0), 0.22 * sin(t * 0.17 * pace + h * 29.0), 0.45 * cos(t * 0.19 * pace + h * 41.0));
                if (dust > 0.5)
                {
                    float2 dd = pos.xz - _TrailPlayer.xz;
                    float rr = length(dd), swirl = (1 - smoothstep(0.3, 1.8, rr)) * 0.5;
                    float ang = swirl * (1 + 0.3 * sin(t * 0.7 + h * 9));
                    float cs = cos(ang), sn = sin(ang);
                    pos.xz = _TrailPlayer.xz + float2(dd.x * cs - dd.y * sn, dd.x * sn + dd.y * cs) * (1 + 0.15 * swirl);
                }
                // the floor's points: rings from the ant's steps lift them as they pass
                float rip = ground > 0.5 ? NestRipple(pos.xz) : 0;
                pos.y += 0.025 * rip;
                // a guiding pulse passing under the ground: its points swell and lift a little, like a wave under skin
                float chev = ground > 0.5 ? Chevron(pos, h) : 0;
                pos.y += 0.05 * chev;
                float dist = distance(pos, _WorldSpaceCameraPos);
                float pixel = 2 * dist / (unity_CameraProjection._m11 * _ScreenParams.y);
                // plants' points vary in size as a capture's do: most near the middle, some smaller, a few much
                // larger and fainter (their light spread wider)
                float gN = (h + frac(h * 7.13) + frac(h * 13.7) - 1.5) * 1.6;
                float vary = ground + dust > 0.5 ? 0.6 + 0.8 * h : clamp(exp(gN * 0.42), 0.4, 2.6);
                float want = _Size * scale * vary * (1 + glow * 0.8) * (1 - ground * 0.35) * (1 + 0.8 * chev);
                float size = max(want, pixel * _MinPixels);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 w = pos + (right * c.x + up * c.y) * size;
                o.pos = mul(UNITY_MATRIX_VP, float4(w, 1));
                o.uv = c;
                // plants breathe, each at its own slow pace and to its own brightness (by the patch of ground it
                // stands on, so a plant breathes as one); their glowing parts most, spending more of the time dim
                float cellH = (Hash(uint3(asuint(floor(p.pos.x * 0.55)), asuint(floor(p.pos.z * 0.55)), 7u)) & 0xFFFFFF) / 16777216.0;
                float pulseB = 0.5 + 0.5 * sin(t * (0.22 + 0.3 * cellH) + cellH * 40 + h * 0.6);
                float breathe = glow > 0.5 ? (0.18 + 0.82 * pulseB * pulseB) * (0.4 + 0.6 * frac(cellH * 7.31)) : 0.78 + 0.22 * pulseB;
                // dust twinkles now and then
                float tw = 0.5 + 0.5 * sin(t * (0.5 + 1.2 * h) + h * 61);
                breathe = lerp(breathe, 0.2 + 1.6 * tw * tw * tw * tw, dust);
                // a point drawn bigger than it wants (far away) spreads the same light over more pixels: dim it
                float spread = saturate(want / size);
                // round the ant the world lights up a little; the ground most, as if its antennae felt it
                float da = length(pos.xz - _TrailPlayer.xz);
                float near = 1 + (0.6 + 1.0 * ground) * (1 - smoothstep(0.5, 3.5 + 1.5 * ground, da));
                // plants are many times denser round the ant: each point dimmer there
                near *= lerp(lerp(_NearDim.z, 1, smoothstep(_NearDim.x, _NearDim.y, da)), 1, ground + dust);
                o.col = rgb * (_Gain + glow * _GlowGain) * (1 - ground * 0.55) * breathe * lerp(0.3, 1, spread) * near * soft / pow(max(vary, 1), 0.8);
                if (kind == 2 && sz > 0 && sz < 10) o.col *= _CoverGain;   // (packed below 1, to keep their colour)
                // a plant an antenna has lit: much brighter, and warmer, while its flare lasts
                if (ground + dust < 0.5 && _FlareCount > 0)
                {
                    float lit = 0;
                    for (int f = 0; f < 16; f++)
                    {
                        if (f >= (int)_FlareCount) break;
                        float4 F = _Flares[f], I = _FlareInfo[f];
                        float inside = (1 - smoothstep(I.x * 0.75, I.x * 1.2, length(pos.xz - F.xz))) * step(F.y - 0.5, pos.y) * step(pos.y, F.y + I.y + 0.5);
                        lit = max(lit, inside * I.z);
                    }
                    o.col = o.col * (1 + 4.5 * lit) + lit * soft * float3(0.1, 0.085, 0.055) * (0.5 + 1.5 * glow);
                }
                // and lights up
                o.col = lerp(o.col, _ChevronColor.rgb * (_ChevronColor.a * 0.3 * soft), saturate(chev));
                // the floor's points: the steps' rings, a slow breath running across, the threads' pools of light
                if (ground > 0.5)
                {
                    o.col *= (1 + 1.4 * rip) * (1 + 0.16 * sin(dot(pos.xz, float2(0.21, 0.13)) - t * 0.5));
                    o.col += NestBeacon(pos) * _BeaconColor.rgb * 0.1 * soft;
                }
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
