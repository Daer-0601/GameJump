using UnityEngine;
using UnityEngine.Rendering;
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
        [Min(0f)] public float mouseSensitivity = 0.16f;
        [FormerlySerializedAs("stickSensitivity")]
        [Min(0f)] public float keyboardLookSpeed = 150f;
        [Tooltip("Segundos para acelerar y frenar la vista de mouse y teclado, como un stick. Cero la deja instantánea.")]
        [Min(0f)] public float keyboardLookRampTime = 0.2f;
        [Range(0.1f, 1f)] public float preciseLookMultiplier = 0.35f;
        public float reach = 2.6f;
        public CarryObject Held { get; set; }
        string hint;
        public string Hint { get => FocusedStation ? FocusedStation.Hint(this) : hint; private set => hint = value; }
        public RecintoStation FocusedStation { get; private set; }
        public bool NeedsFreeHand { get; private set; }
        public string DropHint => Held == null ? string.Empty
            : "[" + DemoPlayerInput.DropLabel(playerIndex) + "] Soltar " + Held.displayName + " al suelo";
        public string Message { get; private set; }
        public float MessageUntil { get; private set; }
        public DemoMechanism Stabilizing { get; private set; }
        public DemoMechanism FocusedMechanism { get; private set; }
        public bool Working { get; private set; }
        public string Whisper { get; private set; }
        public float WhisperUntil { get; private set; }
        public bool HasFocus { get; private set; }
        public float FocusProgress { get; private set; } = -1f;
        public CuadernoVista Notebook { get; set; }
        public bool Reading => Notebook && Notebook.Blocking;
        public float PresentedSpeed { get; private set; }
        public float Pitch => pitch;
        public float ActualSpeed => LanAuthority.IsClientView ? PresentedSpeed
            : controller && controller.enabled ? Vector3.ProjectOnPlane(controller.velocity, Vector3.up).magnitude : PresentedSpeed;
        public string ViewHint => LanAuthority.IsClientView ? replicaHint : Hint;
        [Range(0f, 0.03f)] public float shakeAtFullDanger = 0.01f;
        [Range(0f, 0.04f)] public float shakeAtCritical = 0.018f;
        Vector3 viewRest;
        Vector3 thirdRest;
        bool viewRestReady;
        Transform head;
        Transform[] bodyNodes;
        int[] bodyLayers;
        bool bodyHidden;
        float viewBlend;
        bool firstPerson;
        Renderer[] bodyRenderers;
        const float EyeForward = 0.16f;
        const float EyeLift = 0.05f;
        static readonly Vector3 EyeFallback = new Vector3(0f, 1.62f, 0.16f);
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
        Vector2 lookRate;
        Vector2 lookRateVelocity;
        Vector2 bodyMove;
        float lean;
        float leanVelocity;
        float bobPhase;
        float yawRate;
        const float FootstepInterval = 0.42f;
        const float MaxLookRate = 280f;
        string replicaHint = "";
        Vector3 targetPos;
        float targetYaw;
        float targetPitch;
        bool hasPose;
        public void ConfigureCamera(Vector3 position)
        {
            thirdRest = position;
            viewRest = position;
            viewRestReady = true;
            if (view && viewBlend <= 0f) view.transform.localPosition = position;
        }

        public void SetFirstPerson(bool value, bool immediate)
        {
            if (firstPerson != value)
            {
                lean = 0f;
                leanVelocity = 0f;
            }
            firstPerson = value;
            if (!immediate) return;
            viewBlend = value ? 1f : 0f;
            ApplyBodyHide(viewBlend > 0.2f);
            if (!view || !viewRestReady) return;
            float shaped = Mathf.SmoothStep(0f, 1f, viewBlend);
            viewRest = Vector3.Lerp(thirdRest, EyeLocal(), shaped);
            view.transform.localPosition = viewRest;
            view.transform.localRotation = Quaternion.Euler(pitch + Mathf.Lerp(9f, 0f, shaped), 0f, 0f);
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            footstepsSource = gameObject.AddComponent<AudioSource>();
            footstepsSource.playOnAwake = false;
            footstepsSource.spatialBlend = 0f;
            var motion = GetComponent<DemoMotion>();
            head = motion && motion.visual ? FindNamed(motion.visual, "Head") : null;
            if (!head) head = FindNamed(transform, "Head");
            CacheBody();
            if (motion && motion.visual)
                bodyRenderers = motion.visual.GetComponentsInChildren<Renderer>(true);
            if (view)
            {
                thirdRest = view.transform.localPosition;
                viewRest = thirdRest;
                viewRestReady = true;
            }
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += ConcealOwnShadow;
            RenderPipelineManager.endCameraRendering += RestoreOwnShadow;
        }

        void OnDisable()
        {
            ApplyBodyHide(false);
            RenderPipelineManager.beginCameraRendering -= ConcealOwnShadow;
            RenderPipelineManager.endCameraRendering -= RestoreOwnShadow;
            SetBodyShadows(true);
        }

        // En primera persona el cuerpo queda dentro de la cámara y su sombra se estampaba adelante.
        void ConcealOwnShadow(ScriptableRenderContext context, Camera camera)
        {
            if (view && camera == view && bodyHidden) SetBodyShadows(false);
        }

        void RestoreOwnShadow(ScriptableRenderContext context, Camera camera)
        {
            if (view && camera == view) SetBodyShadows(true);
        }

        void SetBodyShadows(bool cast)
        {
            if (bodyRenderers == null) return;
            var mode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
            foreach (var renderer in bodyRenderers)
                if (renderer) renderer.shadowCastingMode = mode;
        }

        void LateUpdate()
        {
            if (!view || !viewRestReady) return;
            float target = firstPerson ? 1f : 0f;
            viewBlend = Mathf.MoveTowards(viewBlend, target, Time.unscaledDeltaTime * 3.2f);
            float shaped = Mathf.SmoothStep(0f, 1f, viewBlend);
            viewRest = Vector3.Lerp(thirdRest, EyeLocal(), shaped);
            ApplyBodyHide(viewBlend > 0.2f);
            float roll = 0f;
            Vector3 cinematic = Vector3.zero;
            if (game && game.Playing && shaped > 0.02f)
                cinematic = CinematicOffset(out roll) * shaped;
            view.transform.localRotation = Quaternion.Euler(pitch + Mathf.Lerp(9f, 0f, shaped), 0f, roll);

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
            if (shaped >= 0.85f)
            {
                view.transform.localPosition = viewRest + offset + cinematic;
                return;
            }
            Vector3 desired = transform.TransformPoint(viewRest + offset + cinematic);
            Vector3 pivot = transform.position + Vector3.up * 1.45f;
            Vector3 direction = desired - pivot;
            float distance = direction.magnitude;
            if (distance > 0.02f)
            {
                bool close = shaped >= 0.65f;
                float radius = close ? 0.08f : 0.16f;
                float minimum = close ? 0.05f : 0.25f;
                float inset = close ? 0.05f : 0.12f;
                foreach (var hit in Physics.SphereCastAll(pivot, radius, direction.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<DemoPlayer>() || hit.collider.GetComponentInParent<CarryObject>()) continue;
                    distance = Mathf.Min(distance, Mathf.Max(minimum, hit.distance - inset));
                }
                desired = pivot + direction.normalized * distance;
            }
            view.transform.position = desired;
        }

        // Desplazamiento y roll solo en la cámara. El rig no se mueve. Quieto, no hay balanceo.
        Vector3 CinematicOffset(out float roll)
        {
            float dt = Time.deltaTime;
            float speed = ActualSpeed < 0.35f ? 0f : ActualSpeed;
            float stride = moveSpeed > 0.01f ? Mathf.Clamp01(speed / moveSpeed) : 0f;
            float strafe = stride > 0f ? bodyMove.x : 0f;
            lean = Mathf.SmoothDamp(lean, strafe, ref leanVelocity, 0.18f, Mathf.Infinity, dt);
            if (stride == 0f && Mathf.Abs(lean) < 0.001f) lean = 0f;
            bobPhase += speed * dt * 2.15f;
            float sway = Mathf.Sin(bobPhase) * 0.034f * stride;
            float bob = Mathf.Sin(bobPhase * 2f) * 0.014f * stride;
            roll = stride == 0f && Mathf.Abs(lean) < 0.001f
                ? 0f
                : Mathf.Clamp(-lean * 3.4f - yawRate * 0.016f, -5.5f, 5.5f);
            return new Vector3(lean * 0.085f + sway, bob, 0f);
        }

        Vector3 EyeLocal()
        {
            if (!head) return EyeFallback;
            Vector3 eye = head.position + transform.up * EyeLift + transform.forward * EyeForward;
            return transform.InverseTransformPoint(eye);
        }

        void CacheBody()
        {
            var motion = GetComponent<DemoMotion>();
            if (!motion || !motion.visual) return;
            bodyNodes = motion.visual.GetComponentsInChildren<Transform>(true);
            bodyLayers = new int[bodyNodes.Length];
            for (int i = 0; i < bodyNodes.Length; i++)
                bodyLayers[i] = bodyNodes[i] ? bodyNodes[i].gameObject.layer : 0;
        }

        void ApplyBodyHide(bool hide)
        {
            if (hide == bodyHidden || bodyNodes == null) return;
            bodyHidden = hide;
            int hiddenLayer = playerIndex == 0 ? 8 : 9;
            for (int i = 0; i < bodyNodes.Length; i++)
            {
                if (!bodyNodes[i]) continue;
                bodyNodes[i].gameObject.layer = hide ? hiddenLayer : bodyLayers[i];
            }
        }

        static Transform FindNamed(Transform root, string bone)
        {
            if (!root) return null;
            if (string.Equals(root.name, bone, System.StringComparison.OrdinalIgnoreCase)) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindNamed(root.GetChild(i), bone);
                if (found) return found;
            }
            return null;
        }

        // Mensaje propio de este jugador: se ve unos segundos bajo la mira.
        public void Tell(string text, float seconds = 3f)
        {
            Log(text, seconds);
            Whisper = text;
            WhisperUntil = Time.unscaledTime + Mathf.Min(seconds, 4.5f);
        }

        // Aviso compartido: queda anotado en el cuaderno, no se dibuja en pantalla.
        public void Log(string text, float seconds = 3f)
        {
            readingClue = null;
            Message = text;
            MessageUntil = Time.unscaledTime + seconds;
            if (!LanEcho.Applying && LanAuthority.IsHost && (game == null || !game.TalkingNested))
                LanSession.BroadcastTell(playerIndex, text, seconds);
        }

        public void SetReplicaFocus(string hint, DemoMechanism mechanism, RecintoStation station, bool working, bool stabilizing, bool needsHand)
        {
            replicaHint = hint ?? "";
            FocusedMechanism = mechanism;
            FocusedStation = station;
            Working = working;
            Stabilizing = stabilizing ? mechanism : null;
            NeedsFreeHand = needsHand;
        }

        public void SetReplicaPose(Vector3 position, float yaw, float lookPitch, bool snap)
        {
            targetPos = position;
            targetYaw = yaw;
            targetPitch = lookPitch;
            hasPose = true;
            if (!snap) return;
            Vector3 previous = transform.position;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            pitch = lookPitch;
            float step = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.033f;
            PresentedSpeed = Vector3.ProjectOnPlane(position - previous, Vector3.up).magnitude / step;
        }

        void Update()
        {
            if (LanAuthority.IsClientView)
            {
                PresentReplica();
                return;
            }
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
                FocusedStation = null;
                NeedsFreeHand = false;
                crankHeld = false;
                crankHold = 0f;
                crankTarget = null;
                lookRate = Vector2.zero;
                lookRateVelocity = Vector2.zero;
                bodyMove = Vector2.zero;
                yawRate = 0f;
                HasFocus = false;
                FocusProgress = -1f;
                return;
            }
            if (Reading)
            {
                ClearFocus();
                Look(Vector2.zero);
                Move(default);
                return;
            }
            DemoPlayerInput.State input = LanAuthority.LanMatch
                ? (playerIndex == 0
                    ? DemoPlayerInput.Read(0, keyboardLookSpeed, mouseSensitivity, Time.deltaTime, preciseLookMultiplier)
                    : LanFlow.ConsumeRemote())
                : DemoPlayerInput.Read(playerIndex, keyboardLookSpeed, mouseSensitivity, Time.deltaTime, preciseLookMultiplier);
            Look(input.Look, !LanAuthority.LanMatch && playerIndex == 1);
            Interact(input.Use, input.UseHeld, input.Drop);
            if (game.PlayerInputEnabled) Move(input);
        }

        void ClearFocus()
        {
            Hint = string.Empty;
            Working = false;
            Stabilizing = null;
            FocusedMechanism = null;
            FocusedStation = null;
            NeedsFreeHand = false;
            HasFocus = false;
            FocusProgress = -1f;
            crankHeld = false;
            crankHold = 0f;
            crankTarget = null;
        }

        void PresentReplica()
        {
            if (!hasPose) return;
            if (playerIndex != LanAuthority.LocalSlot)
            {
                Vector3 previous = transform.position;
                float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.045f);
                transform.position = Vector3.Lerp(previous, targetPos, blend);
                float yaw = Mathf.LerpAngle(transform.eulerAngles.y, targetYaw, blend);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                pitch = Mathf.Lerp(pitch, targetPitch, blend);
                float step = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
                PresentedSpeed = Vector3.ProjectOnPlane(transform.position - previous, Vector3.up).magnitude / step;
            }
            if (!game || !game.Playing || PresentedSpeed < 0.45f)
            {
                footstepTimer = 0f;
                return;
            }
            footstepTimer -= Time.unscaledDeltaTime;
            if (footstepTimer > 0f) return;
            game.PlayFootstep(footstepsSource);
            footstepTimer = FootstepInterval;
        }

        void Look(Vector2 look, bool ramp)
        {
            if (ramp)
            {
                Look(look);
                return;
            }
            transform.Rotate(0f, look.x, 0f);
            float down = viewBlend > 0.5f ? -75f : -35f;
            float up = viewBlend > 0.5f ? 75f : 60f;
            pitch = Mathf.Clamp(pitch - look.y, down, up);
            if (!view) return;
            float shaped = Mathf.SmoothStep(0f, 1f, viewBlend);
            view.transform.localRotation = Quaternion.Euler(pitch + Mathf.Lerp(9f, 0f, shaped), 0f, 0f);
        }

        void Look(Vector2 look)
        {
            float dt = Time.deltaTime;
            if (!view || dt <= 0f)
            {
                lookRate = Vector2.zero;
                yawRate = 0f;
                return;
            }
            Vector2 targetRate = Vector2.ClampMagnitude(look / dt, MaxLookRate);
            lookRate = keyboardLookRampTime <= 0f
                ? targetRate
                : Vector2.SmoothDamp(lookRate, targetRate, ref lookRateVelocity, keyboardLookRampTime, Mathf.Infinity, dt);
            Vector2 step = lookRate * dt;
            transform.Rotate(0f, step.x, 0f);
            float down = viewBlend > 0.5f ? -75f : -35f;
            float up = viewBlend > 0.5f ? 75f : 60f;
            pitch = Mathf.Clamp(pitch - step.y, down, up);
            yawRate = step.x / dt;
            float shaped = Mathf.SmoothStep(0f, 1f, viewBlend);
            view.transform.localRotation = Quaternion.Euler(pitch + Mathf.Lerp(9f, 0f, shaped), 0f, 0f);
        }

        void Move(DemoPlayerInput.State input)
        {
            if (controller.isGrounded && fallSpeed < 0f) fallSpeed = -2f;
            fallSpeed += Physics.gravity.y * Time.deltaTime;
            float speedMultiplier = input.Run && Stabilizing == null ? Mathf.Max(1f, runMultiplier) : 1f;
            if (Held != null && !Held.isCandle) speedMultiplier *= Mathf.Clamp(crankSpeedMultiplier, 0.1f, 1f);
            bodyMove = Stabilizing == null && !Working ? input.Move : Vector2.zero;
            Vector2 movement = bodyMove;
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
            FocusedStation = null;
            NeedsFreeHand = false;
            HasFocus = false;
            FocusProgress = -1f;
            bool wasCrankHeld = crankHeld;
            float wasCrankHold = crankHold;
            DemoMechanism wasCrankTarget = crankTarget;
            crankHeld = false;
            if (drop && Held != null)
            {
                string itemName = Held.displayName;
                Held.Drop();
                Tell(itemName + " en el suelo.");
                if (game) game.NotifyObjectDropped(playerIndex);
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
                    HasFocus = true;
                    NeedsFreeHand = Held && !Held.isCandle;
                    Hint = growth.Hint(this);
                    Working = growth.Hold(this, useHeld);
                    if (growth.Burn01 > 0f) FocusProgress = growth.Burn01;
                    if (use && !(Held && Held.isCandle))
                        Tell(Held ? "Con la vela en la mano. Suelta " + Held.displayName + " con " + DemoPlayerInput.DropLabel(playerIndex) + "."
                            : "Hace falta la vela en la mano para quemarlo.");
                }
                else if (station != null)
                {
                    HasFocus = true;
                    FocusedStation = station;
                    NeedsFreeHand = station.Contaminated && Held && !Held.isCandle;
                    station.Interact(this, use, useHeld);
                    Working = useHeld && station.AcceptsWork;
                    if (station.Progress01 > 0f) FocusProgress = station.Progress01;
                }
                else if (item != null && item.owner == null)
                {
                    HasFocus = true;
                    NeedsFreeHand = Held != null;
                    Hint = Held == null ? UseHint("Recoger " + item.displayName) : "Manos ocupadas: suelta el objeto primero";
                    if (use && Held == null)
                    {
                        item.PickUp(this);
                        Hint = string.Empty;
                    }
                    else if (use) Tell("Las manos están ocupadas. " + DemoPlayerInput.DropLabel(playerIndex) + " suelta " + Held.displayName + ".");
                }
                else if (mechanism != null)
                {
                    HasFocus = true;
                    FocusedMechanism = mechanism;
                    if (Held)
                    {
                        if (mechanism.unstable) NeedsFreeHand = !Held.isCandle;
                        else if (mechanism.IsBraceAim(hit)) NeedsFreeHand = mechanism.HasCrank && !Held.isCandle;
                        else if (mechanism.done || !game.Investigation || game.Investigation.CanAttempt)
                            NeedsFreeHand = mechanism.HasCrank ? mechanism.done || !Held.isCandle : Held.isCandle;
                    }
                    if (mechanism.unstable)
                    {
                        if (useHeld && mechanism.done && (!Held || Held.isCandle)) { Stabilizing = mechanism; Working = true; }
                        mechanism.Hold(this, useHeld);
                        if (mechanism.Hold01 > 0f) FocusProgress = mechanism.Hold01;
                        Hint = Held && !Held.isCandle ? mechanism.GetHint(this) : UseHint("Mantener para estabilizar " + mechanism.symbol);
                        if (use && Held && !Held.isCandle)
                            Tell("Sin la manivela en la mano. " + DemoPlayerInput.DropLabel(playerIndex) + " la deja en el suelo.");
                    }
                    else if (mechanism.IsBraceAim(hit))
                    {
                        Hint = mechanism.BraceHint(this);
                        if (useHeld && mechanism.CanTakeRole(this))
                        {
                            mechanism.Brace(this, true);
                            Working = true;
                        }
                        if (mechanism.Cooperation01 > 0f) FocusProgress = mechanism.Cooperation01;
                    }
                    else if (useHeld && mechanism.CanTakeRole(this))
                    {
                        crankHeld = true;
                        crankTarget = mechanism;
                        crankHold += Time.deltaTime;
                        mechanism.Operate(this, true);
                        Working = true;
                        Hint = mechanism.OperateHint(this);
                        if (mechanism.Cooperation01 > 0f) FocusProgress = mechanism.Cooperation01;
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
                    HasFocus = true;
                    bool readable = clue.CanRead(game);
                    Hint = game.Investigation && !game.Investigation.Ready
                        ? game.Investigation.NextTask
                        : readable ? UseHint(clue.nextSymbolOnly ? "Leer siguiente símbolo" : "Leer la inscripción") : "Acerca la vela para leer";
                    if (use && clue.TryRead(game, out string text))
                    {
                        Tell(text, 10f);
                        readingClue = clue;
                        CuadernoContenido.PlacaLeida(game, text);
                    }
                    else if (use)
                        Tell(game.Investigation && !game.Investigation.Ready
                            ? "La placa está cubierta de mugre. Primero lo del cuaderno."
                            : "Sin la vela cerca no se distinguen las letras.");
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
