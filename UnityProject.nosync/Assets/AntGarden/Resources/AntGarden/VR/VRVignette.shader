// In a headset, against motion sickness (Scripts/Runtime/VRComfort.cs): a sphere round the eyes, drawn over the world,
// dark by how far from straight ahead each pixel looks (its angle from the eye's own forward), so it sits the same in
// both eyes, whatever the sphere's size or where it is.
Shader "AntGarden/VR/VRVignette"
{
    Properties
    {
        _Strength ("Darkness", Range(0, 1)) = 0
        _Clear ("Clear within (cosine of the angle)", Float) = 0.79
        _Dark ("Dark beyond (cosine of the angle)", Float) = 0.47
    }
    SubShader
    {
        Tags { "Queue" = "Overlay-2" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Front
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Strength, _Clear, _Dark;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.world = mul(unity_ObjectToWorld, vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 ray = normalize(i.world - _WorldSpaceCameraPos);
                float ahead = dot(ray, -UNITY_MATRIX_V[2].xyz);   // the view's forward (view space looks down -z)
                return fixed4(0, 0, 0, _Strength * (1 - smoothstep(_Dark, _Clear, ahead)));
            }
            ENDCG
        }
    }
}
