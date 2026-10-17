#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class FinalTexturePass
{
    const string Folder = "Assets/Game/05_Mapa/Acabados";
    static Material brick, plaster, damp, metal, roof, ground, inside, pumpFloor, stain;

    [MenuItem("Gamejam/Arte/Aplicar texturas al mapa aprobado")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("salir de play primero");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var root = GameObject.Find("Recinto - definitivo");
        if (!root || scene.path != "Assets/Game/00_Scenes/Demo.unity") throw new Exception("abrir demo con el mapa aprobado");
        Directory.CreateDirectory(Folder + "/Meshes");
        Directory.CreateDirectory(Folder + "/Materials");
        AssetDatabase.Refresh();
        var backup = AssetDatabase.GenerateUniqueAssetPath("Assets/Game/05_Mapa/Boceto/DemoAntesDeTexturas.unity");
        EditorSceneManager.SaveScene(scene, backup, true);
        var previousDetail = root.transform.Find("Acabados de textura");
        if (previousDetail) Object.DestroyImmediate(previousDetail.gameObject);
        brick = Mat("Ladrillo deposito", "Brick", 2);
        plaster = Mat("Revoque taller", "Wall", 14);
        damp = Mat("Concreto humedo", "Wall", 9);
        metal = Mat("Metal oxidado", "Metal", 2);
        roof = Mat("Chapa techos", "Metal", 6);
        ground = Mat("Concreto exterior", "Stone", 12);
        inside = Mat("Piso taller", "Floor", 12);
        pumpFloor = Mat("Piso bombas", "Floor", 9);
        stain = Mat("Humedad", "Stain", 7, true);

        // cierra los bordes del techo sin cambiar los caminos
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if (!filter.GetComponent<BoxCollider>()) continue;
            var t = filter.transform;
            if (t.name == "Techo deposito" || t.name == "Techo taller" || t.name == "Techo caseta bombas")
            {
                t.position = new Vector3(t.position.x, 5.1f, t.position.z);
                var size = t.name == "Techo deposito" ? new Vector3(30.6f, 0.35f, 28.6f) : t.name == "Techo taller" ? new Vector3(34.6f, 0.35f, 40.6f) : new Vector3(14.6f, 0.35f, 16.6f);
                t.localScale = size;
            }
            if (t.name == "Piso continuo") { Floor(filter); continue; }
            string group = t.parent.name;
            bool ceiling = t.name.StartsWith("Techo") || t.name.StartsWith("Paso cubierto") || t.name.StartsWith("Pasaje oscuro");
            bool equipment = t.name.Contains("Estanteria") || t.name.Contains("Maquinaria") || t.name.Contains("Equipo") || t.name.Contains("Banco") || t.name.Contains("Mesa") || t.name.Contains("Pilas");
            Material chosen = ceiling ? roof : equipment ? metal : group.StartsWith("02") ? brick : group.StartsWith("04") ? plaster : damp;
            if (t.name.Contains("Almacen") || t.name.Contains("Porton")) chosen = roof;
            filter.GetComponent<Renderer>().sharedMaterial = chosen;
            BoxUV(filter);
        }
        var tank = root.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name == "Tanque provisional");
        if (tank) tank.sharedMaterial = metal;

        var detail = new GameObject("Acabados de textura").transform;
        detail.SetParent(root.transform);
        // dinteles que cierran por encima de las puertas
        Lintel(detail, "Deposito sur", new Vector3(-12, 4.05f, 18), new Vector3(6, 1.9f, 0.5f), brick);
        Lintel(detail, "Deposito norte", new Vector3(-5, 4.05f, 46), new Vector3(6, 1.9f, 0.5f), brick);
        Lintel(detail, "Taller acceso", new Vector3(6, 4.05f, 76), new Vector3(0.5f, 1.9f, 6), plaster);
        Lintel(detail, "Bombas acceso", new Vector3(34, 4.05f, 100), new Vector3(6, 1.9f, 0.5f), damp);
        // las manchas quedan separadas de la pared y sin colision
        Mark(detail, new Vector3(-25.744f, 2.3f, 25), Vector3.right, 5, 3);
        Mark(detail, new Vector3(-18, 2.2f, 111.744f), Vector3.back, 6, 3.5f);
        Mark(detail, new Vector3(26.256f, 2.2f, 108), Vector3.right, 5, 3.5f);
        Mark(detail, new Vector3(39.744f, 2, 96), Vector3.left, 4, 3);

        Physics.SyncTransforms();
        FinalMapBuilder.Validate();
        CheckRoof(root.transform, "Techo deposito", new Vector3(-12, 0, 25));
        CheckRoof(root.transform, "Techo taller", new Vector3(-18, 0, 103));
        CheckRoof(root.transform, "Techo caseta bombas", new Vector3(34, 0, 109));
        // guarda una version transportable con los materiales incluidos
        PrefabUtility.SaveAsPrefabAsset(root, Folder + "/RecintoTexturizado.prefab");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        Debug.Log("TEXTURE_PASS_OK backup=" + backup);
        Preview();
    }

    static Material Mat(string name, string category, int index, bool transparent = false)
    {
        string path = Folder + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        string texturePath = "Assets/External/SBSHorror/256x256/" + (category == "Stain" ? "Stains" : category) + "/Horror_" + category + "_" + index.ToString("00") + "-256x256.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.wrapMode = transparent ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 4;
        importer.alphaIsTransparency = transparent;
        importer.SaveAndReimport();
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", category == "Metal" ? 0.18f : 0.05f);
        if (transparent)
        {
            mat.SetColor("_BaseColor", new Color(0.28f, 0.25f, 0.21f, 0.7f));
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void BoxUV(MeshFilter filter)
    {
        var mesh = Object.Instantiate(filter.sharedMesh);
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = filter.transform.TransformPoint(vertices[i]);
            var n = normals[i];
            uv[i] = Mathf.Abs(n.y) > 0.5f ? new Vector2(p.x, p.z) / 2 : Mathf.Abs(n.x) > 0.5f ? new Vector2(p.z, p.y) / 2 : new Vector2(p.x, p.y) / 2;
        }
        mesh.uv = uv;
        mesh.RecalculateTangents();
        SaveMesh(filter, mesh);
    }

    static void SaveMesh(MeshFilter filter, Mesh mesh)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/Meshes/" + filter.name + ".asset");
        AssetDatabase.CreateAsset(mesh, path);
        filter.sharedMesh = mesh;
    }

    static void Floor(MeshFilter filter)
    {
        // un solo piso con caras contiguas evita capas que parpadeen
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
        for (int x = -38; x < 42; x += 2)
        for (int z = -11; z < 119; z += 2)
        {
            float cx = x + 1, cz = z + 1;
            int material = cx >= -28 && cx <= 6 && cz >= 72 && cz <= 112 ? 1 : cx >= 12 && cx <= 40 && cz >= 90 && cz <= 116 ? 2 : 0;
            int start = vertices.Count;
            foreach (var p in new[] { new Vector3(x, 0, z), new Vector3(x, 0, z + 2), new Vector3(x + 2, 0, z + 2), new Vector3(x + 2, 0, z) })
            {
                vertices.Add(filter.transform.InverseTransformPoint(p));
                uv.Add(new Vector2(p.x, p.z) / 2);
            }
            triangles[material].AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        var mesh = new Mesh { name = "Piso por sectores", subMeshCount = 3 };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv);
        for (int i = 0; i < 3; i++) mesh.SetTriangles(triangles[i], i);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        SaveMesh(filter, mesh);
        filter.GetComponent<Renderer>().sharedMaterials = new[] { ground, inside, pumpFloor };
    }

    static void Lintel(Transform parent, string name, Vector3 position, Vector3 size, Material mat)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = "Dintel " + name;
        obj.transform.SetParent(parent);
        obj.transform.position = position;
        obj.transform.localScale = size;
        obj.GetComponent<Renderer>().sharedMaterial = mat;
        BoxUV(obj.GetComponent<MeshFilter>());
    }

    static void Mark(Transform parent, Vector3 position, Vector3 normal, float width, float height)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        obj.name = "Humedad superficial";
        obj.transform.SetParent(parent);
        obj.transform.position = position;
        obj.transform.rotation = Quaternion.LookRotation(-normal);
        obj.transform.localScale = new Vector3(width, height, 1);
        Object.DestroyImmediate(obj.GetComponent<Collider>());
        var renderer = obj.GetComponent<Renderer>();
        renderer.sharedMaterial = stain;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    static void CheckRoof(Transform root, string name, Vector3 point)
    {
        var t = root.GetComponentsInChildren<Transform>().First(x => x.name == name);
        var bounds = t.GetComponent<BoxCollider>().bounds;
        if (bounds.min.y > 5 || bounds.max.y < 5) throw new Exception("hueco sobre las paredes: " + name);
        if (!Physics.Raycast(point + Vector3.up * 1.7f, Vector3.up, out var hit, 5) || hit.collider.transform != t)
            throw new Exception("falta cobertura: " + name);
        int samples = 0;
        for (float x = bounds.min.x + 0.6f; x < bounds.max.x - 0.6f; x += 1)
        for (float z = bounds.min.z + 0.6f; z < bounds.max.z - 0.6f; z += 1)
        {
            if (!t.GetComponent<Collider>().Raycast(new Ray(new Vector3(x, 4.7f, z), Vector3.up), out _, 1))
                throw new Exception("hueco en techo " + name + " " + x + "," + z);
            samples++;
        }
        if (t.GetComponent<Renderer>().bounds != bounds) Debug.Log("TEXTURE_ROOF_BOUNDS " + name + " visual=" + t.GetComponent<Renderer>().bounds + " collider=" + bounds);
        Debug.Log("TEXTURE_ROOF_OK " + name + " muestras=" + samples);
    }

    [MenuItem("Gamejam/Arte/Capturar revision de texturas")]
    public static void Preview()
    {
        var obj = new GameObject("revision temporal");
        var camera = obj.AddComponent<Camera>();
        var lamp = obj.AddComponent<Light>();
        lamp.type = LightType.Point; lamp.range = 18; lamp.intensity = 5;
        camera.farClipPlane = 160;
        camera.fieldOfView = 75;
        var target = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        camera.targetTexture = target;
        var positions = new[] { new Vector3(-12, 1.7f, 21), new Vector3(-4, 1.7f, 94), new Vector3(34, 1.7f, 103) };
        var looks = new[] { new Vector3(-7, 2, 40), new Vector3(-21, 2, 105), new Vector3(34, 2, 114) };
        for (int i = 0; i < positions.Length; i++)
        {
            obj.transform.position = positions[i]; obj.transform.LookAt(looks[i]);
            camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes("../texturas-sector-" + i + ".png", image.EncodeToPNG());
        }
        RenderTexture.active = previous;
        Object.DestroyImmediate(obj); Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target);
        Debug.Log("TEXTURE_PREVIEWS_OK");
    }
}
#endif
