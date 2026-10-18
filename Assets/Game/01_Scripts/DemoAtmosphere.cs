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
        float[] atmosphereBase;
        float lightDim = 1f;
        int pulseIndex = -1;
        float pulseUntil;
        float pulseSeconds = 1.8f;
        float nextMono;
        float monoHold;
        bool monoArmed;
        [Range(0.7f, 1f)] public float monoDanger = 0.9f;
        public float monoSeconds = 1.4f;
        public float monoGap = 9f;
        const float MonoDepth = -90f;
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
            foreach (var light in FindObjectsByType<Light>())
                if (light.type == LightType.Directional) light.intensity = 0f;
            if (navigationLights != null)
                foreach (var light in navigationLights)
                    if (light) { light.intensity = 0.08f; light.range = 2.8f; }
            if (victoryLights != null) foreach (var light in victoryLights) if (light) light.intensity = 0f;
            CaptureAtmosphere();
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
            colors.saturation.Override(0f);
            nextMono = monoGap;
        }

        public void WrongOrder() { shock = 1f; }

        public bool HasLightNear(Vector3 point, float radius)
        {
            return NearestAtmosphere(point, radius) >= 0;
        }

        public void PulseNear(Vector3 point)
        {
            if (!game || !game.Playing) return;
            int index = NearestAtmosphere(point, 9f);
            if (index < 0) return;
            pulseIndex = index;
            pulseSeconds = 1.8f;
            pulseUntil = Time.time + pulseSeconds;
        }

        int NearestAtmosphere(Vector3 point, float radius)
        {
            if (game == null || game.atmosphereLights == null) return -1;
            int best = -1;
            float bestDistance = radius;
            for (int i = 0; i < game.atmosphereLights.Length; i++)
            {
                var light = game.atmosphereLights[i];
                if (!light || IsVictoryLight(light)) continue;
                float distance = Vector3.Distance(light.transform.position, point);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

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
            ApplyDangerLights(danger, relief, dt);
            ApplyMonochrome(danger, dt);
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
                bool sputter = game.CandleCharge < .5f && Mathf.Sin(Time.time * 29f) > .95f;
                candle.candleLight.intensity = Mathf.Lerp(4.4f, 5.5f, noise) * (game.CandleBurst ? .2f : sputter ? .72f : 1f);
                candle.candleLight.range = game.CandleRange * (game.CandleBurst ? .6f : 1f);
                candle.candleLight.color = new Color(1f, 0.65f, 0.30f);
            }
        }

        void CaptureAtmosphere()
        {
            var lights = game.atmosphereLights;
            atmosphereBase = lights != null ? new float[lights.Length] : System.Array.Empty<float>();
            if (lights == null) return;
            for (int i = 0; i < lights.Length; i++)
                atmosphereBase[i] = lights[i] ? lights[i].intensity : 0f;
        }

        bool IsVictoryLight(Light light)
        {
            if (!light || victoryLights == null) return false;
            for (int i = 0; i < victoryLights.Length; i++)
                if (victoryLights[i] == light) return true;
            return false;
        }

        void ApplyDangerLights(float danger, float relief, float dt)
        {
            float target = Mathf.Lerp(1f, 0.32f, danger);
            if (victory) target = Mathf.Lerp(target, 1f, relief);
            lightDim = Mathf.MoveTowards(lightDim, target, Mathf.Max(dt, 0f) * 0.45f);
            var lights = game.atmosphereLights;
            if (lights == null || atmosphereBase == null) return;
            int count = Mathf.Min(lights.Length, atmosphereBase.Length);
            for (int i = 0; i < count; i++)
            {
                var light = lights[i];
                if (!light || IsVictoryLight(light)) continue;
                float dip = 1f;
                if (game.Playing && i == pulseIndex && Time.time < pulseUntil)
                {
                    float u = Mathf.Clamp01((pulseUntil - Time.time) / Mathf.Max(0.2f, pulseSeconds));
                    dip = Mathf.Lerp(1f, 0.28f, Mathf.Sin(u * Mathf.PI));
                }
                light.intensity = atmosphereBase[i] * lightDim * dip;
            }
        }

        void RestoreAtmosphere()
        {
            var lights = game ? game.atmosphereLights : null;
            if (lights == null || atmosphereBase == null) return;
            int count = Mathf.Min(lights.Length, atmosphereBase.Length);
            for (int i = 0; i < count; i++)
                if (lights[i] && !IsVictoryLight(lights[i])) lights[i].intensity = atmosphereBase[i];
        }

        void ApplyMonochrome(float danger, float dt)
        {
            if (!colors) return;
            bool live = game.Playing && !ended && danger >= monoDanger;
            if (!live)
            {
                monoArmed = false;
                monoHold = 0f;
                if (colors.saturation.value != 0f) colors.saturation.Override(0f);
                return;
            }
            float duration = Mathf.Max(0.2f, monoSeconds);
            float gap = Mathf.Max(duration + 0.5f, monoGap);
            if (!monoArmed)
            {
                monoArmed = true;
                nextMono = Time.time + gap;
            }
            if (monoHold <= 0f && Time.time >= nextMono)
            {
                monoHold = duration;
                nextMono = Time.time + gap;
            }
            float level = 0f;
            if (monoHold > 0f)
            {
                monoHold = Mathf.Max(0f, monoHold - dt);
                float along = 1f - Mathf.Clamp01(monoHold / duration);
                level = Mathf.Sin(along * Mathf.PI);
            }
            colors.saturation.Override(Mathf.Lerp(0f, MonoDepth, level));
        }

        void OnDestroy()
        {
            RestoreAtmosphere();
            if (colors) colors.saturation.Override(0f);
            if (profile) Destroy(profile);
            if (volume) Destroy(volume);
        }
    }
}
