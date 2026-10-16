// Ant exoskeleton: baked colour + normal + roughness, with a fresnel rim glow
// so the ants read as faintly luminous creatures in the dark forest.
Shader "AntGarden/AntShell"
{
    Properties
    {
        _MainTex ("Base colour", 2D) = "white" {}
        _BumpMap ("Normal (OpenGL)", 2D) = "bump" {}
        _RoughMap ("Roughness", 2D) = "gray" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _RimColor ("Rim glow colour", Color) = (0.62, 0.96, 1.0, 1)
        _RimPower ("Rim falloff", Range(0.5, 8)) = 2.6
        _RimStrength ("Rim strength", Range(0, 4)) = 0.9
        _GlowColor ("Body glow colour", Color) = (0.18, 0.60, 0.63, 1)
        _GlowStrength ("Body glow", Range(0, 2)) = 0.25
        _HueShift ("Hue shift (degrees)", Range(-180, 180)) = 0
        _Saturation ("Saturation", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.5
        #include "AntGardenCommon.cginc"

        sampler2D _MainTex, _BumpMap, _RoughMap;
        fixed4 _Tint, _RimColor, _GlowColor;
        float _RimPower, _RimStrength, _GlowStrength, _HueShift, _Saturation;
        float _AntDim;   // global, set by the game: 0 under the open sky, up to ~0.25 in a tunnel (the ants stay in keeping)

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 col = AG_HueShift(tex2D(_MainTex, IN.uv_MainTex).rgb, _HueShift, _Saturation) * _Tint.rgb;
            o.Albedo = col;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            o.Smoothness = 1 - tex2D(_RoughMap, IN.uv_MainTex).r;
            float rim = pow(1 - saturate(dot(normalize(IN.viewDir), o.Normal)), _RimPower);
            float lum = dot(col, float3(0.3, 0.55, 0.15));
            o.Emission = (_RimColor.rgb * rim * _RimStrength + _GlowColor.rgb * lum * _GlowStrength) * (1 - _AntDim);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
