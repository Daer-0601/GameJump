#if UNITY_EDITOR
using System.Collections.Generic;
using Gamejam2026;
using UnityEditor;
using UnityEngine;

// Additive, original geometry. Keeps the existing suit, interaction roots and colliders.
public static class JamDetails
{
    static Material iron, brass, rubber, tape, glass, wax, flame, core;

    [MenuItem("Gamejam/Pulido/Mejorar modelos propios y animación")]
    public static void Apply()
    {
        var game = JamPolish.OpenGame();
        iron = Mat("Rec_Hierro", new Color(.16f, .18f, .19f), .65f, .28f);
        brass = Mat("Rec_Laton", new Color(.38f, .23f, .08f), .72f, .32f);
        rubber = Mat("Rec_Goma", new Color(.065f, .06f, .055f), .05f, .2f);
        tape = Mat("Rec_Cinta", new Color(.72f, .66f, .43f), .05f, .2f);
        glass = Mat("Rec_Visor", new Color(.025f, .07f, .085f), .3f, .78f);
        wax = Mat("Rec_Cera", new Color(.86f, .75f, .54f), 0, .15f);
        // Very weak warm transmission keeps the wax readable below its own flame.
        wax.EnableKeyword("_EMISSION");
        wax.SetColor("_EmissionColor", new Color(.09f, .06f, .025f));
        flame = JamPolish.GetMaterial("Rec_Llama", new Color(1.5f, .7f, .13f), true);
        core = JamPolish.GetMaterial("Rec_Nucleo", new Color(2.4f, 1.8f, .65f), true);

        foreach (var player in game.players)
        {
            var suit = player.transform.Find("Traje de mantenimiento");
            if (!suit) throw new System.InvalidOperationException("Falta el traje existente; no se sustituye el jugador.");
            if (!suit.Find("Detalle traje"))
            {
                var detail = new GameObject("Detalle traje").transform;
                detail.SetParent(suit, false);
                Part(detail, "Visor de cristal", new Vector3(0, 1.50f, .19f), JamModels.Loft("Rec_VisorForma", new[] { new Vector3(0, .135f, .032f), new Vector3(.10f, .135f, .032f) }), glass);
                var bezel = Part(detail, "Marco del visor", new Vector3(0, 1.55f, .211f), Ring("Rec_MarcoVisor", .10f, .012f), iron);
                bezel.localRotation = Quaternion.Euler(90, 0, 0);
                bezel.localScale = new Vector3(1.5f, 1f, .65f);
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1 : 1;
                    var filter = Part(detail, "Filtro respirador " + i, new Vector3(side * .11f, 1.445f, .19f), JamModels.Loft("Rec_Filtro", new[] { new Vector3(0, .052f, .052f), new Vector3(.045f, .052f, .052f), new Vector3(.055f, .041f, .041f) }, 10), rubber);
                    filter.localRotation = Quaternion.Euler(90, 0, 0);
                    var rim = Part(detail, "Aro filtro " + i, new Vector3(side * .11f, 1.445f, .241f), Ring("Rec_AroFiltro", .043f, .006f), brass);
                    rim.localRotation = Quaternion.Euler(90, 0, 0);
                    Part(detail, "Tirante frontal " + i, new Vector3(side * .15f, .91f, .164f), JamModels.Loft("Rec_Tirante", new[] { new Vector3(0, .027f, .013f), new Vector3(.27f, .027f, .013f) }, 4), tape);
                    Part(detail, "Tirante posterior " + i, new Vector3(side * .15f, .91f, -.164f), JamModels.Loft("Rec_Tirante", new[] { new Vector3(0, .027f, .013f), new Vector3(.27f, .027f, .013f) }, 4), tape);
                    var leg = suit.Find(i == 0 ? "Pierna izquierda" : "Pierna derecha");
                    Part(leg, "Rodillera", new Vector3(0, -.30f, .10f), JamModels.Loft("Rec_Rodillera", new[] { new Vector3(-.07f, .065f, .018f), new Vector3(.07f, .065f, .018f) }, 6), rubber);
                    Part(leg, "Suela reforzada", new Vector3(0, -.69f, .04f), JamModels.Loft("Rec_Suela", new[] { new Vector3(0, .115f, .165f), new Vector3(.025f, .115f, .165f) }), rubber);
                    var arm = suit.Find("Manga " + i);
                    Part(arm, "Guante", new Vector3(0, -.59f, 0), JamModels.Loft("Rec_Guante", new[] { new Vector3(0, .065f, .06f), new Vector3(.03f, .082f, .07f), new Vector3(.115f, .077f, .065f) }), rubber);
                    Part(arm, "Puno reforzado", new Vector3(0, -.49f, 0), JamModels.Loft("Rec_Puno", new[] { new Vector3(0, .084f, .075f), new Vector3(.035f, .084f, .075f) }), iron);
                }
                Part(detail, "Cinturon", new Vector3(0, .81f, 0), JamModels.Loft("Rec_Cinturon", new[] { new Vector3(0, .26f, .165f), new Vector3(.065f, .26f, .165f) }), rubber);
                Part(detail, "Hebilla", new Vector3(0, .82f, .177f), JamModels.Loft("Rec_Hebilla", new[] { new Vector3(0, .05f, .018f), new Vector3(.045f, .05f, .018f) }, 4), brass);
            }
            var motion = player.GetComponent<DemoMotion>();
            motion.arms = new[] { suit.Find("Manga 0"), suit.Find("Manga 1") };
            foreach (var child in suit.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 8 + player.playerIndex;
            EditorUtility.SetDirty(motion);
        }

        foreach (var item in Object.FindObjectsByType<CarryObject>())
        {
            var model = item.transform.Find("Modelo artesanal");
            if (!model) continue;
            if (item.isCandle)
            {
                model.Find("Cera irregular").GetComponent<Renderer>().sharedMaterial = wax;
                model.Find("Portavela").GetComponent<Renderer>().sharedMaterial = brass;
                var fire = model.Find("Llama");
                fire.GetComponent<Renderer>().sharedMaterial = flame;
                item.candleLight.transform.localPosition = new Vector3(0, .44f, -.1f);
                var animation = item.GetComponent<DemoCandleFlame>();
                if (!animation) animation = item.gameObject.AddComponent<DemoCandleFlame>();
                animation.candle = item;
                animation.flame = fire;
                if (!model.Find("Detalle vela"))
                {
                    var detail = new GameObject("Detalle vela").transform;
                    detail.SetParent(model, false);
                    Part(detail, "Mecha", new Vector3(0, .33f, 0), JamModels.Loft("Rec_Mecha", new[] { new Vector3(0, .005f, .005f), new Vector3(.028f, .004f, .004f) }, 5), rubber);
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = i * Mathf.PI * .5f;
                        Part(detail, "Gota de cera " + i, new Vector3(Mathf.Cos(angle) * .05f, .16f + i * .018f, Mathf.Sin(angle) * .05f),
                            JamModels.Loft("Rec_Gota" + i, new[] { new Vector3(0, .009f, .009f), new Vector3(.018f, .014f, .014f), new Vector3(.09f - i * .012f, .012f, .012f) }, 7), wax);
                    }
                    Part(detail, "Borde portavela", new Vector3(0, .044f, 0), Ring("Rec_BordeVela", .145f, .008f), brass);
                    var handle = Part(detail, "Asa circular", new Vector3(.16f, .13f, 0), Ring("Rec_AsaVela", .06f, .011f), brass);
                    handle.localRotation = Quaternion.Euler(90, 0, 0);
                    model.Find("Asa").GetComponent<Renderer>().enabled = false;
                    Part(fire, "Llama nucleo", new Vector3(0, .008f, 0), JamModels.Loft("Rec_NucleoForma", new[] { new Vector3(0, .009f, .008f), new Vector3(.023f, .013f, .011f), new Vector3(.06f, .002f, .002f) }, 7), core);
                }
                foreach (var renderer in fire.GetComponentsInChildren<Renderer>())
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                model.Find("Detalle vela/Mecha").GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            else if (!model.Find("Detalle manivela"))
            {
                var detail = new GameObject("Detalle manivela").transform;
                detail.SetParent(model, false);
                for (int i = 0; i < 5; i++)
                    Part(detail, "Relieve del mango " + i, new Vector3(.17f, .115f + i * .028f, 0), Ring("Rec_MangoAro", .046f, .006f), rubber);
                Part(detail, "Acople de laton", new Vector3(-.17f, -.025f, 0), Ring("Rec_Acople", .034f, .01f), brass);
                for (int i = 0; i < 2; i++)
                    Part(detail, "Perno hexagonal " + i, new Vector3(i == 0 ? -.17f : .17f, .078f, 0), JamModels.Loft("Rec_Perno", new[] { new Vector3(0, .043f, .043f), new Vector3(.025f, .043f, .043f) }, 6), brass);
                model.Find("Brazo de hierro").GetComponent<Renderer>().sharedMaterial = iron;
            }
        }
        JamPolish.Save();
        Debug.Log("RECOVERY_MODELS_OK");
    }

    static Material Mat(string name, Color color, float metallic, float smoothness)
    {
        var mat = JamPolish.GetMaterial(name, color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Transform Part(Transform parent, string name, Vector3 position, Mesh mesh, Material material)
    {
        var part = new GameObject(name).transform;
        part.SetParent(parent, false);
        part.localPosition = position;
        part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        return part;
    }

    static Mesh Ring(string name, float radius, float thickness)
    {
        string path = "Assets/Game/02_Polish/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) return existing;
        const int segments = 20, sides = 6;
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        for (int s = 0; s <= segments; s++)
        for (int c = 0; c <= sides; c++)
        {
            float a = s * Mathf.PI * 2 / segments, b = c * Mathf.PI * 2 / sides;
            float r = radius + Mathf.Cos(b) * thickness;
            vertices.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(b) * thickness, Mathf.Sin(a) * r));
            uv.Add(new Vector2((float)s / segments, (float)c / sides));
            if (s == segments || c == sides) continue;
            int i = s * (sides + 1) + c, next = i + sides + 1;
            triangles.AddRange(new[] { i, i + 1, next, i + 1, next + 1, next });
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }
}
#endif
