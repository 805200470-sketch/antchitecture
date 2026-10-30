// In a headset: the controllers in the player's hands (VRHands). Dark plastic lit softly from above, a pale rim along
// the outline so they read against the night, and the buttons a little lit (the vertex colour's alpha: how much a
// part glows). In a Resources folder so the build keeps it: nothing else refers to it.
Shader "AntGarden/VRController"
{
    Properties
    {
        _Rim ("Rim (linear)", Color) = (0.35, 0.75, 0.85, 1)
        _Light ("Brightness", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Rim;
            float _Light;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; fixed4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert(appdata i)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(i.vertex);
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = WorldSpaceViewDir(i.vertex);
                o.color = i.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.n), v = normalize(i.v);
                float key = saturate(dot(n, normalize(float3(0.3, 1, -0.2))) * 0.5 + 0.5);
                float rim = pow(1 - saturate(dot(n, v)), 3);
                float3 c = i.color.rgb * (0.4 + 0.9 * key) + _Rim.rgb * rim * 0.5 + i.color.rgb * i.color.a * 3;
                return fixed4(c * _Light, 1);
            }
            ENDCG
        }
    }
}
