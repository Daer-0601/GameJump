using UnityEngine;

namespace Gamejam2026
{
    // Interactable growth; it reports a completed burn to the rule owner.
    public class RecintoContamination : MonoBehaviour
    {
        public RecintoStation blockedStation;
        public DemoGame game;
        public bool Consumed { get; private set; }
        public float Burn01 => progress / 2f;
        float progress;
        int heldFrame = -1;
        Vector3 size;
        void Start() { size = transform.localScale; }
        public string Hint(DemoPlayer player) => player.Held && player.Held.isCandle
            ? "[" + DemoPlayerInput.UseLabel(player.playerIndex) + "] Mantener 2 s: quemar brote " + Mathf.RoundToInt(Burn01 * 100) + "%"
            : "Brote contaminado: acerca la vela en la mano para quemarlo";
        public bool Hold(DemoPlayer player, bool held)
        {
            if (Consumed || !game.Playing || !held || !player.Held || !player.Held.isCandle) return false;
            if (heldFrame == Time.frameCount) return true;
            heldFrame = Time.frameCount;
            progress = Mathf.Min(2f, progress + Time.deltaTime);
            if (progress >= 2f)
            {
                Consumed = true;
                if (blockedStation) blockedStation.ClearContamination(this);
                game.RestoreCandle();
                if (game.Investigation) game.Investigation.BurnSound(transform.position);
                foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
                foreach (var ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop();
            }
            return !Consumed;
        }
        void Update()
        {
            if (!game || !game.Playing) return;
            if (Consumed)
            {
                transform.localScale = Vector3.MoveTowards(transform.localScale, Vector3.zero, Time.deltaTime * 1.5f);
                if (transform.localScale.sqrMagnitude < .001f) Destroy(gameObject);
                return;
            }
            if (heldFrame < Time.frameCount - 1) progress = Mathf.Max(0, progress - Time.deltaTime);
            transform.localScale = size * (1f + Mathf.Sin(Time.time * 2.1f + transform.position.x) * .025f - Burn01 * .45f);
        }
    }
}
