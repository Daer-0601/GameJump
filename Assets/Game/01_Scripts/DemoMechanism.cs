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
        Quaternion startRotation;

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

        void Update()
        {
            if (wheel && done)
                wheel.localRotation = Quaternion.Slerp(wheel.localRotation, startRotation * Quaternion.Euler(0f, 0f, 90f), Time.deltaTime * 3f);
            if (indicator) indicator.color = done ? new Color(0.3f, 0.85f, 0.65f) : new Color(0.9f, 0.3f, 0.1f);
        }
    }
}
