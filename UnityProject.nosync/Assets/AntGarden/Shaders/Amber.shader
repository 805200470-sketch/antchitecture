// Translucent amber with a warm light inside (the ancestor's shrine). Brighter at the rim, where the light
// travels through more resin; _Flash makes it flare when an ant touches it.
Shader "AntGarden/Amber"
{
    Properties
    {
        _Color ("Amber colour", Color) = (1.0, 0.55, 0.12, 0.42)
        _Glow ("Inner glow", Range(0, 6)) = 1.1
        _Flash ("Touch flare (0..1)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0
        fixed4 _Color;
        float _Glow, _Flash;
        struct Input { float3 viewDir; float3 worldPos; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float rim = 1 - saturate(dot(normalize(IN.viewDir), o.Normal));
            float breathe = 0.82 + 0.18 * sin(_Time.y * 0.9);
            o.Albedo = _Color.rgb * 0.6;
            o.Smoothness = 0.88;
            o.Alpha = saturate(_Color.a + rim * 0.45);
            o.Emission = _Color.rgb * (_Glow * breathe * (0.35 + rim * 0.9)) * (1 + _Flash * 4);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
