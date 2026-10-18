using UnityEngine;

namespace Gamejam2026
{
    // Envuelve los carteles del mundo después de que las salas y los puestos los crean.
    public class DemoMarks : MonoBehaviour
    {
        float next;

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.4f;
            foreach (var text in FindObjectsByType<TextMesh>(FindObjectsInactive.Include))
                WorldMark.Adopt(text);
        }
    }
}
