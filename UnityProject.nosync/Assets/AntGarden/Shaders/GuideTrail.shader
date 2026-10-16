// Small glowing particles: the specks and drifting dust that lead through a ruin tunnel (GuideTrail.cs) and
// the motes rising along the seams between nests (SeamMotes.cs). All are soft camera-facing discs drawn
// additively (flat ones lying on the floor vanish into slivers when the camera looks along the ground).
// Each quad's four vertices sit at its start point; uv = corner (-1..1); uv2.x = distance along the track to the
// heart; uv2.y = kind:
//   0  speck   resting on the floor, breathing (uv4.x = its pace)
//   1  mote    floating, bobbing and drifting a little (uv4.x = pace, uv4.y = how far it bobs)
//   2  drift   travelling along normal (the whole way) over uv4.x seconds and starting again, fading in and out,
//              rising and sinking by uv4.y on the way: dust carried along the path, motes rising off a seam
// uv3.x = a random phase, uv3.y = the radius. Vertex colour: rgb = tint × the particle's own brightness (0..1);
// _Gain (set per renderer) lifts it into HDR. At rest everything is dim. Near the player's ant it brightens a
// little and stays bright a moment behind it (_TrailLag follows the ant with a delay); the way on ahead along the
// track (_PlayerD: the ant's distance to the heart, -1 off the track) lights up, nearest the ant most.
Shader "AntGarden/GuideTrail"
{
    Properties
    {
        _Base ("Resting glow", Range(0, 1)) = 0.38
        _Near ("Extra glow near the ant", Range(0, 2)) = 0.3
        _Ahead ("Extra glow on the way ahead", Range(0, 2)) = 0.4
        _AheadReach ("How far ahead the way lights up (units)", Float) = 10
        _Gain ("Overall gain (HDR)", Float) = 1.1
        _PlayerD ("The ant's distance to the heart along the track (-1: off it)", Float) = -1
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
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            float _Base, _Near, _Ahead, _AheadReach, _Gain, _PlayerD;
            float4 _TrailPlayer, _TrailLag;   // global: the player's ant, and a point trailing behind it

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float2 uv3 : TEXCOORD2;
                float2 uv4 : TEXCOORD3;
                float4 color : COLOR;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 color : COLOR;
                float glow : TEXCOORD1;
                float kind : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float phase = v.uv3.x * 6.2831;
                float t = _Time.y;
                float kind = v.uv2.y, fade = 1, d = v.uv2.x;
                if (kind > 1.5)
                {
                    // drifting: along its way, starting again when it gets there; it fades in and out, and
                    // wavers a little
                    float u = frac(t / max(0.1, v.uv4.x) + v.uv3.x);
                    world += v.normal * u;
                    world.y += v.uv4.y * sin(u * 3.14159);
                    world.xz += 0.05 * float2(sin(t * 1.3 + phase * 3.0), cos(t * 1.1 + phase * 5.0));
                    fade = smoothstep(0.0, 0.18, u) * (1 - smoothstep(0.7, 1.0, u));
                    d -= length(v.normal.xz) * u;
                }
                else if (kind > 0.5)
                {
                    // a mote bobs and drifts a little
                    world.y += v.uv4.y * sin(t * 0.8 + phase);
                    world.xz += 0.07 * float2(sin(t * 0.37 + phase * 1.7), cos(t * 0.29 + phase * 1.3));
                }
                // spread the centre into a camera-facing quad
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                world += (right * v.uv.x + up * v.uv.y) * v.uv3.y;
                o.pos = mul(UNITY_MATRIX_VP, float4(world, 1));
                o.uv = v.uv;
                o.kind = kind;
                // each one breathes on its own (drifting dust keeps a steady glow)
                float breathe = kind > 1.5 ? 1 : 0.65 + 0.35 * sin(t * v.uv4.x + phase);
                // near the ant, and just behind where it has been
                float dn = length(world.xz - _TrailPlayer.xz), dl = length(world.xz - _TrailLag.xz);
                float near = max(1 - smoothstep(0.4, 2.6, dn), 0.6 * (1 - smoothstep(0.4, 2.0, dl)));
                // the way on: what lies a little nearer the heart than the ant lights up, the nearest most
                float k = _PlayerD - d;
                float ahead = _PlayerD >= 0 && k > 0 && k < _AheadReach ? pow(1 - k / _AheadReach, 1.3) * (1 - smoothstep(_AheadReach * 0.7, _AheadReach * 1.2, dn)) : 0;
                o.glow = (_Base * breathe + _Near * near + _Ahead * ahead) * _Gain * fade;
                o.color = v.color.rgb;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv);
                // a small bright core inside a soft halo
                float core = 1 - smoothstep(0.15, 0.45, r);
                float halo = saturate(1 - r);
                halo *= halo;
                float a = core * 0.9 + halo * (i.kind > 0.5 ? 0.45 : 0.35);
                fixed4 c = fixed4(i.color * a * i.glow, 0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0, 0, 0, 0));
                return c;
            }
            ENDCG
        }
    }
}
