using UnityEngine;

namespace Gamejam2026
{
    public class DemoClue : MonoBehaviour
    {
        public TextMesh inscriptionLabel;
        DemoGame game;
        Renderer labelRenderer;

        void Start()
        {
            game = FindAnyObjectByType<DemoGame>();
            if (inscriptionLabel) labelRenderer = inscriptionLabel.GetComponent<Renderer>();
        }

        void Update()
        {
            if (labelRenderer && game) labelRenderer.enabled = CanRead(game);
        }

        public bool CanRead(DemoGame game)
        {
            foreach (var candle in game.candles)
            {
                if (!candle || !candle.gameObject.activeInHierarchy || !candle.candleLight || !candle.candleLight.enabled) continue;
                Vector3 start = candle.candleLight.transform.position;
                Vector3 offset = transform.position - start;
                if (offset.magnitude > 3.8f) continue;
                bool blocked = false;
                foreach (var hit in Physics.RaycastAll(start, offset.normalized, offset.magnitude, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<DemoClue>() == this ||
                        hit.collider.GetComponentInParent<CarryObject>() || hit.collider.GetComponentInParent<DemoPlayer>()) continue;
                    blocked = true;
                    break;
                }
                if (!blocked) return true;
            }
            return false;
        }
    }
}
