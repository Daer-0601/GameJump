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
        [HideInInspector] public float dangerPerSecond = 0.31f; // Kept for older editor setup tools.
        [HideInInspector] public float darknessPenalty = 0.6f; // No longer used by the expedition.
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
        float masterVolume = 1f;
        float ambienceVolume = 1f;
        Resolution[] displayModes = System.Array.Empty<Resolution>();
        int resolutionIndex;
        float axisTimerX;
        float axisTimerY;
        float backspaceTimer;
        MenuCommand command;
        Keyboard textKeyboard;

        readonly Rect[] hits = new Rect[6];
        readonly Rect[] visuals = new Rect[6];
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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (ambience) ambience.Play();
            ApplyAudio();

            bool pendingStart = Memory.AutoStart;
            Memory.AutoStart = false;
            if (pendingStart) Begin();
        }

        public void Begin()
        {
            if (transitioning) return;
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
            DestabilizeDue();
            // Investigation starts without a death clock. Only unresolved damage keeps pressure rising.
            float rate = CountUnstable() * unstablePenalty;
            Danger = Mathf.Clamp(Danger + rate * Time.deltaTime, 0f, 100f);
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
            if (!Playing || mechanism.done) return;
            if (Investigation && !Investigation.Ready)
            {
                if (player) player.Tell("Completen registros, consola y red auxiliar antes del mecanismo.");
                return;
            }
            if (mechanism.id != order[completed])
            {
                Danger = Mathf.Min(100f, Danger + errorPenalty);
                disturbanceUntil = elapsed + 2f;
                if (atmosphere) atmosphere.WrongOrder();
                PlaySfx(errorClip);
                foreach (var teammate in players) teammate.Tell("¡CLAC! Orden incorrecto. Los pasos correctos se conservan.");
                if (Investigation) Investigation.OnWrongProcedure();
                if (Danger >= 100f) Finish(false);
                return;
            }
            mechanism.done = true;
            completed++;
            if (completed == 1)
            {
                FirstMechanismCompleted?.Invoke(mechanism);
                Pulse("El paso se abre");
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
            PlaySfx(gearsClip);
            Pulse(mechanism.symbol + " estable");
            Danger = Mathf.Max(0f, Danger - stabilizeRelief);
            foreach (var teammate in players) teammate.Tell("¡CLONC! " + mechanism.symbol + " estabilizado. −" + stabilizeRelief.ToString("0") + "% peligro.");
            TryWin();
        }

        void TryWin()
        {
            if (completed == order.Length && !AnyUnstable()) Finish(true);
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
                Pulse(pick.symbol + " desajustado");
                foreach (var teammate in players)
                    if (teammate) teammate.Tell(pick.symbol + " se desajusto. Estabilizalo sin la manivela.");
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

        public void Pulse(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            pulseText = text;
            pulseUntil = Time.unscaledTime + 1.5f;
        }

        void NoteDangerBand()
        {
            int band = Danger >= 90f ? 3 : Danger >= dangerAlarmThreshold ? 2 : Danger >= 40f ? 1 : 0;
            if (band > dangerBand)
            {
                if (band == 1) Pulse("El peligro sube");
                else if (band == 2) Pulse("Peligro alto");
                else if (band == 3) Pulse("Peligro crítico");
            }
            dangerBand = band;
        }

        void ApplyAmbience()
        {
            CacheRoomBed();
            if (ambience)
            {
                // Same curve as e8ee163: one looping clip, volume only, never restarted.
                float bed = started ? Mathf.Lerp(0.16f, 0.42f, Danger / 100f) : 0.16f;
                ambience.volume = bed * ambienceVolume;
                if (!ambience.isPlaying && ambience.clip) ambience.Play();
            }
            if (roomBed) roomBed.volume = roomBedVolume * ambienceVolume;
        }

        void SaveAudio()
        {
            PlayerPrefs.SetFloat(MasterKey, masterVolume);
            PlayerPrefs.SetFloat(AmbienceKey, ambienceVolume);
            PlayerPrefs.SetFloat(SfxKey, sfxVolume);
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
            if (optionsOpen && (id == 0 || id == 1)) return 6;
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
            else if (focusIndex == 4) ShiftResolution(direction);
        }

        void ActivateFocused()
        {
            if (optionsOpen)
            {
                if (focusIndex == 3) ToggleFullscreen();
                else if (focusIndex == 4) ShiftResolution(1);
                else if (focusIndex == 5) CloseOptions();
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
            hitCount = 6;
            for (int i = 0; i < 5; i++)
            {
                hits[i] = visuals[i] = new Rect(x, y, columnW, 42f);
                y += 50f;
            }
            hits[5] = visuals[5] = new Rect(x, y + 10f, columnW, 46f);
            helpRect = new Rect(x, hits[5].yMax + 16f, columnW, 72f);
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
            Label(briefRect, "Exploren el recinto: recuperen tres registros y contrasten la consola juntos. Activen la red auxiliar que indique el procedimiento. La manivela sirve para los mecanismos al final.", bodyStyle, Ash);

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
            DrawChoice(hits[3], "PANTALLA COMPLETA    " + (fullscreen ? "SÍ" : "NO"), 3, false, false, false);
            string resolution = displayModes.Length == 0
                ? Screen.width + " × " + Screen.height
                : displayModes[resolutionIndex].width + " × " + displayModes[resolutionIndex].height;
            DrawChoice(hits[4], "RESOLUCIÓN    " + resolution, 4, displayModes.Length == 0, false, false);
            DrawChoice(hits[5], "VOLVER", 5, false, false, false);
            Label(helpRect, "En los volúmenes y la resolución, izquierda y derecha cambian el valor. Enter o A confirman. Esc o B regresan.", smallStyle, Ash);
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

        void DrawHud(float width, float height)
        {
            Box(new Rect(width * 0.5f - 1f, 0f, 2f, height), Color.black);
            for (int i = 0; i < players.Length; i++)
            {
                float left = i * width * 0.5f;
                float half = width * 0.5f;
                Box(new Rect(left + 16f, 16f, half - 32f, 78f), new Color(0.015f, 0.014f, 0.012f, 0.9f));
                Box(new Rect(left + 16f, 16f, 2f, 78f), Paper);
                Label(new Rect(left + 28f, 22f, 28f, 26f), (i + 1).ToString(), hudStyle, Ash);
                string held = players[i].Held ? players[i].Held.displayName : "manos libres";
                if (players[i].Held && !players[i].Held.isCandle) held += " (pesada)";
                Label(new Rect(left + 56f, 22f, half - 84f, 26f), PlayerLabel(i) + "   /   " + held, hudStyle, Paper);
                int loose = CountUnstable();
                string danger = Investigation && !Investigation.Ready
                    ? "Registros " + Investigation.EvidenceSummary + (Investigation.Contrasted ? "   consola OK" : "   falta consola")
                        + "   red " + Investigation.PoweredCount + "/" + Investigation.RequiredPower
                    : completed + "/3 mecanismos    recinto " + (Danger >= 90f ? "crítico" : Danger >= dangerAlarmThreshold ? "inestable" : "en observación");
                if (loose > 0) danger += "    " + loose + (loose == 1 ? " desajustado" : " desajustados");
                if (Danger >= dangerAlarmThreshold) danger += "    alto";
                if (Time.unscaledTime < pulseUntil) danger += "    " + pulseText;
                Label(new Rect(left + 56f, 52f, half - 84f, 24f), danger, hudSmallStyle, Danger >= dangerAlarmThreshold ? Accent : Ash);

                float centerX = left + half * 0.5f;
                float centerY = height * 0.5f;
                Box(new Rect(centerX - 7f, centerY - 0.5f, 14f, 1f), new Color(Paper.r, Paper.g, Paper.b, 0.8f));
                Box(new Rect(centerX - 0.5f, centerY - 7f, 1f, 14f), new Color(Paper.r, Paper.g, Paper.b, 0.8f));

                string prompt = players[i].Hint;
                string dropPrompt = players[i].DropHint;
                bool hasPrompt = !string.IsNullOrEmpty(prompt);
                bool hasDropPrompt = !string.IsNullOrEmpty(dropPrompt);
                float panelHeight = hasPrompt ? (hasDropPrompt ? 92f : 64f) : 40f;
                float panelTop = height - 48f - panelHeight;
                var focus = players[i].FocusedMechanism;
                if (focus && focus.unstable)
                {
                    float y = panelTop - 40f;
                    int percent = Mathf.RoundToInt(focus.Hold01 * 100f);
                    string key = DemoPlayerInput.UseLabel(i);
                    string state = players[i].Stabilizing != null ? "estabilizando" : "desajustado";
                    Label(new Rect(left + 36f, y - 16f, half - 72f, 16f), focus.symbol + "  " + state + "  [" + key + "] mantener  " + percent + "%", hudSmallStyle, Paper);
                    Box(new Rect(left + 36f, y, half - 72f, 8f), new Color(0.12f, 0.1f, 0.08f));
                    Box(new Rect(left + 36f, y, (half - 72f) * focus.Hold01, 8f), players[i].Stabilizing != null ? Accent : Paper);
                }
                if (hasPrompt || hasDropPrompt)
                {
                    Box(new Rect(left + 22f, panelTop, half - 44f, panelHeight), new Color(0.015f, 0.014f, 0.012f, 0.9f));
                    if (hasPrompt) Label(new Rect(left + 36f, panelTop + 10f, half - 72f, 48f), prompt, hudStyle, Paper);
                    if (hasDropPrompt) Label(new Rect(left + 36f, height - 78f, half - 72f, 24f), dropPrompt, hudSmallStyle, Paper);
                }
                if (Time.unscaledTime < players[i].MessageUntil)
                {
                    Box(new Rect(left + 22f, 108f, half - 44f, 86f), new Color(0.015f, 0.014f, 0.012f, 0.92f));
                    Label(new Rect(left + 36f, 118f, half - 72f, 66f), players[i].Message, hudStyle, Paper);
                }
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
            return "El peligro llegó al límite y alcanzó a " + who + "." + clock + " Pueden repartir la luz y la manivela de otra forma.";
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
                return "Esc o Continuar reanudan. Opciones cambia el volumen sin abandonar la expedición.\n" + GameplayHelp;
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
            hudSmallStyle = Style(14, FontStyle.Normal, TextAnchor.MiddleLeft, false);
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
