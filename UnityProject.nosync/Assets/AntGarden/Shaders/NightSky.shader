// Night sky above the fungal forest: deep blue overhead, a teal glow near the horizon,
// faint drifting haze and sparse stars.
Shader "AntGarden/NightSky"
{
    Properties
    {
        _TopColor ("Zenith", Color) = (0.015, 0.035, 0.075, 1)
        _HorizonColor ("Horizon", Color) = (0.10, 0.24, 0.30, 1)
        _BottomColor ("Below horizon", Color) = (0.03, 0.07, 0.09, 1)
        _StarDensity ("Stars", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "AGClock.cginc"

            fixed4 _TopColor, _HorizonColor, _BottomColor;
            float _StarDensity;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                fixed3 col = y > 0 ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(y), 0.55))
                                   : lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-y * 4));
                // sparse stars above the haze line
                float3 cell = floor(d * 220);
                float s = step(1 - 0.004 * _StarDensity, hash31(cell)) * smoothstep(0.15, 0.5, y);
                col += s * (0.6 + 0.4 * sin(AG_TIME * 2 + hash31(cell + 3) * 6.28)) * fixed3(0.7, 0.9, 1.0);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
