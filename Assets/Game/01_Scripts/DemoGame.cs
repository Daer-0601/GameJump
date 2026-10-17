using System;
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
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Range(0f, 100f)] public float dangerAlarmThreshold = 70f;
        public float dangerAlarmInterval = 5f;
        public float dangerPerSecond = 0.33f;
        public float darknessPenalty = 0.3f;
        public float errorPenalty = 10f;
        public float successRelief = 9f;
        public bool Playing => started && !paused && !finished;
        public bool PlayerInputEnabled => Playing && Time.frameCount > inputResumeFrame;
        public float Danger { get; private set; }
        public string SequenceText => "ORDEN: " + mechanisms[order[0]].symbol + " > " + mechanisms[order[1]].symbol + " > " + mechanisms[order[2]].symbol;
        public float progress => completed / 3f;

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
        float[] lightLevels;
        AudioSource sfxSource;

        string name1 = "";
        string name2 = "";
        int overlay = -2;
        int focusIndex;
        int hover = -1;
        int caret;
        int hitCount;
        int pointerFrame = -1;
        bool keyboardEdit;
        float axisTimerX;
        float axisTimerY;
        float backspaceTimer;
        MenuCommand command;
        Keyboard textKeyboard;

        readonly Rect[] hits = new Rect[4];
        readonly Rect[] visuals = new Rect[4];
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
            lightLevels = new float[atmosphereLights.Length];
            for (int i = 0; i < lightLevels.Length; i++) lightLevels[i] = atmosphereLights[i].intensity;
            nextDisturbance = 12f;
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
            sfxSource.volume = 1f;
            for (int i = 0; i < players.Length && i < candles.Length; i++) candles[i].PickUp(players[i]);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (ambience) ambience.Play();

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
        }

        void Simulate()
        {
            if (!Playing) return;

            elapsed += Time.deltaTime;
            float rate = dangerPerSecond;
            foreach (var player in players)
            {
                bool lit = false;
                foreach (var candle in candles)
                {
                    if (!candle || !candle.candleLight || !candle.candleLight.enabled) continue;
                    if (Vector3.Distance(player.transform.position + Vector3.up, candle.candleLight.transform.position) < 5f) lit = true;
                }
                if (!lit) rate += darknessPenalty;
            }
            Danger = Mathf.Clamp(Danger + rate * Time.deltaTime, 0f, 100f);
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
            for (int i = 0; i < atmosphereLights.Length; i++)
            {
                if (!atmosphereLights[i]) continue;
                float flicker = elapsed < disturbanceUntil ? Mathf.Lerp(0.1f, 1f, Mathf.PerlinNoise(elapsed * 13f, i)) : 1f;
                atmosphereLights[i].intensity = lightLevels[i] * flicker;
            }
            if (ambience) ambience.volume = Mathf.Lerp(0.16f, 0.42f, Danger / 100f);
        }

        public void Activate(DemoMechanism mechanism, DemoPlayer player)
        {
            if (!Playing || mechanism.done) return;
            if (mechanism.id != order[completed])
            {
                Danger = Mathf.Min(100f, Danger + errorPenalty);
                disturbanceUntil = elapsed + 2f;
                player.Tell("Orden incorrecto. Los pasos correctos se conservan.");
                if (Danger >= 100f) Finish(false);
                return;
            }
            mechanism.done = true;
            completed++;
            PlaySfx(gearsClip);
            PlaySfx(lightClip);
            Danger = Mathf.Max(0f, Danger - successRelief);
            foreach (var teammate in players) teammate.Tell(mechanism.symbol + " completado. " + completed + "/3");
            if (completed == order.Length) Finish(true);
        }

        void Finish(bool success)
        {
            if (finished) return;
            finished = true;
            won = success;
            PlaySfx(jumpScareClip);
            Time.timeScale = 0f;
            LockPointer();
        }

        void PlaySfx(AudioClip clip)
        {
            if (clip && sfxSource) sfxSource.PlayOneShot(clip, sfxVolume);
        }

        public void PlayFootstep(AudioSource source)
        {
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
        }

        void PauseNow()
        {
            paused = true;
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

        static int ItemCount(int id)
        {
            if (id == 0) return 4;
            if (id < 0) return 0;
            return 3;
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
            if (!keyboardEdit || overlay != 0 || focusIndex > 1 || char.IsControl(character)) return;
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

            if (escape)
            {
                if (editing) { StopEdit(true); return; }
                if (overlay == 1)
                {
                    focusIndex = 0;
                    Queue(MenuCommand.Continue);
                }
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
            if (vertical != 0) MoveFocus(vertical);
        }

        void ActivateFocused()
        {
            if (overlay == 0)
            {
                if (focusIndex == 2) Queue(MenuCommand.Begin);
                else if (focusIndex == 3) Queue(MenuCommand.Quit);
            }
            else if (overlay == 1)
            {
                if (focusIndex == 0) Queue(MenuCommand.Continue);
                else if (focusIndex == 1) Queue(MenuCommand.MainMenu);
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
            return overlay == 0 && index >= 0 && index < 2;
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
                if (overlay >= 0)
                {
                    LayoutOverlay(width, height);
                    HandlePointer();
                    DrawBackdrop(width, height);
                    if (overlay == 0) DrawTitle();
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
            float columnW = Mathf.Min(640f, width - 72f);
            float x = (width - columnW) * 0.5f;
            columnRect = new Rect(x, 0f, columnW, height);
            if (overlay == 0) LayoutTitle(x, columnW, height);
            else LayoutSimple(x, columnW, height);
        }

        void LayoutTitle(float x, float columnW, float height)
        {
            float y = Mathf.Max(height * 0.055f + 18f, 36f);
            titleRect = new Rect(x, y, columnW, 68f);
            y += 78f;
            briefRect = new Rect(x, y, columnW, 72f);
            y += 86f;
            hitCount = 4;
            for (int i = 0; i < 2; i++)
            {
                hits[i] = new Rect(x, y, columnW, 68f);
                visuals[i] = new Rect(x + 16f, y + 22f, columnW - 16f, 46f);
                y += 80f;
            }
            y += 16f;
            hits[2] = visuals[2] = new Rect(x, y + 20f, columnW, 48f);
            hits[3] = visuals[3] = new Rect(x, hits[2].yMax + 8f, columnW, 48f);
            statusRect = new Rect(x, hits[3].yMax + 12f, columnW, 48f);
            float helpTop = statusRect.yMax + 2f;
            float helpBottom = height - height * 0.055f - 10f;
            helpRect = new Rect(x, helpTop, columnW, Mathf.Max(48f, helpBottom - helpTop));
        }

        void LayoutSimple(float x, float columnW, float height)
        {
            const float block = 470f;
            float y = Mathf.Max(height * 0.055f + 18f, (height - block) * 0.4f);
            titleRect = new Rect(x, y, columnW, 72f);
            y += 88f;
            briefRect = new Rect(x, y, columnW, 78f);
            y += 96f;
            hitCount = 3;
            for (int i = 0; i < 3; i++)
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

            if (!mouse.leftButton.wasPressedThisFrame || pointerFrame == Time.frameCount) return;
            pointerFrame = Time.frameCount;
            if (hover < 0)
            {
                StopEdit(true);
                return;
            }

            focusIndex = hover;
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
            Box(new Rect(0f, 0f, width, height), Wash);
            if (vignette) GUI.DrawTexture(new Rect(0f, 0f, width, height), vignette, ScaleMode.StretchToFill, true);
            if (grain)
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.16f);
                float tilesX = Mathf.Max(1f, width / 180f);
                float tilesY = Mathf.Max(1f, height / 180f);
                GUI.DrawTextureWithTexCoords(new Rect(0f, 0f, width, height), grain, new Rect(0f, 0f, tilesX, tilesY));
                GUI.color = previous;
            }
            Box(new Rect(columnRect.x - 40f, 0f, columnRect.width + 80f, height), new Color(0f, 0f, 0f, 0.5f));
            float bar = height * 0.055f;
            Box(new Rect(0f, 0f, width, bar), Color.black);
            Box(new Rect(0f, height - bar, width, bar), Color.black);
        }

        void DrawTitle()
        {
            DrawTracked(titleRect, "A Oscuras", glyphStyle, 7f, Paper);
            Rule(titleRect.center.x, titleRect.yMax + 2f, 168f);
            Label(briefRect, "Encuentren la inscripción y activen los tres mecanismos en el orden indicado. Para llevar la manivela hay que dejar la vela.", bodyStyle, Ash);

            DrawName(0, "Nombre del jugador 1");
            DrawName(1, "Nombre del jugador 2");
            DrawChoice(hits[2], "INICIAR EXPEDICIÓN", 2, false, false, false);
            DrawChoice(hits[3], "SALIR", 3, false, false, false);
            Label(statusRect, "Dos jugadores en un teclado compartido.", smallStyle, Ash);
            Label(helpRect, HelpText(), smallStyle, Ash);
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
                DrawChoice(hits[1], "VOLVER AL MENÚ", 1, false, false, false);
                DrawChoice(hits[2], "SALIR", 2, false, false, false);
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
            if (focused) Box(new Rect(hit.x, hit.y + hit.height * 0.5f - 1f, 11f, 2f), ink);
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
                Label(new Rect(left + 56f, 22f, half - 84f, 26f), PlayerLabel(i) + "   /   " + held, hudStyle, Paper);
                string danger = completed + "/3 mecanismos    peligro " + Mathf.CeilToInt(Danger) + "%";
                if (Danger >= dangerAlarmThreshold) danger += "    alto";
                Label(new Rect(left + 56f, 52f, half - 84f, 24f), danger, hudSmallStyle, Ash);
                Box(new Rect(left + 20f, height - 18f, half - 40f, 4f), new Color(0.16f, 0.15f, 0.13f, 0.9f));
                Box(new Rect(left + 20f, height - 18f, (half - 40f) * Danger / 100f, 4f), Paper);

                float centerX = left + half * 0.5f;
                float centerY = height * 0.5f;
                Box(new Rect(centerX - 7f, centerY - 0.5f, 14f, 1f), new Color(Paper.r, Paper.g, Paper.b, 0.8f));
                Box(new Rect(centerX - 0.5f, centerY - 7f, 1f, 14f), new Color(Paper.r, Paper.g, Paper.b, 0.8f));

                string prompt = players[i].Hint;
                if (!string.IsNullOrEmpty(prompt))
                {
                    Box(new Rect(left + 22f, height - 112f, half - 44f, 64f), new Color(0.015f, 0.014f, 0.012f, 0.9f));
                    Label(new Rect(left + 36f, height - 102f, half - 72f, 48f), "[" + DemoPlayerInput.UseLabel(players[i].playerIndex) + "]  " + prompt, hudStyle, Paper);
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
            if (won) return who + " se coordinaron y cerraron el incidente.";
            return "El peligro llegó al límite y alcanzó a " + who + ". Pueden repartir la luz y la manivela de otra forma.";
        }

        string HelpText()
        {
            if (overlay == 0 && keyboardEdit)
                return "Escribiendo. Enter confirma el nombre. Esc sale del campo. Ctrl+V pega.\n" + GameplayHelp;
            if (overlay == 0)
                return "Flechas o Tab para elegir. Enter confirma o edita un nombre. También puedes usar el ratón.\n" + GameplayHelp;
            if (overlay == 1)
                return "Esc o Continuar reanudan. Volver al menú abandona esta expedición.\n" + GameplayHelp;
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
