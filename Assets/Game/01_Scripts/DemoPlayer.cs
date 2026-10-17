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
        [Min(0f)] public float mouseSensitivity = 0.11f;
        [FormerlySerializedAs("stickSensitivity")]
        [Min(0f)] public float keyboardLookSpeed = 115f;
        public float reach = 2.6f;
        public CarryObject Held { get; set; }
        public string Hint { get; private set; }
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
            if (readingClue && !readingClue.CanRead(game))
            {
                MessageUntil = 0f;
                readingClue = null;
            }
            if (!game || !game.PlayerInputEnabled) return;
            var input = DemoPlayerInput.Read(playerIndex, keyboardLookSpeed, mouseSensitivity, Time.deltaTime);
            Move(input);
            Interact(input.Use, input.UseHeld, input.Drop);
        }

        void Move(DemoPlayerInput.State input)
        {
            transform.Rotate(0f, input.Look.x, 0f);
            pitch = Mathf.Clamp(pitch - input.Look.y, -75f, 75f);
            view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            if (controller.isGrounded && fallSpeed < 0f) fallSpeed = -2f;
            fallSpeed += Physics.gravity.y * Time.deltaTime;
            float speedMultiplier = input.Run ? Mathf.Max(1f, runMultiplier) : 1f;
            var motion = transform.TransformDirection(new Vector3(input.Move.x, 0f, input.Move.y));
            controller.Move((motion * moveSpeed * speedMultiplier + Vector3.up * fallSpeed) * Time.deltaTime);

            if (controller.isGrounded && input.Move.sqrMagnitude > 0.04f)
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
            var ray = new Ray(view.transform.position, view.transform.forward);
            if (Physics.Raycast(ray, out var hit, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                var item = hit.collider.GetComponentInParent<CarryObject>();
                var mechanism = hit.collider.GetComponentInParent<DemoMechanism>();
                var clue = hit.collider.GetComponentInParent<DemoClue>();
                if (item != null && item.owner == null)
                {
                    Hint = Held == null ? "Agarrar " + item.displayName : "Solta lo que llevas primero";
                    if (use && Held == null) item.PickUp(this);
                }
                else if (mechanism != null)
                {
                    if (mechanism.unstable)
                    {
                        mechanism.Hold(this, useHeld);
                        if (useHeld && mechanism.unstable) Stabilizing = mechanism;
                        int percent = Mathf.RoundToInt(mechanism.Hold01 * 100f);
                        Hint = useHeld ? "Estabilizando " + mechanism.symbol + "  " + percent + "%" : "Mantener para estabilizar " + mechanism.symbol;
                    }
                    else
                    {
                        Hint = mechanism.GetHint(this);
                        if (use && Time.time >= nextUse)
                        {
                            nextUse = Time.time + 0.5f;
                            mechanism.Interact(this);
                        }
                    }
                }
                else if (clue != null)
                {
                    bool readable = clue.CanRead(game);
                    Hint = readable ? "Leer la inscripción" : "Acerca la vela para leer";
                    if (use && readable)
                    {
                        Tell(game.SequenceText, 10f);
                        readingClue = clue;
                    }
                }
            }
            if (drop && Held != null) Held.Drop();
        }
    }
}
