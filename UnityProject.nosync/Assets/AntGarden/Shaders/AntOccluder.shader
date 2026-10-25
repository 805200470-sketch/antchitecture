// The particle nest's NPC ants (ParticleAnts.cs): their own skinned meshes drawn as a dim body under their points,
// so nothing behind an ant shows through it and the gaps between its points show the ant's own colours and shape
// (its texture, dim, lit softly from above: _TexLight), not black stripes. _ZWrite and _ColorMask 0 make a copy
// that draws nothing (for the wings: their points are enough).
Shader "AntGarden/AntOccluder"
{
    Properties
    {
        _Color ("Body colour", Color) = (0.02, 0.05, 0.06, 1)
        _MainTex ("The ant's own texture", 2D) = "black" {}
        _TexLight ("How much of its texture shows", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Write depth", Float) = 1
        _ColorMask ("Colour mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        ZWrite [_ZWrite]
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            sampler2D _MainTex;
            float _TexLight;
            float4 _AntTint;     // global: the colour the ants lean to in this nest (a: how far; ParticleAnts)

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 nrm : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                o.nrm = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = _Color;
                if (_TexLight > 0)
                {
                    float key = saturate(dot(normalize(i.nrm), normalize(float3(0.3, 0.9, 0.25))) * 0.6 + 0.4);
                    float3 t = tex2D(_MainTex, i.uv).rgb;
                    float tl = dot(t, float3(0.3, 0.55, 0.15));
                    t = lerp(t, _AntTint.rgb * (tl / max(1e-3, dot(_AntTint.rgb, float3(0.3, 0.55, 0.15)))), _AntTint.a);
                    c.rgb += t * _TexLight * (0.3 + 0.8 * key * key);
                }
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
