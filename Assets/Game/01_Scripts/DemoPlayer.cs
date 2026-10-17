using UnityEngine;
using UnityEngine.Serialization;

namespace Gamejam2026
{
    [RequireComponent(typeof(CharacterController))]
    public class DemoPlayer : MonoBehaviour
    {
        public int playerIndex;
        public Camera view;
        public Transform hand;
        public DemoGame game;
        [Min(0.1f)] public float moveSpeed = 3.2f;
        [Min(1f)] public float runMultiplier = 1.75f;
        [Tooltip("Multiplicador de velocidad al llevar la manivela, tanto al caminar como al correr.")]
        [Range(0.1f, 1f)] public float crankSpeedMultiplier = 0.65f;
        [Min(0f)] public float mouseSensitivity = 0.11f;
        [FormerlySerializedAs("stickSensitivity")]
        [Min(0f)] public float keyboardLookSpeed = 115f;
        [Tooltip("Tiempo para alcanzar la velocidad de giro de J2. Cero desactiva la rampa.")]
        [Min(0f)] public float keyboardLookRampTime = 0.12f;
        [Range(0.1f, 1f)] public float preciseLookMultiplier = 0.35f;
        public float reach = 2.6f;
        public CarryObject Held { get; set; }
        public string Hint { get; private set; }
        public string DropHint => Held == null ? string.Empty
            : "[" + DemoPlayerInput.DropLabel(playerIndex) + "] Soltar " + Held.displayName + " al suelo";
        public string Message { get; private set; }
        public float MessageUntil { get; private set; }
        public DemoMechanism Stabilizing { get; private set; }
        DemoClue readingClue;
        CharacterController controller;
        AudioSource footstepsSource;
        float pitch;
        float fallSpeed;
        float nextUse;
        float footstepTimer;
        Vector2 lookVelocity;
        const float FootstepInterval = 0.42f;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            footstepsSource = gameObject.AddComponent<AudioSource>();
            footstepsSource.playOnAwake = false;
            footstepsSource.spatialBlend = 0f;
        }

        public void Tell(string text, float seconds = 3f)
        {
            readingClue = null;
            Message = text;
            MessageUntil = Time.unscaledTime + seconds;
        }

        void Update()
        {
            if (readingClue)
            {
                if (!readingClue.TryRead(game, out string text))
                {
                    MessageUntil = 0f;
                    readingClue = null;
                }
                else Message = text;
            }
            if (!game || !game.PlayerInputEnabled)
            {
                Stabilizing = null;
                lookVelocity = Vector2.zero;
                return;
            }
            var input = DemoPlayerInput.Read(playerIndex, keyboardLookSpeed, mouseSensitivity, Time.deltaTime, preciseLookMultiplier);
            Look(input.Look);
            Interact(input.Use, input.UseHeld, input.Drop);
            if (game.PlayerInputEnabled) Move(input);
        }

        void Look(Vector2 look)
        {
            if (playerIndex == 1 && Time.deltaTime > 0f)
            {
                Vector2 target = look / Time.deltaTime;
                float step = keyboardLookRampTime > 0f
                    ? keyboardLookSpeed / keyboardLookRampTime * Time.deltaTime : float.PositiveInfinity;
                lookVelocity.x = ApproachLook(lookVelocity.x, target.x, step);
                lookVelocity.y = ApproachLook(lookVelocity.y, target.y, step);
                look = lookVelocity * Time.deltaTime;
            }
            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -75f, 75f);
            view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        static float ApproachLook(float current, float target, float step)
        {
            if (target == 0f) return 0f;
            if (Mathf.Sign(current) != Mathf.Sign(target)) current = 0f;
            if (Mathf.Abs(current) > Mathf.Abs(target)) return target;
            return Mathf.MoveTowards(current, target, step);
        }

        void Move(DemoPlayerInput.State input)
        {
            if (controller.isGrounded && fallSpeed < 0f) fallSpeed = -2f;
            fallSpeed += Physics.gravity.y * Time.deltaTime;
            float speedMultiplier = input.Run && Stabilizing == null ? Mathf.Max(1f, runMultiplier) : 1f;
            if (Held != null && !Held.isCandle) speedMultiplier *= Mathf.Clamp(crankSpeedMultiplier, 0.1f, 1f);
            Vector2 movement = Stabilizing == null ? input.Move : Vector2.zero;
            var motion = transform.TransformDirection(new Vector3(movement.x, 0f, movement.y));
            controller.Move((motion * moveSpeed * speedMultiplier + Vector3.up * fallSpeed) * Time.deltaTime);

            if (controller.isGrounded && movement.sqrMagnitude > 0.04f)
            {
                footstepTimer -= Time.deltaTime;
                if (footstepTimer <= 0f)
                {
                    game.PlayFootstep(footstepsSource);
                    footstepTimer = FootstepInterval / speedMultiplier;
                }
            }
            else
            {
                footstepTimer = 0f;
            }
        }

        void Interact(bool use, bool useHeld, bool drop)
        {
            Hint = string.Empty;
            Stabilizing = null;
            if (drop && Held != null)
            {
                string itemName = Held.displayName;
                Held.Drop();
                Tell(itemName + " en el suelo.");
                use = false;
                useHeld = false;
            }
            var ray = new Ray(view.transform.position, view.transform.forward);
            if (Physics.Raycast(ray, out var hit, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                var item = hit.collider.GetComponentInParent<CarryObject>();
                var mechanism = hit.collider.GetComponentInParent<DemoMechanism>();
                var clue = hit.collider.GetComponentInParent<DemoClue>();
                if (item != null && item.owner == null)
                {
                    Hint = Held == null ? UseHint("Recoger " + item.displayName) : "Manos ocupadas: suelta el objeto primero";
                    if (use && Held == null)
                    {
                        item.PickUp(this);
                        Hint = string.Empty;
                    }
                }
                else if (mechanism != null)
                {
                    if (mechanism.unstable)
                    {
                        if (useHeld && mechanism.done) Stabilizing = mechanism;
                        mechanism.Hold(this, useHeld);
                        int percent = Mathf.RoundToInt(mechanism.Hold01 * 100f);
                        Hint = mechanism.unstable
                            ? UseHint(useHeld ? "Estabilizando " + mechanism.symbol + "  " + percent + "% (sin moverse)"
                                : "Mantener para estabilizar " + mechanism.symbol)
                            : mechanism.GetHint(this);
                    }
                    else
                    {
                        Hint = mechanism.GetHint(this);
                        if (use && Time.time >= nextUse)
                        {
                            nextUse = Time.time + 0.5f;
                            mechanism.Interact(this);
                            Hint = mechanism.GetHint(this);
                        }
                    }
                }
                else if (clue != null)
                {
                    bool readable = clue.CanRead(game);
                    string action = clue.nextSymbolOnly ? "Leer siguiente símbolo" : "Leer la inscripción";
                    Hint = readable ? UseHint(action) : "Acerca la vela para leer";
                    if (clue.nextSymbolOnly && readable && string.IsNullOrEmpty(game.NextSymbol)) Hint = "No quedan símbolos pendientes";
                    if (use && clue.TryRead(game, out string text))
                    {
                        Tell(text, 10f);
                        readingClue = clue;
                    }
                }
            }
        }

        string UseHint(string action) => "[" + DemoPlayerInput.UseLabel(playerIndex) + "] " + action;
    }
}
