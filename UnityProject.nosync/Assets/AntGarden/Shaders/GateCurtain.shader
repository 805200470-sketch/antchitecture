// The light curtain inside a gate arch: an additive, two-sided veil with drifting bands of light,
// brightest at the rim and fading towards the centre and the ground. It fades out as the camera comes
// close, so walking (or arriving) through a gate never washes the whole screen in its colour.
Shader "AntGarden/GateCurtain"
{
    Properties
    {
        _Color ("Colour (HDR)", Color) = (0.45, 1.0, 0.95, 1)
        _Intensity ("Intensity", Range(0, 4)) = 0.9
        _NearFade ("Near fade start, end (units)", Vector) = (1.5, 7, 0, 0)
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
            #include "AGClock.cginc"

            fixed4 _Color;
            float _Intensity;
            float4 _NearFade;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) float eye : TEXCOORD2; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; // x: around the arch 0..1, y: centre (0) .. rim (1)
                o.eye = distance(mul(unity_ObjectToWorld, v.vertex).xyz, _WorldSpaceCameraPos);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float rim = pow(i.uv.y, 3);
                float bands = 0.5 + 0.5 * sin(i.uv.y * 18 - AG_TIME * 2.2 + sin(i.uv.x * 9 + AG_TIME) * 1.5);
                float shimmer = 0.6 + 0.4 * sin(i.uv.x * 40 + AG_TIME * 3.1);
                float a = (0.12 + 0.55 * rim + 0.18 * bands * shimmer) * _Intensity;
                a *= smoothstep(_NearFade.x, _NearFade.y, i.eye);
                fixed4 c = fixed4(_Color.rgb * a, 1);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0, 0, 0, 0));
                return c;
            }
            ENDCG
        }
    }
}
