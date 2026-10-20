using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// The particle nest: what the player's ant walks past lights up a little and slowly goes back. Keeps where the
    /// ant has just been (a point every so often, with when) for the shaders (Shaders/ParticleSolid.shader: the
    /// plants and the floor's things standing up) and for GlassLamps; anything near one of those points lights up over
    /// a moment after the ant comes by, stays a little and fades over some seconds, so a trail of soft light follows
    /// the ant through the plants and nothing changes in a step.
    /// </summary>
    public class WalkLight : MonoBehaviour
    {
        public float spacing = 0.35f;        // a point every this far the ant walks
        public float radius = 1.4f;          // how near something must be to the ant's way to light up
        public float strength = 0.3f;        // how much it lights up (an antenna's touch is 1)

        public static WalkLight Instance { get; private set; }

        const int N = 16;
        readonly Vector4[] trail = new Vector4[N];
        int next;
        Transform ant;
        Vector3 last;
        bool started;

        public void Init(Transform player)
        {
            ant = player;
            Instance = this;
            for (int i = 0; i < N; i++) trail[i] = new Vector4(0, 0, 0, -1000f);
            Shader.SetGlobalFloat("_WalkLit", strength);
            Shader.SetGlobalFloat("_WalkRadius", radius);
            Shader.SetGlobalVectorArray("_WalkTrail", trail);
        }

        void Update()
        {
            if (ant == null) return;
            Vector3 p = ant.position;
            if (!started || (p - last).sqrMagnitude > spacing * spacing)
            {
                trail[next] = new Vector4(p.x, p.y, p.z, Time.timeSinceLevelLoad);
                next = (next + 1) % N;
                last = p;
                started = true;
            }
            Shader.SetGlobalFloat("_WalkLit", strength);
            Shader.SetGlobalFloat("_WalkRadius", radius);
            Shader.SetGlobalVectorArray("_WalkTrail", trail);
        }

        /// <summary>How lit something at p is by the ant walking past it (0..1), as the shaders reckon it.</summary>
        public float LitAt(Vector3 p)
        {
            float now = Time.timeSinceLevelLoad, r2 = radius * radius, lit = 0;
            for (int i = 0; i < N; i++)
            {
                float age = now - trail[i].w, dx = p.x - trail[i].x, dz = p.z - trail[i].z;
                lit = Mathf.Max(lit, Mathf.Exp(-(dx * dx + dz * dz) / r2) * Mathf.SmoothStep(0, 1, age / 0.8f) * (1 - Mathf.SmoothStep(0, 1, (age - 2.5f) / 5.5f)));
            }
            return lit;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Shader.SetGlobalFloat("_WalkLit", 0);
        }
    }
}
