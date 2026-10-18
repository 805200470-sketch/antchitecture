// The particle nest's solid things on the floor (SolidThings.cs: leaves, twigs, moss, mushrooms, pebbles...): each
// drawn as opaque splats over its surface, the way a Gaussian-splat capture shows a thing, read from a buffer of
// { position, normal (octahedral 16+16 bits), colour (gamma rgb; fourth byte: how much it glows), radius }. One
// triangle per splat, holding a dab whose outline is a little irregular and a little longer one way than the other,
// lying tilted between its surface and the camera, so surfaces foreshorten without breaking into gaps. Each splat is
// lit from its normal: a cool moonlight from above, a soft fill, a thin rim, and the ant's own nearness (as if its
// antennae felt the things round it). Glowing parts (mushroom gills) breathe slowly, each thing at its own pace.
// Far away the things thin out splat by splat (the rest a little larger), so the count stays bounded.
Shader "AntGarden/ParticleSolid"
{
    Properties
    {
        _Gain ("Brightness", Float) = 1
        _GlowGain ("Glow brightness (HDR)", Float) = 1.5
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

            struct SolidPoint { float3 pos; uint nrm; uint col; float radius; };
            StructuredBuffer<SolidPoint> _Solid;
            float _Gain, _GlowGain, _MinPixels;
            float4 _TrailPlayer;            // global: the player's ant
            float4 _Lod;                    // x: all splats within, y: thinned to z of them by (units from the camera)
            float4 _MoonDir, _MoonColor, _FillColor;   // the light (direction toward it, world), its colour and the fill's

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
                // far away: fewer splats, the rest a little larger
                float dist = distance(p.pos, _WorldSpaceCameraPos);
                float keep = lerp(1, _Lod.z, saturate((dist - _Lod.x) / max(1e-3, _Lod.y - _Lod.x)));
                if (h > keep) return o;
                float3 n = DecodeNormal(p.nrm);
                float3 V = normalize(_WorldSpaceCameraPos - p.pos);
                if (dot(n, V) < 0) n = -n;
                // the dab lies between its surface and the camera, longer one way than the other, turned at random
                float3 N = normalize(n * 0.6 + V * 0.4);
                float3 R = UNITY_MATRIX_V[0].xyz;
                float3 B = normalize(cross(N, R)), T = cross(B, N);
                float turn = h2 * 6.2831853, cs = cos(turn), sn = sin(turn);
                float3 A1 = T * cs + B * sn, A2 = -T * sn + B * cs;
                float stretch = 1 + 0.6 * Hash(i + 104729u);
                float pixel = 2 * dist / (unity_CameraProjection._m11 * _ScreenParams.y);
                float size = max(p.radius / sqrt(max(keep, 0.05)), pixel * _MinPixels);
                float2 c = corners[id % 3];
                o.pos = mul(UNITY_MATRIX_VP, float4(p.pos + (A1 * (c.x * stretch) + A2 * (c.y / stretch)) * size, 1));
                o.uv = c;
                o.shape = h2;
                // lit from its normal: moonlight from above, a soft fill, a thin rim; brighter near the ant
                float3 albedo = GammaToLinearSpace(float3(p.col & 255, (p.col >> 8) & 255, (p.col >> 16) & 255) / 255.0);
                float glow = ((p.col >> 24) & 255) / 255.0;
                float key = saturate(dot(n, _MoonDir.xyz) * 0.75 + 0.25);
                float rim = pow(1 - saturate(dot(n, V)), 3);
                float near = 1 + 0.9 * (1 - smoothstep(0.8, 4.5, length(p.pos.xz - _TrailPlayer.xz)));
                float3 lit = albedo * (_FillColor.rgb + _MoonColor.rgb * key * key) * near + rim * 0.08 * albedo * 4;
                // glowing parts breathe, each thing at its own pace (by where it is)
                if (glow > 0)
                {
                    float cell = Hash(asuint(floor(p.pos.x * 0.8)) * 73856093u ^ asuint(floor(p.pos.z * 0.8)) * 19349663u);
                    float breathe = 0.35 + 0.65 * (0.5 + 0.5 * sin(_Time.y * (0.45 + 0.6 * cell) + cell * 40));
                    lit += albedo * glow * _GlowGain * breathe;
                }
                o.col = lit * _Gain;
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
                fixed4 col = fixed4(i.col * (1 - 0.1 * smoothstep(0.6, 1.0, r2)), 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
