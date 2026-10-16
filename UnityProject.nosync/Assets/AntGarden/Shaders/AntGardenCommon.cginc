#ifndef ANTGARDEN_COMMON_INCLUDED
#define ANTGARDEN_COMMON_INCLUDED

// Rotate the hue of a colour by `deg` degrees (YIQ rotation keeps luminance) and scale its saturation.
// Used to give regions their own palette (blue fungal forest, warm amber) from the same textures.
inline float3 AG_HueShift(float3 c, float deg, float sat)
{
    float y = dot(c, float3(0.299, 0.587, 0.114));
    float i = dot(c, float3(0.596, -0.274, -0.322));
    float q = dot(c, float3(0.211, -0.523, 0.312));
    float a = radians(deg);
    float cs = cos(a), sn = sin(a);
    float i2 = (i * cs - q * sn) * sat;
    float q2 = (i * sn + q * cs) * sat;
    float3 rgb = float3(y + 0.956 * i2 + 0.621 * q2, y - 0.272 * i2 - 0.647 * q2, y - 1.106 * i2 + 1.703 * q2);
    return max(rgb, 0);
}

#endif
