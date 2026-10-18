using UnityEngine;

namespace Gamejam2026
{
    // ambiente compartido para las dos vistas
    public class RecintoSound : MonoBehaviour
    {
        public DemoGame game;
        public AudioClip[] clips;
        public bool loop;
        public float volume = 0.15f;
        public float radius = 20f;
        public Vector2 interval = new Vector2(18f, 35f);
        public float startDelay = 8f;
        public bool mixBackground;
        public bool onlyWhileShadowMoves;
        AudioSource source;
        float next, target, pan, probe;
        int previous = -1;

        void Start()
        {
            // DemoGame owns the main ambience mix. The old marker must not overwrite it.
            if (mixBackground) return;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.loop = loop;
            source.volume = 0;
            if (!loop && !mixBackground)
            {
                var room = gameObject.AddComponent<AudioReverbFilter>();
                room.reverbPreset = AudioReverbPreset.User;
                room.dryLevel = 0;
                room.room = -1800;
                room.roomHF = -900;
                room.decayTime = 1.6f;
                room.reflectionsLevel = -1600;
                room.reflectionsDelay = 0.035f;
                room.reverbLevel = -1200;
                room.reverbDelay = 0.06f;
            }
            next = Time.time + startDelay;
            if (loop && clips.Length > 0) { source.clip = clips[0]; source.Play(); }
        }

        void Update()
        {
            if (!game || !source || clips.Length == 0) return;
            if (!game.Playing || (onlyWhileShadowMoves && (!game.Investigation || !game.Investigation.ShadowMoving)))
            {
                source.volume = 0;
                return;
            }
            if (Time.time >= probe)
            {
                probe = Time.time + 0.25f;
                float distance = float.MaxValue;
                DemoPlayer closest = null;
                foreach (var player in game.players)
                {
                    float d = Vector3.Distance(player.view.transform.position, transform.position);
                    if (d < distance) { distance = d; closest = player; }
                }
                target = volume * Mathf.Pow(Mathf.Clamp01(1 - distance / radius), 1.4f);
                if (closest)
                {
                    Vector3 ear = closest.view.transform.position;
                    Vector3 direction = transform.position - ear;
                    pan = Mathf.Clamp(Vector3.Dot(closest.view.transform.right, direction.normalized), -0.65f, 0.65f);
                    // las paredes amortiguan los sonidos del otro lado
                    foreach (var hit in Physics.RaycastAll(ear, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.GetComponentInParent<DemoPlayer>() || hit.collider.GetComponentInParent<CarryObject>()) continue;
                        target *= 0.5f;
                        break;
                    }
                }
            }
            source.volume = Mathf.MoveTowards(source.volume, target * game.sfxVolume, Time.deltaTime * 0.2f);
            source.panStereo = Mathf.Lerp(source.panStereo, pan, Time.deltaTime * 3f);
            if (!loop && Time.time >= next)
            {
                next = Time.time + Random.Range(interval.x, interval.y);
                if (target < 0.005f) return;
                int chosen = Random.Range(0, clips.Length);
                if (clips.Length > 1 && chosen == previous) chosen = (chosen + 1) % clips.Length;
                previous = chosen;
                source.pitch = Random.Range(0.88f, 1.04f);
                source.PlayOneShot(clips[chosen]);
            }
        }
    }
}
