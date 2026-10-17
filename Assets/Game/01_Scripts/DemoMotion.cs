using UnityEngine;

namespace Gamejam2026
{
    // Transform animation for the simple maintenance suits; no humanoid rig required.
    public class DemoMotion : MonoBehaviour
    {
        public Transform visual;
        public Transform[] legs;
        DemoPlayer player;
        Vector3 lastPosition;
        Vector3 handOrigin;
        Vector3 visualOrigin;
        float phase;

        void Start()
        {
            player = GetComponent<DemoPlayer>();
            lastPosition = transform.position;
            handOrigin = player.hand.localPosition;
            if (visual) visualOrigin = visual.localPosition;
        }

        void LateUpdate()
        {
            Vector3 displacement = Vector3.ProjectOnPlane(transform.position - lastPosition, Vector3.up);
            lastPosition = transform.position;
            if (!player || !player.game.Playing) return;
            float speed = Time.deltaTime > 0f ? Mathf.Min(displacement.magnitude / Time.deltaTime, player.moveSpeed * player.runMultiplier) : 0f;
            float movement = Mathf.Clamp01(speed / player.moveSpeed);
            phase += speed * Time.deltaTime * 3.5f;
            float wave = Mathf.Sin(phase);
            if (visual)
            {
                visual.localPosition = visualOrigin + Vector3.up * Mathf.Abs(wave) * 0.025f * movement;
                visual.localRotation = Quaternion.Euler(speed > player.moveSpeed + 0.2f ? 5f : 0f, 0f, wave * 2f * movement);
            }
            if (legs != null)
                for (int i = 0; i < legs.Length; i++)
                    if (legs[i]) legs[i].localRotation = Quaternion.Euler(wave * (i == 0 ? 1f : -1f) * 22f * movement, 0f, 0f);
            player.hand.localPosition = handOrigin + new Vector3(Mathf.Cos(phase * 0.5f) * 0.015f, Mathf.Abs(wave) * 0.022f, 0f) * movement;
            player.hand.localRotation = Quaternion.Euler(-wave * 2.5f * movement, 0f, -wave * 3f * movement);
        }
    }
}
