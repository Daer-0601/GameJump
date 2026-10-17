using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Gamejam2026
{
    // Local measurements for the human playtest; never changes danger or the win condition.
    public class DaerPlaytestRecorder : MonoBehaviour
    {
        public DemoGame game;
        public bool recordRounds = true;
        public static string ReportPath => Path.Combine(Application.persistentDataPath, "Playtests", "daer-rounds.csv");
        float firstCompletion = -1f;
        float highestDanger;
        float unstableSeconds;

        void Awake() { if (!game) game = FindAnyObjectByType<DemoGame>(); }

        void OnEnable()
        {
            if (!game) return;
            game.FirstMechanismCompleted += FirstCompleted;
            game.RoundFinished += Finished;
        }

        void OnDisable()
        {
            if (!game) return;
            game.FirstMechanismCompleted -= FirstCompleted;
            game.RoundFinished -= Finished;
        }

        void FirstCompleted(DemoMechanism mechanism) { firstCompletion = game.ElapsedSeconds; }

        void Update()
        {
            if (!game || !game.Playing) return;
            highestDanger = Mathf.Max(highestDanger, game.Danger);
            foreach (var mechanism in game.mechanisms)
                if (mechanism && mechanism.unstable) { unstableSeconds += Time.deltaTime; break; }
        }

        void Finished(bool won)
        {
            if (!recordRounds) return;
            try
            {
                string path = ReportPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (!File.Exists(path)) File.WriteAllText(path, "utc,duration_seconds,won,first_completion_seconds,peak_danger,unstable_seconds,completed_mechanisms\n");
                string row = string.Join(",", DateTime.UtcNow.ToString("O"), Number(game.ElapsedSeconds), won ? "1" : "0",
                    Number(firstCompletion), Number(Mathf.Max(highestDanger, game.Danger)), Number(unstableSeconds), game.CompletedCount.ToString(CultureInfo.InvariantCulture));
                File.AppendAllText(path, row + "\n");
                Debug.Log("DAER_PLAYTEST_SAVED " + path);
            }
            catch (IOException ex) { Debug.LogWarning("No se pudo guardar la prueba local: " + ex.Message); }
            catch (UnauthorizedAccessException ex) { Debug.LogWarning("No se pudo guardar la prueba local: " + ex.Message); }
        }

        static string Number(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
