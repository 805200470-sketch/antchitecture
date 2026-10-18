// The particle nest's pheromones (Pheromones.cs): small arrows of light flying ahead of the ant along the way, and
// the soft glow at the heart of the swarm. Each is a quad whose four vertices sit at its centre: uv = corner
// (-1..1), normal = its direction of flight (world), uv2 = (length, width) of the arrow, uv3.x = 1 for the glow.
// The quad faces the camera with its length along the flight direction as the camera sees it. An arrow is a
// soft chevron pointing the way it flies, with a faint tail behind; drawn additively in HDR, for the bloom.
Shader "AntGarden/Pheromone"
{
    Properties
    {
        _Gain ("Brightness (HDR)", Float) = 1.3
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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

            float _Gain;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float2 uv3 : TEXCOORD2;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float glow : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 c = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 view = normalize(c - _WorldSpaceCameraPos);
                // the flight direction as the camera sees it
                float3 f = v.normal - view * dot(v.normal, view);
                float fl = length(f);
                f = fl > 1e-4 ? f / fl : UNITY_MATRIX_V[1].xyz;
                float3 side = normalize(cross(view, f));
                float len = v.uv2.x, wid = v.uv2.y;
                // a little longer than the arrow, for the tail behind it
                float3 w = c + f * (v.uv.x * len * (v.uv.x < 0 ? 1.8 : 1.0)) + side * (v.uv.y * wid);
                o.pos = mul(UNITY_MATRIX_VP, float4(w, 1));
                o.uv = v.uv;
                o.color = v.color;
                o.glow = v.uv3.x;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float a;
                if (i.glow > 0.5)
                {
                    // the swarm's heart: a soft glow
                    float r2 = dot(i.uv, i.uv);
                    a = exp(-r2 * 5.0) * 0.5;
                }
                else
                {
                    // a chevron pointing along +x: two strokes from the tip back to either side
                    float2 p = i.uv;
                    float2 q = float2(p.x, abs(p.y));
                    float2 a0 = float2(0.75, 0), a1 = float2(-0.35, 0.8);
                    float2 ab = a1 - a0;
                    float t = saturate(dot(q - a0, ab) / dot(ab, ab));
                    float d = length(q - (a0 + ab * t));
                    float stroke = 1 - smoothstep(0.07, 0.2, d);
                    float halo = exp(-d * d * 20.0) * 0.2;
                    // a faint tail behind it
                    float tail = (p.x < -0.2 ? exp(-p.y * p.y * 40.0) * saturate((p.x + 1.0) / 0.8) * 0.3 : 0);
                    a = stroke + halo + tail;
                }
                fixed4 col = fixed4(i.color.rgb * i.color.a * a * _Gain, 0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
                return col;
            }
            ENDCG
        }
    }
}
