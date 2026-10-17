using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gamejam2026
{
    // Presentation only: the existing DemoGame remains the sole owner of game rules.
    public class DemoAtmosphere : MonoBehaviour
    {
        public Light[] victoryLights;
        public Light[] navigationLights;
        DemoGame game;
        Volume volume;
        VolumeProfile profile;
        Vignette vignette;
        FilmGrain grain;
        ChromaticAberration aberration;
        ColorAdjustments colors;
        bool ended;
        bool victory;
        float ending;
        float shock;
        float nextMono = 2f;
        float monoUntil;
        [Range(0.7f, 1f)] public float monoDanger = 0.85f;
        public float monoSeconds = 0.07f;
        public float monoGap = 2.2f;
        public float Blackout => ended && !victory ? Mathf.Clamp01(ending / 1.8f) : 0f;

        public void Initialize(DemoGame owner)
        {
            game = owner;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.003f, 0.0045f, 0.007f);
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.004f, 0.006f, 0.009f);
            foreach (var light in game.atmosphereLights) if (light) light.intensity = 0f;
            foreach (var light in FindObjectsByType<Light>())
                if (light.type == LightType.Directional) light.intensity = 0f;
            if (navigationLights != null)
                foreach (var light in navigationLights)
                    if (light) { light.intensity = 0.08f; light.range = 2.8f; }
            if (victoryLights != null) foreach (var light in victoryLights) if (light) light.intensity = 0f;
            foreach (var player in game.players)
            {
                var cameraData = player.view.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                cameraData.volumeTrigger = player.view.transform;
                player.view.backgroundColor = new Color(0.004f, 0.006f, 0.012f);
                player.view.clearFlags = CameraClearFlags.SolidColor;
            }
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;
            vignette = profile.Add<Vignette>(true);
            vignette.smoothness.Override(0.55f);
            grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            aberration = profile.Add<ChromaticAberration>(true);
            colors = profile.Add<ColorAdjustments>(true);
        }

        public void WrongOrder() { shock = 1f; }

        public void Finish(bool won)
        {
            ended = true;
            victory = won;
            ending = 0f;
            if (!won)
                foreach (var candle in game.candles)
                {
                    if (!candle) continue;
                    if (candle.candleLight) candle.candleLight.enabled = false;
                    foreach (var renderer in candle.GetComponentsInChildren<Renderer>())
                        if (renderer.name.StartsWith("Llama")) renderer.enabled = false;
                }
        }

        void Update()
        {
            if (!game || !profile) return;
            if (ended) ending += Time.unscaledDeltaTime;
            // Freeze presentation while paused, but let the ending finish at timeScale zero.
            float dt = ended ? Time.unscaledDeltaTime : Time.deltaTime;
            shock = Mathf.MoveTowards(shock, 0f, dt * 2.5f);
            float danger = game.Danger / 100f;
            float disturbance = game.Playing ? game.Disturbance : 0f;
            float relief = victory ? Mathf.Clamp01(ending / 1.5f) : 0f;
            float critical = Mathf.InverseLerp(0.75f, 1f, danger);
            RenderSettings.fogDensity = Mathf.Lerp(Mathf.Lerp(0.03f, 0.065f, danger), 0.012f, relief);
            vignette.intensity.Override(Mathf.Lerp(0.22f + danger * 0.18f + critical * 0.08f + shock * 0.13f + disturbance * 0.035f, 0.12f, relief));
            grain.intensity.Override(Mathf.Lerp(0.12f + danger * 0.22f, 0.04f, relief));
            aberration.intensity.Override(Mathf.Lerp(0.015f + danger * 0.055f + shock * 0.06f, 0f, relief));
            bool mono = false;
            if (game.Playing && danger >= monoDanger)
            {
                if (Time.time >= nextMono)
                {
                    nextMono = Time.time + Mathf.Max(0.4f, monoGap);
                    monoUntil = Time.time + Mathf.Clamp(monoSeconds, 0.03f, 0.15f);
                }
                mono = Time.time < monoUntil;
            }
            colors.saturation.Override(mono ? -100f : 0f);
            colors.postExposure.Override(-Blackout * 8f - critical * 0.22f);
            if (victory && victoryLights != null)
            {
                foreach (var light in victoryLights) if (light) light.intensity = relief * 4f;
                RenderSettings.ambientLight = Color.Lerp(new Color(0.006f, 0.008f, 0.012f), Color.white * 0.16f, relief);
            }
            foreach (var candle in game.candles)
            {
                // Never enable a light here: the spare candle and a lost game's flame stay off.
                if (!candle || !candle.gameObject.activeInHierarchy || !candle.candleLight || !candle.candleLight.enabled) continue;
                float noise = Mathf.PerlinNoise(Time.time * 3.2f, 7.3f);
                candle.candleLight.intensity = Mathf.Lerp(4.4f, 5.5f, noise);
                candle.candleLight.range = 5.8f;
                candle.candleLight.color = new Color(1f, 0.65f, 0.30f);
            }
        }

        void OnDestroy()
        {
            if (profile) Destroy(profile);
            if (volume) Destroy(volume);
        }
    }
}
