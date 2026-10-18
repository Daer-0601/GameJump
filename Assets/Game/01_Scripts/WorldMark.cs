using UnityEngine;

namespace Gamejam2026
{
    // Los avisos que antes flotaban se leen en el cuaderno. Aquí solo se ocultan.
    public class WorldMark : MonoBehaviour
    {
        TextMesh label;
        Renderer labelRenderer;

        public static void Adopt(TextMesh text)
        {
            if (!text || text.GetComponent<WorldMark>() || text.GetComponentInParent<WorldMark>()) return;
            if (IsEnvironmentSign(text)) return;
            var mark = text.gameObject.AddComponent<WorldMark>();
            mark.label = text;
        }

        public static bool IsEnvironmentSign(TextMesh text)
        {
            if (!text) return true;
            if (text.name == "Reverso de lectura") return true;
            if (text.GetComponentInParent<DemoClue>()) return true;
            if (text.transform.Find("Chapa de senal")) return true;
            for (Transform node = text.transform; node; node = node.parent)
            {
                string name = node.name;
                if (name.StartsWith("Lectura ") || name.StartsWith("Umbral ") || name.StartsWith("Marca de suelo")) return true;
                if (!name.StartsWith("Cartel ")) continue;
                string upper = name.ToUpperInvariant();
                if (upper.Contains("INSCRIP") || upper.Contains("LEE LA")) return false;
                return true;
            }
            return false;
        }

        void Awake()
        {
            if (!label) label = GetComponent<TextMesh>();
            if (!label || IsEnvironmentSign(label))
            {
                enabled = false;
                return;
            }
            labelRenderer = label.GetComponent<Renderer>();
            Hide();
        }

        void LateUpdate()
        {
            Hide();
        }

        void Hide()
        {
            if (labelRenderer) labelRenderer.enabled = false;
            var plate = transform.Find("Fondo de lectura");
            if (plate) plate.gameObject.SetActive(false);
        }

        // Pintura que solo se lee cuando le da la luz. No brilla en la oscuridad.
        public static void PaintFixed(TextMesh text)
        {
            if (!text) return;
            var renderer = text.GetComponent<Renderer>();
            if (!renderer) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader ? shader : Shader.Find("Standard"));
            Texture atlas = text.font ? text.font.material.mainTexture : null;
            if (atlas && material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", atlas);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
            material.EnableKeyword("_ALPHATEST_ON");
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.45f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.04f);
            Color ink = text.color;
            material.color = ink;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", ink);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }
    }
}
