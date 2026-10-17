using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Gamejam2026
{
    public class DemoGame : MonoBehaviour
    {
        public DemoPlayer[] players;
        public DemoMechanism[] mechanisms;
        public CarryObject[] candles;
        public Light[] atmosphereLights;
        public AudioSource ambience;
        public float dangerPerSecond = 0.33f;
        public float darknessPenalty = 0.3f;
        public float errorPenalty = 10f;
        public float successRelief = 9f;
        public bool twoGamepads;
        public bool Playing => started && !paused && !finished;
        public float Danger { get; private set; }
        public string SequenceText => "ORDEN: " + mechanisms[order[0]].symbol + " > " + mechanisms[order[1]].symbol + " > " + mechanisms[order[2]].symbol;
        int[] order = { 0, 1, 2 };
        int completed;
        bool started;
        bool paused;
        bool finished;
        bool won;
        float elapsed;
        float nextDisturbance;
        float disturbanceUntil;
        float[] lightLevels;
        GUIStyle titleStyle;
        GUIStyle textStyle;
        GUIStyle smallStyle;
        Texture2D pixel;
        public float progress => completed / 3f;

        void Start()
        {
            Time.timeScale = 1f;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            lightLevels = new float[atmosphereLights.Length];
            for (int i = 0; i < lightLevels.Length; i++) lightLevels[i] = atmosphereLights[i].intensity;
            nextDisturbance = 12f;
            for (int i = 0; i < players.Length && i < candles.Length; i++) candles[i].PickUp(players[i]);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (ambience) ambience.Play();
        }

        public void Begin()
        {
            int requiredPads = twoGamepads ? 2 : 1;
            if (Gamepad.all.Count < requiredPads) return;
            started = true;
            LockPointer();
        }

        void LockPointer()
        {
            bool capture = Playing && !twoGamepads;
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        void Update()
        {
            if (!started)
            {
                foreach (var pad in Gamepad.all)
                {
                    if (pad.selectButton.wasPressedThisFrame) twoGamepads = !twoGamepads;
                    if (pad.buttonSouth.wasPressedThisFrame) Begin();
                }
            }
            else if (finished)
            {
                foreach (var pad in Gamepad.all)
                    if (pad.buttonSouth.wasPressedThisFrame) Restart();
            }
            bool toggle = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            foreach (var pad in Gamepad.all) toggle |= pad.startButton.wasPressedThisFrame;
            if (toggle && started && !finished)
            {
                paused = !paused;
                Time.timeScale = paused ? 0f : 1f;
                LockPointer();
            }
            if (finished && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Restart();
            if (!Playing) return;

            // pausa si se desconecta un mando
            if (Gamepad.all.Count < (twoGamepads ? 2 : 1))
            {
                paused = true;
                Time.timeScale = 0f;
                LockPointer();
                return;
            }

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
            if (Danger >= 100f) Finish(false);

            if (elapsed >= nextDisturbance)
            {
                disturbanceUntil = elapsed + 1.4f;
                nextDisturbance = elapsed + Random.Range(14f, 24f) * Mathf.Lerp(1f, 0.6f, Danger / 100f);
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
            Danger = Mathf.Max(0f, Danger - successRelief);
            foreach (var teammate in players) teammate.Tell(mechanism.symbol + " completado. " + completed + "/3");
            if (completed == order.Length) Finish(true);
        }

        void Finish(bool success)
        {
            finished = true;
            won = success;
            Time.timeScale = 0f;
            LockPointer();
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && Playing)
            {
                paused = true;
                Time.timeScale = 0f;
                LockPointer();
            }
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void Styles()
        {
            if (textStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 38, fontStyle = FontStyle.Bold, wordWrap = true };
            titleStyle.normal.textColor = new Color(0.9f, 0.82f, 0.65f);
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            smallStyle = new GUIStyle(textStyle) { fontSize = 15 };
            pixel = Texture2D.whiteTexture;
        }

        void Box(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = Color.white;
        }

        void OnGUI()
        {
            Styles();
            float scale = Mathf.Max(0.5f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            if (!started || paused || finished)
            {
                Box(new Rect(0, 0, width, height), new Color(0.015f, 0.02f, 0.025f, 0.94f));
                GUILayout.BeginArea(new Rect(width / 2f - 340f, height / 2f - 260f, 680f, 550f));
                GUILayout.Label(finished ? (won ? "INCIDENTE NEUTRALIZADO" : "LA OSCURIDAD LOS ALCANZO") : (paused ? "PAUSA" : "GAMEJAM 2026"), titleStyle);
                GUILayout.Space(14);
                if (!started)
                {
                    GUILayout.Label("Encuentren la inscripcion. Usen la manivela para activar los tres mecanismos en el orden indicado.\n\nUna mano: llevar la manivela exige dejar la vela. Organicen la luz y los recorridos juntos.", textStyle);
                    GUILayout.Space(18);
                    twoGamepads = GUILayout.Toggle(twoGamepads, " Dos mandos (desmarcado: teclado y mouse + un mando)");
                    GUILayout.Label("Mandos conectados: " + Gamepad.all.Count + " | BACK cambia modo, A empieza", smallStyle);
                    GUI.enabled = Gamepad.all.Count >= (twoGamepads ? 2 : 1);
                    if (GUILayout.Button("EMPEZAR", GUILayout.Height(44))) Begin();
                    GUI.enabled = true;
                }
                else if (paused)
                {
                    GUILayout.Label("Reconecten los mandos si hace falta.\nLa partida esta detenida.", textStyle);
                    GUI.enabled = Gamepad.all.Count >= (twoGamepads ? 2 : 1);
                    if (GUILayout.Button("CONTINUAR", GUILayout.Height(44))) { paused = false; Time.timeScale = 1f; LockPointer(); }
                    GUI.enabled = true;
                }
                else
                {
                    GUILayout.Label(won ? "Se coordinaron y cerraron el incidente." : "El tiempo y los errores dejaron crecer el peligro. Prueben otra distribucion de tareas.", textStyle);
                    if (GUILayout.Button("VOLVER A INTENTAR", GUILayout.Height(44))) Restart();
                }
                GUILayout.Space(14);
                GUILayout.Label("TECLADO: WASD mover | mouse mirar | E interactuar | Q soltar\nMANDO: sticks mover/mirar | A interactuar | X soltar\nESC / START: pausa", smallStyle);
                GUILayout.EndArea();
                return;
            }

            Box(new Rect(width / 2f - 2f, 0, 4f, height), Color.black);
            for (int i = 0; i < players.Length; i++)
            {
                float left = i * width / 2f;
                Box(new Rect(left + 16, 16, width / 2f - 32, 75), new Color(0.02f, 0.025f, 0.03f, 0.85f));
                GUI.Label(new Rect(left + 30, 24, 450, 28), "JUGADOR " + (i + 1) + "  /  " + (players[i].Held ? players[i].Held.displayName : "manos libres"), textStyle);
                GUI.Label(new Rect(left + 30, 54, 450, 24), completed + "/3 mecanismos  |  peligro " + Mathf.CeilToInt(Danger) + "%", smallStyle);
                Box(new Rect(left + 20, height - 16, (width / 2f - 40) * Danger / 100f, 5), new Color(0.65f, 0.2f, 0.12f));
                GUI.Label(new Rect(left + width / 4f - 4, height / 2f - 14, 20, 30), "+", textStyle);
                string prompt = players[i].Hint;
                if (!string.IsNullOrEmpty(prompt)) GUI.Label(new Rect(left + 25, height - 100, width / 2f - 50, 55), "[E / A] " + prompt, textStyle);
                if (Time.unscaledTime < players[i].MessageUntil)
                {
                    Box(new Rect(left + 22, 106, width / 2f - 44, 90), new Color(0.02f, 0.025f, 0.03f, 0.9f));
                    GUI.Label(new Rect(left + 35, 118, width / 2f - 70, 75), players[i].Message, textStyle);
                }
            }
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
