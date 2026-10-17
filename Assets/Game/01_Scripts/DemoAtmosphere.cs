using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gamejam2026
{
    // Presentation only: the existing DemoGame remains the sole owner of game rules.
    public class DemoAtmosphere : MonoBehaviour
    {
        public Light[] victoryLights;
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
        public float Blackout => ended && !victory ? Mathf.Clamp01(ending / 1.8f) : 0f;

        public void Initialize(DemoGame owner)
        {
            game = owner;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.008f, 0.011f, 0.018f);
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.004f, 0.006f, 0.009f);
            foreach (var light in game.atmosphereLights) if (light) light.intensity = 0f;
            if (victoryLights != null) foreach (var light in victoryLights) if (light) light.intensity = 0f;
            foreach (var player in game.players)
            {
                var cameraData = player.view.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                cameraData.volumeTrigger = player.view.transform;
                player.view.backgroundColor = new Color(0.004f, 0.006f, 0.012f);
                player.view.clearFlags = CameraClearFlags.Skybox;
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
            RenderSettings.fogDensity = Mathf.Lerp(Mathf.Lerp(0.012f, 0.025f, danger), 0.008f, relief);
            vignette.intensity.Override(Mathf.Lerp(0.22f + danger * 0.18f + shock * 0.13f + disturbance * 0.035f, 0.12f, relief));
            grain.intensity.Override(Mathf.Lerp(0.12f + danger * 0.22f, 0.04f, relief));
            aberration.intensity.Override(Mathf.Lerp(0.015f + danger * 0.055f + shock * 0.06f, 0f, relief));
            colors.postExposure.Override(-Blackout * 8f);
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
                candle.candleLight.intensity = Mathf.Lerp(6.2f, 7.4f, noise);
                candle.candleLight.range = 7f;
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
