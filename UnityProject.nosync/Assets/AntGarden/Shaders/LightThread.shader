// The particle nest's guiding threads of light (GuideThreads.cs): thin luminous lines hanging in the air, a bright fine
// core inside a soft halo. Drawn as camera-facing ribbons whose vertices are already in world space: uv.x runs along a
// thread (0..1), uv.y across it (-1..1); the vertex colour's alpha is how bright the thread is there (its life, a pulse
// of light running along it, its flicker). Additive, in HDR, for the bloom.
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
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_FOG_COORDS(1)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float a = i.uv.y;
                float core = exp(-a * a * 60.0), halo = exp(-a * a * 4.5) * 0.3;
                float3 c = lerp(_Halo.rgb, _Core.rgb, saturate(core * 1.3)) * (core + halo) * i.color.rgb * i.color.a * _Gain;
                fixed4 col = fixed4(c, 0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
                return col;
            }
            ENDCG
        }
    }
}
