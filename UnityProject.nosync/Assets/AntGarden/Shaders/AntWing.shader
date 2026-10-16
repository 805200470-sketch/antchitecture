// Translucent wing membrane (Ant_003): alpha-blended, edges glow. The membrane is a closed thin
// shell, so back faces are culled (drawing both would double the opacity).
Shader "AntGarden/AntWing"
{
    Properties
    {
        _MainTex ("Base colour", 2D) = "white" {}
        _Tint ("Tint", Color) = (0.75, 0.95, 1.0, 1)
        _Alpha ("Opacity", Range(0, 1)) = 0.35
        _RimColor ("Rim glow colour", Color) = (0.7, 1.0, 1.0, 1)
        _RimStrength ("Rim strength", Range(0, 4)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        LOD 300
        Cull Back
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.5

        sampler2D _MainTex;
        fixed4 _Tint, _RimColor;
        float _Alpha, _RimStrength;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 col = tex2D(_MainTex, IN.uv_MainTex).rgb * _Tint.rgb;
            float rim = pow(1 - saturate(abs(dot(normalize(IN.viewDir), float3(0, 0, 1)))), 2);
            o.Albedo = col;
            o.Smoothness = 0.8;
            o.Emission = _RimColor.rgb * rim * _RimStrength;
            o.Alpha = saturate(_Alpha + rim * 0.3);
        }
        ENDCG
    }
}
