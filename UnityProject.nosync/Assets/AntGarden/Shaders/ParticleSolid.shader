// The particle nest's solid things on the floor (SolidThings.cs: leaves, twigs, moss, mushrooms, pebbles...): each
// drawn as opaque splats over its surface, the way a Gaussian-splat capture shows a thing, read from a buffer of
// { position, normal (octahedral 16+16 bits), colour (gamma rgb; fourth byte: how much it glows), radius }. One
// triangle per splat, holding a dab whose outline is a little irregular and a little longer one way than the other,
// lying tilted between its surface and the camera, so surfaces foreshorten without breaking into gaps. Each splat is
// lit from its normal: a cool moonlight from above, a soft fill, a thin rim, and the ant's own nearness (as if its
// antennae felt the things round it). Glowing parts (mushroom gills, plants' glowing parts) breathe slowly, each
// thing at its own pace; a plant an antenna has lit flares far brighter while its flare lasts, and what stands up
// beside the ant's way lights up gently as it walks past, then slowly goes back (WalkLight.cs).
// Far away the things thin out splat by splat (the rest a little larger), so the count stays bounded. And it is all
// a little alive (NestField.cginc): what stands up sways in a slow wind, low things part round the ant, rings from its
// steps run over the floor, a thin silvery rim outlines every form, a few splats glint, and the guiding threads cast
// pools of light on the floor below them.
Shader "AntGarden/ParticleSolid"
{
    Properties
    {
        _Gain ("Brightness", Float) = 1
        _GlowGain ("Glow brightness (HDR)", Float) = 0.9
        _MinPixels ("Smallest splat (pixels)", Float) = 0.75
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+30" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
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
            #include "NestField.cginc"

            struct SolidPoint { float3 pos; uint nrm; uint col; float radius; };
            StructuredBuffer<SolidPoint> _Solid;
            float _Gain, _GlowGain, _MinPixels;
            float4 _TrailPlayer;            // global: the player's ant
            float4 _Lod;                    // x: all splats within, y: thinned to z of them by (units from the camera)
            float4 _MoonDir, _MoonColor, _FillColor;   // the light (direction toward it, world), its colour and the fill's
            float _Shell;                               // 1: the fine splats of the plants round the ant
            float4 _NearFade;                           // round the ant the fine splats are drawn out to y, the coarse from x
            float _FlareCount;                          // plants an antenna has lit: where, and how far / high / bright
            float4 _Flares[16], _FlareInfo[16];
            float4 _RimColor;                           // the thin silvery light along every form's outline
            float4 _GlintColor;                         // a few splats catch the light now and then
            float4 _WalkTrail[16];                      // where the ant has just been (xz) and when (w, as _Time.y)
            float _WalkLit, _WalkRadius;                // how much what it walked past lights up, and how near it must be

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 col : COLOR;
                float shape : TEXCOORD1;    // a per-splat seed for its outline
                UNITY_FOG_COORDS(2)
            };

            static const float2 corners[3] = { float2(0, 2), float2(-1.7320508, -1), float2(1.7320508, -1) };

            float Hash(uint n)
            {
                n = n * 747796405u + 2891336453u;
                n = ((n >> ((n >> 28u) + 4u)) ^ n) * 277803737u;
                n = (n >> 22u) ^ n;
                return (n & 0xFFFFFF) / 16777216.0;
            }

            float3 DecodeNormal(uint e)
            {
                float2 f = float2(e & 0xFFFF, e >> 16) / 65535.0 * 2 - 1;
                float3 n = float3(f.x, 1 - abs(f.x) - abs(f.y), f.y);
                if (n.y < 0) n.xz = (1 - abs(n.zx)) * (n.xz >= 0 ? 1 : -1);
                return normalize(n);
            }

            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                o.pos = float4(0, 0, 0, 1);
                uint i = id / 3;
                SolidPoint p = _Solid[i];
                float h = Hash(i), h2 = Hash(i + 7919u);
                // round the ant, the plants' coarse splats (negative radius) give way to the fine ones (the shell): each
                // at its own distance, so the change is dithered over a few units, never a line
                float rad = abs(p.radius);
                if (_NearFade.y > 0)
                {
                    float da = length(p.pos.xz - _TrailPlayer.xz), edge = lerp(_NearFade.x, _NearFade.y, Hash(i + 31337u));
                    if ((_Shell > 0.5 && da > edge) || (_Shell < 0.5 && p.radius < 0 && da < edge)) return o;
                }
                // far away: fewer splats, the rest a little larger
                float dist = distance(p.pos, _WorldSpaceCameraPos);
                float keep = lerp(1, _Lod.z, saturate((dist - _Lod.x) / max(1e-3, _Lod.y - _Lod.x)));
                if (h > keep) return o;
                // alive: what stands up from the floor sways in the wind, low things part round the ant, and rings
                // from its steps run under them
                float floorY = NestFloor(p.pos.xz), above = floorY > -999 ? p.pos.y - floorY : 0;
                float3 pos = p.pos + NestSway(p.pos, above, h) + NestPart(p.pos, above, _TrailPlayer.xz);
                float low = 1 - smoothstep(0.15, 0.6, above);
                float rip = NestRipple(pos.xz) * low;
                pos.y += 0.03 * rip;
                float3 n = DecodeNormal(p.nrm);
                float3 V = normalize(_WorldSpaceCameraPos - pos);
                if (dot(n, V) < 0) n = -n;
                // the dab lies between its surface and the camera, longer one way than the other, turned at random
                float3 N = normalize(n * 0.6 + V * 0.4);
                float3 R = UNITY_MATRIX_V[0].xyz;
                float3 B = normalize(cross(N, R)), T = cross(B, N);
                float turn = h2 * 6.2831853, cs = cos(turn), sn = sin(turn);
                float3 A1 = T * cs + B * sn, A2 = -T * sn + B * cs;
                float stretch = 1 + 0.6 * Hash(i + 104729u);
                float pixel = 2 * dist / (unity_CameraProjection._m11 * _ScreenParams.y);
                float size = max(rad / sqrt(max(keep, 0.05)), pixel * _MinPixels);
                float2 c = corners[id % 3];
                o.pos = mul(UNITY_MATRIX_VP, float4(pos + (A1 * (c.x * stretch) + A2 * (c.y / stretch)) * size, 1));
                o.uv = c;
                o.shape = h2;
                // lit from its normal: moonlight from above, a soft fill, a thin rim; brighter near the ant
                float3 albedo = GammaToLinearSpace(float3(p.col & 255, (p.col >> 8) & 255, (p.col >> 16) & 255) / 255.0);
                float glow = ((p.col >> 24) & 255) / 255.0;
                float key = saturate(dot(n, _MoonDir.xyz) * 0.75 + 0.25);
                float rim = pow(1 - saturate(dot(n, V)), 3);
                float near = 1 + 0.9 * (1 - smoothstep(0.8, 4.5, length(pos.xz - _TrailPlayer.xz)));
                float3 lit = albedo * (_FillColor.rgb + _MoonColor.rgb * key * key) * near + rim * 0.08 * albedo * 4;
                // a thin silvery light along the outlines, as in a scan; a few splats glint now and then
                lit += _RimColor.rgb * rim * (0.5 + 0.5 * key);
                float tw = 0.5 + 0.5 * sin(_Time.y * (0.45 + 1.1 * h) + h * 61);
                lit += _GlintColor.rgb * step(0.972, Hash(i + 555u)) * pow(tw, 8);
                // the steps' rings and the threads' pools of light
                lit *= 1 + 1.2 * rip;
                lit += albedo * NestBeacon(pos) * _BeaconColor.rgb * 4.5;
                // glowing parts breathe, each thing at its own pace (by where it is)
                if (glow > 0)
                {
                    float cell = Hash(asuint(floor(p.pos.x * 0.8)) * 73856093u ^ asuint(floor(p.pos.z * 0.8)) * 19349663u);
                    float breathe = 0.35 + 0.65 * (0.5 + 0.5 * sin(_Time.y * (0.45 + 0.6 * cell) + cell * 40));
                    lit += albedo * glow * _GlowGain * breathe;
                }
                // a plant an antenna has lit: much brighter, and warmer, while its flare lasts
                float flare = 0;
                for (int f = 0; f < 16; f++)
                {
                    if (f >= (int)_FlareCount) break;
                    float4 F = _Flares[f], I = _FlareInfo[f];
                    float inside = (1 - smoothstep(I.x * 0.75, I.x * 1.2, length(pos.xz - F.xz))) * step(F.y - 0.5, pos.y) * step(pos.y, F.y + I.y + 0.5);
                    flare = max(flare, inside * I.z);
                }
                // what stands up beside the way lights up as the ant walks past: rising over a moment, staying a
                // little, going back slowly
                if (_WalkLit > 0 && above > 0.05)
                {
                    float2 toAnt = pos.xz - _TrailPlayer.xz;
                    if (dot(toAnt, toAnt) < 100)
                    {
                        float walk = 0, r2 = _WalkRadius * _WalkRadius;
                        for (int w = 0; w < 16; w++)
                        {
                            float4 W = _WalkTrail[w];
                            float age = _Time.y - W.w;
                            float2 dv = pos.xz - W.xz;
                            walk = max(walk, exp(-dot(dv, dv) / r2) * smoothstep(0, 0.8, age) * (1 - smoothstep(2.5, 8, age)));
                        }
                        flare = max(flare, walk * _WalkLit);
                    }
                }
                lit = lit * (1 + 3 * flare) + flare * albedo * (0.6 + 2.5 * glow) * float3(0.95, 1.0, 1.05);
                // each splat a little lighter or darker than its neighbours, as the points of a capture are
                o.col = lit * _Gain * (0.76 + 0.48 * Hash(i + 999u));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // a dab with a slightly irregular outline
                float r2 = dot(i.uv, i.uv);
                float ang = atan2(i.uv.y, i.uv.x);
                float wobble = 0.12 * sin(ang * 3 + i.shape * 31) + 0.07 * sin(ang * 5 + i.shape * 57);
                clip(1 + wobble - r2);
                // its edge a little darker, so every splat reads as a point of its own
                fixed4 col = fixed4(i.col * (1 - 0.3 * smoothstep(0.45, 1.0, r2)), 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
