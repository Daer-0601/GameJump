using UnityEngine;

namespace Gamejam2026
{
    public class DemoMechanism : MonoBehaviour
    {
        public int id;
        public string symbol;
        public DemoGame game;
        public Light indicator;
        public Transform wheel;
        public Transform crankSocket;
        public CarryObject mountedCrank;
        public bool done;
        public bool unstable;
        public bool HasCrank => mountedCrank != null;
        public Material sparkMaterial;
        public float Hold01 => stabilizeTime <= 0f ? 0f : Mathf.Clamp01(stabilizeProgress / stabilizeTime);
        const float stabilizeTime = 1.5f;
        Quaternion startRotation;
        Quaternion socketStartRotation;
        float stabilizeProgress;
        bool heldThisFrame;
        int lastHoldFrame = -1;
        ParticleSystem sparks;
        AudioSource rattle;
        AudioClip loopClip;
        Vector3 wheelPosition;

        void Awake()
        {
            if (!crankSocket)
            {
                var existing = transform.Find("Soporte manivela");
                if (existing)
                {
                    crankSocket = existing;
                }
                else
                {
                    var socketObj = new GameObject("Soporte manivela");
                    socketObj.transform.SetParent(transform, false);
                    socketObj.transform.localPosition = new Vector3(0f, 1.2f, -0.52f);
                    socketObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    crankSocket = socketObj.transform;
                }
            }
            socketStartRotation = crankSocket.localRotation;
        }

        void Start()
        {
            if (wheel) startRotation = wheel.localRotation;
            if (wheel) wheelPosition = wheel.localPosition;
            rattle = gameObject.AddComponent<AudioSource>();
            rattle.playOnAwake = false;
            rattle.loop = true;
            rattle.spatialBlend = 0f; // Both split-screen players must hear nearby faults.
            rattle.volume = 0f;
            if (game.gearsClip)
            {
                var original = game.gearsClip;
                original.LoadAudioData();
                int frames = Mathf.Min(original.samples, Mathf.RoundToInt(original.frequency * 0.65f));
                var samples = new float[frames * original.channels];
                if (original.GetData(samples, 0))
                {
                    for (int frame = 0; frame < frames; frame++)
                    {
                        float fade = Mathf.Min(1f, Mathf.Min(frame, frames - frame - 1) / (original.frequency * 0.025f));
                        for (int c = 0; c < original.channels; c++) samples[frame * original.channels + c] *= fade;
                    }
                    loopClip = AudioClip.Create("Desajuste - fragmento del engranaje existente", frames, original.channels, original.frequency, false);
                    loopClip.SetData(samples, 0);
                }
                rattle.clip = loopClip ? loopClip : original;
            }
            var fx = new GameObject("Chispas de desajuste");
            fx.transform.SetParent(transform, false);
            fx.transform.position = wheel ? wheel.position : transform.position + Vector3.up;
            sparks = fx.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.045f);
            main.startColor = new Color(1f, 0.52f, 0.12f);
            main.gravityModifier = 0.6f;
            main.maxParticles = 24;
            var emission = sparks.emission;
            emission.rateOverTime = 12f;
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            if (sparkMaterial) sparks.GetComponent<ParticleSystemRenderer>().sharedMaterial = sparkMaterial;
        }

        public string GetHint(DemoPlayer player)
        {
            if (player == null) return string.Empty;
            string useKey = "[" + DemoPlayerInput.UseLabel(player.playerIndex) + "] ";

            if (HasCrank)
            {
                if (player.Held == null)
                    return useKey + "Retirar manivela del soporte " + symbol;
                return "Manos ocupadas: suelta el objeto para retirar la manivela";
            }

            if (player.Held != null && !player.Held.isCandle)
                return useKey + "Colocar manivela en el soporte " + symbol;

            if (player.Held != null && player.Held.isCandle)
                return "Necesitas la manivela (deja la vela con " + DemoPlayerInput.DropLabel(player.playerIndex) + ")";

            return done ? "Mecanismo completado" : "Necesitas la manivela";
        }

        public bool AttachCrank(CarryObject crank, DemoPlayer player)
        {
            if (HasCrank || crank == null || crank.isCandle) return false;
            mountedCrank = crank;
            crank.AttachTo(crankSocket);
            if (player != null && player.Held == crank)
            {
                player.Held = null;
            }
            if (!done)
            {
                game.Activate(this, player);
            }
            else if (player != null) player.Tell("Manivela colocada en el soporte " + symbol + ".");
            return true;
        }

        public bool DetachCrank(DemoPlayer player)
        {
            if (!HasCrank || player == null || player.Held != null) return false;
            var crank = mountedCrank;
            mountedCrank = null;
            crank.PickUp(player);
            player.Tell("Manivela retirada del soporte " + symbol + ". Caminas más lento mientras la llevas.");
            return true;
        }

        public void Interact(DemoPlayer player)
        {
            if (player == null) return;

            if (HasCrank)
            {
                if (player.Held == null)
                {
                    DetachCrank(player);
                }
                else
                {
                    player.Tell("Pulsa " + DemoPlayerInput.DropLabel(player.playerIndex) + " para soltar lo que llevas antes de retirar la manivela.");
                }
            }
            else
            {
                if (player.Held != null && !player.Held.isCandle)
                {
                    AttachCrank(player.Held, player);
                }
                else if (player.Held != null && player.Held.isCandle)
                {
                    player.Tell("Necesitas la manivela. Deja la vela con " + DemoPlayerInput.DropLabel(player.playerIndex) + ".");
                }
                else
                {
                    player.Tell(done ? "Mecanismo completado." : "Necesitas la manivela para este soporte.");
                }
            }
        }

        public void Use(DemoPlayer player)
        {
            Interact(player);
        }

        public void Hold(DemoPlayer player, bool holding)
        {
            if (!game.PlayerInputEnabled || !done || !unstable || !holding || player == null) return;
            heldThisFrame = true;
            if (lastHoldFrame == Time.frameCount) return;
            lastHoldFrame = Time.frameCount;
            stabilizeProgress += Time.deltaTime;
            if (stabilizeProgress < stabilizeTime) return;
            stabilizeProgress = 0f;
            unstable = false;
            game.OnStabilized(this, player);
        }

        void LateUpdate()
        {
            if (!heldThisFrame) stabilizeProgress = 0f;
            heldThisFrame = false;
        }

        void Update()
        {
            bool active = unstable && game.Playing;
            if (active && !sparks.isPlaying) sparks.Play();
            else if (!active && sparks.isPlaying) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (active && rattle.clip)
            {
                if (!rattle.isPlaying) rattle.Play();
                float distance = float.MaxValue;
                foreach (var player in game.players) distance = Mathf.Min(distance, Vector3.Distance(transform.position, player.transform.position));
                rattle.volume = Mathf.Clamp01(1f - distance / 17f) * game.sfxVolume * 0.32f;
                rattle.pitch = 0.8f + Mathf.Sin(Time.time * 7f) * 0.06f;
            }
            else if (rattle.isPlaying) rattle.Stop();
            if (wheel) wheel.localPosition = wheelPosition + (active ? new Vector3(Mathf.Sin(Time.time * 43f), Mathf.Sin(Time.time * 37f), 0f) * 0.012f : Vector3.zero);
            if (wheel && done && !unstable)
                wheel.localRotation = Quaternion.Slerp(wheel.localRotation, startRotation * Quaternion.Euler(0f, 0f, 90f), Time.deltaTime * 3f);
            else if (wheel && unstable)
                wheel.localRotation = startRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 8f) * 25f);

            if (crankSocket && wheel)
            {
                Quaternion delta = wheel.localRotation * Quaternion.Inverse(startRotation);
                crankSocket.localRotation = socketStartRotation * delta;
            }

            if (!indicator) return;
            if (unstable) indicator.color = Color.Lerp(new Color(0.95f, 0.45f, 0.05f), new Color(1f, 0.15f, 0.05f), Mathf.PingPong(Time.time * 4f, 1f));
            else indicator.color = done ? new Color(0.3f, 0.85f, 0.65f) : new Color(0.9f, 0.3f, 0.1f);
            indicator.intensity = unstable ? Mathf.Lerp(0.5f, 1.2f, Mathf.PingPong(Time.time * 3f, 1f)) : 0.4f;
            indicator.range = unstable ? 1.8f : 0.9f;
        }

        void OnDestroy()
        {
            if (loopClip) Destroy(loopClip);
        }
    }
}
