// The particle nest's small life (MicroLife.cs): creatures much smaller than the ant but big enough to be seen, each a
// dozen opaque lit splats placed and moved here on the GPU from its own few numbers (_Creatures: home, kind, seed,
// size, range, colour), so nothing is done for them on the CPU each frame:
//   0 springtail: a slender segmented body with feelers and legs, wandering over the floor, stopping and going, and now
//     and then springing up in a little hop;
//   1 mite: a round bead of a body on short legs, crawling slowly over the floor;
//   2 midge: a speck with two faint wings, flying in a little swarm round a point in the air over the floor, faintly
//     glowing;
//   3 seed fluff: a seed under an umbrella of fine hairs, drifting slowly with the air across the nest, tumbling a
//     little, coming and going (it shrinks away at the ends of its drift).
// Things on the floor keep to it (NestField.cginc's height map). Lit like the floor's things: a cool moonlight from
// above, a soft fill, a thin rim. Far from the camera they are not drawn.
Shader "AntGarden/MicroLife"
{
    Properties
    {
        _Gain ("Brightness", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+40" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
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

            struct Creature { float4 a; float4 b; };   // a: home x, height (in the air), home z, kind; b: seed, size, range, colour
            StructuredBuffer<Creature> _Creatures;
            float _Gain, _MaxDistance;
            float4 _MoonDir, _MoonColor, _FillColor, _Pal[8], _Wind;
            float4 _TrailPlayer;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 col : COLOR;
                UNITY_FOG_COORDS(1)
            };

            static const float2 corners[3] = { float2(0, 2), float2(-1.7320508, -1), float2(1.7320508, -1) };

            float H(float n) { return frac(sin(n * 12.9898 + 4.1414) * 43758.5453); }

            // a crawler's place on the floor at time t: a slow, wandering loop round its home, stopping and going
            float2 Wander(float2 home, float seed, float range, float t)
            {
                float sp = 0.18 + 0.2 * H(seed);
                // stop and go: its way along the loop speeds up and all but stops
                float s = t * sp + 0.9 * sp / 0.45 * sin(t * 0.45 + seed * 6.28);
                float a = s / max(0.3, range);
                float2 o = float2(sin(a * 1.1 + seed * 3.1) + 0.5 * sin(a * 2.3 + seed * 7.7), cos(a * 0.9 + seed * 5.3) + 0.5 * sin(a * 1.7 + seed * 2.9));
                return home + o * range * 0.6;
            }

            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                o.pos = float4(0, 0, 0, 1);
                uint ci = id / 36, k = (id / 3) % 12;
                Creature c = _Creatures[ci];
                float kind = c.a.w, seed = c.b.x, size = c.b.y, range = c.b.z;
                float t = AG_TIME;
                float3 centre, fwd = float3(0, 0, 1), up = float3(0, 1, 0);
                float vis = 1;
                if (kind < 1.5)
                {
                    // on the floor: wandering (springtails faster, mites slower)
                    float tt = kind < 0.5 ? t : t * 0.45;
                    float2 xz = Wander(c.a.xz, seed, range, tt), ahead = Wander(c.a.xz, seed, range, tt + 0.08);
                    float2 dir = ahead - xz;
                    fwd = length(dir) > 1e-5 ? normalize(float3(dir.x, 0, dir.y)) : float3(0, 0, 1);
                    float floorY = NestFloor(xz);
                    if (floorY < -999) return o;
                    float hop = 0;
                    if (kind < 0.5)
                    {
                        // now and then a springtail springs up in a little hop
                        float period = 5 + 6 * H(seed + 3), hp = frac(t / period + seed);
                        float dur = 0.45 / period;
                        if (hp < dur) hop = 0.32 * sin(3.14159 * hp / dur);
                    }
                    centre = float3(xz.x, floorY + size * 0.32 + hop, xz.y);
                }
                else if (kind < 2.5)
                {
                    // a midge: flying round its swarm's point in the air
                    float ph = seed * 6.28;
                    float3 off = float3(sin(t * 2.3 + ph) * 0.3 + sin(t * 0.7 + ph * 1.3) * 0.4,
                                        sin(t * 3.1 + ph * 1.7) * 0.15 + sin(t * 0.5 + ph) * 0.2,
                                        cos(t * 1.9 + ph) * 0.3 + cos(t * 0.6 + ph * 2.1) * 0.4) * range;
                    float floorY = NestFloor(c.a.xz);
                    if (floorY < -999) return o;
                    centre = float3(c.a.x, floorY + c.a.y, c.a.z) + off;
                    float3 d = float3(cos(t * 2.3 + ph) * 0.69, 0, -sin(t * 1.9 + ph) * 0.57);
                    fwd = length(d) > 1e-4 ? normalize(d) : fwd;
                }
                else
                {
                    // seed fluff: drifting with the air across the nest, coming and going at the ends of its drift
                    float span = 22;
                    float2 w = normalize(_Wind.xy);
                    float f = frac(t * (0.08 + 0.05 * H(seed + 5)) / span * 4 + seed);
                    vis = smoothstep(0, 0.12, f) * (1 - smoothstep(0.85, 1, f));
                    float2 xz = c.a.xz + w * (f - 0.5) * span + float2(-w.y, w.x) * sin(t * 0.21 + seed * 9) * 0.8;
                    float floorY = NestFloor(xz);
                    if (floorY < -999) floorY = NestFloor(c.a.xz);
                    if (floorY < -999) return o;
                    centre = float3(xz.x, floorY + c.a.y + 0.35 * sin(t * 0.7 + seed * 5), xz.y);
                    float tumble = t * (0.4 + 0.5 * H(seed + 9)) + seed * 6;
                    up = normalize(float3(sin(tumble) * 0.35, 1, cos(tumble * 0.8) * 0.35));
                }
                float camD = distance(centre, _WorldSpaceCameraPos);
                if (camD > _MaxDistance || camD < 0.5) return o;
                // right in front of the lens it shrinks away (it would fill the view as a blur)
                float nearK = smoothstep(0.6, 2.2, camD);
                float3 right = normalize(cross(up, fwd));
                fwd = cross(right, up);
                // the splat's place in the creature (x right, y up, z forward, in its sizes), its radius and colour
                float3 lp = 0, nrm = float3(0, 1, 0);
                float r = 0, glow = 0;
                float3 albedo = _Pal[(uint)c.b.w % 8].rgb;
                float L = size;
                if (kind < 0.5)
                {
                    // springtail: head, thorax, three segments, two feelers, four legs, an eye
                    if (k < 5) { float z = 0.42 - k * 0.2; lp = float3(0, 0.05 * (1 - k * 0.15), z) * L; r = L * (k == 0 ? 0.13 : 0.15 - k * 0.012); nrm = normalize(float3(0, 0.6, z)); }
                    else if (k < 7) { float s = k == 5 ? 1 : -1; float wav = 0.05 * sin(t * 7 + seed * 9 + s); lp = float3(s * 0.12, 0.1 + wav, 0.66) * L; r = L * 0.045; albedo *= 1.2; }
                    else if (k < 11) { float s = (k & 1) ? 1 : -1, z = k < 9 ? 0.22 : 0.0; float step = 0.03 * sin(t * 14 + k * 1.7 + seed * 4); lp = float3(s * 0.2, -0.12, z + step) * L; r = L * 0.04; albedo *= 0.7; }
                    else { lp = float3(0.06, 0.1, 0.48) * L; r = L * 0.035; albedo = float3(0.85, 0.88, 0.82); glow = 0.6; }
                }
                else if (kind < 1.5)
                {
                    // mite: a round bead of a body, legs, two pale spots on its back
                    if (k < 6) { float a = k * 1.0472; lp = float3(cos(a) * 0.14, 0.12 + 0.06 * (k & 1), sin(a) * 0.14) * L; r = L * 0.24; nrm = normalize(lp + float3(0, 0.1, 0)); }
                    else if (k < 10) { float s = (k & 1) ? 1 : -1, z = k < 8 ? 0.14 : -0.12; float step = 0.03 * sin(t * 9 + k + seed * 5); lp = float3(s * 0.34, -0.05, z + step) * L; r = L * 0.06; albedo *= 0.65; }
                    else { float s = k == 10 ? 1 : -1; lp = float3(s * 0.08, 0.3, 0.05) * L; r = L * 0.07; albedo = lerp(albedo, float3(0.85, 0.86, 0.8), 0.6); }
                }
                else if (kind < 2.5)
                {
                    // midge: a speck of a body, two faint wings beating, a faint glow
                    if (k < 2) { lp = float3(0, 0, 0.3 - k * 0.6) * L; r = L * 0.2; glow = 0.4; albedo = lerp(albedo, float3(0.5, 0.85, 0.85), 0.5); }
                    else if (k < 4) { float s = k == 2 ? 1 : -1, beat = sin(t * 60 + seed * 30); lp = float3(s * 0.45, 0.25 + 0.15 * beat, 0) * L; r = L * 0.26; albedo = float3(0.4, 0.46, 0.46) * (0.6 + 0.4 * beat * beat); }
                    else return o;
                }
                else
                {
                    // seed fluff: the seed, its stalk, and an umbrella of fine hairs (five rays, two beads each)
                    if (k == 0) { lp = float3(0, -0.5, 0) * L; r = L * 0.09; albedo = float3(0.42, 0.36, 0.26); }
                    else if (k == 1) { lp = float3(0, -0.25, 0) * L; r = L * 0.035; albedo = float3(0.7, 0.7, 0.62); }
                    else
                    {
                        uint ray = (k - 2) / 2, bead = (k - 2) % 2;
                        float a = ray * 1.2566 + seed * 3, reachR = bead == 0 ? 0.28 : 0.55;
                        lp = float3(cos(a) * reachR, 0.02 + 0.12 * reachR, sin(a) * reachR) * L;
                        r = L * (bead == 0 ? 0.05 : 0.07);
                        albedo = float3(0.86, 0.88, 0.82);
                        glow = 0.25;
                    }
                    r *= vis;
                }
                float3 pos = centre + right * lp.x + up * lp.y + fwd * lp.z;
                float3 n = normalize(right * nrm.x + up * nrm.y + fwd * nrm.z);
                float dist = distance(pos, _WorldSpaceCameraPos);
                float pixel = 2 * dist / (unity_CameraProjection._m11 * _ScreenParams.y);
                r *= nearK;
                if (r <= 0) return o;
                float sz = max(r, pixel * 0.8);
                float3 R = UNITY_MATRIX_V[0].xyz, U = UNITY_MATRIX_V[1].xyz;
                float2 cc = corners[id % 3];
                o.pos = mul(UNITY_MATRIX_VP, float4(pos + (R * cc.x + U * cc.y) * sz, 1));
                o.uv = cc;
                float3 V = normalize(_WorldSpaceCameraPos - pos);
                float key = saturate(dot(n, _MoonDir.xyz) * 0.75 + 0.25);
                float rim = pow(1 - saturate(abs(dot(n, V))), 3);
                float near = 1 + 0.6 * (1 - smoothstep(0.8, 4.5, length(pos.xz - _TrailPlayer.xz)));
                float3 lit = albedo * (_FillColor.rgb + _MoonColor.rgb * key * key) * near + rim * albedo * 0.35 + albedo * glow * (0.6 + 0.4 * sin(t * 1.3 + seed * 20));
                o.col = lit * _Gain;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r2 = dot(i.uv, i.uv);
                clip(1 - r2);
                fixed4 col = fixed4(i.col * (1.1 - 0.25 * r2), 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
