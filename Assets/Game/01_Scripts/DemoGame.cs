using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

namespace Gamejam2026
{
    [DefaultExecutionOrder(-100)]
    public class DemoGame : MonoBehaviour
    {
        const int MaxName = 18;
        const string NameKey1 = "AOscuras.Name1";
        const string NameKey2 = "AOscuras.Name2";
        const string MasterKey = "AOscuras.VolMaster";
        const string AmbienceKey = "AOscuras.VolAmb";
        const string SfxKey = "AOscuras.VolSfx";
        const string FullKey = "AOscuras.Fullscreen";
        const string WidthKey = "AOscuras.ResW";
        const string HeightKey = "AOscuras.ResH";
        const string CameraKey = "AOscuras.Camera";
        const string GameplayHelp = DemoPlayerInput.Help;

        public DemoPlayer[] players;
        public DemoMechanism[] mechanisms;
        public CarryObject[] candles;
        public Light[] atmosphereLights;
        public AudioSource ambience;
        public AudioClip walkingClip;
        public AudioClip lightClip;
        public AudioClip jumpScareClip;
        public AudioClip dangerClip;
        public AudioClip gearsClip;
        public Texture2D menuBackground;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Range(0f, 100f)] public float dangerAlarmThreshold = 70f;
        public float dangerAlarmInterval = 5f;
        [Range(80f, 99f)] public float criticalDanger = 92f;
        [Range(0.4f, 1f)] public float hitchScale = 0.8f;
        public float hitchSeconds = 0.12f;
        [Range(0f, 1f)] public float dangerPerSecond = 0.04f;
        [Range(0f, 1f)] public float darknessPenalty = 0.03f;
        public float errorPenalty = 12f;
        public float successRelief = 10f;
        public float unstablePenalty = 1.25f;
        public float stabilizeRelief = 8f;
        public float destabilizeMin = 38f;
        public float destabilizeMax = 52f;
        public bool Playing => started && !paused && !finished;
        public bool Finished => finished;
        public bool PlayerInputEnabled => Playing && Time.frameCount > inputResumeFrame;
        public float Danger { get; private set; }
        public float Disturbance => Mathf.Clamp01((disturbanceUntil - elapsed) / 1.4f);
        public string SequenceText => "ORDEN: " + mechanisms[order[0]].symbol + " > " + mechanisms[order[1]].symbol + " > " + mechanisms[order[2]].symbol;
        public float progress => completed / 3f;
        public int CompletedCount => completed;
        public float ElapsedSeconds => elapsed;
        public RecintoInvestigation Investigation { get; private set; }
        public string SymbolAt(int index) => index >= 0 && index < order.Length ? mechanisms[order[index]].symbol : string.Empty;
        public string NextSymbol => completed < order.Length ? mechanisms[order[completed]].symbol : string.Empty;
        public event Action<DemoMechanism> FirstMechanismCompleted;
        public event Action<bool> RoundFinished;
        public float CandleCharge { get; private set; } = 1f;
        public float CandleRange => Mathf.Lerp(2.8f, 7f, CandleCharge);
        public bool Containing { get; private set; }
        public float Containment01 { get; private set; }
        public int ShadowContacts { get; private set; }
        public string DefeatCause { get; private set; } = "errores de procedimiento";
        public bool CandleBurst => Containing && elapsed % 4.7f < .28f;
        string threatText;
        float threatUntil;
        float contactFlashUntil;
        public void WarnThreat(string message)
        {
            threatText = message; threatUntil = elapsed + 7;
            Notify(message, 7);
        }
        public string DangerWords => Danger >= 90 ? "crítico" : Danger >= dangerAlarmThreshold ? "inestable" : "en observación";
        public static readonly Vector3 WorkshopSeal = new Vector3(-20f, .05f, 105f);
        public static readonly Vector3 PumpSeal = new Vector3(34f, .05f, 109f);
        public string ContainmentHint => "Contención: uno en la sala de TALLER 04 y otro en BOMBAS 05; permanezcan dentro de salas distintas";
        public static Bounds SealRoom(bool workshop) => workshop
            ? new Bounds(new Vector3(-19, 1, 101), new Vector3(18, 4, 22))
            : new Bounds(new Vector3(33, 1, 108), new Vector3(14, 4, 16));
        public bool InSeal(DemoPlayer player, bool workshop)
        {
            if (!player) return false;
            return SealRoom(workshop).Contains(player.transform.position);
        }
        public bool SealsOccupied => players.Length == 2 && ((InSeal(players[0], true) && InSeal(players[1], false)) || (InSeal(players[1], true) && InSeal(players[0], false)));
        public void Notify(string message, float seconds = 5f)
        {
            foreach (var p in players) if (p) p.Tell(message, seconds);
        }
        public void RestoreCandle()
        {
            if (!Playing) return;
            CandleCharge = Mathf.Clamp01(CandleCharge + 30f / 360f);
            Notify("Brote consumido. La vela recupera alcance (+30 s de cera).", 4);
        }
        public void AddDanger(float amount, string cause)
        {
            if (!Playing || amount == 0f) return;
            DefeatCause = cause;
            Danger = Mathf.Clamp(Danger + amount, 0, 100);
            NoteDangerBand();
            if (Danger >= 100) Finish(false);
        }

        void AddPressure(float amount, string causeIfFatal)
        {
            if (!Playing || amount <= 0f) return;
            bool fatal = Danger + amount >= 100f;
            if (fatal) DefeatCause = causeIfFatal;
            Danger = Mathf.Clamp(Danger + amount, 0, 100);
            NoteDangerBand();
            if (fatal) Finish(false);
        }

        int CountPlayersOutsideCandle()
        {
            if (players == null || Investigation == null) return 0;
            int count = 0;
            foreach (var player in players)
            {
                if (player && !Investigation.HasCandleLight(player.transform.position + Vector3.up)) count++;
            }
            return count;
        }
        public void ShadowContact()
        {
            if (!Playing) return;
            ShadowContacts++;
            if (ShadowContacts > 1) { DefeatCause = "segundo contacto con la sombra fuera de la vela"; Finish(false); return; }
            if (atmosphere) atmosphere.WrongOrder();
            contactFlashUntil = elapsed + .08f;
            foreach (var p in players) { var motion = p.GetComponent<DemoMotion>(); if (motion) motion.Recoil(); }
            Notify("La sombra os rozó. Ha retrocedido. Otro contacto fuera de la vela termina la expedición. +15%", 7);
            AddDanger(15, "contacto con la sombra");
        }

        readonly bool[] helpDropDismissed = new bool[2];
        readonly bool[] helpContrastDismissed = new bool[2];
        readonly bool[] helpStabilizeDismissed = new bool[2];

        public void ResetLearningTips()
        {
            Array.Clear(helpDropDismissed, 0, helpDropDismissed.Length);
            Array.Clear(helpContrastDismissed, 0, helpContrastDismissed.Length);
            Array.Clear(helpStabilizeDismissed, 0, helpStabilizeDismissed.Length);
        }

        public void NotifyObjectDropped(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < helpDropDismissed.Length)
                helpDropDismissed[playerIndex] = true;
        }

        public void NotifyContrastCompleted(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < helpContrastDismissed.Length)
                helpContrastDismissed[playerIndex] = true;
        }

        public string WorkshopOccupancyString()
        {
            bool j1 = players.Length > 0 && InSeal(players[0], true);
            bool j2 = players.Length > 1 && InSeal(players[1], true);
            if (j1 && j2) return "TALLER 04: J1 + J2";
            if (j1) return "TALLER 04: J1";
            if (j2) return "TALLER 04: J2";
            return "TALLER 04: VACÍO";
        }

        public string PumpOccupancyString()
        {
            bool j1 = players.Length > 0 && InSeal(players[0], false);
            bool j2 = players.Length > 1 && InSeal(players[1], false);
            if (j1 && j2) return "BOMBAS 05: J1 + J2";
            if (j1) return "BOMBAS 05: J1";
            if (j2) return "BOMBAS 05: J2";
            return "BOMBAS 05: VACÍO";
        }

        public string ContainmentStatusText(out bool isHolding)
        {
            int loose = CountUnstable();
            bool j1Workshop = players.Length > 0 && InSeal(players[0], true);
            bool j1Pump = players.Length > 0 && InSeal(players[0], false);
            bool j2Workshop = players.Length > 1 && InSeal(players[1], true);
            bool j2Pump = players.Length > 1 && InSeal(players[1], false);

            bool workshopOccupied = j1Workshop || j2Workshop;
            bool pumpOccupied = j1Pump || j2Pump;
            bool bothWorkshop = j1Workshop && j2Workshop;
            bool bothPump = j1Pump && j2Pump;

            isHolding = SealsOccupied && loose == 0;

            if (isHolding)
                return "AVANZANDO (+4%/s) — Ambas salas ocupadas y sin averías activas.";

            if (loose > 0 && !SealsOccupied)
                return "RETROCEDE: " + loose + " mecanismo(s) desajustado(s) y salas sin cubrir. Estabilizad y cubrid ambas salas.";

            if (loose > 0)
                return "RETROCEDE: Avería activa en el recinto (" + loose + " desajustado" + (loose > 1 ? "s" : "") + "). Estabilizad para continuar.";

            if (bothWorkshop)
                return "RETROCEDE: Ambos operarios están en TALLER 04. Un operario debe vigilar BOMBAS 05.";

            if (bothPump)
                return "RETROCEDE: Ambos operarios están en BOMBAS 05. Un operario debe vigilar TALLER 04.";

            if (!workshopOccupied && !pumpOccupied)
                return "RETROCEDE: Ambas salas desiertas. Un operario a TALLER 04 y otro a BOMBAS 05.";

            if (!workshopOccupied)
                return "RETROCEDE: TALLER 04 desatendido. Un operario debe permanecer dentro.";

            if (!pumpOccupied)
                return "RETROCEDE: BOMBAS 05 desatendido. Un operario debe permanecer dentro.";

            return "RETROCEDE: Cubrid ambas salas a la vez y reparad desajustes para contener.";
        }

        public string GetPlayerTip(int i)
        {
            if (i < 0 || i >= players.Length || players[i] == null) return null;
            var player = players[i];
            int index = player.playerIndex;
            if (index < 0 || index >= helpDropDismissed.Length) return null;
            string useKey = DemoPlayerInput.UseLabel(index);
            string dropKey = DemoPlayerInput.DropLabel(index);

            if (player.Held && player.NeedsFreeHand && !helpDropDismissed[index])
                return "AYUDA: [" + dropKey + "] suelta " + player.Held.displayName + " al suelo para liberar la mano.";

            if (!helpStabilizeDismissed[index] && player.FocusedMechanism && player.FocusedMechanism.unstable)
            {
                return "AYUDA: sin manivela, mantén [" + useKey + "] 1,5 s para estabilizar. Soltar o apartar la mirada interrumpe.";
            }

            if (!helpContrastDismissed[index] && Investigation && Investigation.EvidenceCount == 3 && !Investigation.Contrasted
                && player.FocusedStation && player.FocusedStation.IsContrastStation && !player.FocusedStation.Contaminated)
            {
                string otherKey = DemoPlayerInput.UseLabel(1 - index);
                return "AYUDA: mantén [" + useKey + "] y tu compañero [" + otherKey + "] en esta consola durante 2,5 s para corregir el dato falso.";
            }

            return null;
        }

        int[] order = { 0, 1, 2 };
        int completed;
        bool started;
        bool paused;
        bool finished;
        bool won;
        bool transitioning;
        int inputResumeFrame;
        float elapsed;
        float nextDisturbance;
        float disturbanceUntil;
        float nextDangerAlarm;
        float nextDestabilize = -1f;
        float hitchUntil;
        bool hitchSpent;
        int dangerBand = -1;
        AudioSource roomBed;
        float roomBedVolume = 0.823f;
        bool roomBedCached;
        string pulseText = "";
        float pulseUntil;
        int pulsePriority = -1;
        AudioSource sfxSource;
        DemoAtmosphere atmosphere;
        AudioClip errorClip;

        string name1 = "";
        string name2 = "";
        int overlay = -2;
        int focusIndex;
        int hover = -1;
        int caret;
        int hitCount;
        int pointerFrame = -1;
        bool keyboardEdit;
        bool optionsOpen;
        bool fullscreen;
        bool firstPerson;
        float masterVolume = 1f;
        float ambienceVolume = 1f;
        Resolution[] displayModes = System.Array.Empty<Resolution>();
        int resolutionIndex;
        float axisTimerX;
        float axisTimerY;
        float backspaceTimer;
        MenuCommand command;
        Keyboard textKeyboard;

        readonly Rect[] hits = new Rect[7];
        readonly Rect[] visuals = new Rect[7];
        Rect columnRect;
        Rect titleRect;
        Rect briefRect;
        Rect statusRect;
        Rect helpRect;

        GUIStyle titleStyle;
        GUIStyle glyphStyle;
        GUIStyle bodyStyle;
        GUIStyle captionStyle;
        GUIStyle optionStyle;
        GUIStyle fieldStyle;
        GUIStyle smallStyle;
        GUIStyle hudStyle;
        GUIStyle hudSmallStyle;
        bool stylesReady;
        Font serif;
        Texture2D grain;
        Texture2D vignette;

        static readonly Color Paper = new Color(0.86f, 0.82f, 0.74f, 1f);
        static readonly Color Idle = new Color(0.72f, 0.68f, 0.60f, 1f);
        static readonly Color Ash = new Color(0.58f, 0.54f, 0.48f, 1f);
        static readonly Color DisabledInk = new Color(0.46f, 0.44f, 0.40f, 1f);
        static readonly Color Wash = new Color(0.012f, 0.011f, 0.01f, 0.9f);
        static readonly Color Accent = new Color(0.62f, 0.16f, 0.12f, 1f);

        static class Memory
        {
            public static bool AutoStart;
        }

        enum MenuCommand { None, Begin, Continue, Retry, MainMenu, Quit }

        void OnEnable()
        {
            BindTextKeyboard();
        }

        void OnDisable()
        {
            if (textKeyboard != null) textKeyboard.onTextInput -= OnNameCharacter;
            textKeyboard = null;
        }

        void Start()
        {
            Time.timeScale = 1f;
            LoadSession();
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            nextDisturbance = 12f;
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.volume = 1f;
            if (dangerClip)
            {
                dangerClip.LoadAudioData();
                int frames = Mathf.Min(dangerClip.samples, Mathf.RoundToInt(dangerClip.frequency * 0.35f));
                var samples = new float[frames * dangerClip.channels];
                if (dangerClip.GetData(samples, 0))
                {
                    for (int frame = 0; frame < frames; frame++)
                    {
                        float fade = Mathf.Min(1f, Mathf.Min(frame, frames - frame - 1) / (dangerClip.frequency * 0.02f));
                        for (int c = 0; c < dangerClip.channels; c++) samples[frame * dangerClip.channels + c] *= fade;
                    }
                    errorClip = AudioClip.Create("Error breve - alarma existente", frames, dangerClip.channels, dangerClip.frequency, false);
                    errorClip.SetData(samples, 0);
                }
            }
            EquipCrew();
            atmosphere = GetComponent<DemoAtmosphere>();
            if (!atmosphere) atmosphere = gameObject.AddComponent<DemoAtmosphere>();
            atmosphere.Initialize(this);
            Investigation = gameObject.AddComponent<RecintoInvestigation>();
            Investigation.Initialize(this);
            ResetLearningTips();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (ambience) ambience.Play();
            ApplyAudio();

            bool pendingStart = Memory.AutoStart;
            Memory.AutoStart = false;
            if (pendingStart) Begin();
            ApplyPerspective(true);
        }

        public void Begin()
        {
            if (transitioning) return;
            ResetLearningTips();
            name1 = CleanName(name1, "Jugador 1");
            name2 = CleanName(name2, "Jugador 2");
            SaveNames();
            StopEdit(false);
            started = true;
            inputResumeFrame = Time.frameCount;
            LockPointer();
        }

        void LockPointer()
        {
            bool capture = Playing;
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        void Update()
        {
            BindTextKeyboard();
            ExecuteCommand();
            if (!this) return;

            RefreshOverlay();
            if (overlay >= 0) PollMenu();
            else if (started && !finished && EscapeDown()) PauseNow();

            if (overlay == 2 && RDown()) Queue(MenuCommand.Retry);
            ExecuteCommand();
            if (!this) return;

            RefreshOverlay();
            if (overlay < 0) Simulate();
            ApplyAmbience();
            if (hitchUntil > 0f && (paused || finished || Time.unscaledTime >= hitchUntil))
            {
                hitchUntil = 0f;
                if (!paused && !finished) Time.timeScale = 1f;
            }
        }

        void Simulate()
        {
            if (!Playing) return;

            elapsed += Time.deltaTime;
            CandleCharge = Mathf.Max(0, CandleCharge - Time.deltaTime / 360f);
            DestabilizeDue();
            if (!Playing) return;
            int loose = CountUnstable();
            float unstableRate = loose > 1 ? unstablePenalty * (1f + (loose - 1) * 1.6f) : loose * unstablePenalty;
            if (unstableRate > 0f) AddDanger(unstableRate * Time.deltaTime, "mecanismos desajustados sin estabilizar");
            if (!Playing) return;
            int unlit = CountPlayersOutsideCandle();
            float passive = Mathf.Max(0f, dangerPerSecond) + unlit * Mathf.Max(0f, darknessPenalty);
            if (passive > 0f)
            {
                string cause = unstableRate > 0f
                    ? "mecanismos desajustados sin estabilizar"
                    : unlit > 0 ? "permanecer a oscuras" : "el avance del peligro";
                AddPressure(passive * Time.deltaTime, cause);
            }
            if (!Playing) return;
            if (Containing)
            {
                bool holding = loose == 0 && SealsOccupied;
                Containment01 = Mathf.Clamp01(Containment01 + Time.deltaTime * (holding ? 1f : -1.7f) / 25f);
                if (Containment01 >= 1f) { Finish(true); return; }
            }
            NoteDangerBand();
            if (!hitchSpent && Danger >= criticalDanger && Time.timeScale > 0.95f)
            {
                hitchSpent = true;
                hitchUntil = Time.unscaledTime + Mathf.Max(0.04f, hitchSeconds);
                Time.timeScale = hitchScale;
            }
            if (Danger >= dangerAlarmThreshold && elapsed >= nextDangerAlarm)
            {
                PlaySfx(dangerClip);
                nextDangerAlarm = elapsed + dangerAlarmInterval;
            }
            if (Danger >= 100f) Finish(false);

            if (elapsed >= nextDisturbance)
            {
                disturbanceUntil = elapsed + 1.4f;
                nextDisturbance = elapsed + UnityEngine.Random.Range(14f, 24f) * Mathf.Lerp(1f, 0.6f, Danger / 100f);
            }
        }

        public void Activate(DemoMechanism mechanism, DemoPlayer player)
        {
            if (!Playing || !mechanism || mechanism.done || completed >= order.Length) return;
            if (Investigation && !Investigation.CanAttempt)
            {
                if (player) player.Tell(Investigation.NextTask);
                return;
            }
            bool unverified = Investigation && !Investigation.Contrasted;
            if (unverified || mechanism.id != order[completed])
            {
                AddDanger(errorPenalty, "errores de orden en los mecanismos");
                disturbanceUntil = elapsed + 2f;
                if (atmosphere) atmosphere.WrongOrder();
                PlaySfx(errorClip);
                string consequence = Investigation ? Investigation.OnWrongProcedure() : "";
                Notify("¡CLAC! " + (unverified ? "Datos sin contrastar: procedimiento incorrecto" : "Orden incorrecto") + " (+12%). " + consequence + " Los aciertos se conservan.", 7);
                if (Danger >= 100f) Finish(false);
                return;
            }
            if (Investigation && !Investigation.Ready)
            {
                if (player) player.Tell(Investigation.NextTask);
                return;
            }
            mechanism.done = true;
            completed++;
            if (completed == 1)
            {
                FirstMechanismCompleted?.Invoke(mechanism);
                Pulse("La compuerta se abre", 2);
                Notify("¡GOLPE EN LA COMPUERTA! El primer sello ha abierto el paso.", 5);
            }
            if (nextDestabilize < 0f) ScheduleDestabilize();
            PlaySfx(gearsClip);
            PlaySfx(lightClip);
            Danger = Mathf.Max(0f, Danger - successRelief);
            string note = mechanism.symbol + " completado. " + completed + "/3";
            if (completed == order.Length && AnyUnstable()) note = "Secuencia lista. Falta estabilizar un mecanismo.";
            foreach (var teammate in players) teammate.Tell(note);
            TryWin();
        }

        public void OnStabilized(DemoMechanism mechanism, DemoPlayer player)
        {
            if (!Playing || mechanism == null || player == null) return;
            if (player.playerIndex >= 0 && player.playerIndex < helpStabilizeDismissed.Length)
                helpStabilizeDismissed[player.playerIndex] = true;
            PlaySfx(gearsClip);
            Pulse(mechanism.symbol + " estable", 1);
            Danger = Mathf.Max(0f, Danger - stabilizeRelief);
            foreach (var teammate in players) teammate.Tell("¡CLONC! " + mechanism.symbol + " estabilizado. −" + stabilizeRelief.ToString("0") + "% peligro.");
            TryWin();
        }

        void TryWin()
        {
            if (completed != order.Length || AnyUnstable() || Containing || finished) return;
            Containing = true;
            Containment01 = 0;
            Notify(ContainmentHint + ". Sostengan 25 segundos.", 10);
        }

        void ScheduleDestabilize()
        {
            float wait = UnityEngine.Random.Range(Mathf.Min(destabilizeMin, destabilizeMax), Mathf.Max(destabilizeMin, destabilizeMax));
            nextDestabilize = elapsed + wait;
        }

        void DestabilizeDue()
        {
            if (nextDestabilize < 0f || elapsed < nextDestabilize || mechanisms == null) return;
            DemoMechanism pick = null;
            int available = 0;
            foreach (var mechanism in mechanisms)
            {
                if (!mechanism || !mechanism.done || mechanism.unstable) continue;
                available++;
                if (UnityEngine.Random.Range(0, available) == 0) pick = mechanism;
            }
            if (pick != null)
            {
                pick.unstable = true;
                disturbanceUntil = elapsed + 1.6f;
                PlaySfx(dangerClip);
                Pulse(pick.symbol + " desajustado", 2);
                Notify(pick.symbol + " se desajustó. " + (CountUnstable() > 1 ? "DOS averías: el peligro sube más rápido. " : "") + "Mantener 1,5 s, quieto y sin manivela.", 7);
            }
            ScheduleDestabilize();
        }

        bool AnyUnstable()
        {
            return CountUnstable() > 0;
        }

        int CountUnstable()
        {
            if (mechanisms == null) return 0;
            int count = 0;
            foreach (var mechanism in mechanisms)
                if (mechanism && mechanism.unstable) count++;
            return count;
        }

        void EquipCrew()
        {
            CarryObject crank = null;
            foreach (var item in FindObjectsByType<CarryObject>(FindObjectsInactive.Exclude))
            {
                if (item != null && !item.isCandle)
                {
                    crank = item;
                    break;
                }
            }

            if (players != null && players.Length > 0 && candles != null && candles.Length > 0 && candles[0] != null)
                candles[0].PickUp(players[0]);

            if (candles != null)
            {
                for (int i = 1; i < candles.Length; i++)
                {
                    if (candles[i] == null) continue;
                    if (candles[i].candleLight) candles[i].candleLight.enabled = false;
                    candles[i].gameObject.SetActive(false);
                }
            }

            if (crank != null && players != null && players.Length > 1 && players[1] != null && players[1].Held == null)
                crank.PickUp(players[1]);
        }

        string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        void Finish(bool success)
        {
            if (finished) return;
            finished = true;
            won = success;
            if (atmosphere) atmosphere.Finish(success);
            if (!success) PlaySfx(jumpScareClip);
            hitchUntil = 0f;
            Time.timeScale = 0f;
            LockPointer();
            RoundFinished?.Invoke(success);
        }

        void PlaySfx(AudioClip clip)
        {
            if (clip && sfxSource) sfxSource.PlayOneShot(clip, sfxVolume);
        }

        public void PlayFootstep(AudioSource source)
        {
            var surfaces = GetComponent<RecintoFootsteps>();
            if (surfaces && source && surfaces.Play(source, sfxVolume)) return;
            if (walkingClip && source) source.PlayOneShot(walkingClip, sfxVolume);
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && Playing) PauseNow();
        }

        public void Restart()
        {
            if (transitioning) return;
            transitioning = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (grain) Destroy(grain);
            if (vignette) Destroy(vignette);
            if (errorClip) Destroy(errorClip);
        }

        void PauseNow()
        {
            paused = true;
            hitchUntil = 0f;
            Time.timeScale = 0f;
            LockPointer();
        }

        void Continue()
        {
            paused = false;
            inputResumeFrame = Time.frameCount;
            Time.timeScale = 1f;
            LockPointer();
        }

        void Retry()
        {
            Memory.AutoStart = true;
            SaveNames();
            Restart();
        }

        void ReturnToMenu()
        {
            Memory.AutoStart = false;
            SaveNames();
            Restart();
        }

        void QuitGame()
        {
            SaveNames();
            SaveAudio();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        string PlayerLabel(int index)
        {
            return CleanName(index == 0 ? name1 : name2, "Jugador " + (index + 1));
        }

        void LoadSession()
        {
            name1 = Sanitize(PlayerPrefs.GetString(NameKey1, ""));
            name2 = Sanitize(PlayerPrefs.GetString(NameKey2, ""));
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 1f));
            ambienceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(AmbienceKey, 1f));
            if (PlayerPrefs.HasKey(SfxKey)) sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, sfxVolume));
            fullscreen = PlayerPrefs.HasKey(FullKey) ? PlayerPrefs.GetInt(FullKey, 1) == 1 : Screen.fullScreen;
            firstPerson = PlayerPrefs.GetInt(CameraKey, 0) == 1;
            int wantW = PlayerPrefs.GetInt(WidthKey, Screen.width);
            int wantH = PlayerPrefs.GetInt(HeightKey, Screen.height);
#if UNITY_EDITOR
            fullscreen = Screen.fullScreen;
            wantW = Screen.width;
            wantH = Screen.height;
#endif
            BuildResolutions(wantW, wantH);
#if !UNITY_EDITOR
            if (PlayerPrefs.HasKey(WidthKey) && PlayerPrefs.HasKey(HeightKey) && displayModes.Length > 0)
                ApplyDisplay();
#endif
        }

        void BuildResolutions(int wantW, int wantH)
        {
            var unique = new List<Resolution>();
            foreach (Resolution mode in Screen.resolutions)
            {
                bool seen = false;
                for (int i = 0; i < unique.Count; i++)
                {
                    if (unique[i].width == mode.width && unique[i].height == mode.height) seen = true;
                }
                if (!seen) unique.Add(mode);
            }
            unique.Sort((a, b) => a.width != b.width ? a.width.CompareTo(b.width) : a.height.CompareTo(b.height));
            displayModes = unique.ToArray();
            resolutionIndex = 0;
            for (int i = 0; i < displayModes.Length; i++)
            {
                if (displayModes[i].width == wantW && displayModes[i].height == wantH) resolutionIndex = i;
            }
        }

        float OptionValue(int index)
        {
            if (index == 0) return masterVolume;
            if (index == 1) return ambienceVolume;
            return sfxVolume;
        }

        void SetOption(int index, float value)
        {
            value = Mathf.Clamp01(value);
            if (index == 0) masterVolume = value;
            else if (index == 1) ambienceVolume = value;
            else sfxVolume = value;
            ApplyAudio();
            ApplyAmbience();
        }

        void ApplyAudio()
        {
            AudioListener.volume = masterVolume;
        }

        void CacheRoomBed()
        {
            if (roomBedCached) return;
            roomBedCached = true;
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude))
            {
                if (!source || source == ambience || source.gameObject.name != "AmbientBackground") continue;
                roomBed = source;
                roomBedVolume = source.volume > 0.01f ? source.volume : 0.823f;
                if (!source.isPlaying && source.clip) source.Play();
                break;
            }
        }

        public void Pulse(string text, int priority = 0)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (Time.time < pulseUntil && priority < pulsePriority) return;
            pulseText = text;
            pulsePriority = priority;
            pulseUntil = Time.time + 1.6f;
        }

        void NoteDangerBand()
        {
            int band = Danger >= 90f ? 3 : Danger >= dangerAlarmThreshold ? 2 : Danger >= 15f ? 1 : 0;
            if (band > dangerBand)
            {
                if (band == 1) Pulse("El peligro sube", 0);
                else if (band == 2) Pulse("Peligro alto", 3);
                else if (band == 3) Pulse("Peligro crítico", 4);
            }
            dangerBand = band;
        }

        void ApplyAmbience()
        {
            CacheRoomBed();
            bool running = !paused;
            if (ambience)
            {
                // Same clip and 0.16–0.42 curve. Volume eases; the loop is never restarted.
                float bed = started ? Mathf.Lerp(0.16f, 0.42f, Danger / 100f) : 0.16f;
                float target = bed * ambienceVolume;
                ambience.volume = Mathf.MoveTowards(ambience.volume, target, Time.unscaledDeltaTime * 0.22f);
                HoldLoop(ambience, running);
            }
            if (roomBed)
            {
                // Authored bed stays constant. 0.28 keeps it present while the crescendo can pass it.
                roomBed.volume = roomBedVolume * 0.28f * ambienceVolume;
                HoldLoop(roomBed, running);
            }
        }

        static void HoldLoop(AudioSource source, bool running)
        {
            if (!source || !source.clip) return;
            if (!running)
            {
                if (source.isPlaying) source.Pause();
                return;
            }
            if (source.isPlaying) return;
            if (source.time > 0.01f) source.UnPause();
            if (!source.isPlaying) source.Play();
        }

        void SaveAudio()
        {
            PlayerPrefs.SetFloat(MasterKey, masterVolume);
            PlayerPrefs.SetFloat(AmbienceKey, ambienceVolume);
            PlayerPrefs.SetFloat(SfxKey, sfxVolume);
            PlayerPrefs.SetInt(CameraKey, firstPerson ? 1 : 0);
#if !UNITY_EDITOR
            PlayerPrefs.SetInt(FullKey, fullscreen ? 1 : 0);
            if (displayModes.Length > 0)
            {
                PlayerPrefs.SetInt(WidthKey, displayModes[resolutionIndex].width);
                PlayerPrefs.SetInt(HeightKey, displayModes[resolutionIndex].height);
            }
#endif
            PlayerPrefs.Save();
        }

        void ApplyDisplay()
        {
            if (displayModes.Length == 0)
            {
                Screen.fullScreenMode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                return;
            }
            Resolution mode = displayModes[Mathf.Clamp(resolutionIndex, 0, displayModes.Length - 1)];
            Screen.SetResolution(mode.width, mode.height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        void ApplyPerspective(bool immediate)
        {
            if (players == null) return;
            for (int i = 0; i < players.Length; i++)
                if (players[i]) players[i].SetFirstPerson(firstPerson, immediate);
        }

        void SetPerspective(bool wantFirst)
        {
            if (wantFirst == firstPerson) return;
            firstPerson = wantFirst;
            ApplyPerspective(false);
            SaveAudio();
        }

        void ToggleFullscreen()
        {
            fullscreen = !fullscreen;
            ApplyDisplay();
            SaveAudio();
        }

        void ShiftResolution(int direction)
        {
            if (displayModes.Length == 0 || direction == 0) return;
            resolutionIndex = (resolutionIndex + direction) % displayModes.Length;
            if (resolutionIndex < 0) resolutionIndex += displayModes.Length;
            ApplyDisplay();
            SaveAudio();
        }

        void OpenOptions()
        {
            optionsOpen = true;
            keyboardEdit = false;
            focusIndex = 0;
            hover = -1;
            if (displayModes.Length == 0) BuildResolutions(Screen.width, Screen.height);
        }

        void CloseOptions()
        {
            optionsOpen = false;
            SaveAudio();
            focusIndex = overlay == 1 ? 1 : 3;
            hover = -1;
        }

        void StoreNames()
        {
            PlayerPrefs.SetString(NameKey1, name1 ?? "");
            PlayerPrefs.SetString(NameKey2, name2 ?? "");
        }

        void SaveNames()
        {
            StoreNames();
            PlayerPrefs.Save();
        }

        void Queue(MenuCommand value)
        {
            if (transitioning || value == MenuCommand.None) return;
            command = value;
        }

        void ExecuteCommand()
        {
            MenuCommand next = command;
            command = MenuCommand.None;
            switch (next)
            {
                case MenuCommand.Begin: Begin(); break;
                case MenuCommand.Continue: Continue(); break;
                case MenuCommand.Retry: Retry(); break;
                case MenuCommand.MainMenu: ReturnToMenu(); break;
                case MenuCommand.Quit: QuitGame(); break;
            }
        }

        int CurrentOverlay()
        {
            if (!started) return 0;
            if (finished) return 2;
            if (paused) return 1;
            return -1;
        }

        int ItemCount(int id)
        {
            if (optionsOpen && (id == 0 || id == 1)) return 7;
            if (id == 0) return 5;
            if (id == 1) return 4;
            if (id == 2) return 3;
            return 0;
        }

        void RefreshOverlay()
        {
            int next = CurrentOverlay();
            if (next == overlay)
            {
                int count = ItemCount(next);
                if (count > 0) focusIndex = Mathf.Clamp(focusIndex, 0, count - 1);
                return;
            }

            overlay = next;
            optionsOpen = false;
            StopEdit(true);
            hover = -1;
            if (next == 0)
            {
                if (string.IsNullOrWhiteSpace(name1)) focusIndex = 0;
                else if (string.IsNullOrWhiteSpace(name2)) focusIndex = 1;
                else focusIndex = 2;
            }
            else focusIndex = 0;
        }

        void BindTextKeyboard()
        {
            Keyboard current = Keyboard.current;
            if (textKeyboard == current) return;
            if (textKeyboard != null) textKeyboard.onTextInput -= OnNameCharacter;
            textKeyboard = current;
            if (textKeyboard != null) textKeyboard.onTextInput += OnNameCharacter;
        }

        void OnNameCharacter(char character)
        {
            if (!keyboardEdit || optionsOpen || overlay != 0 || focusIndex > 1 || char.IsControl(character)) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                bool ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
                bool alt = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
                if (ctrl && !alt) return;
            }
            Insert(character);
        }

        void PollMenu()
        {
            Keyboard keyboard = Keyboard.current;
            bool escape = EscapeDown();
            bool editing = keyboardEdit;

            if (keyboardEdit && keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) && keyboard.vKey.wasPressedThisFrame)
            {
                Paste();
                return;
            }

            if (escape || EastDown())
            {
                if (editing) { StopEdit(true); return; }
                if (optionsOpen) { CloseOptions(); return; }
                if (overlay == 1 && escape)
                {
                    focusIndex = 0;
                    Queue(MenuCommand.Continue);
                }
                return;
            }

            if (SouthDown() && !editing && !NameSlot(focusIndex))
            {
                ActivateFocused();
                return;
            }

            bool enter = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
            bool space = keyboard != null && keyboard.spaceKey.wasPressedThisFrame && !editing;
            if (enter || space)
            {
                if (editing)
                {
                    bool advance = keyboardEdit && NameSlot(focusIndex);
                    StopEdit(true);
                    if (advance) MoveFocus(1);
                    return;
                }
                if (enter && NameSlot(focusIndex))
                {
                    keyboardEdit = true;
                    caret = NameOf(focusIndex).Length;
                    return;
                }
                ActivateFocused();
                return;
            }

            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                bool backward = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                StopEdit(true);
                MoveFocus(backward ? -1 : 1);
                return;
            }

            if (keyboardEdit && keyboard != null)
            {
                if (keyboard.homeKey.wasPressedThisFrame) caret = 0;
                if (keyboard.endKey.wasPressedThisFrame) caret = NameOf(focusIndex).Length;
                if (keyboard.deleteKey.wasPressedThisFrame) DeleteForward();
            }

            PollBackspace();
            int vertical = ReadAxis(false);
            int horizontal = ReadAxis(true);
            if (keyboardEdit)
            {
                if (horizontal != 0) MoveCaret(horizontal);
                if (vertical != 0)
                {
                    StopEdit(true);
                    MoveFocus(vertical);
                }
                return;
            }
            if (optionsOpen)
            {
                if (horizontal != 0) NudgeOption(horizontal);
                if (vertical != 0) MoveFocus(vertical);
                return;
            }
            if (vertical != 0) MoveFocus(vertical);
        }

        void NudgeOption(int direction)
        {
            if (focusIndex >= 0 && focusIndex <= 2) SetOption(focusIndex, OptionValue(focusIndex) + direction * 0.05f);
            else if (focusIndex == 3) SetPerspective(direction > 0);
            else if (focusIndex == 5) ShiftResolution(direction);
        }

        void ActivateFocused()
        {
            if (optionsOpen)
            {
                if (focusIndex == 3) SetPerspective(!firstPerson);
                else if (focusIndex == 4) ToggleFullscreen();
                else if (focusIndex == 5) ShiftResolution(1);
                else if (focusIndex == 6) CloseOptions();
                return;
            }
            if (overlay == 0)
            {
                if (focusIndex == 2) Queue(MenuCommand.Begin);
                else if (focusIndex == 3) OpenOptions();
                else if (focusIndex == 4) Queue(MenuCommand.Quit);
            }
            else if (overlay == 1)
            {
                if (focusIndex == 0) Queue(MenuCommand.Continue);
                else if (focusIndex == 1) OpenOptions();
                else if (focusIndex == 2) Queue(MenuCommand.MainMenu);
                else Queue(MenuCommand.Quit);
            }
            else if (overlay == 2)
            {
                if (focusIndex == 0) Queue(MenuCommand.Retry);
                else if (focusIndex == 1) Queue(MenuCommand.MainMenu);
                else Queue(MenuCommand.Quit);
            }
        }

        void MoveFocus(int direction)
        {
            int count = ItemCount(overlay);
            if (count == 0 || direction == 0) return;
            focusIndex = (focusIndex + direction) % count;
            if (focusIndex < 0) focusIndex += count;
        }

        bool NameSlot(int index)
        {
            return !optionsOpen && overlay == 0 && index >= 0 && index < 2;
        }

        string NameOf(int index)
        {
            return index == 0 ? name1 ?? "" : name2 ?? "";
        }

        void SetName(int index, string value)
        {
            value = Sanitize(value);
            if (index == 0) name1 = value;
            else name2 = value;
            caret = Mathf.Clamp(caret, 0, value.Length);
            StoreNames();
        }

        void Insert(char character)
        {
            string value = NameOf(focusIndex);
            caret = Mathf.Clamp(caret, 0, value.Length);
            if (value.Length >= MaxName) return;
            SetName(focusIndex, value.Insert(caret, character.ToString()));
            caret++;
            caret = Mathf.Clamp(caret, 0, NameOf(focusIndex).Length);
        }

        void Paste()
        {
            if (!keyboardEdit || !NameSlot(focusIndex)) return;
            string clip = Sanitize(GUIUtility.systemCopyBuffer ?? "");
            if (clip.Length == 0) return;
            string value = NameOf(focusIndex);
            caret = Mathf.Clamp(caret, 0, value.Length);
            int room = MaxName - value.Length;
            if (room <= 0) return;
            if (clip.Length > room) clip = clip.Substring(0, room);
            SetName(focusIndex, value.Insert(caret, clip));
            caret += clip.Length;
            caret = Mathf.Clamp(caret, 0, NameOf(focusIndex).Length);
        }

        void BackspaceActive()
        {
            if (!NameSlot(focusIndex)) return;
            string value = NameOf(focusIndex);
            caret = Mathf.Clamp(caret, 0, value.Length);
            if (caret == 0 || value.Length == 0) return;
            caret--;
            SetName(focusIndex, value.Remove(caret, 1));
        }

        void DeleteForward()
        {
            if (!NameSlot(focusIndex)) return;
            string value = NameOf(focusIndex);
            caret = Mathf.Clamp(caret, 0, value.Length);
            if (caret >= value.Length) return;
            SetName(focusIndex, value.Remove(caret, 1));
        }

        void MoveCaret(int direction)
        {
            string value = NameOf(focusIndex);
            caret = Mathf.Clamp(caret + direction, 0, value.Length);
        }

        void StopEdit(bool save)
        {
            bool wasEditing = keyboardEdit;
            keyboardEdit = false;
            if (save && wasEditing) SaveNames();
        }

        void PollBackspace()
        {
            Keyboard keyboard = Keyboard.current;
            bool keyHeld = keyboardEdit && keyboard != null && keyboard.backspaceKey.isPressed;
            if (!keyHeld)
            {
                backspaceTimer = 0f;
                return;
            }
            if (Time.unscaledTime < backspaceTimer) return;
            float delay = backspaceTimer == 0f ? 0.32f : 0.045f;
            backspaceTimer = Time.unscaledTime + delay;
            BackspaceActive();
        }

        int ReadAxis(bool horizontal)
        {
            int direction = 0;
            bool held = false;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                KeyControl negative = horizontal ? keyboard.leftArrowKey : keyboard.upArrowKey;
                KeyControl positive = horizontal ? keyboard.rightArrowKey : keyboard.downArrowKey;
                if (negative.isPressed) { direction = -1; held = true; }
                if (positive.isPressed) { direction = 1; held = true; }
            }
            foreach (Gamepad pad in Gamepad.all)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (horizontal)
                {
                    if (pad.dpad.left.isPressed || stick.x <= -0.75f) { direction = -1; held = true; }
                    if (pad.dpad.right.isPressed || stick.x >= 0.75f) { direction = 1; held = true; }
                }
                else
                {
                    if (pad.dpad.up.isPressed || stick.y >= 0.75f) { direction = -1; held = true; }
                    if (pad.dpad.down.isPressed || stick.y <= -0.75f) { direction = 1; held = true; }
                }
            }
            if (!held)
            {
                if (horizontal) axisTimerX = 0f;
                else axisTimerY = 0f;
                return 0;
            }

            float timer = horizontal ? axisTimerX : axisTimerY;
            if (Time.unscaledTime < timer) return 0;
            float repeat = timer == 0f ? 0.36f : 0.14f;
            timer = Time.unscaledTime + repeat;
            if (horizontal) axisTimerX = timer;
            else axisTimerY = timer;
            return direction;
        }

        static bool EscapeDown()
        {
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        }

        static bool RDown()
        {
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        }

        static bool SouthDown()
        {
            foreach (Gamepad pad in Gamepad.all)
                if (pad.buttonSouth.wasPressedThisFrame) return true;
            return false;
        }

        static bool EastDown()
        {
            foreach (Gamepad pad in Gamepad.all)
                if (pad.buttonEast.wasPressedThisFrame) return true;
            return false;
        }

        float GuiScale => Screen.height <= 0 ? 1f : Mathf.Max(0.55f, Screen.height / 900f);

        Vector2 MouseGui(Vector2 screen)
        {
            float scale = GuiScale;
            return new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
        }

        void OnGUI()
        {
            EnsureStyles();
            RefreshOverlay();
            float scale = GuiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            try
            {
                float width = Screen.width / scale;
                float height = Screen.height / scale;
                if (atmosphere && atmosphere.Blackout > 0f)
                    Box(new Rect(0f, 0f, width, height), new Color(0f, 0f, 0f, atmosphere.Blackout));
                if (overlay >= 0)
                {
                    LayoutOverlay(width, height);
                    bool optionsBeforePointer = optionsOpen;
                    HandlePointer();
                    if (optionsOpen != optionsBeforePointer) LayoutOverlay(width, height);
                    DrawBackdrop(width, height);
                    if (optionsOpen) DrawOptions();
                    else if (overlay == 0) DrawTitle();
                    else DrawSimple();
                }
                else DrawHud(width, height);
            }
            finally
            {
                GUI.matrix = Matrix4x4.identity;
                GUI.color = Color.white;
            }
        }

        void LayoutOverlay(float width, float height)
        {
            bool side = overlay == 0 && menuBackground != null;
            float columnW = Mathf.Min(side ? 500f : 640f, width - (side ? 80f : 72f));
            float x = side ? Mathf.Max(40f, width * 0.055f) : (width - columnW) * 0.5f;
            columnRect = new Rect(x, 0f, columnW, height);
            if (optionsOpen) LayoutOptions(x, columnW, height);
            else if (overlay == 0) LayoutTitle(x, columnW, height);
            else LayoutSimple(x, columnW, height);
        }

        void LayoutTitle(float x, float columnW, float height)
        {
            float y = Mathf.Max(height * 0.07f, 40f);
            titleRect = new Rect(x, y, columnW, 64f);
            y = titleRect.yMax + 16f;
            briefRect = new Rect(x, y, columnW, 64f);
            y = briefRect.yMax + 18f;
            hitCount = 5;
            for (int i = 0; i < 2; i++)
            {
                hits[i] = new Rect(x, y, columnW, 66f);
                visuals[i] = new Rect(x + 16f, y + 22f, columnW - 16f, 44f);
                y = hits[i].yMax + 12f;
            }
            y += 8f;
            hits[2] = visuals[2] = new Rect(x, y, columnW, 46f);
            hits[3] = visuals[3] = new Rect(x, hits[2].yMax + 6f, columnW, 46f);
            hits[4] = visuals[4] = new Rect(x, hits[3].yMax + 6f, columnW, 46f);
            statusRect = new Rect(x, hits[4].yMax + 14f, columnW, 36f);
            float helpTop = statusRect.yMax + 4f;
            float helpBottom = height - 28f;
            helpRect = new Rect(x, helpTop, columnW, Mathf.Max(48f, helpBottom - helpTop));
        }

        void LayoutOptions(float x, float columnW, float height)
        {
            float y = Mathf.Max(height * 0.08f, 48f);
            titleRect = new Rect(x, y, columnW, 64f);
            y = titleRect.yMax + 28f;
            hitCount = 7;
            for (int i = 0; i < 6; i++)
            {
                hits[i] = visuals[i] = new Rect(x, y, columnW, 42f);
                y += 50f;
            }
            hits[6] = visuals[6] = new Rect(x, y + 10f, columnW, 46f);
            helpRect = new Rect(x, hits[6].yMax + 16f, columnW, 72f);
            statusRect = helpRect;
        }

        void LayoutSimple(float x, float columnW, float height)
        {
            int count = overlay == 1 ? 4 : 3;
            const float block = 520f;
            float y = Mathf.Max(height * 0.055f + 18f, (height - block) * 0.4f);
            titleRect = new Rect(x, y, columnW, 72f);
            y += 88f;
            briefRect = new Rect(x, y, columnW, 78f);
            y += 96f;
            hitCount = count;
            for (int i = 0; i < count; i++)
            {
                hits[i] = visuals[i] = new Rect(x, y, columnW, 48f);
                y += 56f;
            }
            statusRect = new Rect(x, y + 4f, columnW, 36f);
            helpRect = new Rect(x, y + 42f, columnW, 110f);
        }

        void HandlePointer()
        {
            hover = -1;
            Mouse mouse = Mouse.current;
            if (mouse == null || hitCount == 0) return;
            Vector2 gui = MouseGui(mouse.position.ReadValue());
            for (int i = 0; i < hitCount; i++)
                if (hits[i].Contains(gui)) hover = i;

            if (optionsOpen && hover >= 0 && hover <= 2 && mouse.leftButton.isPressed)
                SetOption(hover, SliderAmount(hits[hover], gui.x));

            if (!mouse.leftButton.wasPressedThisFrame || pointerFrame == Time.frameCount) return;
            pointerFrame = Time.frameCount;
            if (hover < 0)
            {
                StopEdit(true);
                return;
            }

            focusIndex = hover;
            if (optionsOpen && hover <= 2)
            {
                SetOption(hover, SliderAmount(hits[hover], gui.x));
                return;
            }
            if (NameSlot(hover))
            {
                if (keyboardEdit) SaveNames();
                keyboardEdit = true;
                caret = CaretFromMouse(visuals[hover], NameOf(hover), gui.x);
                return;
            }

            StopEdit(true);
            ActivateFocused();
        }

        int CaretFromMouse(Rect visual, string value, float mouseX)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            float local = mouseX - visual.x - 12f;
            if (local <= 0f) return 0;
            int best = 0;
            for (int i = 1; i <= value.Length; i++)
            {
                float width = fieldStyle.CalcSize(new GUIContent(value.Substring(0, i))).x;
                if (local >= width) best = i;
                else break;
            }
            return best;
        }

        void DrawBackdrop(float width, float height)
        {
            bool photo = overlay == 0 && menuBackground != null;
            if (photo)
            {
                DrawCover(menuBackground, width, height);
                Box(new Rect(0f, 0f, columnRect.xMax + 48f, height), new Color(0f, 0f, 0f, 0.62f));
            }
            else
            {
                Color backdrop = finished && won ? new Color(Wash.r, Wash.g, Wash.b, 0.45f) : Wash;
                if (finished && !won && atmosphere) backdrop.a = Mathf.Lerp(0.15f, Wash.a, atmosphere.Blackout);
                Box(new Rect(0f, 0f, width, height), backdrop);
                if (vignette) GUI.DrawTexture(new Rect(0f, 0f, width, height), vignette, ScaleMode.StretchToFill, true);
                Box(new Rect(columnRect.x - 40f, 0f, columnRect.width + 80f, height), new Color(0f, 0f, 0f, 0.5f));
            }
            if (grain)
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, photo ? 0.08f : 0.16f);
                float tilesX = Mathf.Max(1f, width / 180f);
                float tilesY = Mathf.Max(1f, height / 180f);
                GUI.DrawTextureWithTexCoords(new Rect(0f, 0f, width, height), grain, new Rect(0f, 0f, tilesX, tilesY));
                GUI.color = previous;
            }
            if (!photo)
            {
                float bar = height * 0.055f;
                Box(new Rect(0f, 0f, width, bar), Color.black);
                Box(new Rect(0f, height - bar, width, bar), Color.black);
            }
        }

        static void DrawCover(Texture2D texture, float width, float height)
        {
            float image = texture.width / (float)texture.height;
            float screen = width / Mathf.Max(1f, height);
            Rect uv = new Rect(0f, 0f, 1f, 1f);
            if (screen > image)
            {
                float visible = image / screen;
                uv = new Rect(0f, (1f - visible) * 0.5f, 1f, visible);
            }
            else if (image > 0f)
            {
                float visible = screen / image;
                uv = new Rect(1f - visible, 0f, visible, 1f);
            }
            GUI.DrawTextureWithTexCoords(new Rect(0f, 0f, width, height), texture, uv);
        }

        static Rect SliderBar(Rect row)
        {
            return new Rect(row.x + 168f, row.y + row.height * 0.5f - 3f, Mathf.Max(40f, row.width - 230f), 6f);
        }

        static float SliderAmount(Rect row, float mouseX)
        {
            Rect bar = SliderBar(row);
            return Mathf.InverseLerp(bar.x, bar.xMax, mouseX);
        }

        void DrawTitle()
        {
            DrawTracked(titleRect, "A Oscuras", glyphStyle, 7f, Paper);
            Rule(titleRect.center.x, titleRect.yMax + 2f, 168f);
            Label(briefRect, "Un registro miente: contrastad juntos. La vela quema la contaminación.", bodyStyle, Ash);

            DrawName(0, "Nombre del jugador 1");
            DrawName(1, "Nombre del jugador 2");
            DrawChoice(hits[2], "INICIAR EXPEDICIÓN", 2, false, false, false);
            DrawChoice(hits[3], "OPCIONES", 3, false, false, false);
            DrawChoice(hits[4], "SALIR", 4, false, false, false);
            Label(statusRect, "Dos jugadores en un teclado compartido.", smallStyle, Ash);
            Label(helpRect, HelpText(), smallStyle, Ash);
        }

        void DrawOptions()
        {
            DrawTracked(titleRect, "Opciones", glyphStyle, 6f, Paper);
            Rule(titleRect.x + 84f, titleRect.yMax + 6f, 140f);
            Box(new Rect(titleRect.x, titleRect.yMax + 6f, 28f, 1f), Accent);
            DrawSlider(0, "GENERAL");
            DrawSlider(1, "AMBIENTE");
            DrawSlider(2, "EFECTOS");
            DrawChoice(hits[3], "CÁMARA: " + (firstPerson ? "PRIMERA PERSONA" : "TERCERA PERSONA"), 3, false, false, false);
            DrawChoice(hits[4], "PANTALLA COMPLETA    " + (fullscreen ? "SÍ" : "NO"), 4, false, false, false);
            string resolution = displayModes.Length == 0
                ? Screen.width + " × " + Screen.height
                : displayModes[resolutionIndex].width + " × " + displayModes[resolutionIndex].height;
            DrawChoice(hits[5], "RESOLUCIÓN    " + resolution, 5, displayModes.Length == 0, false, false);
            DrawChoice(hits[6], "VOLVER", 6, false, false, false);
            Label(helpRect, "Izquierda y derecha cambian volumen, cámara y resolución. Enter o A confirman. Esc o B regresan.", smallStyle, Ash);
        }

        void DrawSlider(int index, string label)
        {
            Rect row = hits[index];
            bool focused = focusIndex == index;
            bool over = hover == index && !focused;
            Color ink = focused ? Paper : over ? Idle : Ash;
            if (focused) Box(new Rect(row.x, row.y + row.height * 0.5f - 1f, 11f, 2f), Accent);
            Label(new Rect(row.x + 18f, row.y, 140f, row.height), label, optionStyle, ink);
            Rect bar = SliderBar(row);
            Box(bar, new Color(0.16f, 0.14f, 0.12f, 0.95f));
            float value = OptionValue(index);
            Box(new Rect(bar.x, bar.y, bar.width * value, bar.height), focused ? Paper : Idle);
            if (focused) Box(new Rect(bar.x + bar.width * value - 1f, bar.y - 3f, 2f, bar.height + 6f), Accent);
            Label(new Rect(bar.xMax + 8f, row.y, 56f, row.height), Mathf.RoundToInt(value * 100f) + "%", optionStyle, ink);
        }

        void DrawSimple()
        {
            string headline = overlay == 1 ? "Pausa" : (won ? "Incidente neutralizado" : "La oscuridad los alcanzó");
            string body = overlay == 1 ? PauseBody() : ResultBody();
            if (overlay == 1) DrawTracked(titleRect, headline, glyphStyle, 8f, Paper);
            else Label(titleRect, headline, titleStyle, Paper);
            Rule(titleRect.center.x, titleRect.yMax + 4f, overlay == 1 ? 96f : 220f);
            Label(briefRect, body, bodyStyle, Idle);
            if (overlay == 1)
            {
                DrawChoice(hits[0], "CONTINUAR", 0, false, false, false);
                DrawChoice(hits[1], "OPCIONES", 1, false, false, false);
                DrawChoice(hits[2], "VOLVER AL MENÚ", 2, false, false, false);
                DrawChoice(hits[3], "SALIR", 3, false, false, false);
                Label(statusRect, "La partida sigue detenida.", smallStyle, Ash);
            }
            else
            {
                DrawChoice(hits[0], "VOLVER A INTENTAR", 0, false, false, false);
                DrawChoice(hits[1], "VOLVER AL MENÚ", 1, false, false, false);
                DrawChoice(hits[2], "SALIR", 2, false, false, false);
                Label(statusRect, "Reintentar conserva los nombres y empieza de nuevo.", smallStyle, Ash);
            }
            Label(helpRect, HelpText(), smallStyle, Ash);
        }

        void DrawName(int index, string caption)
        {
            bool focused = focusIndex == index;
            bool editing = focused && keyboardEdit;
            bool over = hover == index && !focused;
            Rect box = visuals[index];
            Label(new Rect(box.x, hits[index].y, box.width, 20f), editing ? caption + "    editando" : caption, captionStyle, editing || focused ? Paper : Ash);
            Color border = editing || focused ? Paper : (over ? Idle : new Color(0.30f, 0.28f, 0.25f, 1f));
            Frame(box, border, new Color(0.04f, 0.04f, 0.035f, 0.96f));
            string value = NameOf(index);
            Rect text = new Rect(box.x + 12f, box.y, box.width - 24f, box.height);
            if (string.IsNullOrWhiteSpace(value) && !editing)
                Label(text, "Sin nombre", fieldStyle, DisabledInk);
            else
                Label(text, value, fieldStyle, Paper);
            if (!editing) return;

            caret = Mathf.Clamp(caret, 0, value.Length);
            string before = value.Substring(0, caret);
            float caretX = text.x + fieldStyle.CalcSize(new GUIContent(before)).x;
            Box(new Rect(caretX, box.y + 10f, 2f, box.height - 20f), Paper);
        }

        void DrawChoice(Rect hit, string text, int index, bool disabled, bool marked, bool markOn)
        {
            bool focused = focusIndex == index;
            bool over = hover == index && !focused;
            Color ink = disabled ? DisabledInk : focused ? Paper : over ? Idle : Ash;
            if (focused) Box(new Rect(hit.x, hit.y + hit.height * 0.5f - 1f, 11f, 2f), Accent);
            float textX = hit.x + 18f;
            if (marked)
            {
                var mark = new Rect(hit.x + 20f, hit.y + hit.height * 0.5f - 6f, 12f, 12f);
                if (markOn) Box(mark, ink);
                else Stroke(mark, ink);
                textX = hit.x + 42f;
            }
            Label(new Rect(textX, hit.y, hit.width - (textX - hit.x) - 8f, hit.height), text, optionStyle, ink);
        }

        string ImmediateObjective()
        {
            int loose = CountUnstable();
            if (loose > 0) return "OBJETIVO: estabilizar " + loose + " mecanismo(s) sin llevar la manivela.";
            if (Containing) return "OBJETIVO: uno en TALLER 04 y otro en BOMBAS 05 hasta contener.";
            if (!Investigation) return "OBJETIVO: localizar los registros y descubrir la secuencia.";
            if (Investigation.EvidenceCount < 3)
                return "OBJETIVO: recuperar registros (" + Investigation.EvidenceCount + "/3). Quemad los brotes que bloquean paneles.";
            if (!Investigation.Contrasted) return "OBJETIVO: contrastar juntos los tres registros en la consola.";
            if (Investigation.SurveyedPower < Investigation.RequiredPower)
                return "OBJETIVO: inspeccionar " + (Investigation.RequiredPower - Investigation.SurveyedPower)
                    + " luz/luces auxiliares. Encendedlas y apagadlas antes del aviso.";
            return "OBJETIVO: activar " + NextSymbol + ". Uno sujeta el asa; otro gira la manivela.";
        }

        float HudPanelHeight(string text, float width, GUIStyle style, bool progress = false)
        {
            return Mathf.Ceil(style.CalcHeight(new GUIContent(text), width - 24f)) + 16f + (progress ? 16f : 0f);
        }

        float DrawHudPanel(float x, float y, float width, string text, GUIStyle style, Color accent, float progress = -1f)
        {
            float height = HudPanelHeight(text, width, style, progress >= 0f);
            Box(new Rect(x, y, width, height), new Color(0.015f, 0.014f, 0.012f, 0.94f));
            Box(new Rect(x, y, 3f, height), accent);
            float textHeight = height - 16f - (progress >= 0f ? 16f : 0f);
            Label(new Rect(x + 12f, y + 8f, width - 24f, textHeight), text, style, Paper);
            if (progress >= 0f)
            {
                var bar = new Rect(x + 12f, y + height - 14f, width - 24f, 8f);
                Box(bar, new Color(0.16f, 0.14f, 0.12f));
                bar.width *= Mathf.Clamp01(progress);
                Box(bar, accent);
            }
            return height + 6f;
        }

        void DrawHudBottomPanel(float x, ref float bottom, float width, string text, GUIStyle style, Color accent, float progress = -1f)
        {
            bottom -= HudPanelHeight(text, width, style, progress >= 0f);
            DrawHudPanel(x, bottom, width, text, style, accent, progress);
            bottom -= 6f;
        }

        void DrawHud(float width, float height)
        {
            Box(new Rect(width * 0.5f - 1f, 0f, 2f, height), Color.black);
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (!player) continue;
                float left = i * width * 0.5f;
                float half = width * 0.5f;
                float x = left + 16f;
                float panelWidth = half - 32f;
                float top = 12f;
                bool threatVisible = elapsed < threatUntil && !string.IsNullOrEmpty(threatText);
                string held = player.Held ? player.Held.displayName : "manos libres";
                string identity = "J" + (player.playerIndex + 1) + "  " + PlayerLabel(i) + "  /  " + held;
                top += DrawHudPanel(x, top, panelWidth, identity + "\n" + ImmediateObjective(), hudStyle, Paper);

                if (threatVisible)
                    top += DrawHudPanel(x, top, panelWidth, "AMENAZA: " + threatText, hudStyle, Accent);

                string marks = Investigation ? "Marcas " + Investigation.EvidenceSummary
                    + " | Consola " + (Investigation.Contrasted ? "verificada" : "pendiente") : "Marcas sin descubrir";
                string resources = "Peligro " + Danger.ToString("0") + "% (" + DangerWords + ") | Sellos " + completed + "/3"
                    + " | Vela " + CandleRange.ToString("0.0") + " m"
                    + (CandleCharge <= 0 ? " (luz mínima)" : CandleCharge < 0.5f ? " (chisporrotea)" : "");
                string summary = marks + "\n" + resources;
                if (Investigation)
                {
                    string circuit = Investigation.RequiredPower == 0 ? "opcional"
                        : Mathf.Min(Investigation.SurveyedPower, Investigation.RequiredPower) + "/" + Investigation.RequiredPower;
                    summary += "\nRed " + circuit + " | " + Investigation.LightSummary;
                }
                top += DrawHudPanel(x, top, panelWidth, summary, hudSmallStyle, Ash);

                if (Containing)
                {
                    string status = ContainmentStatusText(out bool advancing);
                    string containment = "CONTENCIÓN " + Mathf.RoundToInt(Containment01 * 100f) + "%\n"
                        + WorkshopOccupancyString() + " | " + PumpOccupancyString() + "\n" + status;
                    top += DrawHudPanel(x, top, panelWidth, containment, hudSmallStyle, advancing ? Paper : Accent, Containment01);
                }

                if (elapsed < contactFlashUntil) Box(new Rect(left, 0, half, height), new Color(.55f, .49f, .38f, .09f));
                float centerX = left + half * 0.5f;
                float centerY = height * 0.5f;
                Box(new Rect(centerX - 7f, centerY - 0.5f, 14f, 1f), new Color(Paper.r, Paper.g, Paper.b, 0.8f));
                Box(new Rect(centerX - 0.5f, centerY - 7f, 1f, 14f), new Color(Paper.r, Paper.g, Paper.b, 0.8f));

                float bottom = height - 16f;
                string prompt = player.Hint;
                if (!string.IsNullOrEmpty(player.DropHint))
                    prompt = string.IsNullOrEmpty(prompt) ? player.DropHint : prompt + "\n" + player.DropHint;
                if (!string.IsNullOrEmpty(prompt))
                    DrawHudBottomPanel(x, ref bottom, panelWidth, prompt, hudStyle, Paper);

                var focus = player.FocusedMechanism;
                if (focus && focus.unstable)
                {
                    string state = player.Stabilizing ? "ESTABILIZANDO" : "DESAJUSTADO";
                    string repair = focus.symbol + " " + state + " " + Mathf.RoundToInt(focus.Hold01 * 100f) + "%"
                        + " | [" + DemoPlayerInput.UseLabel(player.playerIndex) + "] mantener sin manivela";
                    DrawHudBottomPanel(x, ref bottom, panelWidth, repair, hudSmallStyle, Accent, focus.Hold01);
                }
                else if (focus && !focus.done && focus.HasCrank && focus.Cooperation01 > 0f)
                {
                    string cooperation = focus.symbol + " ACTIVACIÓN CONJUNTA " + Mathf.RoundToInt(focus.Cooperation01 * 100f) + "%";
                    DrawHudBottomPanel(x, ref bottom, panelWidth, cooperation, hudSmallStyle, Paper, focus.Cooperation01);
                }

                if (Time.time < pulseUntil && !string.IsNullOrEmpty(pulseText))
                    DrawHudBottomPanel(x, ref bottom, panelWidth, pulseText, hudStyle, pulsePriority >= 3 ? Accent : Paper);

                if (Time.unscaledTime < player.MessageUntil && !string.IsNullOrEmpty(player.Message)
                    && (!threatVisible || player.Message != threatText))
                    DrawHudBottomPanel(x, ref bottom, panelWidth, player.Message, hudSmallStyle, Ash);

                string tip = GetPlayerTip(i);
                if (!string.IsNullOrEmpty(tip)
                    && bottom - HudPanelHeight(tip, panelWidth, hudSmallStyle) >= Mathf.Max(top + 12f, centerY + 36f))
                    DrawHudBottomPanel(x, ref bottom, panelWidth, tip, hudSmallStyle, new Color(0.86f, 0.72f, 0.40f));
            }
        }

        string PauseBody()
        {
            string who = "Expedición de " + PlayerLabel(0) + " y " + PlayerLabel(1) + ".";
            return who + "\nLa expedición está detenida.";
        }

        string ResultBody()
        {
            string who = PlayerLabel(0) + " y " + PlayerLabel(1);
            string clock = " Tiempo " + FormatTime(elapsed) + ".";
            if (won) return who + " se coordinaron y cerraron el incidente." + clock;
            return "Expedición de " + who + " interrumpida por " + DefeatCause + "." + clock;
        }

        string HelpText()
        {
            if (optionsOpen)
                return "Flechas, stick o ratón. Esc o B vuelven.\n" + GameplayHelp;
            if (overlay == 0 && keyboardEdit)
                return "Escribiendo. Enter confirma el nombre. Esc sale del campo. Ctrl+V pega.\n" + GameplayHelp;
            if (overlay == 0)
                return "Flechas, stick o ratón para elegir. Enter o A confirman. En un nombre, Enter escribe.\n" + GameplayHelp;
            if (overlay == 1)
                return "Esc o Continuar reanudan. Opciones cambia el volumen y la cámara sin abandonar la expedición.\n" + GameplayHelp;
            return "R o Volver a intentar empieza otra expedición con los mismos nombres. Volver al menú regresa a los nombres.\n" + GameplayHelp;
        }

        void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        void DrawTracked(Rect area, string text, GUIStyle style, float tracking, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            float[] sizes = new float[text.Length];
            float glyphs = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                sizes[i] = style.CalcSize(new GUIContent(text[i].ToString())).x;
                glyphs += sizes[i];
            }
            float gap = tracking;
            if (text.Length > 1 && glyphs + gap * (text.Length - 1) > area.width)
                gap = Mathf.Max(0f, (area.width - glyphs) / (text.Length - 1));
            float total = glyphs + gap * Mathf.Max(0, text.Length - 1);
            float x = area.x + (area.width - total) * 0.5f;
            Color previous = GUI.color;
            GUI.color = color;
            for (int i = 0; i < text.Length; i++)
            {
                GUI.Label(new Rect(x, area.y, sizes[i] + 8f, area.height), text[i].ToString(), style);
                x += sizes[i] + gap;
            }
            GUI.color = previous;
        }

        void Rule(float centerX, float y, float width)
        {
            Box(new Rect(centerX - width * 0.5f, y, width, 1f), new Color(0.55f, 0.52f, 0.46f, 0.85f));
        }

        void Frame(Rect rect, Color border, Color fill)
        {
            Box(rect, border);
            Box(new Rect(rect.x + 1f, rect.y + 1f, Mathf.Max(0f, rect.width - 2f), Mathf.Max(0f, rect.height - 2f)), fill);
        }

        void Stroke(Rect rect, Color border)
        {
            Box(new Rect(rect.x, rect.y, rect.width, 1f), border);
            Box(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
            Box(new Rect(rect.x, rect.y, 1f, rect.height), border);
            Box(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);
        }

        void Box(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void EnsureStyles()
        {
            EnsureTextures();
            if (stylesReady) return;
            serif = LoadSerif();
            titleStyle = Style(34, FontStyle.Bold, TextAnchor.MiddleCenter, true);
            glyphStyle = Style(48, FontStyle.Bold, TextAnchor.MiddleLeft, false);
            bodyStyle = Style(18, FontStyle.Normal, TextAnchor.UpperLeft, true);
            captionStyle = Style(15, FontStyle.Normal, TextAnchor.MiddleLeft, false);
            optionStyle = Style(22, FontStyle.Normal, TextAnchor.MiddleLeft, false);
            fieldStyle = Style(22, FontStyle.Normal, TextAnchor.MiddleLeft, false);
            smallStyle = Style(14, FontStyle.Normal, TextAnchor.UpperLeft, true);
            hudStyle = Style(18, FontStyle.Normal, TextAnchor.MiddleLeft, true);
            hudSmallStyle = Style(18, FontStyle.Normal, TextAnchor.MiddleLeft, true);
            stylesReady = true;
        }

        GUIStyle Style(int size, FontStyle weight, TextAnchor anchor, bool wrap)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = weight,
                alignment = anchor,
                wordWrap = wrap,
                clipping = TextClipping.Clip,
                richText = false
            };
            style.normal.textColor = Color.white;
            if (serif != null) style.font = serif;
            return style;
        }

        static Font LoadSerif()
        {
            try
            {
                string[] installed = Font.GetOSInstalledFontNames();
                string[] preferred = { "Times New Roman", "Georgia", "Palatino Linotype", "Liberation Serif", "Times", "Nimbus Roman" };
                foreach (string wanted in preferred)
                {
                    foreach (string installedName in installed)
                    {
                        if (installedName.StartsWith(wanted, StringComparison.OrdinalIgnoreCase))
                            return Font.CreateDynamicFontFromOSFont(installedName, 64);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
            return null;
        }

        void EnsureTextures()
        {
            if (grain == null) grain = BuildGrain();
            if (vignette == null) vignette = BuildVignette();
        }

        static Texture2D BuildGrain()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            var random = new System.Random(2026);
            for (int i = 0; i < pixels.Length; i++)
            {
                byte value = (byte)random.Next(0, 255);
                byte alpha = (byte)random.Next(40, 150);
                pixels[i] = new Color32(value, value, value, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static Texture2D BuildVignette()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(u * u * 0.82f + v * v);
                    float alpha = Mathf.SmoothStep(0.28f, 1.12f, distance);
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)(alpha * 200f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            char[] chars = value.ToCharArray();
            int count = 0;
            for (int i = 0; i < chars.Length && count < MaxName; i++)
            {
                if (!char.IsControl(chars[i])) chars[count++] = chars[i];
            }
            return new string(chars, 0, count);
        }

        static string CleanName(string value, string fallback)
        {
            value = Sanitize(value).Trim();
            return value.Length == 0 ? fallback : value;
        }
    }
}
