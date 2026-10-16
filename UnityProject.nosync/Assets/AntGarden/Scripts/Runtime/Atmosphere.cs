using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace AntGarden
{
    /// <summary>
    /// The night mood: gradient night sky, deep-teal exponential fog, soft moonlight with shadows,
    /// a cool rim light that follows the camera, bloom + ACES grading, and drifting glowing spores.
    /// </summary>
    public static class Atmosphere
    {
        /// <summary>Set up sky, fog, lights and post effects; returns the spore particle system.</summary>
        public static ParticleSystem Apply(Camera cam, RuntimeRefs refs, AtmosphereSettings a, QualityPreset q)
        {
            Material sky = refs.sky != null ? new Material(refs.sky) : null; // an instance: biomes recolour it
            if (sky != null) RenderSettings.skybox = sky;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = a.fogColor;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 900f;
            cam.allowHDR = true;
            cam.allowMSAA = false;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = a.fogColor;
            RenderSettings.fogDensity = a.fogDensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = a.ambientSky;
            RenderSettings.ambientEquatorColor = a.ambientEquator;
            RenderSettings.ambientGroundColor = a.ambientGround;
            RenderSettings.reflectionIntensity = 0.35f;

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = a.moonColor;
            moon.intensity = a.moonIntensity;
            moon.transform.rotation = Quaternion.Euler(a.moonEuler);
            moon.shadows = q.shadowDistance > 0 ? LightShadows.Soft : LightShadows.None;
            moon.shadowStrength = 0.75f;
            QualitySettings.shadowDistance = q.shadowDistance;
            QualitySettings.shadowCascades = 2;
            RenderSettings.sun = moon;

            // a cool light from behind the camera that catches silhouettes (follows the camera)
            var rim = new GameObject("RimLight").AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = a.rimLightColor;
            rim.intensity = a.rimLightIntensity;
            rim.shadows = LightShadows.None;
            rim.transform.SetParent(cam.transform, false);
            rim.transform.localRotation = Quaternion.Euler(15, 180, 0);

            SetupPost(cam, refs, a, q);
            var ctl = cam.gameObject.AddComponent<AtmosphereController>();
            ctl.moon = moon;
            ctl.rim = rim;
            ctl.sky = sky;
            ctl.fogDensity = a.fogDensity;
            ctl.tunnelAmbient = a.tunnelAmbient;
            ctl.tunnelMoon = a.tunnelMoon;
            ctl.tunnelExposure = a.tunnelExposure;
            ctl.tunnelRim = a.tunnelRim;
            ctl.tunnelAntDim = a.tunnelAntDim;
            ctl.probe = MakeSkyProbe();
            return MakeSpores(cam, refs, a);
        }

        /// <summary>
        /// A realtime probe that captures only the sky, so glossy ground reflects the current biome's sky
        /// (the default reflection is never rebaked at runtime and would stay blue in the amber nest).
        /// </summary>
        static ReflectionProbe MakeSkyProbe()
        {
            QualitySettings.realtimeReflectionProbes = true;
            var probe = new GameObject("SkyReflection").AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.cullingMask = 0; // the sky alone
            probe.resolution = 32;
            probe.hdr = true;
            probe.size = new Vector3(20000f, 4000f, 20000f);
            return probe;
        }

        static void SetupPost(Camera cam, RuntimeRefs refs, AtmosphereSettings a, QualityPreset q)
        {
            if (refs.postResources == null || refs.postProfile == null) return;
            cam.gameObject.layer = 0;
            var layer = cam.gameObject.AddComponent<PostProcessLayer>();
            layer.Init(refs.postResources);
            layer.volumeLayer = 1; // Default layer
            layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
            var volume = new GameObject("PostVolume").AddComponent<PostProcessVolume>();
            volume.isGlobal = true;
            volume.priority = 1;
            volume.profile = Object.Instantiate(refs.postProfile);
            if (volume.profile.TryGetSettings(out Bloom bloom))
            {
                bloom.enabled.Override(q.bloom);
                bloom.intensity.Override(a.bloomIntensity);
                bloom.threshold.Override(a.bloomThreshold);
            }
            if (volume.profile.TryGetSettings(out ColorGrading grading))
                grading.postExposure.Override(a.exposure);
            if (volume.profile.TryGetSettings(out AmbientOcclusion ao))
                ao.enabled.Override(q.ao);
        }

        static ParticleSystem MakeSpores(Camera cam, RuntimeRefs refs, AtmosphereSettings a)
        {
            if (refs.spores == null || a.sporeCount <= 0) return null;
            var go = new GameObject("Spores");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.95f, 1f, 0.6f), new Color(1f, 0.9f, 0.65f, 0.6f));
            main.maxParticles = a.sporeCount;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.004f;
            var emission = ps.emission;
            emission.rateOverTime = a.sporeCount / 12f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 12f, 36f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.1f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = refs.spores;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        /// <summary>Keep the spore volume centred on the camera (call every frame).</summary>
        public static void Follow(ParticleSystem spores, Camera cam)
        {
            if (spores == null) return;
            spores.transform.position = cam.transform.position + cam.transform.forward * 8f;
        }
    }

    /// <summary>
    /// Blends fog, ambient light, moonlight and sky colours towards the biome of the region the player is
    /// in, so jumping from the blue fungal forest to the amber nest changes the whole mood over a few seconds.
    /// Under a tunnel roof it is a little darker: the ambient light, the moon and the silhouette light are turned
    /// down and the exposure lowered a touch, easing in and out over about a second as the ant walks in or out.
    /// </summary>
    public class AtmosphereController : MonoBehaviour
    {
        public Light moon, rim;
        public Material sky;
        public ReflectionProbe probe;
        public float fogDensity;
        public float blendTime = 2.5f;
        public float tunnelAmbient = 0.9f, tunnelMoon = 0.8f, tunnelRim = 0.9f, tunnelExposure = -0.05f, tunnelAntDim = 0.2f;
        Biome target;
        float elapsed, probeTimer;
        bool settled = true;
        // the biome's ambient light and the moon's own intensity, before the tunnel's shade
        Color skyAmbient, equatorAmbient, groundAmbient;
        float moonIntensity, rimIntensity, shade, shadeTarget, exposureApplied;
        ColorGrading grading;
        bool ready;

        void Ready()
        {
            if (ready) return;
            ready = true;
            skyAmbient = RenderSettings.ambientSkyColor;
            equatorAmbient = RenderSettings.ambientEquatorColor;
            groundAmbient = RenderSettings.ambientGroundColor;
            moonIntensity = moon != null ? moon.intensity : 1f;
            rimIntensity = rim != null ? rim.intensity : 0f;
            var volume = FindObjectOfType<PostProcessVolume>();
            if (volume != null && volume.profile != null) volume.profile.TryGetSettings(out grading);
        }

        /// <summary>1 under a tunnel roof, 0 under the open sky.</summary>
        public void SetShade(float target) => shadeTarget = Mathf.Clamp01(target);

        public void SetBiome(Biome b, bool instant)
        {
            Ready();
            target = b;
            elapsed = 0;
            settled = false;
            if (instant)
            {
                Blend(1f);
                Capture();
            }
        }

        void Update()
        {
            Ready();
            if (Mathf.Abs(shade - shadeTarget) > 1e-3f)
            {
                shade = Mathf.MoveTowards(shade, shadeTarget, Time.deltaTime / 1.2f);
                ApplyShade();
            }
            if (target == null || settled) return;
            Blend(1 - Mathf.Exp(-Time.deltaTime * 3f / blendTime));
            elapsed += Time.deltaTime;
            probeTimer -= Time.deltaTime;
            // re-capture the sky for reflections a few times a second while blending, and once when done
            if (elapsed > blendTime * 2f)
            {
                Blend(1f);
                Capture();
                settled = true;
            }
            else if (probeTimer <= 0) Capture();
        }

        void Capture()
        {
            probeTimer = 0.3f;
            if (probe != null) probe.RenderProbe();
        }

        void Blend(float k)
        {
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, target.fogColor, k);
            skyAmbient = Color.Lerp(skyAmbient, target.ambientSky, k);
            equatorAmbient = Color.Lerp(equatorAmbient, target.ambientEquator, k);
            groundAmbient = Color.Lerp(groundAmbient, target.ambientGround, k);
            ApplyShade();
            if (moon != null) moon.color = Color.Lerp(moon.color, target.moonColor, k);
            if (rim != null) rim.color = Color.Lerp(rim.color, target.rimColor, k);
            if (sky != null)
            {
                sky.SetColor("_TopColor", Color.Lerp(sky.GetColor("_TopColor"), target.skyTop, k));
                sky.SetColor("_HorizonColor", Color.Lerp(sky.GetColor("_HorizonColor"), target.skyHorizon, k));
            }
        }

        void ApplyShade()
        {
            float a = Mathf.Lerp(1, tunnelAmbient, shade);
            RenderSettings.ambientSkyColor = skyAmbient * a;
            RenderSettings.ambientEquatorColor = equatorAmbient * a;
            RenderSettings.ambientGroundColor = groundAmbient * a;
            if (moon != null) moon.intensity = moonIntensity * Mathf.Lerp(1, tunnelMoon, shade);
            if (rim != null) rim.intensity = rimIntensity * Mathf.Lerp(1, tunnelRim, shade);
            Shader.SetGlobalFloat("_AntDim", tunnelAntDim * shade);   // the ants' own glow, in keeping with the dark
            if (grading != null)
            {
                // relative to whatever else has set it (check views lift it for a moment)
                float e = tunnelExposure * shade;
                grading.postExposure.value += e - exposureApplied;
                exposureApplied = e;
            }
        }
    }
}
