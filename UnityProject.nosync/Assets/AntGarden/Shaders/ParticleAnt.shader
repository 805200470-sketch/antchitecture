// The particle nest's NPC ants (ParticleAnts.cs): each ant drawn as points spread over its surface
// (AssetBuilder.SaveAntPoints), skinned here with its own skeleton: every point, and its normal, follows the two
// bones that move it most (_Skin: bone × bindpose matrices, this ant's from _SkinBase on).
// Each point is a small opaque flake facing the camera (a disc with a slightly irregular outline and a darker
// edge, as the plants' splats are), as many and as large as cover the ant (ParticleAnts sizes them from its
// surface), so the ant is a body made of points: near points hide far ones, and the ant's own dark mesh under
// them (AntOccluder) hides whatever lies behind it. Flakes differ a little in size; the larger ones only where
// the surface faces the camera, so the outline stays crisp. Colours: the ant's texture (turned by its own hue,
// _Hue / _Sat; light and dark a little exaggerated, _Contrast), with grains of other colours among them:
// lighter and deeper ones of its own family, now and then a gold or rose fleck. Its eyes: dark, faceted, a few
// facets catching the light.
// Lit by a key light from above the camera, with a thin bright rim along the outline. They shimmer a little.
// Far ants show fewer points (_Count; the last ones shrink away), and an ant in the camera's way dissolves point
// by point (_Fade) instead of vanishing at once.
// The fourth colour byte says what the point is (0 shell, 1 eye, 2 wing: a third of its points, small, pale and a
// little iridescent, so it reads as a thin membrane); look, how much it is one of the ant's markings (its low byte) and
// how much one of its pits (the next; Tools/ants/ant_marks.py, ParticleAnts.LoadMarks).
// Colours in the nests (11-04, ParticleAnts.Colour): an ant's own colours folded into one hue (_Hue; Fold: its own spread
// of hues narrowed, _Keep), quiet (_Sat) and dark (_BodyGain), with no grains or glints of other hues: an NPC's into its
// nest's, the player's ant's into a quiet blue grey, the same in every nest; its markings glowing in one of its nest's own
// lights (_MarkColor, w: how bright), whatever the light on it, and its pits faintly in the same light (_PitGlow), so
// the nest's colour is in its shell all over, not in its bands alone: the player's ant's only (an NPC's w and _PitGlow
// are 0: no NPC glows, the user's decision, 11-04; its pits are its nest's colour a little more vivid, _PitSat); no
// flake brighter than _MaxLight (never white).
Shader "AntGarden/ParticleAnt"
{
    Properties
    {
        _Size ("Point radius (units)", Float) = 0.0042
        _Gain ("Brightness", Float) = 1.3
        _MinPixels ("Smallest point (pixels)", Float) = 0.7
        _Pale ("How far colours go toward a pale glow", Range(0, 1)) = 0.12
        _Contrast ("How much the texture's light and dark are exaggerated", Float) = 1.5
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        ZWrite On
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "AntGardenCommon.cginc"
            #include "AGClock.cginc"

            struct AntPoint { float3 pos; float3 nrm; uint col; uint slots; float w; uint look; };
            StructuredBuffer<AntPoint> _AntPoints;
            StructuredBuffer<float4x4> _Skin;
            float _SkinBase, _Size, _Gain, _MinPixels, _Pale, _Hue, _Sat, _Contrast;
            float _BodyGain;         // how light its body is (dark: its markings carry its light)
            float4 _MarkColor;       // its markings' light (linear; the nest's) and how bright (w)
            float _MeanHue, _Keep;   // an NPC's kind's own mean hue (degrees round the YIQ plane), and how much of its spread round it is kept
            float _PitGlow, _MaxLight;   // how brightly its pits glow, of its markings; and the most light a flake gives
            float _PitSat;               // an NPC's pits: how much more vivid than its shell (of its strength), not glowing
            float _MinChroma;            // none of its shell less vivid than this, of its lightness, in its hue (no grey)
            float _Count;   // how many of its points are shown (far ants fewer): the last ones shrink away
            float4 _AntRimColor, _AntPaleColor;   // global (ParticleAnts, from the nest's theme): its rim light, and the pale it leans to
            float4 _AntTint;                       // global: the colour its points leaned to (no longer read here, 11-04: _Hue, _Sat)
            float _Fade;    // how much of the ant is there: it dissolves point by point (out of the camera's way)

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 col : COLOR;
                float shape : TEXCOORD2;    // a per-point seed for its outline
                UNITY_FOG_COORDS(1)
            };

            static const float2 corners[3] = { float2(0, 2), float2(-1.7320508, -1), float2(1.7320508, -1) };

            float Hash(uint n)
            {
                n = n * 747796405u + 2891336453u;
                n = ((n >> ((n >> 28u) + 4u)) ^ n) * 277803737u;
                n = (n >> 22u) ^ n;
                return (n & 0xFFFFFF) / 16777216.0;
            }

            // an NPC's own colours folded into its nest's band (11-04): each keeps keep of how far its hue lies from its kind's
            // mean, turned to the nest's hue, and sat of its strength (a two-coloured shell, Ant_001's teal and yellow green,
            // was turned whole and its yellow green came out olive in a red or gold nest)
            // (and none less vivid than minC of its lightness, in the nest's own hue: a shell's near white streaks, Ant_002's,
            // stayed grey in every nest, round 5, "不要是灰色的")
            float3 Fold(float3 c, float fromDeg, float toDeg, float keep, float sat, float minC)
            {
                float y = dot(c, float3(0.299, 0.587, 0.114));
                float i = dot(c, float3(0.596, -0.274, -0.322));
                float q = dot(c, float3(0.211, -0.523, 0.312));
                float ch0 = sqrt(i * i + q * q), ch = max(ch0 * sat, minC * max(y, 0));
                if (ch < 1e-6) return float3(1, 1, 1) * max(y, 0);
                float d = ch0 > 1e-6 ? atan2(q, i) - radians(fromDeg) : 0;   // (a grey has no hue of its own: none to keep)
                float a = radians(toDeg) + atan2(sin(d), cos(d)) * keep * saturate(ch0 / max(1e-4, 0.08 * y));   // (and a near grey's is noise)
                float i2 = ch * cos(a), q2 = ch * sin(a);
                return max(float3(y + 0.956 * i2 + 0.621 * q2, y - 0.272 * i2 - 0.647 * q2, y - 1.106 * i2 + 1.703 * q2), 0);
            }

            v2f vert(uint id : SV_VertexID)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                uint i = id / 3;
                AntPoint p = _AntPoints[i];
                uint b = (uint)_SkinBase;
                float4x4 m0 = _Skin[b + (p.slots & 0xFFFF)], m1 = _Skin[b + (p.slots >> 16)];
                float3 pos = lerp(mul(m1, float4(p.pos, 1)).xyz, mul(m0, float4(p.pos, 1)).xyz, p.w);
                float3 nrm = normalize(lerp(mul((float3x3)m1, p.nrm), mul((float3x3)m0, p.nrm), p.w));
                float h = Hash(i), hs = Hash(i + 7919u), hc = Hash(i + 104729u);
                float t = AG_TIME;
                pos += 0.002 * float3(sin(t * (1.1 + h * 1.3) + h * 17.0), sin(t * (0.9 + h * 1.1) + h * 29.0), sin(t * (1.0 + h * 1.7) + h * 41.0));
                float3 V = normalize(_WorldSpaceCameraPos - pos);
                float3 n = dot(nrm, V) < 0 ? -nrm : nrm;
                float facing = saturate(dot(n, V));
                uint kind = (p.col >> 24) & 255;
                float mark = (p.look & 255) / 255.0, pit = ((p.look >> 8) & 255) / 255.0;   // one of its markings, one of its pits
                // a wing is a thin membrane: a third of its points, small and pale
                if (kind == 2 && hc > 0.33) { o.pos = float4(0, 0, 0, 1); return o; }
                // dissolving, and far: fewer points, the last ones shrinking away
                float lodK = saturate((_Count - i) / max(1, _Count * 0.15));
                if (Hash(i + 31337u) > _Fade || lodK <= 0) { o.pos = float4(0, 0, 0, 1); return o; }
                // flakes a little different in size, the larger only where the surface faces the camera
                float grow = kind == 2 ? 0.55 : hs < 0.08 ? 1.3 : 0.8 + 0.4 * h;
                grow = lerp(min(grow, 0.95), grow, smoothstep(0.35, 0.75, facing));
                float dist = distance(pos, _WorldSpaceCameraPos);
                float pixel = 2 * dist / (unity_CameraProjection._m11 * _ScreenParams.y);
                float want = _Size * grow * lodK;
                float size = max(want, pixel * _MinPixels);
                float2 c = corners[id % 3];
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                o.pos = mul(UNITY_MATRIX_VP, float4(pos + n * 0.0012 + (right * c.x + up * c.y) * size, 1));
                o.uv = c;
                // the texture's colour, its light and dark a little exaggerated
                float3 rgb = GammaToLinearSpace(float3(p.col & 255, (p.col >> 8) & 255, (p.col >> 16) & 255) / 255.0);
                // (turned into its hue and made quiet here, not leaned to _AntTint as every ant was: 11-04)
                rgb = Fold(rgb, _MeanHue, _MeanHue + _Hue, _Keep, _Sat * (1 + _PitSat * pit), _MinChroma);
                float lum = max(1e-3, dot(rgb, float3(0.3, 0.55, 0.15)));
                rgb *= pow(lum / 0.18, _Contrast - 1);
                rgb = lerp(rgb, _AntPaleColor.rgb * lum * 2.2, _Pale);
                // grains of other colours: lighter and deeper ones of its own family (no longer a gold or rose fleck now and
                // then, nor a lighter one lifted toward teal: nothing off a nest's palette on a living thing, 11-04)
                if (kind == 0)
                {
                    float3 deep = AG_HueShift(rgb, 6, 1.15) * 0.6;
                    rgb = hc < 0.12 ? rgb * 1.45 + _AntPaleColor.rgb * lum * 0.4
                        : hc < 0.2 ? deep
                        : rgb;
                }
                // lit: a key light from above the camera (wrapped), and a thin bright rim along the outline
                float3 L = normalize(UNITY_MATRIX_V[1].xyz * 0.9 - UNITY_MATRIX_V[2].xyz * 0.35 + UNITY_MATRIX_V[0].xyz * 0.25);
                float key = saturate(dot(n, L) * 0.6 + 0.4);
                key *= key;
                float rim = pow(1 - facing, 4);
                float3 lit = (rgb * (0.45 + 1.4 * key) + _AntRimColor.rgb * rim * 0.5 * (0.3 + lum * 2)) * _BodyGain;
                float twinkle = 0.9 + 0.1 * sin(t * (0.6 + h) + h * 23);
                // its markings: in its nest's light, glowing whatever the light on it (a little more where lit)
                if (kind == 0 && mark > 0.02 && _MarkColor.w > 0) lit = lerp(lit, _MarkColor.rgb * _MarkColor.w * (0.8 + 0.35 * key), saturate(mark * 1.2));
                // its pits, the small holes all over its shell: a faint glow in the same light, strongest beside its markings
                if (kind == 0 && pit > 0.02 && _PitGlow > 0) lit = lerp(lit, _MarkColor.rgb * _MarkColor.w * _PitGlow * (0.8 + 0.35 * key), saturate(pit * 1.4));
                // the wing's points: pale, a little iridescent (toward its markings' light: no violet in a warm nest)
                if (kind == 2) lit = lerp(_AntRimColor.rgb * 0.92, lerp(_AntPaleColor.rgb, _MarkColor.rgb, 0.35), hc * 3) * (0.25 + 0.5 * key) + _AntRimColor.rgb * rim * 0.4;
                // an eye: dark facets, a few of them catching the light (its nest's pale; they were a grey blue, 11-04)
                if (kind == 1) lit = hc < 0.2 ? _AntPaleColor.rgb * 0.5 * (0.5 + 0.8 * key) : lit * (hc < 0.5 ? 0.6 : 0.3);
                o.col = lit * _Gain * twinkle;
                // never blown out: no flake brighter than _MaxLight, its hue kept (11-04)
                o.col *= min(1, _MaxLight / max(1e-4, max(o.col.r, max(o.col.g, o.col.b))));
                o.shape = h;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // a flake with a slightly irregular outline (a disc pushed a little out of round, cheaply: no angle per
                // pixel, there are many of them on an ant near the camera), its edge a little darker, so every point
                // reads as one
                float2 d = float2(cos(i.shape * 31), sin(i.shape * 31));
                float r2 = dot(i.uv, i.uv) * (1 + 0.14 * dot(i.uv, d) * (1 - i.shape));
                clip(1 - r2);
                fixed4 col = fixed4(i.col * (1 - 0.18 * smoothstep(0.4, 1.0, r2)), 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
