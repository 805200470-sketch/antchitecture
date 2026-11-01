using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// In a headset, against motion sickness (the user, 11-01: "目前有一些眩晕感"): while the ant crawls, and the view with
    /// it though the player stands still, the edges of the view darken, so less of the world slides past the corners of
    /// the eyes, where sliding sickens most; standing, the whole view is clear. A sphere round the eyes
    /// (Resources/AntGarden/VR/VRVignette.shader), dark by how far from straight ahead each pixel looks, so it sits the
    /// same in both eyes whatever its size; drawn over the world, after the screen's text (which stays inside the clear
    /// middle).
    /// </summary>
    public class VRComfort : MonoBehaviour
    {
        public float clearWithin = 38f;     // degrees from straight ahead: always clear
        public float darkBeyond = 62f;      // and as dark as the crawl makes it from here out
        float strength, now;
        AntController ant;
        Material mat;
        Renderer sphere;

        /// <summary>The dark edges for the player's ant; darkness: how dark they grow at a full crawl (0 to 1).</summary>
        public void Init(AntController player, float darkness)
        {
            var shader = Resources.Load<Shader>("AntGarden/VR/VRVignette");
            if (shader == null)
            {
                Debug.LogWarning("[AntGarden] VR: the comfort shader is missing; the view's edges stay clear");
                enabled = false;
                return;
            }
            ant = player;
            strength = Mathf.Clamp01(darkness);
            mat = new Material(shader);
            mat.SetFloat("_Clear", Mathf.Cos(clearWithin * Mathf.Deg2Rad));
            mat.SetFloat("_Dark", Mathf.Cos(darkBeyond * Mathf.Deg2Rad));
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "VR Comfort";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 4f;   // well past the near plane (its size is never seen)
            sphere = go.GetComponent<Renderer>();
            sphere.sharedMaterial = mat;
            sphere.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sphere.receiveShadows = false;
            sphere.enabled = false;
        }

        void LateUpdate()
        {
            if (mat == null || ant == null) return;
            float crawl = Mathf.Clamp01(Mathf.Abs(ant.speed) / Mathf.Max(0.05f, ant.cfg.crawlSpeed));
            float want = strength * Mathf.SmoothStep(0f, 1f, crawl);
            // darker quickly as the ant sets off, clearing a little more slowly as it stops
            now = Mathf.MoveTowards(now, want, Time.deltaTime * (want > now ? 2.5f : 1.2f));
            sphere.enabled = now > 0.01f;
            mat.SetFloat("_Strength", now);
        }
    }
}
