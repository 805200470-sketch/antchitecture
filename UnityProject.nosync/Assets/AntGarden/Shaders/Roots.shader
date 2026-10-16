// Roots hanging from a tunnel ceiling. Vertex colour: rgb = colour, a = glow (1 at a glowing tip).
Shader "AntGarden/Roots"
{
    Properties
    {
        _Glow ("Tip glow strength", Range(0, 8)) = 2.2
        _Smooth ("Smoothness", Range(0, 1)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        float _Glow, _Smooth;
        struct Input { float4 color : COLOR; float3 worldPos; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = IN.color.rgb * (1 - IN.color.a) + 0.04 * IN.color.a;
            o.Smoothness = _Smooth;
            // the glowing tips breathe slowly, each at its own phase
            float breathe = 0.7 + 0.3 * sin(_Time.y * 1.3 + dot(IN.worldPos, float3(0.7, 0.3, 0.9)));
            o.Emission = IN.color.rgb * IN.color.a * _Glow * breathe;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
