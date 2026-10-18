using UnityEngine;

namespace Gamejam2026
{
    public class DemoCandleFlame : MonoBehaviour
    {
        public CarryObject candle;
        public Transform flame;
        Vector3 origin;
        Vector3 scale;
        DemoGame game;

        void Start()
        {
            if (!candle) candle = GetComponent<CarryObject>();
            game = FindAnyObjectByType<DemoGame>();
            if (flame)
            {
                origin = flame.localPosition;
                scale = flame.localScale;
                RecintoSpecialEffects.AttachCandleEmbers(flame);
            }
        }

        void Update()
        {
            if (!flame || !candle || !candle.candleLight) return;
            flame.gameObject.SetActive(candle.candleLight.enabled);
            if (!game || !game.Playing || !candle.candleLight.enabled) return;
            float noise = Mathf.PerlinNoise(Time.time * 3.2f, 7.3f);
            float size = Mathf.Lerp(.45f, 1f, game.CandleCharge) * (game.CandleBurst ? .3f : 1f);
            flame.localScale = Vector3.Scale(scale, new Vector3(1.02f - noise * .08f, .92f + noise * .16f, 1f)) * size;
            flame.localPosition = origin + new Vector3(Mathf.Sin(Time.time * 2.7f) * .003f, 0, Mathf.Sin(Time.time * 1.9f) * .002f);
            flame.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 2.1f) * 3f, 0, Mathf.Sin(Time.time * 2.7f) * 4f);
        }
    }
}
