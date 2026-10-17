// Tripo-generated plants (normalized to 1 unit tall, pivot at the base).
// - ORM texture: R = ambient occlusion, G = roughness, B = metallic
// - Ground blend: the base fades into moss so plants grow out of the terrain instead of standing on it
// - Glow band: a local-height range (pods, curls, canopy drops) glows and breathes; with _GlowLumMin
//   set, only the bright parts of the texture inside the band glow (milky pods, gills, resin)
// - Gentle sway that increases with height
// - Per-instance hue shift / saturation / glow tint, so each region can have its own palette and
//   stacked trunk segments can switch the base blend off (_GroundMul = 0)
// - Per-instance glow: its own brightness (_GlowBoost), breathing rhythm (_GlowPhase) and a touch flare
//   (_GlowFlash, set by GlowPlants when an ant brushes the plant) that floods the whole plant with light
// - _GlowSoft widens the band's lower edge into a gradient; _GlowUnder makes undersides and rims glow
//   more than the tops (a canopy lit from within rather than a flat lamp)
Shader "AntGarden/Plant"
{
    Properties
    {
        _MainTex ("Base colour", 2D) = "white" {}
        _BumpMap ("Normal (OpenGL)", 2D) = "bump" {}
        _ORM ("ORM (AO, roughness, metallic)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _RoughnessMin ("Minimum roughness", Range(0, 1)) = 0.45
        _MetallicScale ("Metallic scale", Range(0, 1)) = 0.0
        _GroundColor ("Ground moss colour", Color) = (0.10, 0.20, 0.19, 1)
        _GlowTint ("Glow colour (per instance)", Color) = (0.55, 1.0, 0.95, 1)
        _HueShift ("Hue shift (degrees)", Range(-180, 180)) = 0
        _Saturation ("Saturation", Range(0, 2)) = 1
        _GroundMul ("Base blend on/off", Range(0, 1)) = 1
        _GroundBlend ("Ground blend height (0..1 of plant)", Range(0, 0.5)) = 0.08
        _GlowColor ("Glow colour", Color) = (0.55, 1.0, 0.95, 1)
        _GlowMinY ("Glow from (local y)", Range(0, 1)) = 1
        _GlowMaxY ("Glow to (local y)", Range(0, 1.2)) = 1.1
        _Glow2MinY ("Second glow band from", Range(0, 1)) = 1
        _Glow2MaxY ("Second glow band to", Range(0, 1.2)) = 1.1
        _GlowStrength ("Glow strength", Range(0, 8)) = 0
        _GlowTexture ("Glow follows texture brightness", Range(0, 1)) = 0.6
        _GlowLumMin ("Glow only where the texture is brighter than (0 = everywhere in the band)", Range(0, 1)) = 0
        _GlowSoft ("Glow band lower edge softness (0..1 of plant)", Range(0.01, 0.5)) = 0.06
        _GlowUnder ("Glow mostly on undersides and rims", Range(0, 1)) = 0
        _Sway ("Sway (units at the top)", Range(0, 0.2)) = 0.02
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (Off for open shells)", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300
        Cull [_Cull]

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma multi_compile_instancing
        #pragma target 3.5
        #include "AntGardenCommon.cginc"

        sampler2D _MainTex, _BumpMap, _ORM;
        fixed4 _GlowColor;
        float _RoughnessMin, _MetallicScale, _GroundBlend, _GlowMinY, _GlowMaxY, _Glow2MinY, _Glow2MaxY, _GlowStrength, _GlowTexture, _GlowLumMin, _Sway;
        float _GlowSoft, _GlowUnder;
        float _PlantDim;   // global, set by the particle look (ParticleLook.cs): the mesh sinks behind its points

        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _Tint)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _GroundColor)
            UNITY_DEFINE_INSTANCED_PROP(fixed4, _GlowTint)
            UNITY_DEFINE_INSTANCED_PROP(float, _HueShift)
            UNITY_DEFINE_INSTANCED_PROP(float, _Saturation)
            UNITY_DEFINE_INSTANCED_PROP(float, _GroundMul)
            UNITY_DEFINE_INSTANCED_PROP(float, _GlowBoost)
            UNITY_DEFINE_INSTANCED_PROP(float, _GlowPhase)
            UNITY_DEFINE_INSTANCED_PROP(float, _GlowFlash)
            UNITY_DEFINE_INSTANCED_PROP(float, _GlowMemory)
        UNITY_INSTANCING_BUFFER_END(Props)

        struct Input
        {
            float2 uv_MainTex;
            float localY;
            float localNy;
            fixed facing : VFACE;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.localY = v.vertex.y;
            o.localNy = v.normal.y;
            float3 wp = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            float h = saturate(v.vertex.y);
            float phase = _Time.y * 0.7 + wp.x * 0.13 + wp.z * 0.11;
            float2 sway = float2(sin(phase), cos(phase * 0.8)) * _Sway * h * h;
            // sway in world units, converted back to object space (uniform scale assumed)
            float s = length(unity_ObjectToWorld._m00_m10_m20);
            v.vertex.xz += sway / max(s, 1e-3);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Tint);
            float hue = UNITY_ACCESS_INSTANCED_PROP(Props, _HueShift);
            float sat = UNITY_ACCESS_INSTANCED_PROP(Props, _Saturation);
            fixed3 col = AG_HueShift(tex2D(_MainTex, IN.uv_MainTex).rgb, hue, sat) * tint.rgb;
            fixed3 orm = tex2D(_ORM, IN.uv_MainTex).rgb;
            // the base fades softly into the region's ground colour (switched off for stacked segments)
            float ground = (1 - smoothstep(0, max(_GroundBlend, 1e-3), IN.localY)) * UNITY_ACCESS_INSTANCED_PROP(Props, _GroundMul);
            o.Albedo = lerp(col, UNITY_ACCESS_INSTANCED_PROP(Props, _GroundColor).rgb, ground * 0.6) * (1 - _PlantDim);
            float3 n = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            n.z *= IN.facing < 0 ? -1 : 1; // back faces of open shells (trunk interiors) light correctly
            o.Normal = n;
            o.Occlusion = orm.r;
            o.Smoothness = 1 - max(orm.g, _RoughnessMin);
            o.Metallic = orm.b * _MetallicScale;

            float band = smoothstep(_GlowMinY, _GlowMinY + _GlowSoft, IN.localY) * (1 - smoothstep(_GlowMaxY - 0.04, _GlowMaxY, IN.localY));
            band = max(band, smoothstep(_Glow2MinY, _Glow2MinY + 0.04, IN.localY) * (1 - smoothstep(_Glow2MaxY - 0.03, _Glow2MaxY, IN.localY)));
            // every plant breathes at its own pace and phase
            float ph = UNITY_ACCESS_INSTANCED_PROP(Props, _GlowPhase) * 6.2832;
            float breathe = 0.72 + 0.28 * sin(_Time.y * (1.1 + 0.9 * frac(ph * 0.37)) + IN.localY * 3 + ph);
            // a memory plant breathes in two slow blinks, then rests: you learn to spot it
            float cyc = frac(_Time.y / 3.6 + ph * 0.159);
            float blink = 0.35 + 0.65 * max(exp(-pow((cyc - 0.12) * 14, 2)), exp(-pow((cyc - 0.3) * 14, 2)));
            breathe = lerp(breathe, blink, UNITY_ACCESS_INSTANCED_PROP(Props, _GlowMemory));
            float lum = dot(col, float3(0.3, 0.55, 0.15));
            fixed3 glow = UNITY_ACCESS_INSTANCED_PROP(Props, _GlowTint).rgb;
            float bright = _GlowLumMin > 0 ? smoothstep(_GlowLumMin, _GlowLumMin + 0.2, lum) : 1;
            float under = lerp(1, saturate(0.65 - 0.6 * IN.localNy * (IN.facing < 0 ? -1 : 1)), _GlowUnder);
            float3 e = glow * band * bright * under * breathe * _GlowStrength * lerp(1, lum * 2, _GlowTexture);
            // touched: the glow flares several times brighter and spreads over the whole plant, then fades
            float flash = UNITY_ACCESS_INSTANCED_PROP(Props, _GlowFlash);
            e = e * UNITY_ACCESS_INSTANCED_PROP(Props, _GlowBoost) * (1 + flash * 3.5)
                + glow * (0.15 + lum) * flash * (_GlowStrength * 0.5 + 0.6) * step(1e-3, _GlowStrength);
            o.Emission = e * (1 - _PlantDim * 0.7);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
