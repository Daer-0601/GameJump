#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Gamejam2026;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class JamRecoveryCheck
{
    public const string Report = "Temp/JamRecoveryCheck.txt";
    [MenuItem("Gamejam/Pulido/Comprobar audio y presentación en Play")]
    public static void Run()
    {
        JamPolish.OpenGame();
        File.WriteAllText(Report, "Verificación de presentación y salida de audio real del motor.\n");
        SessionState.SetBool("Recovery.Check", true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Bind()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("Recovery.Check", false)) return;
            SessionState.SetBool("Recovery.Check", false);
            new GameObject("Verificación temporal de presentación").AddComponent<JamRecoveryProbe>();
        };
    }
}

public class JamRecoveryProbe : MonoBehaviour
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    int assertions;
    bool originalBackground;

    void Require(bool condition, string text)
    {
        if (!condition) throw new Exception(text);
        assertions++;
        File.AppendAllText(JamRecoveryCheck.Report, "OK " + text + "\n");
    }

    IEnumerator Start()
    {
        originalBackground = Application.runInBackground;
        Application.runInBackground = true;
        var check = Check();
        while (true)
        {
            bool more;
            object wait;
            try { more = check.MoveNext(); wait = more ? check.Current : null; }
            catch (Exception ex) { File.AppendAllText(JamRecoveryCheck.Report, "FAILED " + ex + "\n"); Debug.LogException(ex); break; }
            if (!more)
            {
                File.AppendAllText(JamRecoveryCheck.Report, "PASSED " + assertions + " comprobaciones\n");
                Debug.Log("RECOVERY_CHECK_PASSED " + assertions);
                break;
            }
            yield return wait;
        }
        Application.runInBackground = originalBackground;
        Time.timeScale = 1f;
        EditorApplication.isPlaying = false;
    }

    IEnumerator Check()
    {
        yield return null;
        var game = FindAnyObjectByType<DemoGame>();
        FindAnyObjectByType<DaerPlaytestRecorder>().recordRounds = false;
        game.Begin();
        game.SkipIntro();
        yield return null;
        foreach (var player in game.players) player.enabled = false;
        Require(game.Playing, "partida iniciada con teclado y sin mando");
        Require(FindObjectsByType<AudioListener>().Count(l => l.enabled && l.gameObject.activeInHierarchy) == 1, "un único AudioListener activo para pantalla partida");
        Require(!AudioListener.pause && AudioListener.volume > .5f, "mezclador de Unity activo y sin silenciar");
        Require(game.ambience && game.ambience.clip && game.ambience.isPlaying && !game.ambience.mute, "ambiente original asignado y reproduciéndose");
        Require(game.walkingClip && game.lightClip && game.gearsClip && game.dangerClip && game.jumpScareClip, "clips de pasos, luz, engranaje, alarma y susto conservados");
        float ambiencePeak = 0, outputPeak = 0;
        float until = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < until)
        {
            ambiencePeak = Mathf.Max(ambiencePeak, Peak(game.ambience));
            outputPeak = Mathf.Max(outputPeak, Peak(null));
            yield return null;
        }
        File.AppendAllText(JamRecoveryCheck.Report, $"AUDIO ambiente={ambiencePeak:F6} salida={outputPeak:F6} volumen={game.ambience.volume:F3}\n");
        Require(game.ambience.volume >= .15f, "el ambiente ya no queda reducido a 0.035 por otra rutina");
        Require(ambiencePeak > .00001f && outputPeak > .00001f, "hay muestras de audio no nulas en el ambiente y la salida final");
        Require(FindObjectsByType<Light>().Where(l => l.type == LightType.Directional).All(l => l.intensity == 0f), "luz lunar global apagada");
        Require(game.players.All(p => p.view.clearFlags == CameraClearFlags.SolidColor), "cielo luminoso retirado de ambas cámaras");
        Require(RenderSettings.ambientLight.maxColorComponent < .01f && RenderSettings.fogDensity >= .03f, "oscuridad y niebla iniciales recuperadas");
        var candle = game.candles[0];
        Require(candle.candleLight.enabled && candle.candleLight.range <= 5.8f && candle.candleLight.intensity >= 4.3f, "vela cálida de alcance corto como luz móvil principal");
        Require(game.candles.Skip(1).All(c => !c.gameObject.activeSelf && !c.candleLight.enabled), "vela de reserva apagada");
        var volume = game.GetComponents<Volume>().First(v => v.sharedProfile && v.sharedProfile.Has<Vignette>());
        Require(volume.sharedProfile.Has<FilmGrain>() && volume.sharedProfile.Has<ChromaticAberration>(), "viñeta, grano y aberración URP activos");
        ScreenCapture.CaptureScreenshot("Temp/RecoveryOscuridad.png");
        yield return null;

        var walker = game.players[0];
        var stepSource = walker.GetComponents<AudioSource>().First();
        game.PlayFootstep(stepSource);
        float stepsPeak = 0;
        until = Time.realtimeSinceStartup + .45f;
        while (Time.realtimeSinceStartup < until) { stepsPeak = Mathf.Max(stepsPeak, Peak(stepSource)); yield return null; }
        Require(stepsPeak > .00001f, "pasos producen señal audible en su AudioSource");
        var surfaces = game.GetComponent<RecintoFootsteps>();
        var bank = surfaces.concrete;
        surfaces.concrete = Array.Empty<AudioClip>();
        Require(!surfaces.Play(stepSource, game.sfxVolume), "banco vacío permite recurrir al clip de pasos original");
        game.PlayFootstep(stepSource);
        yield return null;
        Require(stepSource.isPlaying, "el fallback de pasos continúa reproduciendo audio");
        surfaces.concrete = bank;

        foreach (var player in game.players)
            Require(player.GetComponent<DemoMotion>() && player.transform.Find("Traje de mantenimiento").GetComponentsInChildren<Renderer>().Any(r => r.enabled), "traje y animación presentes en J" + (player.playerIndex + 1));
        Require(candle.transform.Find("Modelo artesanal") && game.players[1].Held.transform.Find("Modelo artesanal"), "modelos propios de vela y manivela conservados");
        bool detailed = candle.GetComponent<DemoCandleFlame>();
        if (detailed)
        {
            foreach (var player in game.players)
                Require(player.transform.Find("Traje de mantenimiento/Detalle traje") && player.GetComponent<DemoMotion>().arms.Length == 2, "visor, respirador y brazos animados en J" + (player.playerIndex + 1));
            var fire = candle.GetComponent<DemoCandleFlame>().flame;
            var fireScale = fire.localScale;
            yield return new WaitForSeconds(.12f);
            Require(Vector3.Distance(fireScale, fire.localScale) > .00001f, "la forma de la llama también parpadea suavemente");
            var move = typeof(DemoPlayer).GetMethod("Move", Private);
            var walking = new DemoPlayerInput.State(Vector2.up, Vector2.zero, false, false, false, false);
            var running = new DemoPlayerInput.State(Vector2.up, Vector2.zero, true, false, false, false);
            Position(walker, new Vector3(-1.5f, .1f, -6));
            float startZ = walker.transform.position.z;
            float walkLeg = 0, runLeg = 0;
            float moveStart = Time.time;
            until = Time.realtimeSinceStartup + 10f;
            while (Time.time - moveStart < 1.25f && Time.realtimeSinceStartup < until)
            {
                move.Invoke(walker, new object[] { walking });
                walkLeg = Mathf.Max(walkLeg, Quaternion.Angle(Quaternion.identity, walker.GetComponent<DemoMotion>().legs[0].localRotation));
                yield return null;
            }
            float walked = walker.transform.position.z - startZ;
            float walkSeconds = Time.time - moveStart;
            startZ = walker.transform.position.z;
            moveStart = Time.time;
            until = Time.realtimeSinceStartup + 10f;
            while (Time.time - moveStart < 1.25f && Time.realtimeSinceStartup < until)
            {
                move.Invoke(walker, new object[] { running });
                runLeg = Mathf.Max(runLeg, Quaternion.Angle(Quaternion.identity, walker.GetComponent<DemoMotion>().legs[0].localRotation));
                yield return null;
            }
            float runSeconds = Time.time - moveStart;
            float ran = walker.transform.position.z - startZ;
            File.AppendAllText(JamRecoveryCheck.Report, $"MOTION walk={walked:F3}/{walkSeconds:F3}s leg={walkLeg:F2} run={ran:F3}/{runSeconds:F3}s leg={runLeg:F2} playing={game.Playing}\n");
            Require(walkLeg > 10f && runLeg > walkLeg && ran / Mathf.Max(runSeconds, .01f) > walked / Mathf.Max(walkSeconds, .01f) * 1.2f, "caminar y correr animan piernas, cuerpo y mano con distinta intensidad");
        }
        var first = game.mechanisms.First(m => m.symbol == game.NextSymbol);
        Position(walker, first.transform.position + new Vector3(-.8f, 0, -2));
        Position(game.players[1], first.transform.position + new Vector3(.8f, 0, -2));
        first.Use(game.players[1]);
        if (detailed)
        {
            Require(first.mountedCrank.Inserting, "colocar la manivela inicia una inserción visible");
            typeof(DemoGame).GetMethod("PauseNow", Private).Invoke(game, null);
            Vector3 pausedPosition = first.mountedCrank.transform.localPosition;
            yield return new WaitForSecondsRealtime(.15f);
            Require(Vector3.Distance(pausedPosition, first.mountedCrank.transform.localPosition) < .00001f, "la animación de inserción respeta Esc pausa");
            typeof(DemoGame).GetMethod("Continue", Private).Invoke(game, null);
        }
        yield return new WaitForSeconds(.6f);
        Require(first.HasCrank && first.mountedCrank.owner == null && game.players[1].Held == null, "manivela montada conserva la regla de una mano");
        Require(first.mountedCrank.transform.localPosition.sqrMagnitude < .0001f, "manivela termina dentro del soporte");
        Require(first.DetachCrank(game.players[1]) && game.players[1].Held != null, "se puede retirar la manivela después de colocarla");
        if (detailed)
        {
            first.Use(game.players[1]);
            first.DetachCrank(game.players[1]);
            yield return new WaitForSeconds(.45f);
            Require(!game.players[1].Held.Inserting && game.players[1].Held.transform.parent == game.players[1].hand, "retirar durante la inserción cancela el movimiento sin perder el objeto");
        }
        first.unstable = true;
        yield return new WaitForSeconds(.15f);
        var loop = first.GetComponents<AudioSource>().First(s => s.loop);
        float faultPeak = 0;
        until = Time.realtimeSinceStartup + .7f;
        while (Time.realtimeSinceStartup < until) { faultPeak = Mathf.Max(faultPeak, Peak(loop)); yield return null; }
        Require(loop.isPlaying && faultPeak > .00001f && first.GetComponentInChildren<ParticleSystem>().isPlaying, "desajuste visible y con salida de audio real");
        var before = first.wheel.localPosition;
        yield return null;
        Require(Vector3.Distance(before, first.wheel.localPosition) > .00001f, "mecanismo desajustado tiembla");
        Require(FindAnyObjectByType<DemoPassage>().OpenRequested, "el primer acierto sigue abriendo el paso de Daer");
        Require(FindObjectsByType<DemoClue>().Count(c => c.nextSymbolOnly) == 1, "segundo cartel de Daer conservado");
        float fogBefore = RenderSettings.fogDensity;
        float grainBefore = volume.sharedProfile.components.OfType<FilmGrain>().First().intensity.value;
        var wrong = game.mechanisms.First(m => !m.done && m.symbol != game.NextSymbol);
        for (int i = 0; i < 3; i++) game.Activate(wrong, game.players[1]);
        yield return null;
        Require(RenderSettings.fogDensity > fogBefore && volume.sharedProfile.components.OfType<FilmGrain>().First().intensity.value > grainBefore, "niebla y grano se endurecen al subir el peligro");
        Position(game.players[0], new Vector3(-1.5f, .1f, -1));
        Position(game.players[1], new Vector3(0, .1f, .8f));
        game.players[0].transform.rotation = Quaternion.identity;
        game.players[1].transform.rotation = Quaternion.LookRotation(game.players[0].transform.position - game.players[1].transform.position);
        game.players[0].view.transform.LookAt(game.players[1].transform.position + Vector3.up * 1.15f);
        game.players[1].view.transform.localRotation = Quaternion.identity;
        yield return new WaitForSeconds(.25f);
        Require(loop.volume < .005f, "el ruido del desajuste se atenúa al alejar a ambos jugadores");
        ScreenCapture.CaptureScreenshot("Temp/RecoveryModelos.png");
        yield return null;
        if (detailed)
        {
            var rightArm = game.players[0].GetComponent<DemoMotion>().arms[1];
            var grip = game.players[0].hand.TransformPoint(new Vector3(.16f, .11f, 0));
            Require(Vector3.Distance(rightArm.TransformPoint(new Vector3(0, -.58f, 0)), grip) < .2f, "el brazo acompaña el objeto sostenido sin un rig humanoide");
            Require(candle.GetComponent<DemoCandleFlame>().flame.GetComponentsInChildren<Renderer>().All(r => r.shadowCastingMode == ShadowCastingMode.Off), "la llama no proyecta sombras opacas sobre la cera");
        }
    }

    static float Peak(AudioSource source)
    {
        var data = new float[1024];
        if (source) source.GetOutputData(data, 0); else AudioListener.GetOutputData(data, 0);
        float peak = 0;
        foreach (var sample in data) peak = Mathf.Max(peak, Mathf.Abs(sample));
        return peak;
    }

    static void Position(DemoPlayer player, Vector3 point)
    {
        var controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.position = point;
        controller.enabled = true;
        Physics.SyncTransforms();
    }
}
#endif
