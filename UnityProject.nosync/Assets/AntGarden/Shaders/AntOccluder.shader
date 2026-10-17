// The particle nest's NPC ants (ParticleAnts.cs): their own skinned meshes drawn as a dark, unlit body under
// their points, so nothing behind an ant shows through it, and the gaps between its points are dark, not
// see-through. _ZWrite and _ColorMask 0 make a copy that draws nothing (for the wings: their points are enough).
Shader "AntGarden/AntOccluder"
{
    Properties
    {
        _Color ("Body colour", Color) = (0.02, 0.05, 0.06, 1)
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

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_FOG_COORDS(0)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = _Color;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
