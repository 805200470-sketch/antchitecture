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
            float4 _NearDensity;                        // the fine splats: all of a plant's within x of the camera, at least y further
            float4 _ThingNear;                          // the things' and vines' ranked splats: all within x of the camera, y of them further
            float _FlareCount;                          // plants an antenna has lit: where, and how far / high / bright
            float4 _Flares[16], _FlareInfo[16];
            float4 _RimColor;                           // the thin silvery light along every form's outline
            float4 _GlintColor;                         // a few splats catch the light now and then
            float4 _WalkTrail[16];                      // where the ant has just been (xz) and when (w, as _Time.y)
            float _WalkLit, _WalkRadius;                // how much what it walked past lights up, and how near it must be
            float _NearLift;                            // how much brighter things are right round the ant (SolidThings; 0.9)
            float _ScatterRadius;                       // what the ant walks through scatters this far round it (0: no)

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
                // two 12-bit halves (octahedral); the top byte is a rank (bits 24..30) and a building's flag (bit 31)
                float2 f = float2(e & 0xFFFu, (e >> 12) & 0xFFFu) / 4095.0 * 2 - 1;
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
                // a plant's fine splats: as many as its distance from the camera wants (its detail near, thinning off)
                if (_Shell > 0.5 && p.radius > 0)
                {
                    float want = clamp(_NearDensity.x * _NearDensity.x / max(dist * dist, 1e-4), _NearDensity.y, 1);
                    if (((p.nrm >> 24) & 127u) / 128.0 >= want) return o;
                    rad /= sqrt(want);
                }
                // the things' and vines' splats, made finer than they are drawn further off (SolidThings.Densify,
                // GuideThreads.VineSplats; ranked 1..127): as fine as the plants' right in front of the camera, fewer
                // and larger from a few units off
                uint rk = (p.nrm >> 24) & 127u;
                if (_Shell < 0.5 && p.radius > 0 && rk > 0 && _ThingNear.x > 0)
                {
                    float want = clamp(_ThingNear.x * _ThingNear.x / max(dist * dist, 1e-4), _ThingNear.y, 1);
                    if ((rk - 1) / 127.0 >= want) return o;
                    rad /= sqrt(want);
                }
                float keep = lerp(1, _Lod.z, saturate((dist - _Lod.x) / max(1e-3, _Lod.y - _Lod.x)));
                if (h > keep) return o;
                // alive: what stands up from the floor sways in the wind, low things part round the ant, and rings
                // from its steps run under them
                float floorY = NestFloor(p.pos.xz), above = floorY > -999 ? p.pos.y - floorY : 0;
                float3 pos = p.pos + NestSway(p.pos, above, h) + NestPart(p.pos, above, _TrailPlayer.xz);
                float low = 1 - smoothstep(0.15, 0.6, above);
                float rip = NestRipple(pos.xz) * low;
                pos.y += 0.03 * rip;
                // where the ant is and has just been: what stands up there lights up as it passes (walk), and what it
                // walks through scatters outward and shrinks away, gathering again over a couple of seconds after it
                // has gone (scatter)
                float walk = 0, scatter = 0;
                float3 away = float3(0, 1, 0);
                if (above > 0.03 && _ScatterRadius > 0)
                {
                    float2 toAnt = pos.xz - _TrailPlayer.xz;
                    if (dot(toAnt, toAnt) < 100)
                    {
                        float r2 = _WalkRadius * _WalkRadius;
                        float3 dv0 = pos - (_TrailPlayer.xyz + float3(0, 0.25, 0));
                        scatter = saturate(1 - length(dv0) / _ScatterRadius);
                        away = dv0;
                        for (int w = 0; w < 16; w++)
                        {
                            float4 W = _WalkTrail[w];
                            float age = _Time.y - W.w;
                            float2 dh = pos.xz - W.xz;
                            walk = max(walk, exp(-dot(dh, dh) / r2) * smoothstep(0, 0.8, age) * (1 - smoothstep(2.5, 8, age)));
                            float3 dv = pos - (W.xyz + float3(0, 0.25, 0));
                            float sc = saturate(1 - length(dv) / _ScatterRadius) * (1 - smoothstep(0.3, 2.2, age));
                            if (sc > scatter) { scatter = sc; away = dv; }
                        }
                    }
                }
                if (scatter > 0) pos += normalize(away + float3(0, 1e-4, 0)) * (0.3 * scatter) + float3(0, 0.08 * scatter, 0);
                float3 n = DecodeNormal(p.nrm);
                float3 V = normalize(_WorldSpaceCameraPos - pos);
                if (dot(n, V) < 0) n = -n;
                // the dab lies between its surface and the camera, longer one way than the other, turned at random
                float3 N = normalize(n * 0.6 + V * 0.4);
                float3 R = UNITY_MATRIX_V[0].xyz;
                float3 B = normalize(cross(N, R)), T = cross(B, N);
                float turn = h2 * 6.2831853, cs = cos(turn), sn = sin(turn);
                float3 A1 = T * cs + B * sn, A2 = -T * sn + B * cs;
                float stretch = 1 + 0.3 * Hash(i + 104729u);
                float pixel = 2 * dist / (unity_CameraProjection._m11 * _ScreenParams.y);
                float size = max(rad / sqrt(max(keep, 0.05)), pixel * _MinPixels);
                // shrunk away where it is scattered, on the line between the camera and the ant (the ant is never
                // hidden behind a vine), and right at the lens
                float sight = 0;
                if (_ScatterRadius > 0)
                {
                    float3 ac = _TrailPlayer.xyz + float3(0, 0.25, 0) - _WorldSpaceCameraPos;
                    float al2 = dot(ac, ac);
                    if (al2 > 0.25)
                    {
                        float t = saturate(dot(pos - _WorldSpaceCameraPos, ac) / al2);
                        float3 off = pos - (_WorldSpaceCameraPos + ac * t);
                        sight = (1 - smoothstep(0.25, 0.45, length(off))) * smoothstep(0.02, 0.12, t) * (1 - smoothstep(0.82, 0.95, t));
                    }
                }
                size *= saturate(1 - max(max(scatter, sight), 1 - smoothstep(0.25, 0.8, dist)));
                if (size < 1e-5) return o;
                float2 c = corners[id % 3];
                o.pos = mul(UNITY_MATRIX_VP, float4(pos + (A1 * (c.x * stretch) + A2 * (c.y / stretch)) * size, 1));
                o.uv = c;
                o.shape = h2;
                // lit from its normal: moonlight from above, a soft fill, a thin rim; brighter near the ant
                float3 albedo = GammaToLinearSpace(float3(p.col & 255, (p.col >> 8) & 255, (p.col >> 16) & 255) / 255.0);
                float glow = ((p.col >> 24) & 255) / 255.0;
                float key = saturate(dot(n, _MoonDir.xyz) * 0.85 + 0.15);
                float rim = pow(1 - saturate(dot(n, V)), 3);
                // a building (ThingPen.Building: the kiln's spires, arches, chimneys and cells) lights only where it glows,
                // along its paths and at a few points, when the ant comes near, walks past or touches it; not the whole of it
                bool building = (p.nrm >> 31) != 0;
                float near = 1 + _NearLift * (1 - smoothstep(0.8, 4.5, length(pos.xz - _TrailPlayer.xz))) * (building ? 0.3 : 1);
                float3 lit = albedo * (_FillColor.rgb + _MoonColor.rgb * key * key) * near + rim * 0.08 * albedo * 4;
                // a thin silvery light along the outlines, as in a scan; a few splats glint now and then
                lit += _RimColor.rgb * rim * (0.5 + 0.5 * key);
                float tw = 0.5 + 0.5 * sin(_Time.y * (0.45 + 1.1 * h) + h * 61);
                lit += _GlintColor.rgb * step(0.972, Hash(i + 555u)) * pow(tw, 8);
                // the steps' rings and the threads' pools of light
                lit *= 1 + 1.2 * rip;
                lit += albedo * NestBeacon(pos) * _BeaconColor.rgb * lerp(1.2, 4.5, low);
                // glowing parts breathe slowly, a plant as one (NestBreath), never quite out
                if (glow > 0)
                {
                    float b = NestBreath(p.pos);
                    lit += albedo * glow * _GlowGain * (0.4 + 0.6 * b * b);
                }
                // a plant an antenna has lit: much brighter, and warmer, while its flare lasts
                float flare = 0, part = building ? lerp(0.12, 1, saturate(glow * 3)) : 1;
                for (int f = 0; f < 16; f++)
                {
                    if (f >= (int)_FlareCount) break;
                    float4 F = _Flares[f], I = _FlareInfo[f];
                    float inside = (1 - smoothstep(I.x * 0.75, I.x * 1.2, length(pos.xz - F.xz))) * step(F.y - 0.5, pos.y) * step(pos.y, F.y + I.y + 0.5);
                    flare = max(flare, inside * I.z);
                }
                // what stands up beside the way lights up as the ant walks past: rising over a moment, staying a
                // little, going back slowly
                if (_WalkLit > 0 && above > 0.05) flare = max(flare, walk * _WalkLit);
                flare *= part;
                lit = lit * (1 + 3 * flare) + flare * albedo * (0.6 + 2.5 * glow) * float3(0.95, 1.0, 1.05);
                // each splat a little lighter or darker than its neighbours, as the points of a capture are
                o.col = lit * _Gain * (0.88 + 0.24 * Hash(i + 999u));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // a dab with a slightly irregular outline
                float r2 = dot(i.uv, i.uv);
                float ang = atan2(i.uv.y, i.uv.x);
                float wobble = 0.07 * sin(ang * 3 + i.shape * 31) + 0.04 * sin(ang * 5 + i.shape * 57);
                clip(1 + wobble - r2);
                // its edge a little darker, so every splat reads as a point of its own
                fixed4 col = fixed4(i.col * (1 - 0.1 * smoothstep(0.45, 1.0, r2)), 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
