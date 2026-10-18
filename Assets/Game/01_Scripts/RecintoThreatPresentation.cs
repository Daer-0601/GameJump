using UnityEngine;

namespace Gamejam2026
{
    // Presentación de la amenaza. No decide victoria, derrota ni el recorrido de la sombra.
    public class RecintoThreatPresentation : MonoBehaviour
    {
        public AudioClip warning;
        public AudioClip movement;
        public AudioClip stop;
        public AudioClip contact;
        public AudioClip knock;
        public AudioClip flicker;
        [Range(40f, 140f)] public float knockCooldown = 80f;
        [Range(1, 4)] public int maxKnocks = 3;
        [Range(40f, 140f)] public float flickerCooldown = 70f;
        [Range(1, 4)] public int maxFlickers = 3;
        [Range(40f, 140f)] public float splitCooldown = 75f;
        [Range(0, 4)] public int maxSplitLights = 3;
        public AudioClip Movement => movement;
        public int Knocks => knocks;

        DemoGame game;
        RecintoInvestigation investigation;
        DemoAtmosphere atmosphere;
        AudioSource voice;
        Transform door;
        Light roomLight;
        float roomBase;
        bool placesCached;
        int knocks;
        int flickers;
        int splitLights;
        bool splitTold;
        bool flickerTold;
        float nextKnock = 18f;
        float nextFlicker = 55f;
        float nextSplit = 70f;
        bool flickering;
        float flickerUntil;
        bool wasMoving;
        bool warnedMissing;

        public void Bind(DemoGame owner)
        {
            game = owner;
            investigation = GetComponent<RecintoInvestigation>();
            atmosphere = GetComponent<DemoAtmosphere>();
            CachePlaces();
        }

        void Awake()
        {
            if (!game) game = GetComponent<DemoGame>();
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.loop = false;
        }

        void OnDisable()
        {
            RestoreLight();
        }

        void OnDestroy()
        {
            RestoreLight();
        }

        void Update()
        {
            if (!game || !game.Playing)
            {
                RestoreLight();
                return;
            }
            TickFlicker();
            if (!LanAuthority.Simulates) return;
            if (!investigation) investigation = GetComponent<RecintoInvestigation>();
            if (!atmosphere) atmosphere = GetComponent<DemoAtmosphere>();
            if (investigation && !investigation.ScareWindowOpen) return;
            if (TryKnock()) return;
            if (TryFlicker()) return;
            TrySplitLight();
        }

        public void Warn(Vector3 point)
        {
            Emit(0, warning, point, 0.42f);
        }

        public void NoteMotion(bool moving, Vector3 point)
        {
            if (moving == wasMoving) return;
            wasMoving = moving;
            if (moving || !game || !game.Playing) return;
            Emit(1, stop, point, 0.34f);
            if (LanAuthority.Simulates)
                game.WarnThreat("La sombra se detuvo. Sigue en el recinto; no deis por hecho que se ha ido.");
        }

        public void Contact(Vector3 point)
        {
            Emit(2, contact, point, 0.5f);
        }

        public void Present(byte kind, Vector3 point, float amount)
        {
            AudioClip clip = kind == 0 ? warning : kind == 1 ? stop : kind == 2 ? contact : null;
            LanEcho.Apply(() => Emit(kind, clip, point, amount));
        }

        void Emit(byte kind, AudioClip clip, Vector3 point, float amount)
        {
            if (!LanEcho.Applying && LanAuthority.IsHost)
                LanSession.BroadcastThreat(kind, point, amount);
            if (kind == 4) BeginFlicker();
            else if (kind == 5) PulseNear(point);
            else if (kind == 3) Play(knock, point, amount);
            else Play(clip, point, amount);
        }

        bool TryKnock()
        {
            if (knocks >= maxKnocks || !door || !knock || game.ElapsedSeconds < nextKnock) return false;
            foreach (var player in game.players)
            {
                if (!player || Vector3.Distance(player.transform.position, door.position) > 6.5f) continue;
                knocks++;
                nextKnock = game.ElapsedSeconds + knockCooldown;
                if (investigation) investigation.HoldQuiet(12f);
                Vector3 point = door.position - door.forward * 1.1f;
                Emit(3, knock, point, 0.4f);
                game.WarnThreat(knocks == 1
                    ? "Un golpe seco, detrás del portón cerrado."
                    : "Otro golpe en el portón. Sigue cerrado.");
                return true;
            }
            return false;
        }

        bool TryFlicker()
        {
            if (flickers >= maxFlickers || flickering || !roomLight || game.ElapsedSeconds < nextFlicker) return false;
            foreach (var player in game.players)
            {
                if (!player || Vector3.Distance(player.transform.position, roomLight.transform.position) > 8f) continue;
                flickers++;
                nextFlicker = game.ElapsedSeconds + flickerCooldown;
                if (investigation) investigation.HoldQuiet(12f);
                Emit(4, flicker, roomLight.transform.position, 0.28f);
                if (!flickerTold)
                {
                    flickerTold = true;
                    game.WarnThreat("La luz del patio tembló.");
                }
                return true;
            }
            return false;
        }

        bool TrySplitLight()
        {
            if (splitLights >= maxSplitLights || game.ElapsedSeconds < nextSplit || !atmosphere) return false;
            if (game.players == null || game.players.Length < 2) return false;
            var first = game.players[0];
            var second = game.players[1];
            if (!first || !second) return false;
            if (Vector3.Distance(first.transform.position, second.transform.position) < 16f) return false;
            DemoPlayer far = first;
            float farLight = DistanceToCandle(first);
            float otherLight = DistanceToCandle(second);
            if (otherLight > farLight) far = second;
            if (!atmosphere.HasLightNear(far.transform.position, 9f)) return false;
            splitLights++;
            nextSplit = game.ElapsedSeconds + splitCooldown;
            if (investigation) investigation.HoldQuiet(12f);
            Emit(5, null, far.transform.position, 0.3f);
            if (!splitTold)
            {
                splitTold = true;
                game.WarnThreat("La luz de la sala bajó un momento. Estáis separados; podéis reuniros.");
            }
            return true;
        }

        float DistanceToCandle(DemoPlayer player)
        {
            if (!player || game.candles == null) return 999f;
            float best = 999f;
            foreach (var candle in game.candles)
            {
                if (!candle || !candle.candleLight || !candle.candleLight.enabled) continue;
                best = Mathf.Min(best, Vector3.Distance(player.transform.position, candle.transform.position));
            }
            return best;
        }

        void BeginFlicker()
        {
            if (!roomLight || flickering) return;
            if (!placesCached) CachePlaces();
            flickering = true;
            flickerUntil = Time.time + 1.6f;
            Play(flicker, roomLight.transform.position, 0.28f);
        }

        void PulseNear(Vector3 point)
        {
            if (!atmosphere) atmosphere = GetComponent<DemoAtmosphere>();
            if (atmosphere) atmosphere.PulseNear(point);
        }

        void TickFlicker()
        {
            if (!flickering || !roomLight) return;
            if (Time.time >= flickerUntil)
            {
                RestoreLight();
                return;
            }
            float remaining = flickerUntil - Time.time;
            float dip = Mathf.PingPong(remaining * 3.2f, 1f);
            roomLight.intensity = Mathf.Lerp(roomBase * 0.22f, roomBase, dip);
        }

        void RestoreLight()
        {
            if (roomLight) roomLight.intensity = roomBase;
            flickering = false;
        }

        void CachePlaces()
        {
            if (placesCached) return;
            placesCached = true;
            foreach (var mark in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (!door && mark.name == "Porton cerrado") door = mark;
            }
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (light.name != "Luz Patio interior" || light.type != LightType.Point) continue;
                roomLight = light;
                roomBase = light.intensity;
                break;
            }
            if (!door) Debug.LogWarning("RecintoThreatPresentation: no está el Portón cerrado; el golpe no tiene origen.");
            if (!roomLight) Debug.LogWarning("RecintoThreatPresentation: no está Luz Patio interior; el parpadeo no tiene luz local.");
        }

        void Play(AudioClip clip, Vector3 point, float amount)
        {
            if (!clip || !voice || !game || !game.Playing)
            {
                if (!clip && !warnedMissing)
                {
                    warnedMissing = true;
                    Debug.LogWarning("RecintoThreatPresentation: falta un clip de amenaza o ambiente en 04_Audio.");
                }
                return;
            }
            DemoPlayer ear = null;
            float nearest = float.MaxValue;
            foreach (var player in game.players)
            {
                if (!player || !player.view) continue;
                float distance = Vector3.Distance(player.view.transform.position, point);
                if (distance < nearest) { nearest = distance; ear = player; }
            }
            if (!ear) return;
            Vector3 delta = point - ear.view.transform.position;
            voice.panStereo = Mathf.Clamp(Vector3.Dot(ear.view.transform.right, delta.sqrMagnitude > 0.01f ? delta.normalized : Vector3.forward), -0.6f, 0.6f);
            voice.PlayOneShot(clip, game.sfxVolume * amount * Mathf.Clamp01(1f - nearest / 28f));
        }
    }
}
