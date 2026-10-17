using UnityEngine;

namespace Gamejam2026
{
    // Small local motion; no physics or collisions on decorative larvae.
    public class RecintoWorm : MonoBehaviour
    {
        float phase;
        Vector3 rest;
        public void Initialize(float offset) { phase = offset; rest = transform.localPosition; }
        void Update()
        {
            transform.localPosition = rest + new Vector3(Mathf.Sin(Time.time * 1.8f + phase) * 0.025f, 0f, Mathf.Cos(Time.time * 1.4f + phase) * 0.015f);
        }
    }
}
