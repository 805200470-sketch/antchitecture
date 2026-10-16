// Canyon terrain: three layers (floor / bank / plateau) blended by vertex colour, each sampled
// triplanar so the steep canyon banks never stretch, with triplanar normal maps for surface relief.
// Vertex colour: r = floor, g = bank, b = plateau weights, a = cavity (1 = open, 0 = occluded).
// _HueShift / _Saturation recolour the same textures per region (blue fungal forest, warm amber...).
// The albedo alpha is a height map (white = raised). Near the camera the floor is parallax-occlusion
// mapped with it (grains, pebbles and cracks stand out and hide each other), the layers meet along their
// heights instead of a soft smear, and low spots are shaded as crevices.
Shader "AntGarden/Terrain"
{
    Properties
    {
        _FloorTex ("Floor", 2D) = "gray" {}
        _BankTex ("Bank", 2D) = "gray" {}
        _PlateauTex ("Plateau", 2D) = "gray" {}
        _FloorNormal ("Floor normal", 2D) = "bump" {}
        _FloorTex2 ("Floor variant 2", 2D) = "gray" {}
        _FloorTex3 ("Floor variant 3", 2D) = "gray" {}
        _FloorNormal2 ("Floor variant 2 normal", 2D) = "bump" {}
        _FloorNormal3 ("Floor variant 3 normal", 2D) = "bump" {}
        _FloorVariety ("Floor variants on (0 = one floor texture)", Range(0, 1)) = 0
        _VarietyScale ("Size of the floor variant patches (1 / units)", Float) = 0.06
        _BankNormal ("Bank normal", 2D) = "bump" {}
        _PlateauNormal ("Plateau normal", 2D) = "bump" {}
        _DetailScale ("Close-up detail scale (x floor scale)", Float) = 3.7
        _DetailMix ("Close-up detail amount", Range(0, 1)) = 0.45
        _ParallaxDepth ("Floor relief depth (world units, 0 = off)", Range(0, 0.4)) = 0.12
        _ParallaxRange ("Relief fades out beyond this distance", Float) = 22
        _HeightBlend ("Layers meet along their heights", Range(0, 1)) = 0.6
        _Cavity ("Crevice shading from the height map", Range(0, 1)) = 0.2
        _NormalStrength ("Normal strength", Range(0, 3)) = 1.4
        _FloorTint ("Floor tint", Color) = (0.85, 1.05, 1.08, 1)
        _BankTint ("Bank tint", Color) = (0.75, 0.98, 0.92, 1)
        _PlateauTint ("Plateau tint", Color) = (0.55, 0.72, 0.76, 1)
        _HueShift ("Hue shift (degrees)", Range(-180, 180)) = 0
        _Saturation ("Saturation", Range(0, 2)) = 1
        _FloorScale ("Floor texture scale (per unit)", Float) = 0.11
        _BankScale ("Bank texture scale (per unit)", Float) = 0.08
        _PlateauScale ("Plateau texture scale (per unit)", Float) = 0.05
        _FloorSmooth ("Floor smoothness", Range(0, 1)) = 0.38
        _BankSmooth ("Bank smoothness", Range(0, 1)) = 0.12
        _DetailStrength ("Large-scale variation", Range(0, 1)) = 0.35
        _GlowSpecks ("Glow specks on the floor", Range(0, 2)) = 0.6
        _GlowColor ("Glow colour", Color) = (0.55, 1.0, 0.92, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.5
        #include "AntGardenCommon.cginc"

        sampler2D _FloorTex, _BankTex, _PlateauTex, _FloorNormal, _BankNormal, _PlateauNormal;
        sampler2D _FloorTex2, _FloorTex3, _FloorNormal2, _FloorNormal3;
        float _FloorVariety, _VarietyScale;
        float _DetailScale, _DetailMix, _ParallaxDepth, _ParallaxRange, _HeightBlend, _Cavity;
        fixed4 _FloorTint, _BankTint, _PlateauTint, _GlowColor;
        float _FloorScale, _BankScale, _PlateauScale, _FloorSmooth, _BankSmooth, _DetailStrength, _GlowSpecks;
        float _NormalStrength, _HueShift, _Saturation;

        struct Input
        {
            float3 wpos;
            float3 wnrm;
            float4 color : COLOR;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.wnrm = UnityObjectToWorldNormal(v.normal);
        }

        float hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float vnoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            float2 u = f * f * (3 - 2 * f);
            return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x), lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), u.x), u.y);
        }

        // rgb = colour, a = height; the top projection can be shifted (parallax) by uvShift
        fixed4 triplanar(sampler2D tex, float3 p, float3 w, float scale, float2 uvShift)
        {
            fixed4 x = tex2D(tex, p.zy * scale);
            fixed4 y = tex2D(tex, p.xz * scale + uvShift);
            fixed4 z = tex2D(tex, p.xy * scale);
            return x * w.x + y * w.y + z * w.z;
        }

        // parallax occlusion mapping on the top projection: march the view ray down through the height
        // field (alpha) and return the uv shift where it first hits the surface
        float2 parallax(sampler2D tex, float2 uv, float2 dx, float2 dy, float3 V, float depthUv)
        {
            // more layers when the view is low (where stepping would show), fewer from above
            float steps = lerp(28, 10, saturate(V.y));
            float layer = 1.0 / steps;
            float2 step = -V.xz / max(V.y, 0.3) * depthUv * layer;
            float rayH = 1, h = tex2Dgrad(tex, uv, dx, dy).a;
            float2 cur = uv;
            [loop] for (int i = 0; i < 28 && i < steps && h < rayH; i++)
            {
                cur += step;
                rayH -= layer;
                h = tex2Dgrad(tex, cur, dx, dy).a;
            }
            // refine between the last two samples
            float after = h - rayH;
            float before = tex2Dgrad(tex, cur - step, dx, dy).a - (rayH + layer);
            float t = saturate(after / max(after - before, 1e-4));
            return lerp(cur, cur - step, t) - uv;
        }

        // world-space triplanar normal ("whiteout" blend), n = geometric world normal
        float3 triplanarNormal(sampler2D nm, float3 p, float3 n, float3 w, float scale, float2 uvShift)
        {
            float3 tx = UnpackNormal(tex2D(nm, p.zy * scale));
            float3 ty = UnpackNormal(tex2D(nm, p.xz * scale + uvShift));
            float3 tz = UnpackNormal(tex2D(nm, p.xy * scale));
            tx.xy *= _NormalStrength; ty.xy *= _NormalStrength; tz.xy *= _NormalStrength;
            float3 s = sign(n);
            tx = float3(tx.xy + n.zy, abs(tx.z) * n.x);
            ty = float3(ty.xy + n.xz, abs(ty.z) * n.y);
            tz = float3(tz.xy + n.xy, abs(tz.z) * n.z);
            return normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = normalize(IN.wnrm);
            float3 w = pow(abs(n), 4);
            w /= (w.x + w.y + w.z);
            float4 c = IN.color;
            float total = max(1e-3, c.r + c.g + c.b);
            float3 lw = c.rgb / total;

            float dist = distance(IN.wpos, _WorldSpaceCameraPos);
            float3 V = normalize(_WorldSpaceCameraPos - IN.wpos);
            float2 uvF = IN.wpos.xz * _FloorScale;
            float2 dxF = ddx(uvF), dyF = ddy(uvF);
            // relief: only near the camera, on flattish floor, and eased off at grazing angles (where any
            // height-field trick turns into streaks)
            float pom = _ParallaxDepth * saturate(1 - (dist - _ParallaxRange * 0.5) / (_ParallaxRange * 0.5)) * saturate(w.y * 1.5 - 0.3) * saturate(lw.x * 2)
                      * smoothstep(0.06, 0.32, V.y);
            float2 shift = 0;
            if (pom > 1e-4) shift = parallax(_FloorTex, uvF, dxF, dyF, V, pom * _FloorScale);
            // the floor: up to three sister textures (packed earth, moss, gravel...) in large soft patches;
            // at a patch edge the one whose surface is higher there wins, so moss and stones overlap the earth
            // instead of cross-fading (top projection only; the steep sides use the first texture)
            float2 uvTop = IN.wpos.xz * _FloorScale + shift;
            fixed4 fy = tex2D(_FloorTex, uvTop);
            float wb = 0, wc = 0;
            float2 uvB = uvTop * 0.93 + 0.37, uvC = uvTop * 1.07 + 0.71;
            if (_FloorVariety > 0)
            {
                fixed4 tb = tex2D(_FloorTex2, uvB), tc = tex2D(_FloorTex3, uvC);
                float2 q = IN.wpos.xz * _VarietyScale;
                float n1 = vnoise(q) * 0.7 + vnoise(q * 2.9 + 13.1) * 0.3;
                float n2 = vnoise(q * 0.83 + 47.3) * 0.7 + vnoise(q * 2.3 - 8.7) * 0.3;
                wb = smoothstep(0.46, 0.6, n1 + (tb.a - fy.a) * 0.22) * _FloorVariety;
                wc = smoothstep(0.52, 0.66, n2 + (tc.a - max(fy.a, tb.a)) * 0.22) * _FloorVariety;
                fy = lerp(lerp(fy, tb, wb), tc, wc);
            }
            fixed4 floorT = tex2D(_FloorTex, IN.wpos.zy * _FloorScale) * w.x + fy * w.y + tex2D(_FloorTex, IN.wpos.xy * _FloorScale) * w.z;
            fixed3 floorC = floorT.rgb * _FloorTint.rgb;
            // close up, the floor gets a finer copy of itself (top projection only: the floor is flattish),
            // fading in near the camera, so the ground stays crisp at ant scale
            float near = 1 - saturate((dist - 3) / 14);
            float2 duv = (IN.wpos.xz * _FloorScale + shift) * _DetailScale;
            fixed3 detail = tex2D(_FloorTex, duv).rgb;
            float dl = dot(detail, float3(0.3, 0.55, 0.15)) / max(1e-3, dot(tex2D(_FloorTex, duv * 0.27).rgb, float3(0.3, 0.55, 0.15)));
            floorC *= lerp(1, clamp(dl, 0.55, 1.6), _DetailMix * near * w.y);
            fixed4 bankT = triplanar(_BankTex, IN.wpos, w, _BankScale, 0);
            fixed4 platT = triplanar(_PlateauTex, IN.wpos, w, _PlateauScale, 0);
            fixed3 bankC = bankT.rgb * _BankTint.rgb;
            fixed3 platC = platT.rgb * _PlateauTint.rgb;
            // layers meet along their heights: the raised parts of one push through the other at the seam
            float3 lh = lw + float3(floorT.a, bankT.a, platT.a) * _HeightBlend;
            float3 hb = max(lh - (max(lh.x, max(lh.y, lh.z)) - 0.3), 0);
            lw = lerp(lw, hb / max(1e-4, hb.x + hb.y + hb.z), step(1e-3, _HeightBlend));
            float height = floorT.a * lw.x + bankT.a * lw.y + platT.a * lw.z;
            fixed3 albedo = floorC * lw.x + bankC * lw.y + platC * lw.z;

            // broad light/dark patches so the ground never reads as a repeating tile
            float big = vnoise(IN.wpos.xz * 0.045) * 0.65 + vnoise(IN.wpos.xz * 0.13) * 0.35;
            albedo *= lerp(1.0, 0.65 + 0.7 * big, _DetailStrength);
            o.Albedo = AG_HueShift(albedo, _HueShift, _Saturation);

            // surface relief: each layer's own normal map, plus the close-up floor detail
            float3 nf = triplanarNormal(_FloorNormal, IN.wpos, n, w, _FloorScale, shift);
            if (_FloorVariety > 0)
            {
                // the top projection's relief follows the floor variant patches
                float3 t1 = UnpackNormal(tex2D(_FloorNormal, uvTop));
                float3 t2 = UnpackNormal(tex2D(_FloorNormal2, uvB));
                float3 t3 = UnpackNormal(tex2D(_FloorNormal3, uvC));
                float3 tt = lerp(lerp(t1, t2, wb), t3, wc);
                tt.xy *= _NormalStrength;
                float3 ty = normalize(float3(tt.xy + n.xz, abs(tt.z) * n.y));
                nf = normalize(lerp(nf, ty.xzy, w.y));
            }
            float3 nb = triplanarNormal(_BankNormal, IN.wpos, n, w, _BankScale, 0);
            float3 np = triplanarNormal(_PlateauNormal, IN.wpos, n, w, _PlateauScale, 0);
            float3 nd = UnpackNormal(tex2D(_FloorNormal, duv));
            nf = normalize(nf + float3(nd.x, 0, nd.y) * (_NormalStrength * 0.6 * near * w.y));
            float3 wn = normalize(nf * lw.x + nb * lw.y + np * lw.z);
            float3 T = WorldNormalVector(IN, float3(1, 0, 0));
            float3 B = WorldNormalVector(IN, float3(0, 1, 0));
            float3 N = WorldNormalVector(IN, float3(0, 0, 1));
            o.Normal = normalize(float3(dot(wn, T), dot(wn, B), dot(wn, N)));

            o.Smoothness = (_FloorSmooth * lw.x + _BankSmooth * (lw.y + lw.z)) * lerp(0.6, 1.0, height);
            // crevices: the low parts of the height map are shaded, as in a real rough surface
            o.Occlusion = lerp(0.35, 1.0, c.a) * lerp(1 - _Cavity, 1.0, smoothstep(0.05, 0.7, height));

            // sparse bioluminescent specks on the canyon floor: a small round dot in a few grid cells
            float2 g = IN.wpos.xz * 1.3;
            float2 cell = floor(g);
            float2 jitter = float2(hash21(cell + 3.1), hash21(cell + 9.7)) * 0.5 - 0.25;
            float dotR = length(frac(g) - 0.5 - jitter);
            float speck = step(0.93, hash21(cell)) * (1 - smoothstep(0.02, 0.07, dotR));
            float twinkle = 0.6 + 0.4 * sin(_Time.y * 1.3 + hash21(cell + 7) * 6.28);
            o.Emission = _GlowColor.rgb * speck * twinkle * lw.x * _GlowSpecks * 3;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
