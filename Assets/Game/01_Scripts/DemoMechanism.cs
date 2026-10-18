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
        public float Cooperation01 => CooperationSeconds <= 0f ? 0f : Mathf.Clamp01(cooperation / CooperationSeconds);
        public Transform BracePoint => bracePoint;
        public const float CooperationSeconds = 2f;
        const float stabilizeTime = 1.5f;
        const float TapSeconds = 0.22f;
        Quaternion startRotation;
        Quaternion socketStartRotation;
        float stabilizeProgress;
        float cooperation;
        bool heldThisFrame;
        int lastHoldFrame = -1;
        int braceFrame = -1;
        int operateFrame = -1;
        bool braceHeld;
        bool operateHeld;
        bool lightsTold;
        Transform bracePoint;
        DemoPlayer bracer;
        DemoPlayer operatorPlayer;
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
            EnsureBrace();
        }

        void EnsureBrace()
        {
            var existing = transform.Find("Punto de apoyo");
            if (!existing)
            {
                var point = new GameObject("Punto de apoyo");
                point.transform.SetParent(transform, false);
                // Outside the front casing, on the right, so the partner can stand beside the crank.
                point.transform.localPosition = new Vector3(0.92f, 1.05f, -0.2f);
                var box = point.AddComponent<BoxCollider>();
                box.size = new Vector3(0.36f, 0.46f, 0.36f);
                var grip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                grip.name = "Asa";
                grip.transform.SetParent(point.transform, false);
                grip.transform.localScale = new Vector3(0.07f, 0.18f, 0.07f);
                Destroy(grip.GetComponent<Collider>());
                var donor = GetComponentInChildren<Renderer>();
                if (donor && donor.sharedMaterial) grip.GetComponent<Renderer>().sharedMaterial = donor.sharedMaterial;
                var plate = new GameObject("Rótulo del asa");
                plate.transform.SetParent(point.transform, false);
                plate.transform.localPosition = new Vector3(0f, 0.32f, -0.12f);
                plate.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var text = plate.AddComponent<TextMesh>();
                text.text = "ASA";
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.characterSize = 0.045f;
                text.fontSize = 48;
                text.color = new Color(0.86f, 0.8f, 0.66f);
                existing = point.transform;
            }
            bracePoint = existing;
        }

        public bool IsBraceAim(RaycastHit hit)
        {
            if (bracePoint && hit.collider && hit.collider.transform.IsChildOf(bracePoint)) return true;
            if (!bracePoint) return false;
            Vector3 crank = crankSocket ? crankSocket.position : transform.position;
            return Vector3.Distance(hit.point, bracePoint.position) + 0.08f < Vector3.Distance(hit.point, crank);
        }

        void Start()
        {
            if (wheel) startRotation = wheel.localRotation;
            if (wheel) wheelPosition = wheel.localPosition;
            rattle = gameObject.AddComponent<AudioSource>();
            rattle.playOnAwake = false;
            rattle.loop = true;
            // One AudioListener sits on the first camera. Full 3D attenuation would ignore player 2,
            // so loudness follows the nearest player and the clip is started once.
            rattle.spatialBlend = 0f;
            rattle.dopplerLevel = 0f;
            rattle.minDistance = 1.6f;
            rattle.maxDistance = 17f;
            rattle.rolloffMode = AudioRolloffMode.Linear;
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
            if (unstable && player.Held && !player.Held.isCandle) return "Suelta la manivela para estabilizar (" + DemoPlayerInput.DropLabel(player.playerIndex) + ")";
            if (!done && game.Investigation && !game.Investigation.CanAttempt)
                return game.Investigation.NextTask;
            string useKey = "[" + DemoPlayerInput.UseLabel(player.playerIndex) + "] ";

            if (!done && !HasCrank && player.Held && !player.Held.isCandle && game.Investigation && !game.Investigation.Contrasted)
                return useKey + "Colocar SIN CONTRASTE: error (+12%). Busquen primero la consola";

            if (HasCrank && !done)
            {
                if (player.Held && player.Held.isCandle)
                    return useKey + "Mantener para accionar " + symbol + ". Puedes conservar la vela.";
                if (player.Held == null)
                    return useKey + "Mantener para accionar " + symbol + ". Pulsación corta retira la manivela.";
            }

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
            if (!done && game.Investigation && !game.Investigation.CanAttempt)
            {
                if (player) player.Tell(game.Investigation.NextTask);
                return false;
            }
            mountedCrank = crank;
            crank.AttachTo(crankSocket);
            if (player != null && player.Held == crank)
            {
                player.Held = null;
            }
            if (player != null)
            {
                player.Tell(done
                    ? "Manivela colocada en el soporte " + symbol + "."
                    : "Manivela en " + symbol + ". Uno sujeta el asa y el otro mantiene el giro.", 6f);
            }
            return true;
        }

        public bool CanTakeRole(DemoPlayer player)
        {
            return player && game && game.PlayerInputEnabled && !done && !unstable && HasCrank
                && mountedCrank && !mountedCrank.Inserting
                && (player.Held == null || player.Held.isCandle);
        }

        public string BraceHint(DemoPlayer player)
        {
            if (player == null) return string.Empty;
            string key = "[" + DemoPlayerInput.UseLabel(player.playerIndex) + "] ";
            if (!HasCrank) return "Primero coloquen la manivela. El asa solo sujeta durante el giro.";
            if (player.Held && !player.Held.isCandle) return "Suelta la manivela en el soporte antes de sujetar el asa.";
            if (done) return "Mecanismo completado";
            if (unstable) return GetHint(player);
            int percent = Mathf.RoundToInt(Cooperation01 * 100f);
            string contrast = UncontrastedWarning();
            if (!operateHeld)
                return key + "Mantener para sujetar " + symbol + ". Falta compañero: alguien debe accionar la manivela. No suma peligro." + contrast;
            return key + "Sujetando " + symbol + "  " + percent + "%. Mantén hasta completar el giro." + contrast;
        }

        public string OperateHint(DemoPlayer player)
        {
            if (player == null) return string.Empty;
            string key = "[" + DemoPlayerInput.UseLabel(player.playerIndex) + "] ";
            int percent = Mathf.RoundToInt(Cooperation01 * 100f);
            string contrast = UncontrastedWarning();
            if (!braceHeld)
                return key + "Mantener para accionar " + symbol + "  " + percent + "%. Falta compañero: alguien debe sujetar el asa. No suma peligro." + contrast;
            return key + "Accionando " + symbol + "  " + percent + "%. Mantén junto al asa." + contrast;
        }

        string UncontrastedWarning()
        {
            if (!game || !game.Investigation || game.Investigation.Contrasted) return string.Empty;
            return " Datos sin contrastar: terminarlo será un error (+12%).";
        }

        public void Brace(DemoPlayer player, bool holding)
        {
            if (!holding || !CanTakeRole(player)) return;
            braceFrame = Time.frameCount;
            bracer = player;
        }

        public void Operate(DemoPlayer player, bool holding)
        {
            if (!holding || !CanTakeRole(player)) return;
            operateFrame = Time.frameCount;
            operatorPlayer = player;
        }

        public static bool IsShortCrankTap(float heldSeconds) => heldSeconds > 0f && heldSeconds < TapSeconds;

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
            if (!game.PlayerInputEnabled || !done || !unstable || !holding || player == null || (player.Held && !player.Held.isCandle)) return;
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
            if (!heldThisFrame)
            {
                if (stabilizeProgress > 0.15f && game) game.Pulse("Estabilización interrumpida");
                stabilizeProgress = 0f;
            }
            heldThisFrame = false;
            if (!game || !game.Playing) return;

            braceHeld = braceFrame == Time.frameCount;
            operateHeld = operateFrame == Time.frameCount;
            bool together = braceHeld && operateHeld && bracer && operatorPlayer && bracer != operatorPlayer && CanTakeRole(bracer) && CanTakeRole(operatorPlayer);
            if (done || unstable || !HasCrank)
            {
                if (!HasCrank || done)
                {
                    cooperation = 0f;
                    lightsTold = false;
                }
                return;
            }
            if (!together) return;
            cooperation += Time.deltaTime;
            if (cooperation < CooperationSeconds) return;
            bool waitingForLights = game.Investigation && game.Investigation.Contrasted && !game.Investigation.Ready;
            if (waitingForLights)
            {
                cooperation = CooperationSeconds;
                if (!lightsTold)
                {
                    lightsTold = true;
                    game.Notify(game.Investigation.NextTask, 6f);
                }
                return;
            }
            lightsTold = false;
            cooperation = 0f;
            game.Activate(this, operatorPlayer);
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
                float atten = Mathf.Clamp01(1f - distance / rattle.maxDistance);
                rattle.volume = atten * atten * game.sfxVolume * 0.45f;
                rattle.pitch = 0.8f + Mathf.Sin(Time.time * 7f) * 0.06f;
            }
            else if (rattle.isPlaying) rattle.Stop();
            if (wheel) wheel.localPosition = wheelPosition + (active ? new Vector3(Mathf.Sin(Time.time * 43f), Mathf.Sin(Time.time * 37f), 0f) * 0.012f : Vector3.zero);
            if (wheel && done && !unstable)
                wheel.localRotation = Quaternion.Slerp(wheel.localRotation, startRotation * Quaternion.Euler(0f, 0f, 90f), Time.deltaTime * 3f);
            else if (wheel && unstable)
                wheel.localRotation = startRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 8f) * 25f);
            else if (wheel && !done)
                wheel.localRotation = Quaternion.Slerp(wheel.localRotation, startRotation * Quaternion.Euler(0f, 0f, Cooperation01 * 90f), Time.deltaTime * 6f);

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
