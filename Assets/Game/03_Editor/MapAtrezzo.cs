#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gamejam2026;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class MapAtrezzo
{
    const string FbxFolder = "Assets/Game/04_Atrezzo/FBX";
    const string TexFolder = "Assets/Game/04_Atrezzo/Tex";
    const string MatFolder = "Assets/Game/04_Atrezzo/Materials";
    const string KitFolder = "Assets/Game/04_Atrezzo/Resources/Atrezzo";
    const string ScenePath = "Assets/Game/00_Scenes/Demo.unity";

    static Transform map;
    static Transform root;
    static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
    static readonly Dictionary<string, Vector3> sizes = new Dictionary<string, Vector3>();
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static readonly HashSet<string> used = new HashSet<string>();
    static readonly List<Bounds> keeps = new List<Bounds>();
    static Light[] lights = Array.Empty<Light>();
    static Material glass;
    static float shelfScale = 0.1f;
    static readonly Quaternion Upright = Quaternion.Euler(-90f, 0f, 0f);
    static readonly HashSet<string> NativeUp = new HashSet<string>
    {
        "crate", "generator", "toolbox", "broom", "hose", "vice"
    };
    static int seq;
    static int placed;

    [MenuItem("Gamejam/Mapa definitivo/Colocar atrezzo")]
    public static void Build()
    {
        EditorSceneManager.OpenScene(ScenePath);
        map = GameObject.Find("Recinto - definitivo")?.transform;
        if (!map) throw new Exception("falta el recinto definitivo");
        seq = 0;
        placed = 0;
        used.Clear();
        keeps.Clear();
        sizes.Clear();
        materials.Clear();
        Directory.CreateDirectory(MatFolder);
        LoadPrefabs();
        FixScale();
        PrepareTextures();
        ClearPrevious();
        lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        root = new GameObject("Atrezzo del recinto").transform;
        root.SetParent(map, false);
        BuildKeeps();
        shelfScale = 2.15f / Mathf.Max(0.01f, IdentitySize("shelf").y);
        PublishKit();
        DressMechanisms();
        PlaceShelves();
        PlaceZones();
        PlaceCables();
        PlacePipes();
        PlaceRats();
        PlaceMissing();
        Physics.SyncTransforms();
        FinalMapBuilder.Validate();
        PrefabUtility.SaveAsPrefabAsset(map.gameObject, "Assets/Game/05_Mapa/RecintoDefinitivo.prefab");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("ATREZZO_READY piezas=" + placed + " modelos=" + used.Count + "/" + prefabs.Count);
    }

    static Quaternion Rest(string key) => NativeUp.Contains(key) ? Quaternion.identity : Upright;

    static void LoadPrefabs()
    {
        prefabs.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { FbxFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab) prefabs[Path.GetFileNameWithoutExtension(path)] = prefab;
        }
        if (!prefabs.ContainsKey("barrel") || !prefabs.ContainsKey("rat") || !prefabs.ContainsKey("shelf"))
            throw new Exception("faltan fbx de atrezzo");
    }

    static void FixScale()
    {
        var probe = UnityEngine.Object.Instantiate(prefabs["barrel"]);
        probe.transform.position = Vector3.zero;
        probe.transform.rotation = Rest("barrel");
        probe.transform.localScale = Vector3.one;
        var size = RendererBounds(probe).size;
        UnityEngine.Object.DestroyImmediate(probe);
        Debug.Log("ATREZZO_BARREL " + size);
        if (size.y < 0.7f || size.y > 1.35f) throw new Exception("escala de barril incorrecta " + size);
    }

    static void PrepareTextures()
    {
        foreach (var folder in Directory.GetDirectories(TexFolder))
        {
            foreach (var file in Directory.GetFiles(folder))
            {
                if (!file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) && !file.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                string asset = ToAsset(file);
                var importer = AssetImporter.GetAtPath(asset) as TextureImporter;
                if (!importer) continue;
                string name = Path.GetFileName(file).ToLowerInvariant();
                bool dirty = false;
                if (name.Contains("_nor"))
                {
                    if (importer.textureType != TextureImporterType.NormalMap) { importer.textureType = TextureImporterType.NormalMap; dirty = true; }
                    if (!importer.flipGreenChannel) { importer.flipGreenChannel = true; dirty = true; }
                }
                else if (name.Contains("_arm"))
                {
                    if (importer.sRGBTexture) { importer.sRGBTexture = false; dirty = true; }
                }
                if (dirty) importer.SaveAndReimport();
            }
        }
    }

    static void ClearPrevious()
    {
        var previous = map.Find("Atrezzo del recinto");
        if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        foreach (var nombre in new[] { "Rata j1", "Rata j2" })
        {
            var rata = map.Find(nombre);
            if (rata) UnityEngine.Object.DestroyImmediate(rata.gameObject);
        }
        var decor = map.Find("Detalles reutilizados del mapa nuevo");
        if (decor)
        {
            for (int i = decor.childCount - 1; i >= 0; i--)
            {
                var child = decor.GetChild(i);
                if (child.name.StartsWith("Reutilizado - ")) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
        foreach (var nombre in new[] { "Estanteria oeste", "Estanteria centro", "Estanteria norte", "Maquinaria taller", "Equipo fondo", "Equipo tuberias", "Pilas junto al muro" })
        {
            var bloque = map.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == nombre);
            if (bloque) bloque.gameObject.SetActive(false);
        }
        foreach (var mech in UnityEngine.Object.FindObjectsByType<DemoMechanism>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var old = mech.transform.Find("Tuberias del soporte");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
    }

    static void BuildKeeps()
    {
        AddPath(FinalMapBuilder.SharedRoute, 1.12f);
        AddPath(FinalMapBuilder.WorkshopRoute, 1.12f);
        AddPath(FinalMapBuilder.PumpRoute, 1.12f);
        Keep(-3.6f, -8.2f, 3.6f, -3.1f);
        Keep(-5.2f, 3f, -0.6f, 6.8f);
        Keep(0.4f, 1f, 5.4f, 5.4f);
        Keep(-7.4f, 37.6f, -2.6f, 44.4f);
        Keep(-23.4f, 100.2f, -18.6f, 109.6f);
        Keep(31.4f, 106.2f, 36.8f, 114.6f);
        Keep(10.2f, 54f, 17.8f, 62.8f);
        Keep(-11.3f, 34f, -8.3f, 43.4f);
        Keep(-9f, 81.2f, -6f, 91.4f);
        Keep(-16.5f, 92f, -11.5f, 97f);
        Keep(-17.2f, 22f, -12.8f, 26.5f);
        Keep(21.8f, 68f, 26.2f, 72.5f);
        Keep(-14.2f, 80f, -9.8f, 84.5f);
        Keep(-21.2f, 102f, -16.8f, 106.5f);
        Keep(28.8f, 103f, 33.2f, 110.5f);
        Keep(-26.4f, 21.2f, -23.6f, 25.2f);
        Keep(2.0f, 21.6f, 4.4f, 25.6f);
        Keep(-26.4f, 36.8f, -23.6f, 40.8f);
        Keep(5.3f, 63.6f, 8.2f, 67.8f);
        Keep(-25.6f, 71.2f, -22.4f, 74.6f);
        Keep(-28.3f, 80.0f, -25.4f, 84.0f);
        Keep(37.4f, 105.8f, 40.2f, 110.0f);
        Keep(12.4f, 89.0f, 16.0f, 92.8f);
        Keep(-16.2f, 17.4f, -8.8f, 19.2f);
        Keep(-8.2f, 44.6f, -1.8f, 47.2f);
        Keep(5.4f, 72.6f, 7.0f, 79.4f);
        Keep(30.6f, 99.4f, 37.4f, 101.2f);
        Keep(-16.4f, 88.6f, -11.6f, 91.4f);
    }

    static void AddPath(Vector3[] points, float half)
    {
        for (int i = 1; i < points.Length; i++)
        {
            var a = points[i - 1];
            var b = points[i];
            Keep(Mathf.Min(a.x, b.x) - half, Mathf.Min(a.z, b.z) - half, Mathf.Max(a.x, b.x) + half, Mathf.Max(a.z, b.z) + half);
        }
    }

    static void Keep(float x0, float z0, float x1, float z1)
    {
        var bounds = new Bounds();
        bounds.SetMinMax(new Vector3(x0, 0f, z0), new Vector3(x1, 2.4f, z1));
        keeps.Add(bounds);
    }

    static void PublishKit()
    {
        Directory.CreateDirectory("Assets/Game/04_Atrezzo/Resources");
        Directory.CreateDirectory(KitFolder);
        foreach (var key in new[] { "cart", "generator", "stool", "boombox", "toolbox", "crate" })
        {
            if (!prefabs.ContainsKey(key)) continue;
            var obj = Create(key, 1f);
            obj.name = key;
            obj.transform.SetParent(null, true);
            obj.transform.position = Vector3.zero;
            foreach (var collider in obj.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            PrefabUtility.SaveAsPrefabAsset(obj, KitFolder + "/" + key + ".prefab");
            UnityEngine.Object.DestroyImmediate(obj);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void DressMechanisms()
    {
        foreach (var mech in UnityEngine.Object.FindObjectsByType<DemoMechanism>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var cascara = mech.transform.Find("Carcasa industrial importada");
            if (cascara) cascara.gameObject.SetActive(false);
            var bloque = mech.transform.Find("Base interactuable");
            if (bloque)
            {
                var mesh = bloque.GetComponent<MeshRenderer>();
                if (mesh) mesh.enabled = false;
            }
            var disco = mech.transform.Find("Indicador de giro");
            if (disco)
            {
                var mesh = disco.GetComponent<MeshRenderer>();
                if (mesh) mesh.enabled = false;
            }
            var group = new GameObject("Tuberias del soporte").transform;
            group.SetParent(mech.transform, false);
            bool towardPath = mech.symbol == "A";
            float side = towardPath ? -0.62f : 0f;
            float back = towardPath ? 0.08f : 0.38f;
            AddChildPipe(group, "pipe_pipe02", new Vector3(side, 0.98f, back), Quaternion.identity);
            AddChildPipe(group, "pipe_pipe06", new Vector3(side, 1.38f, back), Quaternion.Euler(0f, towardPath ? 90f : 0f, 90f));
            AddChildPipe(group, "pipe_pipe04", new Vector3(side - 0.18f, 0.42f, back + 0.12f), Quaternion.Euler(0f, towardPath ? -90f : 180f, 0f));
            AddChildPipe(group, "pipe_pipe08", new Vector3(side, 0.55f, back - 0.16f), Quaternion.Euler(0f, 180f, 0f));
            AddChildPipe(group, "pipe_pipe01", new Vector3(side + 0.32f, 0.44f, back), Quaternion.Euler(0f, 0f, 90f));
        }
    }

    static void AddChildPipe(Transform group, string key, Vector3 local, Quaternion extra)
    {
        if (!prefabs.ContainsKey(key)) return;
        var obj = Create(key, 1f);
        obj.transform.SetParent(group, true);
        obj.transform.localPosition = local;
        obj.transform.localRotation = extra * Rest(key);
        foreach (var collider in obj.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
        Preparar(obj, key);
        used.Add(key);
        placed++;
    }

    static void PlaceShelves()
    {
        ShelfRow(new Vector3(-25.72f, 0f, 19.6f), Vector3.forward, Vector3.right, 2);
        ShelfRow(new Vector3(-25.72f, 0f, 27.2f), Vector3.forward, Vector3.right, 2);
        ShelfRow(new Vector3(-25.72f, 0f, 33.4f), Vector3.forward, Vector3.right, 2);
        ShelfRow(new Vector3(-24.2f, 0f, 18.28f), Vector3.right, Vector3.forward, 2);
        ShelfRow(new Vector3(-27.72f, 0f, 73.4f), Vector3.forward, Vector3.right, 3);
        ShelfRow(new Vector3(-27.72f, 0f, 100.4f), Vector3.forward, Vector3.right, 2);
        ShelfRow(new Vector3(26.28f, 0f, 101.6f), Vector3.forward, Vector3.right, 2);
    }

    static void ShelfRow(Vector3 origin, Vector3 tangent, Vector3 inward, int count)
    {
        var size = IdentitySize("shelf");
        float width = Mathf.Max(size.x, size.z);
        float depth = Mathf.Min(size.x, size.z);
        for (int i = 0; i < count; i++)
        {
            var obj = Create("shelf", shelfScale);
            Vector3 largo = size.x >= size.z ? Vector3.right : Vector3.forward;
            Vector3 fondo = size.x >= size.z ? Vector3.forward : Vector3.right;
            float yaw = Vector3.SignedAngle(largo, tangent, Vector3.up);
            if (Vector3.Dot(Quaternion.Euler(0f, yaw, 0f) * fondo, inward) < 0f) yaw += 180f;
            obj.transform.rotation = Turn("shelf", yaw);
            obj.transform.position = origin + tangent * ((width + 0.12f) * i) + inward * (depth * 0.5f + 0.08f);
            Ground(obj, 0f);
            Accept(obj, "shelf");
        }
    }

    static void PlaceZones()
    {
        PlaceEntrance();
        PlaceDepot();
        PlacePatio();
        PlaceWorkshop();
        PlacePumps();
    }

    static void PlaceEntrance()
    {
        Put("sign", 2.6f, 8.2f, 12f);
        Put("sign", -21.6f, 8.6f, -8f);
        Put("tyre", -34.6f, -8.4f, 20f);
        Put("tyre", -33.9f, -8.1f, -15f);
        LayTyre(-34.2f, -7.2f, 40f);
        Put("trash", -34.7f, -6.4f, 18f);
        Put("trash", -34.2f, -5.9f, -22f);
        Lean("broom", new Vector3(-17.2f, 0f, -7.7f), Vector3.forward);
        Put("barrel", 38.4f, -8.6f, 12f);
        Put("cement", 37.6f, -7.8f, -20f);
        Put("trash", 8.6f, 11.2f, 30f);
        Put("crate", -32.4f, 11.6f, 8f);
        Put("crate", -31.7f, 12.3f, -14f);
    }

    static void PlaceDepot()
    {
        Put("crate", -23.4f, 21.8f, 8f);
        Put("crate", -23.0f, 22.6f, -14f);
        Put("crate", 2.15f, 20.7f, -6f);
        Put("barrel", 2.4f, 43.6f, 18f);
        Put("trash", -24.8f, 44.6f, -16f);
        Put("trash", -24.2f, 44.1f, 22f);
        Lean("broom", new Vector3(-25.72f, 0f, 29.8f), Vector3.right);
        Put("cart", 2.2f, 28.8f, -85f);
        Put("cement", -24.8f, 33.8f, 10f);
        Put("toolbox", -24.7f, 28.4f, 8f);
        Put("stool", 2.35f, 20.6f, 12f);
    }

    static void PlacePatio()
    {
        Picnic(24.6f, 63.55f, Vector3.right);
        Put("chair", 22.6f, 64.6f, 110f);
        Put("chair", 26.4f, 64.4f, -70f);
        Put("trash", 7.15f, 63.2f, 14f);
        Put("trash", 7.55f, 63.7f, -28f);
        Put("trash", 7.35f, 64.15f, 40f);
        Put("tyre", 28.8f, 63.3f, 8f);
        LayTyre(28.2f, 64.0f, 25f);
        Put("barrel", 7.3f, 78.4f, 20f);
        Put("sign", 18.4f, 63.1f, 6f);
        Hose(new Vector3(29.72f, 0f, 68.6f), Vector3.left);
    }

    static void PlaceWorkshop()
    {
        Put("vice", -18.4f, 78.2f, 180f, 1f, 1.20f);
        Put("toolbox", -22.2f, 77.95f, 10f, 1f, 1.20f);
        Put("boombox", -16.6f, 78.4f, 200f, 1f, 1.20f);
        Put("stool", -23.8f, 80.4f, 18f);
        Put("welder", -26.6f, 86.8f, 90f);
        Put("cart", -25.6f, 74.2f, 25f);
        Lean("broom", new Vector3(5.72f, 0f, 86.6f), Vector3.left);
        Lean("ladder", new Vector3(5.72f, 0f, 107.4f), Vector3.left);
        Put("trash", 4.3f, 110.6f, 16f);
        Put("stool", -26.4f, 93.2f, -8f);
        Put("cement", -26.5f, 108.6f, 14f);
        Put("crate", 3.6f, 110.8f, -10f);
        Tipped("chair", 3.5f, 73.8f, 35f);
    }

    static void PlacePumps()
    {
        Put("barrel", 13.4f, 114.4f, -12f);
        Put("barrel", 38.2f, 114.6f, 16f);
        Put("trash", 13.2f, 92.6f, 24f);
        Put("cement", 13.6f, 93.4f, -18f);
        Put("cement", 14.2f, 94.0f, 22f);
        Lean("ladder", new Vector3(39.72f, 0f, 103.2f), Vector3.left);
        Hose(new Vector3(39.55f, 0f, 114.6f), Vector3.left);
        Put("generator", 38.4f, 101.6f, 90f);
        Put("tyre", 13.3f, 85.6f, 10f);
        LayTyre(13.8f, 86.4f, -30f);
    }

    static void Picnic(float x, float z, Vector3 along)
    {
        var obj = Create("picnic", 1f);
        var size = IdentitySize("picnic");
        Vector3 largo = size.x >= size.z ? Vector3.right : Vector3.forward;
        obj.transform.rotation = Turn("picnic", Vector3.SignedAngle(largo, along, Vector3.up));
        obj.transform.position = new Vector3(x, 0f, z);
        Ground(obj, 0f);
        Accept(obj, "picnic");
    }

    static void Hose(Vector3 wall, Vector3 inward)
    {
        var obj = Create("hose", 1f);
        obj.transform.rotation = Quaternion.LookRotation(inward, Vector3.up);
        obj.transform.position = wall + inward * 0.08f;
        FlushWall(obj, wall, inward);
        var bounds = RendererBounds(obj);
        obj.transform.position += Vector3.up * (1.05f - bounds.min.y);
        Accept(obj, "hose");
    }

    static void Lean(string key, Vector3 wall, Vector3 inward)
    {
        var obj = Create(key, 1f);
        Vector3 dir = (Vector3.up - inward * 0.28f).normalized;
        obj.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir) * Rest(key);
        obj.transform.position = wall + inward * 0.38f;
        Ground(obj, 0f);
        FlushWall(obj, wall, inward);
        Accept(obj, key);
    }

    static void Tipped(string key, float x, float z, float yaw)
    {
        var obj = Create(key, 1f);
        obj.transform.rotation = Quaternion.Euler(78f, yaw, 0f) * Rest(key);
        obj.transform.position = new Vector3(x, 0f, z);
        Ground(obj, 0f);
        Accept(obj, key);
    }

    static void LayTyre(float x, float z, float yaw)
    {
        var obj = Create("tyre", 1f, Quaternion.Euler(0f, yaw, 0f));
        obj.transform.position = new Vector3(x, 0f, z);
        Ground(obj, 0f);
        Accept(obj, "tyre");
    }

    static void PlaceCables()
    {
        var keys = prefabs.Keys.Where(k => k.StartsWith("cable_")).OrderBy(k => k).ToList();
        int n = keys.Count;
        int a = n / 4;
        CableRun(new Vector3(-25.74f, 0f, 19.4f), Vector3.right, Vector3.forward, 2.18f, keys.Take(a));
        CableRun(new Vector3(-27.74f, 0f, 73.3f), Vector3.right, Vector3.forward, 2.18f, keys.Skip(a).Take(a));
        CableRun(new Vector3(39.74f, 0f, 71.2f), Vector3.left, Vector3.forward, 2.05f, keys.Skip(a * 2).Take(a));
        CableRun(new Vector3(27.2f, 0f, 115.74f), Vector3.back, Vector3.right, 2.18f, keys.Skip(a * 3));
    }

    static void CableRun(Vector3 wall0, Vector3 inward, Vector3 along, float y, IEnumerable<string> keys)
    {
        Vector3 cursor = wall0;
        foreach (var key in keys)
        {
            var obj = Create(key, 1f);
            obj.transform.rotation = Quaternion.LookRotation(inward, along) * Rest(key);
            obj.transform.position = cursor + Vector3.up * y;
            FlushWall(obj, wall0, inward);
            if (!Accept(obj, key)) continue;
            var bounds = RendererBounds(obj);
            float minA = float.PositiveInfinity, maxA = float.NegativeInfinity;
            Vector3 ext = bounds.extents;
            Vector3 c = bounds.center;
            for (int i = 0; i < 8; i++)
            {
                var p = c + new Vector3((i & 1) == 0 ? -ext.x : ext.x, (i & 2) == 0 ? -ext.y : ext.y, (i & 4) == 0 ? -ext.z : ext.z);
                float d = Vector3.Dot(p, along);
                if (d < minA) minA = d;
                if (d > maxA) maxA = d;
            }
            cursor += along * (maxA - minA + 0.04f);
        }
    }

    static void PlacePipes()
    {
        FloorPipe("pipe_pipe02", 15.2f, 103.4f, 0f);
        FloorPipe("pipe_pipe03", 15.6f, 104.6f, 35f);
        FloorPipe("pipe_pipe05", 16.4f, 102.2f, 10f);
        FloorPipe("pipe_pipe07", 14.8f, 101.6f, 0f);
        FloorPipe("pipe_pipe01", 37.6f, 108.4f, 90f);
        FloorPipe("pipe_pipe04", 37.2f, 109.2f, 0f);
        FloorPipe("pipe_pipe06", 38.0f, 110.0f, 20f);
        FloorPipe("pipe_pipe08", 37.4f, 111.2f, 180f);
        FloorPipe("pipe_pipe02", -24.6f, 36.4f, 0f);
        FloorPipe("pipe_pipe03", 36.6f, 81.6f, 90f);
    }

    static void FloorPipe(string key, float x, float z, float yaw)
    {
        if (!prefabs.ContainsKey(key)) return;
        var obj = Create(key, 1f);
        obj.transform.rotation = Turn(key, yaw);
        obj.transform.position = new Vector3(x, 0f, z);
        Ground(obj, 0f);
        Accept(obj, key);
    }

    static void PlaceRats()
    {
        Rata("Rata j1", new[]
        {
            new Vector3(-9.85f, 0.02f, 35.1f),
            new Vector3(-9.85f, 0.02f, 38.2f),
            new Vector3(-10.45f, 0.02f, 41.1f),
            new Vector3(-9.7f, 0.02f, 39.4f)
        });
        Rata("Rata j2", new[]
        {
            new Vector3(-7.45f, 0.02f, 82.4f),
            new Vector3(-7.45f, 0.02f, 85.8f),
            new Vector3(-8.05f, 0.02f, 89.2f),
            new Vector3(-7.35f, 0.02f, 86.6f)
        });
    }

    static void Rata(string nombre, Vector3[] puntos)
    {
        var anchor = new GameObject(nombre);
        anchor.transform.SetParent(map, false);
        var centro = Vector3.zero;
        foreach (var punto in puntos) centro += punto;
        anchor.transform.position = centro / puntos.Length;
        var camino = new GameObject("Camino").transform;
        camino.SetParent(anchor.transform, false);
        foreach (var punto in puntos)
        {
            var marca = new GameObject("Punto").transform;
            marca.SetParent(camino, false);
            marca.position = punto;
        }
        var cuerpo = new GameObject("Cuerpo").transform;
        cuerpo.SetParent(anchor.transform, false);
        cuerpo.position = puntos[0];
        var size = IdentitySize("rat");
        float longitud = Mathf.Max(size.x, size.z);
        float escala = 0.2f / Mathf.Max(0.05f, longitud);
        var modelo = Create("rat", 1f);
        modelo.transform.SetParent(cuerpo, false);
        Vector3 largo = size.x >= size.z ? Vector3.right : Vector3.forward;
        modelo.transform.localRotation = Turn("rat", Vector3.SignedAngle(largo, Vector3.forward, Vector3.up));
        modelo.transform.localScale = Vector3.one * escala;
        modelo.transform.localPosition = Vector3.zero;
        var bounds = RendererBounds(modelo);
        modelo.transform.position += Vector3.up * (puntos[0].y - bounds.min.y);
        foreach (var collider in modelo.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
        anchor.AddComponent<RataSusto>();
        used.Add("rat");
        placed++;
    }

    static void PlaceMissing()
    {
        var leftovers = new List<(float x, float z, float yaw)>
        {
            (-34.6f, 20.8f, 90f), (-34.6f, 22.0f, 90f), (-34.6f, 23.2f, 90f),
            (38.6f, 71.6f, -90f), (38.6f, 72.8f, -90f),
            (13.4f, 85.0f, 0f), (-26.4f, 110.6f, 90f)
        };
        int slot = 0;
        foreach (var key in prefabs.Keys.OrderBy(k => k))
        {
            if (used.Contains(key)) continue;
            if (slot >= leftovers.Count) { Debug.LogWarning("ATREZZO_SIN_SITIO " + key); continue; }
            var place = leftovers[slot++];
            float scale = key == "shelf" ? shelfScale : 1f;
            Put(key, place.x, place.z, place.yaw, scale);
            if (!used.Contains(key)) Debug.LogWarning("ATREZZO_SIN_SITIO " + key);
        }
    }

    static GameObject Put(string key, float x, float z, float yaw, float scale = 1f, float ground = 0f)
    {
        if (!prefabs.ContainsKey(key)) return null;
        var obj = Create(key, scale);
        obj.transform.rotation = Turn(key, yaw);
        obj.transform.position = new Vector3(x, 0f, z);
        Ground(obj, ground);
        return Accept(obj, key) ? obj : null;
    }

    static GameObject Create(string key, float scale, Quaternion? rotation = null)
    {
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[key]);
        obj.name = "Atrezzo - " + key + " " + (++seq);
        if (root) obj.transform.SetParent(root, false);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = rotation ?? Rest(key);
        obj.transform.localScale = Vector3.one * scale;
        Paint(obj, key);
        return obj;
    }

    static bool Accept(GameObject obj, string key)
    {
        if (!obj) return false;
        bool cable = key.StartsWith("cable_");
        float techo = 30f;
        for (int intento = 0; intento < 7; intento++)
        {
            var bounds = RendererBounds(obj);
            techo = Ceiling(bounds.center.x, bounds.center.z);
            if (bounds.max.y > techo) break;
            var check = Shrink(bounds);
            bool blocked = HitsKeep(check) || HitsLamp(check) || (!cable && HitsSolid(check, obj));
            if (!blocked)
            {
                Preparar(obj, key);
                used.Add(key);
                placed++;
                if (placed % 60 == 0) Debug.Log("ATREZZO_PROGRESS " + placed);
                return true;
            }
            if (cable) break;
            Vector3 empuje = Empuje(check, obj);
            if (empuje.sqrMagnitude < 0.0001f) break;
            empuje.y = 0f;
            obj.transform.position += empuje;
        }
        UnityEngine.Object.DestroyImmediate(obj);
        return false;
    }

    static float Ceiling(float x, float z)
    {
        if (x > -26.3f && x < 4.3f && z > 17.7f && z < 46.3f) return 4.4f;
        if (x > -28.3f && x < 6.3f && z > 71.7f && z < 112.3f) return 4.4f;
        if (x > 9.7f && x < 18.3f && z > 53.7f && z < 62.3f) return 2.85f;
        if (x > 32.7f && x < 40.3f && z > 79.7f && z < 88.3f) return 2.85f;
        if (x > 25.7f && x < 40.3f && z > 99.7f && z < 116.3f) return 4.45f;
        return 30f;
    }

    static Bounds Shrink(Bounds bounds)
    {
        bounds.min += new Vector3(0.04f, 0.05f, 0.04f);
        bounds.max -= new Vector3(0.04f, 0.02f, 0.04f);
        if (bounds.size.y < 0.08f) bounds.max = new Vector3(bounds.max.x, bounds.min.y + 0.08f, bounds.max.z);
        if (bounds.size.x < 0.05f) bounds.max = new Vector3(bounds.min.x + 0.05f, bounds.max.y, bounds.max.z);
        if (bounds.size.z < 0.05f) bounds.max = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z + 0.05f);
        return bounds;
    }

    static bool HitsKeep(Bounds bounds)
    {
        foreach (var keep in keeps)
            if (bounds.Intersects(keep)) return true;
        return false;
    }

    static bool HitsLamp(Bounds bounds)
    {
        foreach (var light in lights)
        {
            if (!light || light.type != LightType.Point) continue;
            if (bounds.Contains(light.transform.position)) return true;
        }
        return false;
    }

    static bool HitsSolid(Bounds bounds, GameObject self)
    {
        Physics.SyncTransforms();
        var hits = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (!hit) continue;
            if (hit.transform == self.transform || hit.transform.IsChildOf(self.transform)) continue;
            if (hit.name.Contains("Piso")) continue;
            if (hit.bounds.max.y <= 0.02f) continue;
            return true;
        }
        return false;
    }

    static Vector3 Empuje(Bounds bounds, GameObject self)
    {
        Vector3 mejor = Vector3.zero;
        float mayor = 0f;
        void Considerar(Bounds otro)
        {
            float ox = Mathf.Min(bounds.max.x, otro.max.x) - Mathf.Max(bounds.min.x, otro.min.x);
            float oz = Mathf.Min(bounds.max.z, otro.max.z) - Mathf.Max(bounds.min.z, otro.min.z);
            if (ox <= 0f || oz <= 0f) return;
            Vector3 paso = ox < oz
                ? new Vector3(bounds.center.x < otro.center.x ? -(ox + 0.03f) : ox + 0.03f, 0f, 0f)
                : new Vector3(0f, 0f, bounds.center.z < otro.center.z ? -(oz + 0.03f) : oz + 0.03f);
            if (paso.sqrMagnitude > mayor)
            {
                mayor = paso.sqrMagnitude;
                mejor = paso;
            }
        }
        foreach (var keep in keeps) if (bounds.Intersects(keep)) Considerar(keep);
        var hits = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (!hit || hit.name.Contains("Piso") || hit.bounds.max.y <= 0.02f) continue;
            if (hit.transform == self.transform || hit.transform.IsChildOf(self.transform)) continue;
            Considerar(hit.bounds);
        }
        return mejor;
    }

    static void Preparar(GameObject obj, string key)
    {
        bool sombra = key == "shelf" || key == "crate" || key == "barrel" || key == "picnic" || key == "cart" || key == "generator" || key == "welder" || key == "ladder";
        bool solido = sombra || key == "tyre" || key == "toolbox" || key == "vice" || key == "chair" || key.StartsWith("pipe_");
        foreach (var t in obj.GetComponentsInChildren<Transform>())
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
        {
            renderer.shadowCastingMode = sombra ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        if (solido) AddCollider(obj);
    }

    static void AddCollider(GameObject obj)
    {
        bool primero = true;
        Bounds local = new Bounds(Vector3.zero, Vector3.zero);
        foreach (var filter in obj.GetComponentsInChildren<MeshFilter>())
        {
            var mesh = filter.sharedMesh;
            if (!mesh) continue;
            var caja = mesh.bounds;
            var centro = caja.center;
            var mitad = caja.extents;
            for (int i = 0; i < 8; i++)
            {
                var esquina = centro + new Vector3(
                    (i & 1) == 0 ? -mitad.x : mitad.x,
                    (i & 2) == 0 ? -mitad.y : mitad.y,
                    (i & 4) == 0 ? -mitad.z : mitad.z);
                Vector3 enRaiz = obj.transform.InverseTransformPoint(filter.transform.TransformPoint(esquina));
                if (primero) { local = new Bounds(enRaiz, Vector3.zero); primero = false; }
                else local.Encapsulate(enRaiz);
            }
        }
        if (primero) return;
        var box = obj.AddComponent<BoxCollider>();
        box.center = local.center;
        box.size = local.size * 0.96f;
    }

    static void Ground(GameObject obj, float y)
    {
        var bounds = RendererBounds(obj);
        obj.transform.position += Vector3.up * (y - bounds.min.y);
    }

    static void FlushWall(GameObject obj, Vector3 wallPoint, Vector3 inward)
    {
        var bounds = RendererBounds(obj);
        float minIn = float.PositiveInfinity;
        Vector3 ext = bounds.extents;
        Vector3 c = bounds.center;
        for (int i = 0; i < 8; i++)
        {
            var p = c + new Vector3((i & 1) == 0 ? -ext.x : ext.x, (i & 2) == 0 ? -ext.y : ext.y, (i & 4) == 0 ? -ext.z : ext.z);
            minIn = Mathf.Min(minIn, Vector3.Dot(p, inward));
        }
        float wallIn = Vector3.Dot(wallPoint, inward);
        obj.transform.position += inward * (wallIn + 0.03f - minIn);
    }

    static Quaternion Turn(string key, float yaw) => Quaternion.Euler(0f, yaw, 0f) * Rest(key);

    static Vector3 IdentitySize(string key)
    {
        if (sizes.TryGetValue(key, out var size)) return size;
        var probe = Create(key, 1f);
        probe.transform.rotation = Rest(key);
        probe.transform.position = Vector3.zero;
        size = RendererBounds(probe).size;
        UnityEngine.Object.DestroyImmediate(probe);
        sizes[key] = size;
        return size;
    }

    static Bounds RendererBounds(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.one * 0.1f);
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void Paint(GameObject obj, string key)
    {
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true))
        {
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                string hint = ((slots[i] ? slots[i].name : "") + " " + renderer.gameObject.name).ToLowerInvariant();
                slots[i] = Pick(hint, key);
            }
            renderer.sharedMaterials = slots;
        }
    }

    static Material Pick(string hint, string key)
    {
        if (hint.Contains("glass")) return Glass();
        if (hint.Contains("speaker")) return Make("boombox_speakers", "boombox", "speakers_diff", "speakers_nor", "speakers_arm");
        if (hint.Contains("bristle")) return Make("broom_bristles", "broom", "bristles_diff", null, null);
        if (hint.Contains("bottom")) return Make("picnic_bottom", "picnic", "bottom_diff", "bottom_nor", "bottom_arm");
        if (hint.Contains("top") || hint.Contains("table")) return Make("picnic_top", "picnic", "top_diff", "top_nor", "top_arm");
        if (hint.Contains("group02")) return Make("pipe_b", "pipe", "group02_diff", "group02_nor", "group02_arm");
        if (hint.Contains("group01") || key.StartsWith("pipe_")) return Make("pipe_a", "pipe", "group01_diff", "group01_nor", "group01_arm");
        if (hint.Contains("boxes")) return Make("cable_box", "cable", "boxes_diff", "boxes_nor", "boxes_arm");
        if (key.StartsWith("cable_") || hint.Contains("electric_cable")) return Make("cable_main", "cable", "cables_diff", "cables_nor", "cables_arm");
        string folder = FolderOf(key);
        return Make(folder, folder, "_diff", "_nor", "_arm");
    }

    static string FolderOf(string key)
    {
        if (key.StartsWith("cable_")) return "cable";
        if (key.StartsWith("pipe_")) return "pipe";
        return key;
    }

    static Material Glass()
    {
        if (glass) return glass;
        glass = AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/Vidrio.mat");
        if (glass) return glass;
        glass = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        glass.name = "Vidrio";
        glass.SetFloat("_Surface", 1f);
        glass.SetFloat("_Blend", 0f);
        glass.SetOverrideTag("RenderType", "Transparent");
        glass.SetFloat("_ZWrite", 0f);
        glass.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        glass.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        glass.renderQueue = 3000;
        glass.SetColor("_BaseColor", new Color(0.55f, 0.68f, 0.72f, 0.28f));
        glass.enableInstancing = true;
        AssetDatabase.CreateAsset(glass, MatFolder + "/Vidrio.mat");
        return glass;
    }

    static Material Make(string id, string folder, string diffToken, string normalToken, string armToken)
    {
        if (materials.TryGetValue(id, out var cached)) return cached;
        string path = MatFolder + "/" + id + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = Shader.Find("Universal Render Pipeline/Lit");
        var diffuse = Texture(folder, diffToken, null);
        material.SetTexture("_BaseMap", diffuse);
        material.SetColor("_BaseColor", Color.white);
        var normal = normalToken == null ? null : Texture(folder, normalToken, diffToken);
        if (normal)
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_BumpScale", 1f);
        }
        material.SetFloat("_Smoothness", 0.18f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        materials[id] = material;
        return material;
    }

    static Texture2D Texture(string folder, string token, string avoid)
    {
        string dir = TexFolder + "/" + folder;
        if (!Directory.Exists(dir)) return null;
        string tokenLow = token.ToLowerInvariant();
        foreach (var file in Directory.GetFiles(dir))
        {
            string name = Path.GetFileName(file).ToLowerInvariant();
            if (!name.EndsWith(".jpg") && !name.EndsWith(".png")) continue;
            if (!name.Contains(tokenLow)) continue;
            bool generico = tokenLow == "_diff" || tokenLow == "_nor" || tokenLow == "_arm";
            if (generico && (name.Contains("speaker") || name.Contains("bristle") || name.Contains("bottom") || name.Contains("top") || name.Contains("boxes") || name.Contains("group"))) continue;
            if (avoid != null && name.Contains(avoid.ToLowerInvariant()) && tokenLow != avoid.ToLowerInvariant()) continue;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ToAsset(file));
        }
        return null;
    }

    static string ToAsset(string path)
    {
        path = path.Replace('\\', '/');
        int index = path.IndexOf("Assets/", StringComparison.Ordinal);
        return index >= 0 ? path.Substring(index) : path;
    }
}
#endif
