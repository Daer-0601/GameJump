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

public static class JamPlayCheck
{
    static void PlaceCrank(DemoGame game, DemoPlayer player, DemoMechanism target)
    {
        if (target.HasCrank) return;
        foreach (var mechanism in game.mechanisms)
        {
            if (!mechanism.HasCrank) continue;
            mechanism.DetachCrank(player);
            break;
        }
        target.Use(player);
    }

    [MenuItem("Gamejam/Comprobar pulido en Play")]
    public static void Run()
    {
        JamPolish.OpenGame();
        File.WriteAllText("Temp/JamPlayCheck.txt", "Iniciando comprobación en Play\n");
        SessionState.SetBool("JamPolish.Check", true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Bind()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("JamPolish.Check", false)) return;
            SessionState.SetBool("JamPolish.Check", false);
            new GameObject("Verificación temporal de pulido").AddComponent<JamPlayProbe>();
        };
    }
}

public class JamPlayProbe : MonoBehaviour
{
    static readonly MethodInfo Interact = typeof(DemoPlayer).GetMethod("Interact", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly MethodInfo Simulate = typeof(DemoGame).GetMethod("Simulate", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo NextDestabilize = typeof(DemoGame).GetField("nextDestabilize", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo Elapsed = typeof(DemoGame).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic);
    Keyboard originalKeyboard;
    Keyboard testKeyboard;
    DemoGame game;

    void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        File.AppendAllText("Temp/JamPlayCheck.txt", "OK " + message + "\n");
    }

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        var test = Check();
        while (true)
        {
            bool more;
            object wait;
            try { more = test.MoveNext(); wait = more ? test.Current : null; }
            catch (Exception ex)
            {
                File.AppendAllText("Temp/JamPlayCheck.txt", "FAILED " + ex + "\n");
                Debug.LogException(ex);
                break;
            }
            if (!more) break;
            yield return wait;
        }
        if (testKeyboard != null) InputSystem.RemoveDevice(testKeyboard);
        if (originalKeyboard != null && originalKeyboard.added) originalKeyboard.MakeCurrent();
        EditorApplication.isPlaying = false;
    }

    IEnumerator Check()
    {
        yield return new WaitForSecondsRealtime(1f);
        game = FindAnyObjectByType<DemoGame>();
        originalKeyboard = Keyboard.current;
        testKeyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.W, Key.LeftShift, Key.UpArrow, Key.RightShift, Key.E, Key.A));
        InputSystem.Update();
        var one = DemoPlayerInput.Read(0, 115f, 0f, 0.02f);
        var two = DemoPlayerInput.Read(1, 115f, 0f, 0.02f);
        Require(one.Move.y > 0.7f && two.Move.y == 1 && one.Run && two.Run && one.UseHeld && two.UseHeld, "teclas de movimiento, carrera y uso de ambos jugadores");
        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
        InputSystem.Update();
        game.Begin();
        yield return null;
        Require(game.Playing, "inicia sin exigir mando");
        ScreenCapture.CaptureScreenshot("Temp/JamStart.png");
        foreach (var player in game.players) player.enabled = false;
        var traveller = game.players[1];
        Vector3 initialPosition = traveller.transform.position;
        Quaternion initialLook = traveller.view.transform.rotation;
        foreach (var mechanism in game.mechanisms)
        {
            Vector3 centre = new Vector3(0f, 0.1f, 0.5f);
            Vector3 direction = Vector3.ProjectOnPlane(mechanism.transform.position - centre, Vector3.up).normalized;
            Vector3 destination = mechanism.transform.position - direction * 1.9f;
            destination.y = 0.1f;
            Position(traveller, centre, destination + Vector3.up * 1.2f);
            var controller = traveller.GetComponent<CharacterController>();
            for (int step = 0; step < 120; step++)
            {
                Vector3 remaining = Vector3.ProjectOnPlane(destination - traveller.transform.position, Vector3.up);
                if (remaining.magnitude < 0.15f) break;
                controller.Move(Vector3.ClampMagnitude(remaining, 0.18f) + Vector3.down * 0.03f);
            }
            Require(Vector3.ProjectOnPlane(destination - traveller.transform.position, Vector3.up).magnitude < 0.5f,
                "recorrido físico transitable hacia " + mechanism.symbol);
        }
        Position(traveller, initialPosition, initialPosition + Vector3.forward * 3f + Vector3.up * 1.6f);
        traveller.view.transform.rotation = initialLook;
        Require(game.players[0].Held.isCandle && !game.players[1].Held.isCandle, "vela J1 y manivela J2");
        Require(game.walkingClip && game.lightClip && game.jumpScareClip && game.dangerClip && game.gearsClip && game.ambience,
            "referencias de ambiente, pasos, luz, engranaje, alarma y susto conservadas");
        Require(game.candles.Skip(1).All(c => !c.gameObject.activeSelf && !c.candleLight.enabled), "segunda vela desactivada");
        var candle = game.candles[0];
        candle.Drop();
        Require(candle.candleLight.enabled, "vela soltada sigue encendida");
        var clue = FindAnyObjectByType<DemoClue>();
        var player1 = game.players[0];
        Position(player1, clue.transform.position - Vector3.forward * 2f - Vector3.up * clue.transform.position.y, clue.transform.position);
        candle.transform.position = new Vector3(12, 0, -12);
        Interact.Invoke(player1, new object[] { true, true, false });
        Require(!clue.CanRead(game) && !(player1.Message ?? "").StartsWith("ORDEN:"), "inscripción no revela el orden sin vela");
        candle.transform.position = clue.transform.position + new Vector3(0.8f, -0.7f, -0.8f);
        Interact.Invoke(player1, new object[] { true, true, false });
        Require(clue.CanRead(game) && player1.Message == game.SequenceText, "interacción revela orden con vela cercana");
        candle.transform.position = new Vector3(12, 0, -12);
        player1.enabled = true;
        yield return null;
        player1.enabled = false;
        Require(player1.MessageUntil <= Time.unscaledTime, "el texto leído se oculta al alejar la vela");
        candle.PickUp(player1);
        var crankPlayer = game.players[1];
        string[] order = game.SequenceText.Replace("ORDEN: ", "").Split(" > ");
        var first = game.mechanisms.First(m => m.symbol == order[0]);
        var wrong = game.mechanisms.First(m => m.symbol != order[0]);
        float danger = game.Danger;
        wrong.Use(crankPlayer);
        Require(game.progress == 0 && game.Danger > danger, "error aumenta peligro y conserva progreso");
        PlaceCrank(game, crankPlayer, first);
        float progress = game.progress;
        wrong = game.mechanisms.First(m => !m.done && m.symbol != order[1]);
        PlaceCrank(game, crankPlayer, wrong);
        Require(game.progress == progress && first.done, "error posterior conserva el paso correcto");
        Elapsed.SetValue(game, (float)Elapsed.GetValue(game) + 2f);
        NextDestabilize.SetValue(game, (float)Elapsed.GetValue(game) - 0.1f);
        Simulate.Invoke(game, null);
        Require(first.unstable, "temporizador desajusta un mecanismo completado");
        yield return null;
        Require(first.GetComponent<AudioSource>().isPlaying && first.GetComponentInChildren<ParticleSystem>().isPlaying,
            "desajuste reproduce loop y partículas");
        foreach (string symbol in order.Skip(1)) PlaceCrank(game, crankPlayer, game.mechanisms.First(m => m.symbol == symbol));
        Require(game.progress == 1f && game.Playing, "tres hechos no ganan mientras haya desajuste");
        Position(player1, first.transform.position + Vector3.back * 2f, first.wheel.position);
        Interact.Invoke(player1, new object[] { false, true, false });
        Require(player1.Stabilizing == first, "raycast encuentra mecanismo y muestra progreso sin manivela");
        yield return null;
        yield return null;
        Require(first.Hold01 == 0f, "soltar/interrumpir reinicia la estabilización");
        float beforeRepair = game.Danger;
        float start = Time.time;
        while (game.Playing && Time.time - start < 2.5f)
        {
            Interact.Invoke(player1, new object[] { false, true, false });
            yield return null;
        }
        Require(!first.unstable && !game.Playing && game.Danger < beforeRepair, "mantener 1,5 s estabiliza, alivia peligro y permite ganar");
        yield return new WaitForSecondsRealtime(2f);
        var atmosphere = game.GetComponent<DemoAtmosphere>();
        Require(atmosphere.victoryLights.All(l => l.intensity > 0f) && candle.candleLight.enabled, "victoria enciende luces blancas y conserva vela");
        Position(game.players[1], player1.transform.position + new Vector3(1.8f, 0f, -2.2f), player1.transform.position + Vector3.up * 1.1f);
        ScreenCapture.CaptureScreenshot("Temp/JamVictory.png");
        yield return null;
        game.Restart();
        yield return new WaitForSecondsRealtime(2f);
        game = FindAnyObjectByType<DemoGame>();
        game.Begin();
        yield return null;
        foreach (var p in game.players) p.enabled = false;
        Require(game.progress == 0f && game.Danger < 3f, "reinicio limpio");
        game.dangerPerSecond = 0f;
        game.darknessPenalty = 0f;
        game.unstablePenalty = 0f;
        string next = game.SequenceText.Replace("ORDEN: ", "").Split(" > ")[0];
        var incorrect = game.mechanisms.First(m => m.symbol != next);
        for (int i = 0; i < 12 && game.Playing; i++)
        {
            if (incorrect.HasCrank) incorrect.DetachCrank(game.players[1]);
            incorrect.Use(game.players[1]);
        }
        Require(game.Danger == 100f && !game.Playing, "peligro 100 produce derrota");
        yield return new WaitForSecondsRealtime(2f);
        atmosphere = game.GetComponent<DemoAtmosphere>();
        Require(!game.candles[0].candleLight.enabled && atmosphere.Blackout == 1f, "derrota apaga vela y completa fundido a negro");
        ScreenCapture.CaptureScreenshot("Temp/JamDefeat.png");
        yield return null;
        Require(game.candles.Skip(1).All(c => !c.gameObject.activeSelf && !c.candleLight.enabled), "segunda vela permanece apagada tras finales");
        File.AppendAllText("Temp/JamPlayCheck.txt", "ALL_PASSED\n");
    }

    static void Position(DemoPlayer player, Vector3 position, Vector3 lookAt)
    {
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = position;
        player.view.transform.LookAt(lookAt);
        controller.enabled = true;
        Physics.SyncTransforms();
    }
}
#endif
