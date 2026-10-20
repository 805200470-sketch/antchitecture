// The particle nest's guiding filaments of light (GuideThreads.cs): thin luminous lines hanging in the air, a bright
// fine core inside a soft halo, and a small bright knot where a line turns. Vertices are already in world space.
// A line segment is a camera-facing quad: uv.y runs across it (-1..1). A knot (uv2.x = 1) is a small round sprite:
// uv is its position inside it (-1..1). The vertex colour's alpha is how bright it is there (its life, the light
// running along it, its flicker). Additive, in HDR, for the bloom.
Shader "AntGarden/LightThread"
{
    Properties
    {
        _Gain ("Brightness (HDR)", Float) = 2
        _Core ("Core colour", Color) = (0.92, 0.95, 0.9, 1)
        _Halo ("Halo colour", Color) = (0.55, 0.9, 0.95, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+6" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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
            float4 _Core, _Halo;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float knot : TEXCOORD1;
                half4 color : COLOR;
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.knot = v.uv2.x;
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float core, halo;
                if (i.knot > 0.5)
                {
                    float r2 = dot(i.uv, i.uv);
                    core = exp(-r2 * 18.0);
                    halo = exp(-r2 * 3.0) * 0.35;
                }
                else
                {
                    float a = i.uv.y;
                    core = exp(-a * a * 60.0);
                    halo = exp(-a * a * 4.5) * 0.3;
                }
                float3 c = lerp(_Halo.rgb, _Core.rgb, saturate(core * 1.3)) * (core + halo) * i.color.rgb * i.color.a * _Gain;
                fixed4 col = fixed4(c, 0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
                return col;
            }
            ENDCG
        }
    }
}
