// In a headset: the player's hands round the controllers (VRHands), drawn as the headset's own home draws them: a
// dark, see-through shape with a pale line along its outline. Its depth first, so only its nearest surface is drawn
// (no finger seen through the palm); the vertex colour's alpha fades it (the wrist, toward the arm). In a Resources
// folder so the build keeps it: nothing else refers to it.
Shader "AntGarden/VRGhostHand"
{
    Properties
    {
        _Fill ("Fill (linear; alpha: how solid)", Color) = (0.015, 0.016, 0.02, 0.5)
        _Line ("Outline (linear)", Color) = (0.75, 0.82, 0.86, 1)
        _Light ("Brightness", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        CGINCLUDE
        #include "UnityCG.cginc"

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
        ENDCG

        Pass
        {
            ZWrite On
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment depth
            fixed4 depth(v2f i) : SV_Target { return 0; }
            ENDCG
        }

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            fixed4 _Fill, _Line;
            float _Light;

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.n), v = normalize(i.v);
                float edge = smoothstep(0.72, 0.9, 1 - abs(dot(n, v)));   // the outline: where the surface turns away
                float3 c = lerp(_Fill.rgb, _Line.rgb * _Light, edge);
                return fixed4(c, lerp(_Fill.a, _Line.a, edge) * i.color.a);
            }
            ENDCG
        }
    }
}
