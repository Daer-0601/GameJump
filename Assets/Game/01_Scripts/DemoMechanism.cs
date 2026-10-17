using UnityEngine;

namespace Gamejam2026
{
    public class DemoMechanism : MonoBehaviour
    {
        public int id;
        public string symbol;
        public DemoGame game;
        public Light indicator;
        public Transform wheel;
        public bool done;
        public bool unstable;
        public float Hold01 => stabilizeTime <= 0f ? 0f : Mathf.Clamp01(stabilizeProgress / stabilizeTime);
        const float stabilizeTime = 1.5f;
        Quaternion startRotation;
        float stabilizeProgress;
        bool heldThisFrame;

        void Start()
        {
            if (wheel) startRotation = wheel.localRotation;
        }

        public void Use(DemoPlayer player)
        {
            if (done) return;
            if (player.Held == null || player.Held.isCandle)
            {
                player.Tell("Necesitas la manivela. Deja la vela con " + DemoPlayerInput.DropLabel(player.playerIndex) + ".");
                return;
            }
            game.Activate(this, player);
        }

        public void Hold(DemoPlayer player, bool holding)
        {
            if (!done || !unstable || !holding || player == null) return;
            heldThisFrame = true;
            stabilizeProgress += Time.deltaTime;
            if (stabilizeProgress < stabilizeTime) return;
            stabilizeProgress = 0f;
            unstable = false;
            game.OnStabilized(this, player);
        }

        void LateUpdate()
        {
            if (!heldThisFrame) stabilizeProgress = 0f;
            heldThisFrame = false;
        }

        void Update()
        {
            if (wheel && done && !unstable)
                wheel.localRotation = Quaternion.Slerp(wheel.localRotation, startRotation * Quaternion.Euler(0f, 0f, 90f), Time.deltaTime * 3f);
            else if (wheel && unstable)
                wheel.localRotation = startRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 8f) * 25f);
            if (!indicator) return;
            if (unstable) indicator.color = Color.Lerp(new Color(0.95f, 0.45f, 0.05f), new Color(1f, 0.15f, 0.05f), Mathf.PingPong(Time.time * 4f, 1f));
            else indicator.color = done ? new Color(0.3f, 0.85f, 0.65f) : new Color(0.9f, 0.3f, 0.1f);
        }
    }
}
