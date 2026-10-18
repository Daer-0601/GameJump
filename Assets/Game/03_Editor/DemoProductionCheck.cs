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

public static class DemoProductionCheck
{
    [MenuItem("Gamejam/Producción/Comprobar demo en Play")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play antes de comprobar.");
        File.WriteAllText("Temp/Production.check", "Unity " + Application.unityVersion + "\n");
        SessionState.SetBool("Production.BeforeBackground", PlayerSettings.runInBackground);
        EditorApplication.isPaused = false;
        SessionState.SetBool("Production.Check", true);
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    static void Bind()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("Production.Check",false)) return;
            SessionState.SetBool("Production.Check",false);
            EditorApplication.isPaused = false;
            Application.runInBackground = true;
            new GameObject("Verificación temporal de producción").AddComponent<DemoProductionProbe>();
        };
    }
}

public class DemoProductionProbe : MonoBehaviour
{
    DemoGame game;
    Keyboard original, keyboard;
    int endings;
    static BindingFlags Flags => BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name,Flags).SetValue(obj,value);
    static object Get(object obj, string name) => obj.GetType().GetField(name,Flags).GetValue(obj);
    static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name,Flags).Invoke(obj,args);
    static void Require(bool value, string text)
    {
        if (!value) throw new Exception("FAILED " + text);
        File.AppendAllText("Temp/Production.check", "OK " + text + "\n");
    }
    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        var check = Check();
        while (true)
        {
            object wait;
            try { if (!check.MoveNext()) break; wait = check.Current; }
            catch (Exception ex) { File.AppendAllText("Temp/Production.check",ex + "\n"); Debug.LogException(ex); break; }
            yield return wait;
        }
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (original != null && original.added) original.MakeCurrent();
        PlayerSettings.runInBackground = SessionState.GetBool("Production.BeforeBackground", false);
        Time.timeScale = 1;
        EditorApplication.isPlaying = false;
    }
    IEnumerator Check()
    {
        yield return new WaitForSecondsRealtime(1);
        game = FindAnyObjectByType<DemoGame>();
        Require(game && game.Investigation && game.players.Length == 2 && game.mechanisms.Length == 3,"una partida, dos jugadores y tres mecanismos");
        Require(!game.Playing && game.CandleCharge == 1 && game.Danger == 0,"menú sin consumir vela ni peligro");
        Require(FindObjectsByType<AudioListener>().Length == 1,"un solo AudioListener");
        foreach (var player in game.players)
        {
            var motion = player.GetComponent<DemoMotion>();
            Require(motion && motion.animator && motion.animator.runtimeAnimatorController && motion.grip,"operario con rig, Animator y agarre " + player.playerIndex);
            Vector3 cam = player.view.transform.localPosition;
            bool third = cam.z < -2f;
            bool eyes = cam.y > 1.2f && cam.z > -0.5f;
            Require(third || eyes,"cámara en tercera o en los ojos " + player.playerIndex);
        }
        var sites = (Vector3[])typeof(RecintoInvestigation).GetField("Sites",BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        for (int index = 0; index < sites.Length; index++)
        {
            bool accessible = false;
            foreach (float distance in new[] { 1.35f,1.8f,2.2f }) foreach (float angle in new[] { -30f,0f,30f })
            {
                Vector3 forward = Quaternion.Euler(0,(index % 2 == 0 ? 25 : -35) + angle,0) * Vector3.forward;
                Vector3 standing = sites[index] - forward * distance;
                bool free = !Physics.CheckCapsule(standing + Vector3.up * .4f,standing + Vector3.up * 1.4f,.3f,~0,QueryTriggerInteraction.Ignore);
                Vector3 eye = standing + Vector3.up * 1.15f, delta = sites[index] + Vector3.up - eye;
                bool visible = !Physics.Raycast(eye,delta.normalized,out var hit,delta.magnitude,~0,QueryTriggerInteraction.Ignore)
                    || hit.collider.GetComponentInParent<RecintoStation>() || hit.collider.GetComponentInParent<RecintoContamination>();
                if (free && visible) accessible = true;
            }
            Require(accessible,"puesto posible " + index + " tiene acceso físico y panel visible");
        }
        Require(game.Investigation.Shadow == null,"la araña no entra al recinto");
        ScreenCapture.CaptureScreenshot("Temp/ProductionMenu.png");
        original = Keyboard.current; keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift,Key.UpArrow,Key.RightShift,Key.E,Key.U)); InputSystem.Update();
        var a = DemoPlayerInput.Read(0,115,0,.02f); var b = DemoPlayerInput.Read(1,115,0,.02f);
        Require(a.Move.y == 1 && b.Move.y == 1 && a.UseHeld && b.UseHeld && a.Run && b.Run,"teclado simultáneo: WASD/flechas, E/U y dos Shift");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.I,Key.L)); InputSystem.Update(); b = DemoPlayerInput.Read(1,115,0,.02f);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.I,Key.L,Key.RightCtrl)); InputSystem.Update(); var precise = DemoPlayerInput.Read(1,115,0,.02f);
        Require(Mathf.Approximately(precise.Look.magnitude,b.Look.magnitude * .35f),"Ctrl derecho conserva precisión J2");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update();
        game.Begin(); game.SkipIntro(); yield return null;
        yield return new WaitForSecondsRealtime(.2f);
        ScreenCapture.CaptureScreenshot("Temp/ProductionGameplay.png"); yield return null;
        foreach (var player in game.players) player.enabled = false;
        var spawn = game.players.Select(p => p.transform.position).ToArray();
        float motionUntil = Time.time + .4f;
        while (Time.time < motionUntil)
        {
            foreach (var p in game.players) Call(p,"Move",new DemoPlayerInput.State(Vector2.up,Vector2.zero,false,false,false,false));
            yield return null;
        }
        Require(game.players[0].GetComponent<DemoMotion>().animator.GetFloat("Speed") > 2.7f && game.players[1].GetComponent<DemoMotion>().animator.GetFloat("Speed") > 1.8f,"blend de caminar y carga lenta recibe velocidad real del CharacterController");
        motionUntil = Time.time + .4f;
        while (Time.time < motionUntil)
        {
            foreach (var p in game.players) Call(p,"Move",new DemoPlayerInput.State(Vector2.up,Vector2.zero,true,false,false,false));
            yield return null;
        }
        Require(game.players[0].GetComponent<DemoMotion>().animator.GetFloat("Speed") > 5 && game.players[1].GetComponent<DemoMotion>().animator.GetFloat("Speed") > 3.2f,"blend de carrera conserva penalización de manivela");
        ScreenCapture.CaptureScreenshot("Temp/ProductionLocomotion.png"); yield return null;
        for (int i = 0; i < game.players.Length; i++) Place(game.players[i],spawn[i]);
        Require(game.players[0].Held.isCandle && !game.players[1].Held.isCandle,"una vela J1 y manivela J2");
        var candle = game.candles[0]; candle.Drop(); Require(candle.candleLight.enabled,"vela en el suelo sigue encendida"); candle.PickUp(game.players[0]);
        yield return null;
        Require(Vector3.Dot(candle.transform.up,Vector3.up) > .99f,"vela y llama quedan verticales con el rig importado");
        float fuel = game.CandleCharge, elapsed = game.ElapsedSeconds;
        Call(game,"PauseNow"); yield return new WaitForSecondsRealtime(.25f);
        Require(game.ElapsedSeconds == elapsed && game.CandleCharge == fuel,"pausa congela reglas y combustible"); Call(game,"Continue"); yield return null;
        var investigation = game.Investigation;
        Require(investigation.Stations.Count == 6 && investigation.DisplayedSymbol(investigation.FalseIndex) != game.SymbolAt(investigation.FalseIndex),"seis puestos y exactamente un registro falso");
        Set(game,"<CandleCharge>k__BackingField",0f); float safeDanger = game.Danger; Call(game,"Simulate");
        Require(game.CandleRange == 2.8f && game.Danger < safeDanger + 1f && !game.Finished,"vela al mínimo no produce un golpe de peligro ni derrota");
        Set(game,"<CandleCharge>k__BackingField",.25f);
        var growth = FindObjectsByType<RecintoContamination>().First(g => g.blockedStation);
        var blocked = growth.blockedStation; Require(blocked.Contaminated && !blocked.AcceptsWork,"brote bloquea el puesto");
        Set(game.players[0],"<Working>k__BackingField",true);
        bool interactionCaptured = false;
        float until = Time.time + 2.4f;
        while (growth && !growth.Consumed && Time.time < until)
        {
            growth.Hold(game.players[0],true);
            if (!interactionCaptured && growth.Burn01 > .7f)
            {
                Require(game.players[0].GetComponent<DemoMotion>().animator.GetCurrentAnimatorStateInfo(0).IsName("Interact"),"Animator entra agachado y sostiene la interacción");
                ScreenCapture.CaptureScreenshot("Temp/ProductionInteraction.png"); interactionCaptured = true;
            }
            yield return null;
        }
        Set(game.players[0],"<Working>k__BackingField",false);
        Require((!growth || growth.Consumed) && !blocked.Contaminated && game.CandleCharge > .3f,"quemar 2 s desbloquea y devuelve 30 s de cera");
        foreach (var patch in FindObjectsByType<RecintoContamination>())
        {
            if (patch.blockedStation) patch.blockedStation.ClearContamination(patch);
            Destroy(patch.gameObject);
        }
        yield return null;
        for (int i = 0; i < 3; i++) investigation.RecordEvidence(i);
        Require(investigation.CanAttempt && !investigation.Ready,"registros sin contrastar aún no permiten acertar");
        var wrong = game.mechanisms.First(m => m.symbol != game.SymbolAt(0)); float danger = game.Danger;
        game.Activate(wrong,game.players[1]);
        Require(!wrong.done && game.Danger == danger + 12,"dato incorrecto suma 12% sin avanzar");
        var unverified = game.mechanisms.First(m => m.symbol == game.SymbolAt(0)); danger = game.Danger;
        game.Activate(unverified,game.players[1]);
        Require(!unverified.done && game.Danger == danger + 12,"un dato sin contraste nunca pasa por válido aunque coincida con el primer mecanismo");
        foreach (var patch in FindObjectsByType<RecintoContamination>()) { if (patch.blockedStation) patch.blockedStation.ClearContamination(patch); Destroy(patch.gameObject); }
        var console = investigation.Stations.First(s => s.name == "Consola de contraste");
        until = Time.time + 2.7f;
        while (Time.time < until) { console.Interact(game.players[0],false,true); yield return null; }
        Require(!investigation.Contrasted,"un solo jugador no completa el contraste");
        until = Time.time + 2.8f;
        while (!investigation.Contrasted && Time.time < until) { console.Interact(game.players[0],false,true); console.Interact(game.players[1],false,true); yield return null; }
        Require(investigation.FalseRevealed && investigation.DisplayedSymbol(investigation.FalseIndex) == game.SymbolAt(investigation.FalseIndex),"consola corrige la marca falsa y el HUD");
        var light = investigation.Stations.First(s => s.name == "Luz X");
        foreach (var station in investigation.Stations.Where(s => s.name.StartsWith("Luz "))) Call(station,"Complete");
        Require(investigation.Ready && light.SecondsLeft >= 44 && light.SecondsLeft <= 70,"luz avisa 45–70 s y completa inspección");
        Call(light,"SwitchOff"); Require(!light.IsPowerOn && light.SecondsLeft == 0 && investigation.Ready,"apagar cancela el aviso y conserva inspección");
        Call(light,"Complete"); Set(light,"expires",game.ElapsedSeconds - 1); yield return null;
        Require(investigation.Shadow == null,"la luz vencida ya no llama a la araña"); Call(light,"SwitchOff");
        foreach (var station in investigation.Stations.Where(s => s.IsPowerOn)) Call(station,"SwitchOff");
        investigation.enabled = false; // Isolate containment and repair rules from random navigation in this probe.
        game.ShadowContact(); Require(game.ShadowContacts == 1 && !game.Finished,"primer contacto asusta y permite continuar");
        foreach (var symbol in Enumerable.Range(0,3).Select(game.SymbolAt).ToArray()) game.Activate(game.mechanisms.First(m => m.symbol == symbol),game.players[1]);
        Require(game.Containing && !game.Finished && game.CompletedCount == 3,"tres aciertos inician contención sin victoria inmediata");
        var mechanism = game.mechanisms[0]; mechanism.unstable = true;
        game.mechanisms[1].unstable = true; float beforeTwo = game.Danger; Call(game,"Simulate");
        Require(game.Danger - beforeTwo > game.unstablePenalty * Time.deltaTime * 2.5f,"dos desajustes aceleran el peligro más que dos aislados");
        game.mechanisms[1].unstable = false;
        mechanism.Hold(game.players[1],true); Require(mechanism.Hold01 == 0,"manivela impide estabilizar");
        game.players[1].Held.Drop();
        until = Time.time + 1.8f;
        while (mechanism.unstable && Time.time < until) { mechanism.Hold(game.players[1],true); yield return null; }
        Require(!mechanism.unstable,"mantener 1,5 s sin manivela estabiliza");
        mechanism.unstable = true; yield return null; Call(game,"PauseNow"); yield return null;
        Require(!mechanism.GetComponent<AudioSource>().isPlaying,"pausa corta loop de desajuste"); Call(game,"Continue");
        mechanism.unstable = false;
        Set(game,"nextDestabilize",game.ElapsedSeconds + 1000);
        Set(game,"<Containment01>k__BackingField",.5f); float containment = game.Containment01; yield return new WaitForSeconds(.2f);
        Require(game.Containment01 < containment && !game.SealsOccupied,"fuera de dos salas la contención retrocede");
        Place(game.players[0],DemoGame.WorkshopSeal); Place(game.players[1],DemoGame.PumpSeal);
        Require(game.SealsOccupied,"contención exige salas diferentes existentes");
        Place(game.players[0],DemoGame.WorkshopSeal + Vector3.left * 5);
        Require(game.SealsOccupied,"la sala completa cuenta, también lejos del centro de contención");
        Place(game.players[0],DemoGame.WorkshopSeal);
        Set(game,"<Containment01>k__BackingField",0f); game.RoundFinished += won => endings++;
        Time.timeScale = 8;
        float deadline = Time.realtimeSinceStartup + 30;
        while (!game.Finished && Time.realtimeSinceStartup < deadline) yield return null;
        Require(game.Finished && game.Containment01 == 1 && endings == 1,"25 s completan victoria una sola vez (playing=" + game.Playing + ", progreso=" + game.Containment01 + ", peligro=" + game.Danger + ", finales=" + endings + ", tiempo=" + game.ElapsedSeconds + ")");
        yield return new WaitForSecondsRealtime(1.6f);
        Require(game.GetComponent<DemoAtmosphere>().victoryLights.All(l => l.intensity > 0),"victoria enciende luces blancas existentes");
        ScreenCapture.CaptureScreenshot("Temp/ProductionVictory.png"); yield return null;
        Call(game,"ReturnToMenu"); yield return new WaitForSecondsRealtime(1.5f);
        game = FindAnyObjectByType<DemoGame>();
        Require(!game.Playing && game.Danger == 0 && game.CandleCharge == 1 && game.ShadowContacts == 0 && game.CompletedCount == 0 && game.Investigation.EvidenceCount == 0,"volver al menú reinicia vela, sombra, puestos, secuencia y peligro");
        game.Begin(); game.SkipIntro(); yield return null; game.ShadowContact(); game.ShadowContact();
        Require(game.Finished && game.DefeatCause.Contains("sombra"),"segundo contacto pierde por sombra");
        Call(game,"Retry"); yield return new WaitForSecondsRealtime(1.5f); game = FindAnyObjectByType<DemoGame>();
        Require(game.Playing && game.ShadowContacts == 0 && game.CompletedCount == 0 && game.Danger < 1 && game.CandleCharge > .99f,"reintentar entra limpio a la expedición");
        game.AddDanger(100,"errores de orden en los mecanismos"); Require(game.Finished && game.DefeatCause.Contains("errores"),"peligro 100 pierde con causa real");
        ScreenCapture.CaptureScreenshot("Temp/ProductionDefeat.png"); yield return null;
        File.AppendAllText("Temp/Production.check","ALL_PASSED\n");
    }
    static void Place(DemoPlayer player, Vector3 position)
    {
        var controller = player.GetComponent<CharacterController>(); controller.enabled = false; player.transform.position = position + Vector3.up * .1f; controller.enabled = true;
        Physics.SyncTransforms();
    }
}
#endif
