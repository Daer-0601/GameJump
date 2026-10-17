using UnityEngine;
using UnityEngine.InputSystem;

namespace Gamejam2026
{
    [RequireComponent(typeof(CharacterController))]
    public class DemoPlayer : MonoBehaviour
    {
        public int playerIndex;
        public Camera view;
        public Transform hand;
        public DemoGame game;
        public float moveSpeed = 3.2f;
        public float mouseSensitivity = 0.11f;
        public float stickSensitivity = 115f;
        public float reach = 2.6f;
        public CarryObject Held { get; set; }
        public string Hint { get; private set; }
        public string Message { get; private set; }
        public float MessageUntil { get; private set; }
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
            Message = text;
            MessageUntil = Time.unscaledTime + seconds;
        }

        void Update()
        {
            if (!game || !game.Playing) return;
            bool keyboardMode = !game.twoGamepads && playerIndex == 0;
            int padIndex = game.twoGamepads ? playerIndex : playerIndex - 1;
            Gamepad pad = padIndex >= 0 && Gamepad.all.Count > padIndex ? Gamepad.all[padIndex] : null;
            var keyboard = Keyboard.current;
            Vector2 move = Vector2.zero;
            Vector2 look = Vector2.zero;
            bool use = false;
            bool drop = false;

            if (keyboardMode && keyboard != null)
            {
                move = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                if (Mouse.current != null) look = Mouse.current.delta.ReadValue() * mouseSensitivity;
                use = keyboard.eKey.wasPressedThisFrame;
                drop = keyboard.qKey.wasPressedThisFrame;
            }
            else if (pad != null)
            {
                move = pad.leftStick.ReadValue();
                look = pad.rightStick.ReadValue() * stickSensitivity * Time.deltaTime;
                use = pad.buttonSouth.wasPressedThisFrame;
                drop = pad.buttonWest.wasPressedThisFrame;
            }

            transform.Rotate(0f, look.x, 0f);
            pitch = Mathf.Clamp(pitch - look.y, -75f, 75f);
            view.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            if (controller.isGrounded && fallSpeed < 0f) fallSpeed = -2f;
            fallSpeed += Physics.gravity.y * Time.deltaTime;
            var motion = transform.TransformDirection(new Vector3(move.x, 0f, move.y));
            controller.Move((Vector3.ClampMagnitude(motion, 1f) * moveSpeed + Vector3.up * fallSpeed) * Time.deltaTime);

            if (controller.isGrounded && move.sqrMagnitude > 0.04f)
            {
                footstepTimer -= Time.deltaTime;
                if (footstepTimer <= 0f)
                {
                    game.PlayFootstep(footstepsSource);
                    footstepTimer = FootstepInterval;
                }
            }
            else
            {
                footstepTimer = 0f;
            }

            Hint = string.Empty;
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
                    Hint = mechanism.done ? "Mecanismo completado" : "Usar manivela en " + mechanism.symbol;
                    if (use && Time.time >= nextUse)
                    {
                        nextUse = Time.time + 0.7f;
                        mechanism.Use(this);
                    }
                }
                else if (clue != null)
                {
                    Hint = "Leer la inscripcion";
                    if (use) Tell(game.SequenceText, 10f);
                }
            }
            if (drop && Held != null) Held.Drop();
        }
    }
}
