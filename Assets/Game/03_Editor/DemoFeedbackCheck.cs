#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Gamejam2026;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class DemoFeedbackCheck
{
    public const string Result = "Temp/Feedback.check";

    [MenuItem("Gamejam/Producción/Comprobar cuaderno avisos y sombra")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Comprueba con la escena guardada y fuera de Play.");
        File.WriteAllText(Result, "Unity " + Application.unityVersion + "\n");
        SessionState.SetBool("Feedback.Background", Application.runInBackground);
        SessionState.SetBool("Feedback.Check", true);
        EditorApplication.isPaused = false;
        var gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        EditorWindow.GetWindow(gameView).Show();
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Bind()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("Feedback.Check", false)) return;
            SessionState.SetBool("Feedback.Check", false);
            Application.runInBackground = true;
            EditorApplication.isPaused = false;
            File.AppendAllText(Result, "ENTERED_PLAY\n");
            new GameObject("Comprobación temporal de feedback").AddComponent<DemoFeedbackProbe>();
        };
    }
}

public class DemoFeedbackProbe : MonoBehaviour
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
    static object Get(object obj, string field) => obj.GetType().GetField(field, Flags).GetValue(obj);
    static void TickShadow(RecintoInvestigation inv) => typeof(RecintoInvestigation).GetMethod("UpdateShadow", Flags).Invoke(inv, null);
    static void Require(bool value, string text)
    {
        if (!value) throw new Exception("FAILED " + text);
        File.AppendAllText(DemoFeedbackCheck.Result, "OK " + text + "\n");
    }

    IEnumerator Start()
    {
        Application.runInBackground = true;
        var check = Check();
        while (true)
        {
            object wait;
            try { if (!check.MoveNext()) break; wait = check.Current; }
            catch (Exception ex) { File.AppendAllText(DemoFeedbackCheck.Result, ex + "\n"); Debug.LogException(ex); break; }
            yield return wait;
        }
        (check as IDisposable)?.Dispose();
        LanFlow.ClearRemote();
        Application.runInBackground = SessionState.GetBool("Feedback.Background", false);
        EditorApplication.isPlaying = false;
    }

    IEnumerator Check()
    {
        yield return null;
        var game = FindAnyObjectByType<DemoGame>();
        Require(game && game.Investigation && !game.Playing, "escena Demo en el menú");
        game.Begin(); game.SkipIntro();
        yield return new WaitForSecondsRealtime(.2f);

        // El paquete conserva el bloqueo aunque incluya entradas no neutras o acciones pendientes.
        LanFlow.ClearRemote();
        LanFlow.StoreInput(new LanInput { moveY = 1, useHeld = true, sequence = 1 });
        LanFlow.StoreEdge(true, true);
        var packet = new LanInput { moveX = 1, lookX = 8, run = true, useHeld = true, reading = true, sequence = 2 };
        using (var writer = new FastBufferWriter(128, Allocator.Temp))
        {
            writer.WriteNetworkSerializable(packet);
            using (var reader = new FastBufferReader(writer, Allocator.Temp)) reader.ReadNetworkSerializable(out packet);
        }
        Require(packet.reading, "el estado de lectura viaja en el paquete LAN");
        LanFlow.StoreInput(packet);
        LanFlow.StoreEdge(true, true);
        var input = LanFlow.ConsumeRemote();
        Require(LanFlow.RemoteReading && input.Move == Vector2.zero && input.Look == Vector2.zero
            && !input.Use && !input.UseHeld && !input.Drop && !input.Run, "leer cancela movimiento, mirada, carrera, uso y soltar pendientes");
        LanFlow.StoreInput(new LanInput { reading = false, sequence = 3 });
        input = LanFlow.ConsumeRemote();
        Require(!LanFlow.RemoteReading && !input.Use && !input.Drop && input.Look == Vector2.zero, "cerrar el cuaderno no recupera acciones anteriores");
        LanFlow.StoreInput(new LanInput { moveY = 1, sequence = 4 });
        LanFlow.StoreEdge(true, false);
        input = LanFlow.ConsumeRemote();
        Require(input.Move.y == 1 && input.Use, "cerrar permite entradas nuevas");
        LanFlow.StoreInput(new LanInput { reading = true, sequence = 2 });
        Require(!LanFlow.RemoteReading, "un paquete antiguo no vuelve a bloquear la lectura");
        LanFlow.ClearRemote();

        var inv = game.Investigation;
        inv.enabled = false;
        var shadow = inv.Shadow;
        Require(shadow, "sombra real disponible");
        inv.ApplyShadow(shadow.transform.position, 0, false, false);
        Require(!inv.ShadowRevealed && !inv.ShadowMoving, "la réplica oculta una sombra que aún no apareció");
        inv.ApplyShadow(shadow.transform.position, 0, false, true);
        Require(inv.ShadowRevealed && !inv.ShadowMoving, "la réplica muestra la sombra aunque su primer estado sea detenido");
        var positions = new Vector3[2]; var rotations = new Quaternion[2];
        var cameraPositions = new Vector3[2]; var cameraRotations = new Quaternion[2];
        for (int i = 0; i < 2; i++)
        {
            var p = game.players[i]; p.enabled = false;
            positions[i] = p.transform.position; rotations[i] = p.transform.rotation;
            cameraPositions[i] = p.view.transform.position; cameraRotations[i] = p.view.transform.rotation;
            p.GetComponent<CharacterController>().enabled = false;
            p.transform.position = new Vector3(500 + i * 4, 0, 504);
            p.view.transform.position = p.transform.position + Vector3.up;
            p.view.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        }
        shadow.transform.SetPositionAndRotation(new Vector3(500, 0, 500), Quaternion.identity);
        Set(inv, "shadowShown", true); Set(inv, "travelBudget", 7f); Set(inv, "moveAfter", -1f);
        Set(inv, "contactAfter", game.ElapsedSeconds + 1000f); Set(inv, "nextAppearance", game.ElapsedSeconds + 1000f);
        Set(inv, "nextAbandonedStep", game.ElapsedSeconds + 1000f); Set(inv, "retreating", false); Set(inv, "shadowTarget", null);
        var route = (Queue<Vector3>)Get(inv, "route");
        route.Clear(); route.Enqueue(shadow.transform.position + Vector3.right * 8f);
        Vector3 start = shadow.transform.position;
        for (int observer = 0; observer < 2; observer++)
        {
            game.players[1 - observer].view.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            game.players[observer].view.transform.LookAt(start + Vector3.up * .25f);
            Physics.SyncTransforms();
            for (int frame = 0; frame < 5; frame++) { TickShadow(inv); yield return null; }
            Require((shadow.transform.position - start).sqrMagnitude < .000001f && !inv.ShadowMoving
                && !(bool)Get(inv, "retreating") && Mathf.Approximately((float)Get(inv, "travelBudget"), 7f), "mirada J" + (observer + 1) + " detiene la sombra sin gastar ruta ni iniciar retirada");
        }
        yield return new WaitForSeconds(.3f);
        var animator = shadow.GetComponentInChildren<Animator>();
        Require(animator && animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "al mirarla queda en reposo y deja de gatear");
        game.players[0].view.transform.LookAt(start + Vector3.up * .25f);
        game.players[1].view.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        var wall = new GameObject("Pared temporal de prueba");
        wall.transform.position = new Vector3(500, 1, 502);
        wall.AddComponent<BoxCollider>().size = new Vector3(12, 4, .4f);
        Physics.SyncTransforms();
        for (int frame = 0; frame < 5; frame++) { TickShadow(inv); yield return null; }
        Require(inv.ShadowMoving && (shadow.transform.position - start).sqrMagnitude > .000001f, "mirar a través de una pared no detiene el avance");
        Destroy(wall); yield return null;
        game.players[0].view.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        start = shadow.transform.position;
        for (int frame = 0; frame < 5; frame++) { TickShadow(inv); yield return null; }
        Require(inv.ShadowMoving && (shadow.transform.position - start).sqrMagnitude > .000001f, "sin miradas retoma la ruta");
        Set(inv, "retreating", true); Set(inv, "travelBudget", 0f);
        game.players[0].view.transform.LookAt(shadow.transform.position + Vector3.up * .25f);
        start = shadow.transform.position;
        for (int frame = 0; frame < 5; frame++) { TickShadow(inv); yield return null; }
        Require(!inv.ShadowMoving && (shadow.transform.position - start).sqrMagnitude < .000001f, "la mirada también detiene la retirada tras contacto");

        for (int i = 0; i < 2; i++)
        {
            var p = game.players[i];
            p.transform.SetPositionAndRotation(positions[i], rotations[i]);
            p.view.transform.SetPositionAndRotation(cameraPositions[i], cameraRotations[i]);
            p.GetComponent<CharacterController>().enabled = true;
            p.enabled = true;
        }
        game.players[0].Notebook.Toggle();
        game.players[1].Notebook.Toggle();
        yield return new WaitForSecondsRealtime(1.15f);
        var original = Keyboard.current;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            Vector3 before = game.players[0].transform.position;
            float yaw = game.players[0].transform.eulerAngles.y;
            var held = game.players[0].Held;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.H, Key.E, Key.Q, Key.LeftShift));
            InputSystem.Update();
            yield return null;
            Require(game.players[0].Reading && Vector3.Distance(before, game.players[0].transform.position) < .02f
                && Mathf.Abs(Mathf.DeltaAngle(yaw, game.players[0].transform.eulerAngles.y)) < .01f && game.players[0].Held == held,
                "cuaderno local conserva el bloqueo de movimiento, mirada e interacción");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            game.WarnThreat("Algo cruza fuera de la vela. Mirad a la sombra para detenerla.");
            game.Pulse("Peligro alto: estabilizad el mecanismo desajustado", 3);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Temp/FeedbackBook.png");
            yield return null;
            game.players[0].Notebook.Toggle(); game.players[1].Notebook.Toggle();
            yield return new WaitForSecondsRealtime(.9f);
            game.WarnThreat("Algo cruza fuera de la vela. Mirad a la sombra para detenerla.");
            game.Pulse("Peligro crítico", 4);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Temp/FeedbackWorld.png");
            yield return null;
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            if (original != null && original.added) original.MakeCurrent();
        }
        File.AppendAllText(DemoFeedbackCheck.Result, "ALL_PASSED\n");
    }
}
#endif
