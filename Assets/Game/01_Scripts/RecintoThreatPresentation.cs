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
        public AudioClip Movement => movement;

        DemoGame game;
        AudioSource voice;
        Transform door;
        Light roomLight;
        float roomBase;
        bool placesCached;
        bool knockPlayed;
        bool flickerPlayed;
        bool flickering;
        float flickerUntil;
        bool wasMoving;
        bool warnedMissing;

        public void Bind(DemoGame owner)
        {
            game = owner;
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
            TryKnock();
            TryFlicker();
            TickFlicker();
        }

        public void Warn(Vector3 point)
        {
            PlayEvent(0, warning, point, 0.42f);
        }

        public void NoteMotion(bool moving, Vector3 point)
        {
            if (moving == wasMoving) return;
            wasMoving = moving;
            if (moving || !game || !game.Playing) return;
            PlayEvent(1, stop, point, 0.34f);
            if (LanAuthority.Simulates)
                game.WarnThreat("La sombra se detuvo. Sigue en el recinto; no deis por hecho que se ha ido.");
        }

        public void Contact(Vector3 point)
        {
            PlayEvent(2, contact, point, 0.5f);
        }

        public void Present(byte kind, Vector3 point, float amount)
        {
            AudioClip clip = kind == 0 ? warning : kind == 1 ? stop : kind == 2 ? contact : null;
            LanEcho.Apply(() => Play(clip, point, amount));
        }

        void PlayEvent(byte kind, AudioClip clip, Vector3 point, float amount)
        {
            if (!LanEcho.Applying && LanAuthority.IsHost)
                LanSession.BroadcastThreat(kind, point, amount);
            Play(clip, point, amount);
        }

        void TryKnock()
        {
            if (knockPlayed || !door || !knock) return;
            foreach (var player in game.players)
            {
                if (!player || Vector3.Distance(player.transform.position, door.position) > 6.5f) continue;
                knockPlayed = true;
                Play(knock, door.position - door.forward * 1.1f, 0.4f);
                game.WarnThreat("Un golpe seco, detrás del portón cerrado.");
                return;
            }
        }

        void TryFlicker()
        {
            if (flickerPlayed || flickering || !roomLight) return;
            foreach (var player in game.players)
            {
                if (!player || Vector3.Distance(player.transform.position, roomLight.transform.position) > 8f) continue;
                flickerPlayed = true;
                flickering = true;
                flickerUntil = Time.time + 1.15f;
                Play(flicker, roomLight.transform.position, 0.28f);
                return;
            }
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
            float dip = Mathf.PingPong(remaining * 4.5f, 1f);
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
                if (!player) continue;
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
