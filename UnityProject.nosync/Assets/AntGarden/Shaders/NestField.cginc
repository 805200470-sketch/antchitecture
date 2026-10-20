// The particle nest's living field, shared by its point shaders (ParticleSolid, ParticleCarpet, ParticleLook): the
// floor's height under any point (_FloorHeightMap, baked by ParticleLook), a slow wind that sways what stands up from
// the floor (more the higher it stands), the ant's own nearness parting what it walks through, rings spreading from its
// steps over the floor (_Ripples, LabNest), and the pools of light the guiding threads cast on the floor below them
// (_Beacons, GuideThreads), and the slow breath of everything that glows (NestBreath). Everything here is small and
// slow: the ant stays the thing that moves.
#ifndef ANTGARDEN_NEST_FIELD
#define ANTGARDEN_NEST_FIELD

sampler2D _FloorHeightMap;
float4 _FloorHeightRect;    // the height map's corner (x, z) and size (x, z); size 0: no map
float4 _NestWind;           // x: how far things sway (units at 3 units up), y: how fast the gusts run, z: their spacing
float4 _Ripples[8];         // x, z: where a step fell; w: how strong; y: when (time)
float4 _Beacons[4];         // x, y, z: where a thread of light meets the floor; w: how bright
float4 _BeaconColor;        // rgb: its pool's colour (linear)
float4 _GrainGlow;          // rgb: the colour of the floor's few glowing grains (linear); a: how bright (0: none)

float NestHash2(float2 c) { return frac(sin(dot(c, float2(127.1, 311.7))) * 43758.5453); }

// smooth value noise over the plane (0..1)
float NestNoise2(float2 p)
{
    float2 i = floor(p), f = frac(p), u = f * f * (3 - 2 * f);
    return lerp(lerp(NestHash2(i), NestHash2(i + float2(1, 0)), u.x), lerp(NestHash2(i + float2(0, 1)), NestHash2(i + float2(1, 1)), u.x), u.y);
}

// the slow breath of what glows (0..1): one pace everywhere (about eleven seconds a breath), its phase drifting
// smoothly over the nest, so a plant breathes as one and its neighbours a little out of step, like a wave passing
// (GlassLamps.cs reckons the same on the CPU)
float NestBreath(float3 p)
{
    return 0.5 + 0.5 * sin(_Time.y * 0.55 + NestNoise2(p.xz * 0.35) * 6.2831853);
}

// the floor's height under xz (the nearest sample; -1000 where there is no map)
float NestFloor(float2 xz)
{
    if (_FloorHeightRect.z <= 0) return -1000;
    float2 uv = (xz - _FloorHeightRect.xy) / _FloorHeightRect.zw;
    if (any(uv < 0) || any(uv > 1)) return -1000;
    return tex2Dlod(_FloorHeightMap, float4(uv, 0, 0)).r;
}

// how a point standing above the floor sways in the wind (slow gusts running across the nest, a little turn of
// direction in each); above: its height over the floor
float3 NestSway(float3 p, float above, float seed)
{
    if (_NestWind.x <= 0 || above <= 0.05) return 0;
    float t = _Time.y;
    float2 dir = normalize(float2(0.8, 0.6));
    float phase = dot(p.xz, dir) / max(0.5, _NestWind.z) - t * _NestWind.y;
    float gust = 0.6 * sin(phase) + 0.3 * sin(phase * 2.3 + 1.7) + 0.1 * sin(t * 1.7 + seed * 6.28);
    float lift = pow(saturate(above / 3), 1.4) * 3;   // the higher, the more
    float2 side = float2(-dir.y, dir.x) * 0.35 * sin(phase * 0.7 + seed * 3.0);
    return float3((dir + side) * gust, 0).xzy * (_NestWind.x * lift);
}

// the ant parting what it walks through: low things near it lean away from it
float3 NestPart(float3 p, float above, float2 ant)
{
    float2 d = p.xz - ant;
    float r = length(d);
    if (r > 1.3 || above > 1.2) return 0;
    float push = (1 - smoothstep(0.25, 1.3, r)) * (1 - smoothstep(0.4, 1.2, above)) * 0.09;
    return float3(d / max(r, 1e-3) * push, 0).xzy + float3(0, -push * 0.3, 0);
}

// rings spreading from the ant's steps: how much a point at xz lifts and brightens now (0..1)
float NestRipple(float2 xz)
{
    float t = _Time.y, sum = 0;
    for (int k = 0; k < 8; k++)
    {
        float4 R = _Ripples[k];
        if (R.w <= 0) continue;
        float age = t - R.y;
        if (age < 0 || age > 2.6) continue;
        // a soft, uneven ring (never a perfect circle), widening as it slows
        float2 off = xz - R.xz;
        float ang = atan2(off.y, off.x), seed = frac(R.y * 0.137) * 6.28;
        float ring = age * 1.1 * (1 + 0.16 * sin(ang * 3 + seed) + 0.09 * sin(ang * 5 - seed * 1.7));
        float d = length(off) - ring, width = 0.03 + 0.05 * age;
        sum += R.w * exp(-d * d / width) * (1 - age / 2.6) * (1 - smoothstep(1.6, 2.8, ring));
    }
    return saturate(sum);
}

// the pools of light on the floor below the guiding threads: how lit a point is (0..)
float NestBeacon(float3 p)
{
    float sum = 0;
    for (int k = 0; k < 4; k++)
    {
        float4 B = _Beacons[k];
        if (B.w <= 0) continue;
        float2 d = p.xz - B.xz;
        float r2 = dot(d, d);
        sum += B.w * (exp(-r2 / 0.45) + 0.35 * exp(-r2 / 3.0)) * (1 - smoothstep(0.5, 3.5, abs(p.y - B.y)));
    }
    return sum;
}

#endif
