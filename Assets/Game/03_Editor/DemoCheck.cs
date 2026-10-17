#if UNITY_EDITOR
using System;
using System.Linq;
using Gamejam2026;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class DemoCheck
{
    const string Active = "Gamejam.DemoCheck";
    static double readyAt;

    static DemoCheck()
    {
        EditorApplication.update += Tick;
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Game/00_Scenes/Demo.unity");
        SessionState.SetInt(Active, 1);
        EditorApplication.isPlaying = true;
    }

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

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("DEMO_CHECK_FAILED: " + message);
        Debug.Log("DEMO_CHECK_OK: " + message);
    }

    static void Tick()
    {
        int phase = SessionState.GetInt(Active, 0);
        if (phase == 0 || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (readyAt == 0) { readyAt = EditorApplication.timeSinceStartup + 2; return; }
        if (EditorApplication.timeSinceStartup < readyAt) return;
        try
        {
            var game = UnityEngine.Object.FindAnyObjectByType<DemoGame>();
            Require(game != null, "partida presente");
            var player = game.players[0];
            Require(game.players.Length == 2 && game.mechanisms.Length == 3, "dos jugadores y tres mecanismos");
            Require(game.players[0].view.rect.width == 0.5f && game.players[1].view.rect.x == 0.5f, "pantalla dividida");
            Require(game.players[0].Held && game.players[0].Held.isCandle, "jugador 1 empieza con la vela");
            Require(game.players[1].Held && !game.players[1].Held.isCandle, "jugador 2 empieza con la manivela");
            game.Begin();
            Require(game.Playing, "iniciar sin requerir mandos");
            if (phase == 1)
            {
                game.mechanisms[0].Use(player);
                Require(game.progress == 0, "no activar con vela");
                var candle = player.Held;
                candle.Drop();
                Require(player.Held == null && candle.owner == null && candle.candleLight.enabled, "soltar conserva la luz");
                var crank = game.players[1].Held;
                Require(crank != null && !crank.isCandle, "la manivela esta en juego");
                crank.PickUp(player);
                Require(crank.owner == game.players[1], "no robar un objeto ocupado");
                crank.Drop();
                crank.PickUp(player);
                candle.PickUp(player);
                Require(player.Held == crank && candle.owner == null, "un objeto por jugador");
                var sequence = game.SequenceText.Replace("ORDEN: ", "").Split(" > ");
                var first = game.mechanisms.First(m => m.symbol == sequence[0]);
                var wrong = game.mechanisms.First(m => m.symbol != sequence[0]);
                float initialDanger = game.Danger;
                wrong.Use(player);
                Require(game.Danger > initialDanger && game.progress == 0, "error aumenta peligro sin avanzar");
                PlaceCrank(game, player, first);
                Require(first.done && game.progress > 0, "paso correcto avanza");
                float beforeRepeat = game.Danger;
                first.Use(player);
                Require(game.Danger == beforeRepeat, "paso completado no se castiga");
                foreach (string symbol in sequence.Skip(1)) PlaceCrank(game, player, game.mechanisms.First(m => m.symbol == symbol));
                Require(game.progress == 1 && !game.Playing, "secuencia completa gana");
                game.Restart();
                SessionState.SetInt(Active, 2);
                readyAt = EditorApplication.timeSinceStartup + 2;
                return;
            }

            Require(game.progress == 0 && game.Danger < 2f, "reinicio limpio");
            player.Held.Drop();
            var tool = game.players[1].Held;
            Require(tool != null && !tool.isCandle, "la manivela sigue en la partida");
            tool.Drop();
            tool.PickUp(player);
            string next = game.SequenceText.Replace("ORDEN: ", "").Split(" > ")[0];
            var incorrect = game.mechanisms.First(m => m.symbol != next);
            for (int i = 0; i < 12 && game.Playing; i++)
            {
                if (incorrect.HasCrank) incorrect.DetachCrank(player);
                incorrect.Use(player);
            }
            Require(game.Danger >= 100f && !game.Playing && game.progress == 0, "limite de peligro provoca derrota");
            SessionState.SetInt(Active, 0);
            Debug.Log("DEMO_CHECK_ALL_PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            SessionState.SetInt(Active, 0);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}

#endif
