using UnityEngine;

namespace Gamejam2026
{
    public class DemoCandleFlame : MonoBehaviour
    {
        public CarryObject candle;
        public Transform flame;
        Vector3 origin;
        Vector3 scale;

        void Start()
        {
            if (!candle) candle = GetComponent<CarryObject>();
            if (flame) { origin = flame.localPosition; scale = flame.localScale; }
        }

        void Update()
        {
            if (!flame || !candle || !candle.candleLight || !candle.candleLight.enabled || Time.timeScale == 0f) return;
            float noise = Mathf.PerlinNoise(Time.time * 3.2f, 7.3f);
            flame.localScale = Vector3.Scale(scale, new Vector3(1.02f - noise * .08f, .92f + noise * .16f, 1f));
            flame.localPosition = origin + new Vector3(Mathf.Sin(Time.time * 2.7f) * .003f, 0, Mathf.Sin(Time.time * 1.9f) * .002f);
            flame.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 2.1f) * 3f, 0, Mathf.Sin(Time.time * 2.7f) * 4f);
        }
    }
}
