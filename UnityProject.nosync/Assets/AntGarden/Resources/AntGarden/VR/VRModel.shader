// In a headset: Meta's own models of the controllers (MetaDevices), their own colour maps (the buttons' letters
// among them), lit by themselves rather than by the scene: the particle nest has no lights a lit model could take, and
// its own lit shader showed them near black. A soft light from above the eyes and a faint rim, so their shape reads in
// the dark. In a Resources folder so the build keeps it: nothing else refers to it.
Shader "AntGarden/VRModel"
{
    Properties
    {
        _BaseMap ("Colour", 2D) = "white" {}
        _Light ("Brightness", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "IgnoreProjector" = "True" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            float _Light;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 v : TEXCOORD2; };

            v2f vert(appdata i)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = WorldSpaceViewDir(i.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.n), v = normalize(i.v);
                float3 L = normalize(UNITY_MATRIX_V[1].xyz * 0.8 + v * 0.6);
                float key = saturate(dot(n, L) * 0.5 + 0.5);
                float rim = pow(1 - saturate(abs(dot(n, v))), 3);
                float3 c = tex2D(_BaseMap, i.uv).rgb * (0.35 + 0.85 * key) + rim * 0.12;
                return fixed4(c * _Light, 1);
            }
            ENDCG
        }
    }
}
