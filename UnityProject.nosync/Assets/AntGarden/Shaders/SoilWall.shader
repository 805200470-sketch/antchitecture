// Cross-section of the soil below the hexagon rim: dark strata with roots and
// glowing seams, so each nest module reads as a cut-out diorama.
Shader "AntGarden/SoilWall"
{
    Properties
    {
        _TopColor ("Top (moss)", Color) = (0.10, 0.22, 0.22, 1)
        _MidColor ("Soil", Color) = (0.045, 0.10, 0.12, 1)
        _DeepColor ("Deep soil", Color) = (0.02, 0.045, 0.06, 1)
        _SeamColor ("Glowing seams", Color) = (0.35, 0.85, 0.80, 1)
        _SeamStrength ("Seam glow", Range(0, 3)) = 0.6
        _Depth ("Wall depth (units)", Float) = 7
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        fixed4 _TopColor, _MidColor, _DeepColor, _SeamColor;
        float _SeamStrength, _Depth;

        struct Input
        {
            float3 wpos;
            float4 color : COLOR; // r = 0 at the rim top .. 1 at the wall bottom
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
        }

        float hash11(float p) { return frac(sin(p * 127.1) * 43758.5453); }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float t = saturate(IN.color.r);
            float along = IN.wpos.x * 0.7 + IN.wpos.z * 0.7;
            // wavy strata
            float layer = t * 9 + sin(along * 0.35) * 0.35 + sin(along * 1.3) * 0.12;
            float band = frac(layer);
            fixed3 col = lerp(_TopColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.12, t));
            col = lerp(col, _DeepColor.rgb, smoothstep(0.35, 1.0, t));
            col *= 0.8 + 0.4 * hash11(floor(layer));
            float seam = smoothstep(0.92, 0.985, band) * step(0.55, hash11(floor(layer) + 3.7)) * smoothstep(0.08, 0.2, t);
            o.Albedo = col;
            o.Smoothness = 0.08;
            o.Emission = _SeamColor.rgb * seam * _SeamStrength * (0.7 + 0.3 * sin(_Time.y + along));
        }
        ENDCG
    }
    FallBack "Diffuse"
}
