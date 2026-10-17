#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Gamejam2026;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class FinalMapBuilder
{
    const string Folder = "Assets/Game/05_Mapa/Boceto";
    static Transform root;
    static Material wall, floor, obstacle;
    static Vector3 P(float x, float z) => new Vector3(x, 0.1f, z);
    public static readonly Vector3[] SharedRoute = { P(0, -6), P(0, 13), P(-12, 13), P(-12, 30), P(-7, 30), P(-7, 43), P(-5, 43), P(-5, 51), P(14, 51), P(14, 67), P(22, 67), P(22, 76) };
    public static readonly Vector3[] WorkshopRoute = { P(22, 76), P(-4, 76), P(-4, 96), P(-21, 96), P(-21, 103) };
    public static readonly Vector3[] PumpRoute = { P(22, 76), P(36, 76), P(36, 87), P(24, 87), P(24, 96), P(34, 96), P(34, 109) };

    [MenuItem("Gamejam/Mapa/Montar boceto con bloques")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("salir de play antes de montar el mapa");
        var scene = SceneManager.GetActiveScene();
        var game = UnityEngine.Object.FindFirstObjectByType<DemoGame>();
        if (!game || scene.path != "Assets/Game/00_Scenes/Demo.unity") throw new Exception("abrir la escena demo");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        // guarda una copia antes de cambiar el escenario
        var backup = AssetDatabase.GenerateUniqueAssetPath(Folder + "/DemoAntesDelGraybox.unity");
        EditorSceneManager.SaveScene(scene, backup, true);
        var oldRoot = GameObject.Find("Recinto - definitivo");
        if (oldRoot) UnityEngine.Object.DestroyImmediate(oldRoot);
        var clue = UnityEngine.Object.FindFirstObjectByType<DemoClue>();
        if (clue) clue.transform.SetParent(null, true);
        foreach (var mechanism in game.mechanisms) mechanism.transform.SetParent(null, true);
        foreach (var item in UnityEngine.Object.FindObjectsByType<CarryObject>(FindObjectsSortMode.None)) item.transform.SetParent(null, true);
        foreach (var name in new[] { "Escenario - editar libremente", "Arte - recinto de mantenimiento", "Recorridos A B C" })
        {
            var previous = GameObject.Find(name);
            if (previous) previous.SetActive(false);
        }
        root = new GameObject("Recinto - definitivo").transform;
        wall = Mat("Muros", new Color(0.36f, 0.38f, 0.4f));
        floor = Mat("Piso", new Color(0.27f, 0.29f, 0.3f));
        obstacle = Mat("Equipos", new Color(0.2f, 0.23f, 0.25f));
        Box("Piso continuo", 2, -0.5f, 54, 80, 1, 130, floor);
        WallX("Perimetro frente", -37, -5, -11);
        WallX("Perimetro frente", 5, 41, -11);
        WallX("Porton cerrado", -5, 5, -11);
        WallX("Perimetro fondo", -37, 41, 119);
        WallZ("Perimetro oeste", -37, -11, 119);
        WallZ("Perimetro este", 41, -11, 119);

        Group("01 Entrada");
        Solid("Garita", -23, -3, 12, 10, 4);
        WallX("Limite entrada", -37, -5, 10);
        WallX("Limite entrada", 5, 41, 10);
        Sign("ENTRADA", 0, 3, 8);
        Lamp("Entrada", 0, 4, 1, 2.5f, 9);

        Group("02 Carga y deposito");
        // las fachadas obligan a atravesar el deposito
        WallX("Fachada sur", -37, -15, 18);
        WallX("Fachada sur", -9, 41, 18);
        WallZ("Deposito oeste", -26, 18, 46);
        WallZ("Deposito este", 4, 18, 46);
        WallX("Fachada norte", -37, -8, 46);
        WallX("Fachada norte", -2, 41, 46);
        Roof("Techo deposito", -26, 4, 18, 46, 6);
        WallX("Tabique deposito", -26, -9, 32);
        WallX("Tabique deposito", -1, 4, 32);
        Solid("Estanteria oeste", -22, 25, 4, 9, 3);
        Solid("Estanteria centro", -3, 24, 4, 7, 3);
        Solid("Estanteria norte", -19, 40, 8, 4, 3);
        Solid("Almacen cerrado oeste", -28, 52, 14, 8, 5);
        Solid("Almacen cerrado este", 29, 36, 18, 36, 7);
        WallX("Limite trasero carga", -37, 10, 58);
        WallX("Limite trasero carga", 18, 41, 58);
        WallZ("Conexion oeste", 10, 58, 62);
        WallZ("Conexion este", 18, 58, 62);
        Roof("Paso cubierto", 10, 18, 54, 62, 3.4f);
        Sign("CARGA / DEPOSITO", -12, 3.4f, 17.5f);
        Lamp("Deposito acceso", -12, 3, 21, 2, 7);
        Lamp("Deposito fondo", -5, 3, 42, 1.4f, 6);

        Group("03 Patio interior");
        WallX("Patio sur", 6, 10, 62);
        WallX("Patio sur", 18, 30, 62);
        WallZ("Patio oeste sur", 6, 62, 73);
        WallZ("Patio oeste norte", 6, 79, 80);
        WallZ("Patio este sur", 30, 62, 73);
        WallZ("Patio este norte", 30, 79, 84);
        WallX("Patio norte", 6, 30, 80);
        Solid("Pilas junto al muro", 9, 69, 3, 4, 2);
        Sign("PATIO INTERIOR", 21, 3, 79.4f);
        Lamp("Patio interior", 24, 3.5f, 68, 1.5f, 7);

        Group("04 Taller");
        WallX("Taller sur", -28, 6, 72);
        WallX("Taller norte", -28, 6, 112);
        WallZ("Taller oeste", -28, 72, 112);
        WallZ("Taller acceso sur", 6, 72, 73);
        WallZ("Taller acceso norte", 6, 79, 112);
        WallX("Taller tabique oeste", -28, -7, 90);
        WallX("Taller tabique este", -1, 6, 90);
        WallZ("Cuarto fondo sur", -10, 90, 93);
        WallZ("Cuarto fondo norte", -10, 100, 112);
        Roof("Techo taller", -28, 6, 72, 112, 6);
        Solid("Banco de trabajo", -20, 78, 10, 3, 1.2f);
        Solid("Maquinaria taller", -21, 85, 6, 3, 2.8f);
        Solid("Equipo fondo", -2, 104, 4, 8, 2.7f);
        Sign("TALLER", 3, 3, 72.4f);
        Lamp("Taller entrada", -2, 4, 78, 2, 8);
        Lamp("Taller fondo", -22, 4, 108, 1, 6);

        Group("05 Bombas y tuberias");
        WallX("Pasaje sur", 30, 40, 70);
        WallZ("Pasaje este", 40, 70, 90);
        WallX("Quiebre del pasaje", 30, 33, 80);
        WallZ("Quiebre lateral", 33, 80, 84);
        WallX("Paso de vuelta", 12, 33, 84);
        WallX("Patio bombas sur oeste", 12, 21, 90);
        WallX("Patio bombas sur este", 27, 40, 90);
        WallZ("Patio bombas oeste", 12, 84, 116);
        WallZ("Patio bombas este", 40, 90, 116);
        WallX("Patio bombas norte", 12, 40, 116);
        Roof("Pasaje oscuro", 33, 40, 80, 88, 3.4f);
        WallX("Caseta bombas acceso oeste", 26, 31, 100);
        WallX("Caseta bombas acceso este", 37, 40, 100);
        WallZ("Caseta bombas oeste", 26, 100, 116);
        Roof("Techo caseta bombas", 26, 40, 100, 116, 5);
        var tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tank.name = "Tanque provisional";
        tank.transform.SetParent(root);
        tank.transform.position = new Vector3(18, 4, 105);
        tank.transform.localScale = new Vector3(6, 4, 6);
        tank.GetComponent<Renderer>().sharedMaterial = obstacle;
        Solid("Equipo tuberias", 16, 95, 5, 3, 2);
        Sign("BOMBAS", 34, 3, 99.5f);
        Lamp("Bombas patio", 18, 5, 110, 1.4f, 8);
        Lamp("Bombas caseta", 34, 3.5f, 112, 1, 6);

        // marcas para conectar la jugabilidad del otro proyecto
        root = GameObject.Find("Recinto - definitivo").transform;
        Marker("Inicio jugador 1", P(-1.5f, -6));
        Marker("Inicio jugador 2", P(1.5f, -6));
        Marker("Objetivo carga A", P(-5, 40));
        Marker("Objetivo taller B", P(-21, 106));
        Marker("Objetivo bombas C", P(34, 112));
        Marker("Pista", new Vector3(-3, 1.3f, 5));
        Marker("Manivela", new Vector3(3, 0.85f, 3));
        var markers = root.Find("Marcas de conexion");
        for (int i = 0; i < 2; i++)
        {
            game.players[i].transform.SetPositionAndRotation(markers.Find("Inicio jugador " + (i + 1)).position, Quaternion.identity);
            game.candles[i].transform.position = P(i == 0 ? -1.5f : 1.5f, -4);
        }
        var targets = new[] { "Objetivo carga A", "Objetivo taller B", "Objetivo bombas C" };
        for (int i = 0; i < 3; i++) game.mechanisms[i].transform.position = markers.Find(targets[i]).position;
        clue.transform.position = markers.Find("Pista").position;
        var crank = UnityEngine.Object.FindObjectsByType<CarryObject>(FindObjectsSortMode.None).First(c => !c.isCandle);
        crank.transform.position = markers.Find("Manivela").position;
        Box("Mesa de la manivela", 3, 0.4f, 3, 1.5f, 0.8f, 0.8f, obstacle);
        Sign("PISTA", -3, 2.5f, 4.7f);
        foreach (var old in game.atmosphereLights) if (old) old.gameObject.SetActive(false);
        game.atmosphereLights = root.GetComponentsInChildren<Light>();
        var moon = GameObject.Find("Luz nocturna").GetComponent<Light>();
        moon.gameObject.SetActive(true);
        moon.enabled = true;
        moon.intensity = 0.18f;
        moon.color = new Color(0.42f, 0.55f, 0.8f);
        moon.shadows = LightShadows.Soft;
        moon.shadowBias = 0.02f;
        moon.shadowNormalBias = 0.15f;
        moon.transform.rotation = Quaternion.Euler(48f, -32f, 0);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.045f, 0.055f, 0.075f);
        RenderSettings.fogDensity = 0.018f;
        foreach (var player in game.players) player.view.farClipPlane = 170;
        // la geometria no depende de los scripts de la demo
        PrefabUtility.SaveAsPrefabAsset(root.gameObject, Folder + "/RecintoGraybox.prefab");
        
        EditorUtility.SetDirty(game);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root.gameObject;
        Physics.SyncTransforms();
        Validate();
        Debug.Log("GRAYBOX_READY backup=" + backup);
    }

    [MenuItem("Gamejam/Mapa/Comprobar recorridos")]
    public static void Validate()
    {
        CheckPath("Entrada a patio", SharedRoute);
        CheckPath("Patio a taller", WorkshopRoute);
        CheckPath("Patio a bombas", PumpRoute);
        if (Physics.GetIgnoreLayerCollision(0, 0)) throw new Exception("colisiones del piso desactivadas");
    }

    static void CheckPath(string name, Vector3[] points)
    {
        float distance = 0;
        for (int i = 1; i < points.Length; i++)
        {
            var delta = points[i] - points[i - 1];
            if (Physics.SphereCast(points[i - 1] + Vector3.up, 0.4f, delta.normalized, out var hit, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                throw new Exception(name + " bloqueado por " + hit.collider.name);
            distance += delta.magnitude;
        }
        Debug.Log("GRAYBOX_ROUTE_OK " + name + " metros=" + distance + " segundos_teoricos=" + distance / 3.2f);
    }

    static void Group(string name)
    {
        root = new GameObject(name).transform;
        root.SetParent(GameObject.Find("Recinto - definitivo").transform);
    }

    static void Marker(string name, Vector3 position)
    {
        var parent = root.Find("Marcas de conexion");
        if (!parent) { parent = new GameObject("Marcas de conexion").transform; parent.SetParent(root); }
        var marker = new GameObject(name).transform;
        marker.SetParent(parent);
        marker.position = position;
    }

    static Material Mat(string name, Color color)
    {
        var path = Folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.color = color;
        material.SetFloat("_Smoothness", 0);
        return material;
    }

    static void Box(string name, float x, float y, float z, float width, float height, float depth, Material material)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(root);
        obj.transform.position = new Vector3(x, y, z);
        obj.transform.localScale = new Vector3(width, height, depth);
        obj.GetComponent<Renderer>().sharedMaterial = material;
    }

    static void WallX(string name, float from, float to, float z) => Box(name, (from + to) / 2, 2.5f, z, to - from, 5, 0.5f, wall);
    static void WallZ(string name, float x, float from, float to) => Box(name, x, 2.5f, (from + to) / 2, 0.5f, 5, to - from, wall);
    static void Roof(string name, float x1, float x2, float z1, float z2, float height) => Box(name, (x1 + x2) / 2, height, (z1 + z2) / 2, x2 - x1, 0.35f, z2 - z1, wall);
    static void Solid(string name, float x, float z, float width, float depth, float height) => Box(name, x, height / 2, z, width, height, depth, obstacle);

    static void Lamp(string name, float x, float y, float z, float intensity, float range)
    {
        var light = new GameObject("Luz " + name).AddComponent<Light>();
        light.transform.SetParent(root);
        light.transform.position = new Vector3(x, y, z);
        light.type = LightType.Point;
        light.color = new Color(1, 0.73f, 0.45f);
        light.intensity = intensity;
        light.range = range;
    }

    static void Sign(string text, float x, float y, float z)
    {
        var sign = new GameObject("Cartel " + text).AddComponent<TextMesh>();
        sign.transform.SetParent(root);
        sign.transform.position = new Vector3(x, y, z);
        sign.text = text;
        sign.fontSize = 48;
        sign.characterSize = 0.09f;
        sign.anchor = TextAnchor.MiddleCenter;
    }
}
#endif
