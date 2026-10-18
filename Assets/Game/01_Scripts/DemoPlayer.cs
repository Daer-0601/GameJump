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
        public DemoMechanism FocusedMechanism { get; private set; }
        public bool Working { get; private set; }
        public float ActualSpeed => controller ? Vector3.ProjectOnPlane(controller.velocity, Vector3.up).magnitude : 0f;
        [Range(0f, 0.03f)] public float shakeAtFullDanger = 0.008f;
        [Range(0f, 0.04f)] public float shakeAtCritical = 0.014f;
        Vector3 viewRest;
        bool viewRestReady;
        DemoClue readingClue;
        CharacterController controller;
        AudioSource footstepsSource;
        float pitch;
        float fallSpeed;
        float nextUse;
        float footstepTimer;
        float crankHold;
        bool crankHeld;
        DemoMechanism crankTarget;
        Vector2 lookVelocity;
        const float FootstepInterval = 0.42f;
        public void ConfigureCamera(Vector3 position)
        {
            viewRest = position;
            viewRestReady = true;
            if (view) view.transform.localPosition = position;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            footstepsSource = gameObject.AddComponent<AudioSource>();
            footstepsSource.playOnAwake = false;
            footstepsSource.spatialBlend = 0f;
            if (view)
            {
                viewRest = view.transform.localPosition;
                viewRestReady = true;
            }
        }

        void LateUpdate()
        {
            if (!view || !viewRestReady) return;
            float amount = 0f;
            if (game && game.Playing)
            {
                float danger = game.Danger / 100f;
                amount = shakeAtFullDanger * danger;
                if (game.Danger >= game.dangerAlarmThreshold) amount += shakeAtCritical * Mathf.InverseLerp(game.dangerAlarmThreshold, 100f, game.Danger);
            }
            Vector3 offset = Vector3.zero;
            if (amount > 0.0001f)
            {
                float t = Time.time;
                offset = new Vector3(
                    Mathf.PerlinNoise(t * 17f, 2.3f + playerIndex) - 0.5f,
                    Mathf.PerlinNoise(8f + playerIndex, t * 15f) - 0.5f,
                    0f) * amount * 2f;
            }
            Vector3 pivot = transform.position + Vector3.up * 1.45f;
            Vector3 desired = transform.TransformPoint(viewRest + offset);
            Vector3 direction = desired - pivot;
            float distance = direction.magnitude;
            foreach (var hit in Physics.SphereCastAll(pivot, .16f, direction.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<DemoPlayer>() || hit.collider.GetComponentInParent<CarryObject>()) continue;
                distance = Mathf.Min(distance, Mathf.Max(.25f, hit.distance - .12f));
            }
            view.transform.position = pivot + direction.normalized * distance;
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
                Working = false;
                Stabilizing = null;
                FocusedMechanism = null;
                crankHeld = false;
                crankHold = 0f;
                crankTarget = null;
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
            pitch = Mathf.Clamp(pitch - look.y, -35f, 60f);
            view.transform.localRotation = Quaternion.Euler(pitch + 9f, 0f, 0f);
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
            Vector2 movement = Stabilizing == null && !Working ? input.Move : Vector2.zero;
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
            Working = false;
            Stabilizing = null;
            FocusedMechanism = null;
            bool wasCrankHeld = crankHeld;
            float wasCrankHold = crankHold;
            DemoMechanism wasCrankTarget = crankTarget;
            crankHeld = false;
            if (drop && Held != null)
            {
                string itemName = Held.displayName;
                Held.Drop();
                Tell(itemName + " en el suelo.");
                use = false;
                useHeld = false;
            }
            var ray = new Ray(view.transform.position, view.transform.forward);
            float cameraOffset = Vector3.Distance(view.transform.position, transform.position + Vector3.up * 1.3f);
            var hits = Physics.RaycastAll(ray, reach + cameraOffset, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<DemoPlayer>()) continue;
                if (Vector3.Distance(transform.position + Vector3.up, hit.point) > reach + .35f) break;
                var station = hit.collider.GetComponentInParent<RecintoStation>();
                var growth = hit.collider.GetComponentInParent<RecintoContamination>();
                var item = hit.collider.GetComponentInParent<CarryObject>();
                var mechanism = hit.collider.GetComponentInParent<DemoMechanism>();
                var clue = hit.collider.GetComponentInParent<DemoClue>();
                if (growth != null && !growth.Consumed)
                {
                    Hint = growth.Hint(this);
                    Working = growth.Hold(this, useHeld);
                }
                else if (station != null)
                {
                    Hint = station.Hint(this);
                    station.Interact(this, use, useHeld);
                    Working = useHeld && station.AcceptsWork;
                }
                else if (item != null && item.owner == null)
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
                    FocusedMechanism = mechanism;
                    if (mechanism.unstable)
                    {
                        if (useHeld && mechanism.done && (!Held || Held.isCandle)) { Stabilizing = mechanism; Working = true; }
                        mechanism.Hold(this, useHeld);
                        int percent = Mathf.RoundToInt(mechanism.Hold01 * 100f);
                        Hint = Held && !Held.isCandle ? mechanism.GetHint(this) : mechanism.unstable
                            ? UseHint(useHeld ? "Estabilizando " + mechanism.symbol + "  " + percent + "% (sin moverse)"
                                : "Mantener para estabilizar " + mechanism.symbol)
                            : mechanism.GetHint(this);
                    }
                    else if (mechanism.IsBraceAim(hit))
                    {
                        Hint = mechanism.BraceHint(this);
                        if (useHeld && mechanism.CanTakeRole(this))
                        {
                            mechanism.Brace(this, true);
                            Working = true;
                        }
                    }
                    else if (useHeld && mechanism.CanTakeRole(this))
                    {
                        crankHeld = true;
                        crankTarget = mechanism;
                        crankHold += Time.deltaTime;
                        mechanism.Operate(this, true);
                        Working = true;
                        Hint = mechanism.OperateHint(this);
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
                    Hint = game.Investigation && !game.Investigation.Ready
                        ? game.Investigation.NextTask
                        : readable ? UseHint(action) : "Acerca la vela para leer";
                    if (clue.nextSymbolOnly && readable && string.IsNullOrEmpty(game.NextSymbol)) Hint = "No quedan símbolos pendientes";
                    if (use && clue.TryRead(game, out string text))
                    {
                        Tell(text, 10f);
                        readingClue = clue;
                    }
                }
                break;
            }
            if (!crankHeld && !drop)
            {
                if (wasCrankHeld && !useHeld && DemoMechanism.IsShortCrankTap(wasCrankHold) && wasCrankTarget
                    && wasCrankTarget.HasCrank && !wasCrankTarget.done && Held == null)
                    wasCrankTarget.DetachCrank(this);
                crankHold = 0f;
                crankTarget = null;
            }
        }

        string UseHint(string action) => "[" + DemoPlayerInput.UseLabel(playerIndex) + "] " + action;
    }
}
