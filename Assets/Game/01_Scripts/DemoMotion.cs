using UnityEngine;

namespace Gamejam2026
{
    // Transform animation for the simple maintenance suits; no humanoid rig required.
    public class DemoMotion : MonoBehaviour
    {
        public Transform visual;
        public Transform[] legs;
        public Transform[] arms;
        DemoPlayer player;
        Vector3 lastPosition;
        Vector3 handOrigin;
        Vector3 visualOrigin;
        float phase;
        float lastYaw;
        float turn;
        float smoothSpeed;
        Quaternion[] armOrigins;
        Vector3[] armScales;

        void Start()
        {
            player = GetComponent<DemoPlayer>();
            lastPosition = transform.position;
            handOrigin = player.hand.localPosition;
            if (visual) visualOrigin = visual.localPosition;
            lastYaw = transform.eulerAngles.y;
            armOrigins = new Quaternion[arms == null ? 0 : arms.Length];
            armScales = new Vector3[armOrigins.Length];
            for (int i = 0; i < armOrigins.Length; i++)
            {
                armOrigins[i] = arms[i] ? arms[i].localRotation : Quaternion.identity;
                armScales[i] = arms[i] ? arms[i].localScale : Vector3.one;
            }
        }

        void LateUpdate()
        {
            Vector3 displacement = Vector3.ProjectOnPlane(transform.position - lastPosition, Vector3.up);
            lastPosition = transform.position;
            float yaw = Mathf.DeltaAngle(lastYaw, transform.eulerAngles.y);
            lastYaw = transform.eulerAngles.y;
            if (!player || !player.game.Playing) return;
            float speed = Time.deltaTime > 0f ? Mathf.Min(displacement.magnitude / Time.deltaTime, player.moveSpeed * player.runMultiplier) : 0f;
            float teleportDistance = Mathf.Max(1f, player.moveSpeed * player.runMultiplier * Time.deltaTime * 2f);
            if (displacement.magnitude > teleportDistance) speed = 0f;
            float blend = 1f - Mathf.Exp(-Time.deltaTime * 12f);
            smoothSpeed = Mathf.Lerp(smoothSpeed, speed, blend);
            turn = Mathf.Lerp(turn, Mathf.Clamp(yaw / Mathf.Max(Time.deltaTime, .001f), -160f, 160f), blend);
            float movement = Mathf.Clamp01(smoothSpeed / player.moveSpeed);
            float running = Mathf.InverseLerp(player.moveSpeed, player.moveSpeed * player.runMultiplier, smoothSpeed);
            phase += smoothSpeed * Time.deltaTime * 2.6f;
            float wave = Mathf.Sin(phase);
            if (visual)
            {
                visual.localPosition = visualOrigin + Vector3.up * Mathf.Abs(wave) * Mathf.Lerp(.025f, .045f, running) * movement;
                visual.localRotation = Quaternion.Euler(running * 7f, 0f, wave * Mathf.Lerp(2f, 3f, running) * movement);
            }
            if (legs != null)
                for (int i = 0; i < legs.Length; i++)
                    if (legs[i]) legs[i].localRotation = Quaternion.Euler(wave * (i == 0 ? 1f : -1f) * Mathf.Lerp(22f, 32f, running) * movement, 0f, 0f);
            for (int i = 0; i < armOrigins.Length; i++)
            {
                if (!arms[i]) continue;
                bool carrying = i == 1 && player.Held;
                if (carrying && !player.Stabilizing)
                {
                    Vector3 grip = player.hand.TransformPoint(player.Held.isCandle ? new Vector3(.16f, .11f, 0) : new Vector3(.17f, .17f, 0));
                    Vector3 direction = arms[i].parent.InverseTransformDirection(grip - arms[i].position);
                    arms[i].localRotation = Quaternion.Slerp(arms[i].localRotation, Quaternion.FromToRotation(Vector3.down, direction), blend);
                    float reach = Mathf.Clamp(direction.magnitude / .58f, 1f, 1.5f);
                    arms[i].localScale = Vector3.Lerp(arms[i].localScale, Vector3.Scale(armScales[i], new Vector3(1, reach, 1)), blend);
                    continue;
                }
                float angle = wave * (i == 0 ? -1 : 1) * 18f * movement;
                if (carrying || player.Stabilizing) angle = -38f + angle * .15f;
                arms[i].localRotation = Quaternion.Slerp(arms[i].localRotation, armOrigins[i] * Quaternion.Euler(angle, 0, 0), blend);
                arms[i].localScale = Vector3.Lerp(arms[i].localScale, armScales[i], blend);
            }
            float brace = player.Stabilizing ? .15f : 1f;
            player.hand.localPosition = handOrigin + new Vector3(Mathf.Cos(phase * .5f) * .018f, Mathf.Abs(wave) * Mathf.Lerp(.022f, .035f, running), 0f) * movement * brace;
            player.hand.localRotation = Quaternion.Slerp(player.hand.localRotation,
                Quaternion.Euler((-wave * 3f * movement - running * 3f) * brace, 0, (-wave * 4f * movement - turn * .045f) * brace), blend);
        }
    }
}
