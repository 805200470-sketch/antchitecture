// The particle nest's guiding pulses (LabNest.cs sets the globals): on the floor along the glowing ways, soft
// chevrons of light run out ahead of the ant, two beats and a pause like a heartbeat, their middle a little ahead
// of their sides so they point the way on, their edges wavering like something alive. _TrackField holds, near a
// way, the distance along it to the heart (r; -1: no way here) and how far off its middle (g, signed);
// _TrailPlayerD is the ant's own distance to the heart along the way (-1: off it).
#ifndef ANTGARDEN_CHEVRON
#define ANTGARDEN_CHEVRON

sampler2D _TrackField;
float4 _TrackRect;          // the field's corner (x, z) and size (x, z)
float _TrailPlayerD, _ChevronOn, _ChevronDebug;
float _ChevronSpeed, _ChevronPeriod;   // how fast they run (units/s) and how far apart (units)
float4 _ChevronColor;       // rgb: their colour (linear); a: their brightness

// how much a point at p (with its own random value h) is lit by a passing pulse, 0..1
float Chevron(float3 p, float h)
{
    if (_ChevronOn < 0.5 || _TrailPlayerD < 0) return 0;
    float2 uv = (p.xz - _TrackRect.xy) / _TrackRect.zw;
    if (any(uv < 0) || any(uv > 1)) return 0;
    float2 tf = tex2Dlod(_TrackField, float4(uv, 0, 0)).rg;
    if (_ChevronDebug > 0.5) return tf.x >= 0 ? (_ChevronDebug > 1.5 ? saturate((_TrailPlayerD - tf.x) / 10) : 1) : 0;
    if (tf.x < 0) return 0;
    float k = _TrailPlayerD - tf.x;     // how far ahead of the ant, along the way
    float s = abs(tf.y);                // how far off the way's middle
    float lane = 1 - smoothstep(0.2, 0.85, s);
    // they rise out of the ground a little behind the ant (where the camera sees them) and run on ahead
    float ahead = smoothstep(-3.2, -1.6, k) * (1 - smoothstep(9.0, 15.0, k));
    if (lane * ahead <= 0) return 0;
    float t = _Time.y;
    // wavering edges, a little different for every point
    float wob = 0.18 * sin(tf.x * 1.7 + t * 0.9) + 0.1 * sin(tf.x * 4.3 - t * 1.6) + 0.12 * (h - 0.5);
    // the middle ahead of the sides: a chevron pointing on; in step with the warm specks' own pulses
    float x = k + 1.5 + s * 1.7 + wob;
    float ph = frac((x - t * _ChevronSpeed) / _ChevronPeriod) * _ChevronPeriod;
    float beat = exp(-ph * ph * 5.0) + 0.5 * exp(-(ph - 0.95) * (ph - 0.95) * 5.0);
    // the light runs out of the ground as the pulse passes, a few grains brighter than the rest
    return saturate(beat) * lane * ahead * (0.65 + 0.7 * h);
}

#endif
