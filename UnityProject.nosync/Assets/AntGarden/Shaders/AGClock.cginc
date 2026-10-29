// The particle nest's clock for what moves by itself (breath, wind, twinkles, small life, chevrons, rings): the game's
// time, or one fixed moment (NestClock.cs; SceneShots: every still at the same moment). What answers the ant's own steps
// (the walk light, the ripples) keeps _Time.y: its moments were stamped with the game's time.
#ifndef ANTGARDEN_CLOCK
#define ANTGARDEN_CLOCK
float4 _AGClock;   // x > 0.5: fixed at y
#define AG_TIME (_AGClock.x > 0.5 ? _AGClock.y : _Time.y)
#endif
