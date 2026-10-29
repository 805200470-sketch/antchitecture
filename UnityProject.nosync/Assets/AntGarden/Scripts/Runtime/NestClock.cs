using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// The time the particle nest's own animations run on (the plants' breath, the wind, twinkles, the small life, the
    /// threads' flicker, the pheromones, the floating spores): the game's, or one fixed moment, so every still of a check
    /// (SceneShots) is taken at the same moment and two versions compared show only what changed, not where the
    /// fireflies happened to be. Shaders read it as AG_TIME (Shaders/AGClock.cginc). What answers the ant's own steps
    /// (the light it walks past, the rings from its feet) keeps the game's time: its moments were stamped with it.
    /// </summary>
    public static class NestClock
    {
        static float fixedAt = -1f;

        public static bool IsFixed => fixedAt >= 0f;
        /// <summary>Time.time, or the fixed moment.</summary>
        public static float Now => fixedAt >= 0f ? fixedAt : Time.time;
        /// <summary>Time.timeSinceLevelLoad (the shaders' _Time.y), or the fixed moment.</summary>
        public static float SinceLoad => fixedAt >= 0f ? fixedAt : Time.timeSinceLevelLoad;

        public static void Fix(float t)
        {
            fixedAt = t;
            Shader.SetGlobalVector("_AGClock", new Vector4(1f, t, 0f, 0f));
        }

        public static void Release()
        {
            fixedAt = -1f;
            Shader.SetGlobalVector("_AGClock", Vector4.zero);
        }
    }
}
