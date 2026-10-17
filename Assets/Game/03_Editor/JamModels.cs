#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamejam2026;
using UnityEditor;
using UnityEngine;

// Original low-poly geometry generated locally. See CREDITOS.md for the CC0 dedication.
public static class JamModels
{
    const string Folder = "Assets/Game/02_Polish";

    [MenuItem("Gamejam/Pulido/3 - Modelos simples")]
    public static void Apply()
    {
        var game = JamPolish.OpenGame();
        var iron = JamPolish.GetMaterial("Hierro_pulido", new Color(0.17f, 0.19f, 0.20f));
        var brass = JamPolish.GetMaterial("Laton", new Color(0.48f, 0.30f, 0.10f));
        var wax = JamPolish.GetMaterial("Cera_tallada", new Color(0.84f, 0.73f, 0.51f));
        var flame = JamPolish.GetMaterial("Llama_calida", new Color(1f, 0.65f, 0.2f), true);
        var visor = JamPolish.GetMaterial("Visor", new Color(0.16f, 0.20f, 0.22f));
        var rubber = JamPolish.GetMaterial("Goma", new Color(0.055f, 0.05f, 0.045f));
        var tape = JamPolish.GetMaterial("Cintas", new Color(0.68f, 0.64f, 0.43f));
        foreach (var player in game.players)
        {
            if (player.transform.Find("Traje de mantenimiento")) continue;
            var old = player.transform.Find("Cuerpo provisional");
            if (old) old.gameObject.SetActive(false);
            var suit = new GameObject("Traje de mantenimiento").transform;
            suit.SetParent(player.transform, false);
            var cloth = JamPolish.GetMaterial(player.playerIndex == 0 ? "Traje_azul" : "Traje_ocre",
                player.playerIndex == 0 ? new Color(0.12f, 0.27f, 0.30f) : new Color(0.44f, 0.28f, 0.10f));
            Part(suit, "Torso", Vector3.up * 0.70f, Loft("Torso", new[] { new Vector3(0,.21f,.13f), new Vector3(.15f,.27f,.16f), new Vector3(.58f,.29f,.15f), new Vector3(.66f,.20f,.13f) }), cloth);
            Part(suit, "Capucha", Vector3.up * 1.35f, Loft("Capucha", new[] { new Vector3(0,.16f,.15f), new Vector3(.14f,.20f,.18f), new Vector3(.29f,.17f,.16f), new Vector3(.34f,.10f,.10f) }), cloth);
            var mask = Part(suit, "Máscara", new Vector3(0,1.48f,.157f), Loft("Mascara", new[] { new Vector3(0,.15f,.028f), new Vector3(.13f,.15f,.03f) }, 4), visor);
            mask.transform.localRotation = Quaternion.Euler(0f,45f,0f);
            for (int i = 0; i < 2; i++)
            {
                var leg = new GameObject(i == 0 ? "Pierna izquierda" : "Pierna derecha").transform;
                leg.SetParent(suit, false);
                leg.localPosition = new Vector3(i == 0 ? -.145f : .145f,.73f,0);
                Part(leg, "Pantalón", Vector3.zero, Loft("Pierna", new[] { new Vector3(-.57f,.095f,.10f), new Vector3(-.30f,.105f,.11f), new Vector3(0,.12f,.13f) }), cloth);
                Part(leg, "Bota", new Vector3(0,-.69f,.04f), Loft("Bota", new[] { new Vector3(0,.11f,.16f), new Vector3(.16f,.105f,.145f) }), rubber);
                var arm = Part(suit, "Manga " + i, new Vector3(i == 0 ? -.32f : .32f,1.26f,0), Loft("Manga", new[] { new Vector3(-.46f,.075f,.085f), new Vector3(-.10f,.10f,.10f), new Vector3(0,.11f,.11f) }), cloth);
                arm.transform.localRotation = Quaternion.Euler(-8f,0f,i == 0 ? -9f : 9f);
                Part(suit, "Cinta " + i, new Vector3(i == 0 ? -.18f : .18f,1.11f,.155f), Loft("Cinta", new[] { new Vector3(0,.033f,.014f), new Vector3(.15f,.033f,.014f) }, 4), tape);
            }
            var motion = player.GetComponent<DemoMotion>();
            if (!motion) motion = player.gameObject.AddComponent<DemoMotion>();
            motion.visual = suit;
            motion.legs = new[] { suit.Find("Pierna izquierda"), suit.Find("Pierna derecha") };
            foreach (var child in suit.GetComponentsInChildren<Transform>()) child.gameObject.layer = 8 + player.playerIndex;
            player.view.cullingMask &= ~(1 << (8 + player.playerIndex));
            EditorUtility.SetDirty(motion);
        }

        foreach (var item in UnityEngine.Object.FindObjectsByType<CarryObject>(FindObjectsInactive.Exclude))
        {
            if (item.transform.Find("Modelo artesanal")) continue;
            // Keep the original interaction colliders and the candle's Light; replace only visuals.
            foreach (var renderer in item.GetComponentsInChildren<MeshRenderer>()) renderer.enabled = false;
            var model = new GameObject("Modelo artesanal").transform;
            model.SetParent(item.transform, false);
            if (item.isCandle)
            {
                Part(model,"Portavela",Vector3.zero,Loft("Portavela",new[] {new Vector3(0,.12f,.12f),new Vector3(.025f,.16f,.16f),new Vector3(.05f,.14f,.14f)}),brass);
                Part(model,"Cera irregular",Vector3.up * .04f,Loft("Vela",new[] {new Vector3(0,.063f,.057f),new Vector3(.11f,.060f,.055f),new Vector3(.24f,.055f,.052f),new Vector3(.28f,.061f,.050f),new Vector3(.30f,.049f,.047f)},9),wax);
                Part(model,"Llama",Vector3.up * .34f,Loft("Llama",new[] {new Vector3(0,.015f,.014f),new Vector3(.035f,.028f,.02f),new Vector3(.09f,.004f,.004f)},7),flame);
                Part(model,"Asa",new Vector3(.11f,.07f,0),Loft("Asa",new[] {new Vector3(0,.018f,.025f),new Vector3(.14f,.018f,.025f)}),brass);
            }
            else
            {
                var bar = Part(model,"Brazo de hierro",new Vector3(-.17f,.09f,0),Loft("Manivela_barra",new[] {new Vector3(0,.032f,.035f),new Vector3(.34f,.032f,.035f)},6),iron);
                bar.transform.localRotation = Quaternion.Euler(0f,0f,-90f);
                Part(model,"Empuñadura",new Vector3(.17f,.09f,0),Loft("Manivela_mango",new[] {new Vector3(0,.047f,.047f),new Vector3(.16f,.047f,.047f),new Vector3(.19f,.035f,.035f)}),rubber);
                Part(model,"Eje de acople",new Vector3(-.17f,-.07f,0),Loft("Manivela_eje",new[] {new Vector3(0,.026f,.026f),new Vector3(.16f,.026f,.026f)},6),brass);
            }
        }
        foreach (var item in UnityEngine.Object.FindObjectsByType<CarryObject>(FindObjectsInactive.Exclude))
        {
            var model = item.transform.Find("Modelo artesanal");
            if (!model) continue;
            var renderers = model.GetComponentsInChildren<MeshRenderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var collider = item.GetComponent<BoxCollider>();
            if (!collider) collider = item.gameObject.AddComponent<BoxCollider>();
            collider.center = item.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size;
        }
        JamPolish.Save();
    }

    static GameObject Part(Transform parent, string name, Vector3 position, Mesh mesh, Material material)
    {
        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = material;
        return part;
    }

    // Rings are (height, radius X, radius Z). Duplicate vertices per face for faceted normals.
    public static Mesh Loft(string name, Vector3[] rings, int sides = 8)
    {
        string path = Folder + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) return existing;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        void Face(Vector3 a, Vector3 b, Vector3 c)
        {
            int index = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
        }
        Vector3 Point(int ring, int side)
        {
            float angle = (side + .5f) * Mathf.PI * 2f / sides;
            return new Vector3(Mathf.Cos(angle) * rings[ring].y, rings[ring].x, Mathf.Sin(angle) * rings[ring].z);
        }
        for (int s = 0; s < sides; s++)
        {
            int next = (s + 1) % sides;
            Face(new Vector3(0,rings[0].x,0),Point(0,s),Point(0,next));
            int last = rings.Length - 1;
            Face(new Vector3(0,rings[last].x,0),Point(last,next),Point(last,s));
            for (int r = 0; r < rings.Length - 1; r++)
            {
                Face(Point(r,s),Point(r + 1,s),Point(r,next));
                Face(Point(r,next),Point(r + 1,s),Point(r + 1,next));
            }
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,path);
        return mesh;
    }
}
#endif
