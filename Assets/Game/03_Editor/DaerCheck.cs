#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamejam2026;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public static class DaerCheck
{
    public const string Report = "Temp/DaerCheck.txt";

    [MenuItem("Gamejam/Daer/Comprobar en Play (sin registrar partidas humanas)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play antes de comprobar.");
        JamPolish.OpenGame();
        if (!GameObject.Find(DaerSetup.RootName)) throw new InvalidOperationException("Prepara primero el paso de Daer.");
        File.WriteAllText(Report, "Comprobación automática de Daer; no sustituye la prueba con dos personas.\n");
        SessionState.SetBool("Daer.Check", true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Bind()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("Daer.Check", false)) return;
            SessionState.SetBool("Daer.Check", false);
            new GameObject("Comprobación temporal de Daer").AddComponent<DaerPlayProbe>();
        };
    }
}

public class DaerPlayProbe : MonoBehaviour
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo Interact = typeof(DemoPlayer).GetMethod("Interact", Private);
    static readonly FieldInfo Order = typeof(DemoGame).GetField("order", Private);
    static readonly MethodInfo Pause = typeof(DemoGame).GetMethod("PauseNow", Private);
    static readonly MethodInfo Continue = typeof(DemoGame).GetMethod("Continue", Private);
    Keyboard originalKeyboard;
    Keyboard testKeyboard;
    bool originalBackground;
    int assertions;

    void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        assertions++;
        File.AppendAllText(DaerCheck.Report, "OK " + message + "\n");
    }

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        originalBackground = Application.runInBackground;
        Application.runInBackground = true;
        var test = Check();
        while (true)
        {
            bool more;
            object wait;
            try { more = test.MoveNext(); wait = more ? test.Current : null; }
            catch (Exception ex)
            {
                File.AppendAllText(DaerCheck.Report, "FAILED " + ex + "\n");
                Debug.LogException(ex);
                break;
            }
            if (!more)
            {
                File.AppendAllText(DaerCheck.Report, "PASSED " + assertions + " comprobaciones\n");
                Debug.Log("DAER_CHECK_PASSED " + assertions);
                break;
            }
            yield return wait;
        }
        if (testKeyboard != null) InputSystem.RemoveDevice(testKeyboard);
        if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
        Application.runInBackground = originalBackground;
        Time.timeScale = 1f;
        EditorApplication.isPlaying = false;
    }

    IEnumerator Check()
    {
        yield return null;
        originalKeyboard = Keyboard.current;
        testKeyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.W, Key.UpArrow, Key.E, Key.U));
        InputSystem.Update();
        var input1 = DemoPlayerInput.Read(0, 115, 0, .02f);
        var input2 = DemoPlayerInput.Read(1, 115, 0, .02f);
        Require(input1.Move.y > .7f && input2.Move.y > .7f && input1.UseHeld && input2.UseHeld, "teclado actual de los dos jugadores (E y U)");
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
        InputSystem.Update();

        int[][] permutations = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
        foreach (var order in permutations)
        {
            var game = FindAnyObjectByType<DemoGame>();
            var recorder = FindAnyObjectByType<DaerPlaytestRecorder>();
            recorder.recordRounds = false;
            var passage = FindAnyObjectByType<DemoPassage>();
            var clues = FindObjectsByType<DemoClue>(FindObjectsSortMode.None);
            var clue = clues.Single(c => c.nextSymbolOnly);
            var originalClue = clues.Single(c => !c.nextSymbolOnly);
            Order.SetValue(game, order);
            int openings = 0;
            int finishes = 0;
            bool won = false;
            passage.onOpening.AddListener(() => openings++);
            game.RoundFinished += success => { finishes++; won = success; };
            game.Begin();
            yield return null;
            foreach (var player in game.players) player.enabled = false;
            Require(game.Playing && game.players.Length == 2 && game.players[0].Held.isCandle && !game.players[1].Held.isCandle, "inicio sin mando, vela y manivela: " + string.Join("", order));
            Require(!passage.OpenRequested && !passage.IsOpen && passage.blocker.enabled, "compuerta reiniciada y cerrada");
            Require(game.candles.Skip(1).All(c => !c.gameObject.activeSelf && !c.candleLight.enabled), "vela de reserva sigue apagada");
            Require(game.dangerPerSecond == .31f && game.darknessPenalty == .6f && game.errorPenalty == 12f && game.successRelief == 10f && game.unstablePenalty == 1.25f && game.stabilizeRelief == 8f && game.destabilizeMin == 38f && game.destabilizeMax == 52f, "valores de dificultad actuales conservados");

            var worker = game.players[1];
            var candle = game.candles[0];
            if (order == permutations[0])
            {
                FinalMapBuilder.Validate();
                Require(true, "rutas originales A/B/C transitables con el nuevo paso cerrado");
                Position(worker, new Vector3(-14, .1f, 88), new Vector3(-14, 1.5f, 94.5f));
                MoveToward(worker, new Vector3(-14, .1f, 92));
                Require(worker.transform.position.z < 90, "la compuerta cerrada bloquea físicamente el atajo");
            }
            candle.Drop();
            candle.transform.position = clue.transform.position + Vector3.back * 8;
            Physics.SyncTransforms();
            Require(!clue.TryRead(game, out _) && !clue.inscriptionLabel.GetComponent<Renderer>().enabled, "segundo cartel ilegible sin vela próxima");
            candle.transform.position = clue.transform.position + new Vector3(.6f, -.5f, -1f);
            Physics.SyncTransforms();
            yield return null;
            Require(clue.TryRead(game, out string next) && next == game.mechanisms[order[0]].symbol && clue.inscriptionLabel.text == next, "segundo cartel revela solo el siguiente símbolo");
            candle.candleLight.enabled = false;
            Require(!clue.TryRead(game, out _), "una vela apagada no revela el cartel");
            candle.candleLight.enabled = true;
            if (order == permutations[0])
            {
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.transform.position = Vector3.Lerp(candle.candleLight.transform.position, clue.transform.position, .5f);
                blocker.transform.localScale = Vector3.one * .4f;
                Physics.SyncTransforms();
                Require(!clue.TryRead(game, out _), "la inscripción no se lee a través de obstáculos");
                Destroy(blocker);
                yield return null;
                Position(worker, new Vector3(-14, .1f, 92.7f), clue.transform.position);
                Interact.Invoke(worker, new object[] { true, true, false });
                Require(worker.Message == next, "interacción del jugador muestra solo el símbolo en su HUD");
                ScreenCapture.CaptureScreenshot("Temp/DaerSegundoCartel.png");
                candle.transform.position += Vector3.back * 8;
                worker.enabled = true;
                yield return null;
                worker.enabled = false;
                Require(worker.MessageUntil <= Time.unscaledTime, "se oculta la lectura del HUD al alejar la vela");
                candle.transform.position = originalClue.transform.position + new Vector3(.6f, -.5f, -1f);
                Physics.SyncTransforms();
                Require(originalClue.TryRead(game, out string full) && full == game.SequenceText, "la primera inscripción conserva el orden completo");
                candle.transform.position = clue.transform.position + new Vector3(.6f, -.5f, -1f);
            }

            var wrong = game.mechanisms[order[1]];
            PlaceCrank(game, worker, wrong);
            Require(!passage.OpenRequested && openings == 0 && game.CompletedCount == 0 && game.NextSymbol == next, "un error no abre el paso ni adelanta el símbolo");
            PlaceCrank(game, worker, game.mechanisms[order[0]]);
            Require(passage.OpenRequested && openings == 1 && game.CompletedCount == 1 && game.NextSymbol == game.mechanisms[order[1]].symbol, "el primer acierto abre una sola vez y avanza el cartel");
            if (order == permutations[0])
            {
                Pause.Invoke(game, null);
                float before = passage.Open01;
                yield return new WaitForSecondsRealtime(.15f);
                Require(passage.Open01 == before, "la apertura respeta la pausa");
                Continue.Invoke(game, null);
            }
            yield return new WaitForSeconds(1.7f);
            Require(passage.IsOpen && !passage.blocker.enabled && openings == 1, "compuerta completamente abierta sin colisión");
            Require(clue.inscriptionLabel.text == game.NextSymbol, "texto visible actualizado tras el acierto");
            if (order == permutations[0])
            {
                Position(worker, new Vector3(-14, .1f, 88), clue.transform.position);
                MoveToward(worker, new Vector3(-14, .1f, 92));
                Require(worker.transform.position.z > 91.5f, "el jugador atraviesa físicamente el nuevo paso");
            }
            PlaceCrank(game, worker, game.mechanisms[order[1]]);
            var first = game.mechanisms[order[0]];
            first.unstable = true;
            PlaceCrank(game, worker, game.mechanisms[order[2]]);
            yield return null;
            Require(game.Playing && game.CompletedCount == 3 && !clue.TryRead(game, out _) && !clue.inscriptionLabel.GetComponent<Renderer>().enabled, "al terminar el orden no revela más símbolos; aún exige estabilizar");
            bool inputRepair = order == permutations[0];
            if (inputRepair)
            {
                Position(worker, first.transform.position + Vector3.back * 2f, first.wheel.position);
                Interact.Invoke(worker, new object[] { false, true, false });
                Require(worker.Stabilizing == first && first.Hold01 > 0f, "mirar al mecanismo inicia la barra de estabilización");
                yield return null;
                Interact.Invoke(worker, new object[] { false, false, false });
                yield return null;
                Require(first.Hold01 == 0f, "soltar antes de tiempo interrumpe la estabilización");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.U));
                InputSystem.Update();
            }
            float deadline = Time.realtimeSinceStartup + 4f;
            while (first.unstable && Time.realtimeSinceStartup < deadline)
            {
                if (inputRepair)
                {
                    var held = DemoPlayerInput.Read(1, 115, 0, Time.deltaTime);
                    Interact.Invoke(worker, new object[] { false, held.UseHeld, false });
                }
                else first.Hold(worker, true);
                yield return null;
            }
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            InputSystem.Update();
            Require(!first.unstable && !game.Playing && won && finishes == 1 && openings == 1, "mantener estabilización permite ganar; no reabre la compuerta");
            if (order == permutations[0])
            {
                yield return new WaitForSecondsRealtime(1.7f);
                var ending = game.GetComponent<DemoAtmosphere>();
                Require(ending.victoryLights.Any(l => l && l.intensity >= 3.5f && l.color == Color.white), "la victoria enciende las luces blancas del mapa");
                Require(candle.candleLight.enabled, "la vela permanece encendida al ganar");
            }
            yield return SceneManager.LoadSceneAsync("Assets/Game/00_Scenes/Demo.unity");
            yield return null;
        }
        var lossGame = FindAnyObjectByType<DemoGame>();
        FindAnyObjectByType<DaerPlaytestRecorder>().recordRounds = false;
        lossGame.Begin();
        yield return null;
        foreach (var player in lossGame.players) player.enabled = false;
        bool lost = false;
        lossGame.RoundFinished += success => lost = !success;
        var currentOrder = (int[])Order.GetValue(lossGame);
        var wrongTarget = lossGame.mechanisms[currentOrder[1]];
        for (int attempt = 0; attempt < 12 && lossGame.Playing; attempt++)
        {
            if (wrongTarget.HasCrank) wrongTarget.DetachCrank(lossGame.players[1]);
            wrongTarget.Use(lossGame.players[1]);
        }
        Require(lost && !lossGame.Playing && lossGame.Danger == 100f && lossGame.CompletedCount == 0, "derrota al 100% conservada");
        Require(!FindAnyObjectByType<DemoPassage>().OpenRequested, "una derrota sin aciertos conserva el paso cerrado");
        Require(!lossGame.candles[0].candleLight.enabled && lossGame.candles[0].GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Llama")).All(r => !r.enabled), "la derrota apaga la vela y sus dos capas de llama");
        yield return new WaitForSecondsRealtime(2f);
        Require(lossGame.GetComponent<DemoAtmosphere>().Blackout >= .99f, "la derrota completa el fundido a negro");
    }

    static void PlaceCrank(DemoGame game, DemoPlayer player, DemoMechanism target)
    {
        foreach (var mechanism in game.mechanisms)
            if (mechanism.HasCrank) mechanism.DetachCrank(player);
        target.Use(player);
    }

    static void Position(DemoPlayer player, Vector3 position, Vector3 look)
    {
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = position;
        player.transform.rotation = Quaternion.identity;
        controller.enabled = true;
        player.view.transform.LookAt(look);
        Physics.SyncTransforms();
    }

    static void MoveToward(DemoPlayer player, Vector3 target)
    {
        var controller = player.GetComponent<CharacterController>();
        for (int i = 0; i < 70; i++)
        {
            Vector3 remaining = Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up);
            if (remaining.magnitude < .1f) break;
            controller.Move(Vector3.ClampMagnitude(remaining, .12f) + Vector3.down * .02f);
        }
    }
}
#endif
