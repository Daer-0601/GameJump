#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Gamejam2026;

public static class DemoBuilder
{
    const string ScenePath = "Assets/Game/00_Scenes/Demo.unity";
    static Material stone;
    static Material iron;
    static Material wax;
    static Material wood;
    static Transform environment;

    [MenuItem("Gamejam/Crear escena de la demo")]
    public static void CreateScene()
    {
        Directory.CreateDirectory("Assets/Game/00_Scenes");
        Directory.CreateDirectory("Assets/Game/Materials");
        AssetDatabase.Refresh();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        stone = Material("Piedra", new Color(0.4f, 0.42f, 0.4f), FindTexture("Concrete Floor"));
        iron = Material("Hierro", new Color(0.28f, 0.24f, 0.19f));
        wax = Material("Cera", new Color(0.87f, 0.77f, 0.53f));
        wood = Material("Madera", new Color(0.28f, 0.21f, 0.15f), FindTexture("Dark-Wood"));
        environment = new GameObject("Escenario - editar libremente").transform;
        var ground = Material("Suelo", new Color(0.5f, 0.52f, 0.5f), FindTexture("Concrete Floor"));
        ground.mainTextureScale = new Vector2(8, 8);
        Shape("Suelo", PrimitiveType.Cube, new Vector3(0, -0.25f, 2), new Vector3(32, 0.5f, 36), ground, environment);
        Shape("Muro norte", PrimitiveType.Cube, new Vector3(0, 2, 20), new Vector3(32, 4, 0.6f), stone, environment);
        Shape("Muro sur", PrimitiveType.Cube, new Vector3(0, 2, -16), new Vector3(32, 4, 0.6f), stone, environment);
        Shape("Muro oeste", PrimitiveType.Cube, new Vector3(-16, 2, 2), new Vector3(0.6f, 4, 36), stone, environment);
        Shape("Muro este", PrimitiveType.Cube, new Vector3(16, 2, 2), new Vector3(0.6f, 4, 36), stone, environment);

        // los modelos quedan guardados en la escena
        Model("Shacks", "Large Shed A", new Vector3(-10, 0, 14), 9f, 0, true);
        Model("Shacks", "Large Shed B", new Vector3(10, 0, 14), 8f, 180, true);
        Model("Shacks", "Shack A", new Vector3(-12, 0, -8), 5f, 90, true);
        Model("Plumbing", "Generator", new Vector3(11, 0, -9), 3.5f, 180, true);
        Model("Shacks", "Pallet", new Vector3(-7, 0, -1), 2.5f, 15, true);
        Model("Shacks", "Barrel", new Vector3(6, 0, 3), 1.2f, 0, true);
        Model("Shacks", "Barrel", new Vector3(7.5f, 0, 3), 1.2f, 30, true);
        Model("Shacks", "Concrete Barricade", new Vector3(-4, 0, 6), 3.2f, 0, true);
        Model("Shacks", "Concrete Barricade", new Vector3(4, 0, -2), 3.2f, 90, true);

        var game = new GameObject("Partida - ajustar dificultad aqui").AddComponent<DemoGame>();
        game.players = new[] { Player(0, new Vector3(-1.5f, 0.1f, -11), game), Player(1, new Vector3(1.5f, 0.1f, -11), game) };
        game.candles = new[] { Candle(new Vector3(-1.5f, 0.1f, -10)), Candle(new Vector3(1.5f, 0.1f, -10)) };
        CarryObject crank = Crank(new Vector3(0, 0.85f, -7));
        Shape("Mesa manivela", PrimitiveType.Cube, new Vector3(0, 0.4f, -7), new Vector3(1.5f, 0.8f, 0.8f), wood, environment);
        Label("MANIVELA", new Vector3(0, 1.5f, -7.45f), 0, 0.05f);
        game.mechanisms = new[]
        {
            Mechanism(0, "A", new Vector3(-10, 0, 4), game),
            Mechanism(1, "B", new Vector3(10, 0, 5), game),
            Mechanism(2, "C", new Vector3(0, 0, 15), game)
        };

        var plaque = Shape("Inscripcion - interactuar para leer", PrimitiveType.Cube, new Vector3(0, 1.3f, -1), new Vector3(2.2f, 1.5f, 0.25f), stone, environment);
        plaque.AddComponent<DemoClue>();
        Label("LEE LA INSCRIPCION\n[E / U]", new Vector3(0, 1.55f, -1.15f), 0, 0.045f);
        Label("A", new Vector3(-10, 2.7f, 4), 0, 0.16f);
        Label("B", new Vector3(10, 2.7f, 5), 0, 0.16f);
        Label("C", new Vector3(0, 2.7f, 15), 0, 0.16f);

        var moon = new GameObject("Luz nocturna").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.46f, 0.58f, 0.7f);
        moon.intensity = 0.3f;
        moon.shadows = LightShadows.Soft;
        moon.transform.rotation = Quaternion.Euler(45, -35, 0);
        game.atmosphereLights = new[]
        {
            PointLight("Farol entrada", new Vector3(0, 3.5f, -8), new Color(1f, 0.63f, 0.29f), 1.5f, 7f),
            PointLight("Farol fondo", new Vector3(0, 4, 13), new Color(0.48f, 0.61f, 0.68f), 1.1f, 8f)
        };
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.11f, 0.14f, 0.17f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.025f, 0.035f, 0.045f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.025f;

        var audio = new GameObject("Ambiente - audio original CC0").AddComponent<AudioSource>();
        audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/External/Audio/voice in the woods.wav");
        audio.loop = true;
        audio.playOnAwake = false;
        audio.volume = 0.16f;
        game.ambience = audio;
        PlayerSettings.productName = "gamejam2026";
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.runInBackground = false;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        EditorSettings.serializationMode = SerializationMode.ForceText;
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("DEMO_SCENE_READY: " + ScenePath);
    }

    static DemoPlayer Player(int index, Vector3 position, DemoGame game)
    {
        var root = new GameObject("Jugador " + (index + 1));
        root.transform.position = position;
        var controller = root.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.3f;
        controller.center = Vector3.up * 0.9f;
        var player = root.AddComponent<DemoPlayer>();
        player.playerIndex = index;
        player.game = game;
        var cameraObject = new GameObject("Camara");
        cameraObject.transform.SetParent(root.transform, false);
        cameraObject.transform.localPosition = Vector3.up * 1.6f;
        var camera = cameraObject.AddComponent<Camera>();
        camera.rect = new Rect(index * 0.5f, 0, 0.5f, 1);
        camera.fieldOfView = 70f;
        camera.nearClipPlane = 0.08f;
        camera.farClipPlane = 90;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.035f, 0.045f);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        if (index == 0) cameraObject.AddComponent<AudioListener>();
        player.view = camera;
        var hand = new GameObject("Objeto en mano").transform;
        hand.SetParent(cameraObject.transform, false);
        hand.localPosition = new Vector3(0.26f, -0.4f, 0.9f);
        player.hand = hand;
        var body = Shape("Cuerpo provisional", PrimitiveType.Capsule, position + Vector3.up * 0.8f, new Vector3(0.5f, 0.8f, 0.5f), Material("Jugador" + index, index == 0 ? new Color(0.27f, 0.4f, 0.45f) : new Color(0.45f, 0.31f, 0.22f)), root.transform);
        UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
        body.layer = 8 + index;
        camera.cullingMask &= ~(1 << (8 + index));
        return player;
    }

    static CarryObject Candle(Vector3 position)
    {
        var root = new GameObject("Vela");
        root.transform.position = position;
        var candle = root.AddComponent<CarryObject>();
        candle.isCandle = true;
        candle.displayName = "vela";
        Shape("Cera provisional", PrimitiveType.Cylinder, position + Vector3.up * 0.15f, new Vector3(0.11f, 0.15f, 0.11f), wax, root.transform);
        var flame = Shape("Llama provisional", PrimitiveType.Sphere, position + Vector3.up * 0.34f, new Vector3(0.05f, 0.1f, 0.05f), Material("Llama", new Color(1f, 0.68f, 0.25f), null, true), root.transform);
        UnityEngine.Object.DestroyImmediate(flame.GetComponent<Collider>());
        candle.candleLight = PointLight("Luz de vela", position + Vector3.up * 0.5f, new Color(1f, 0.73f, 0.42f), 5f, 8f);
        candle.candleLight.transform.SetParent(root.transform, true);
        return candle;
    }

    static CarryObject Crank(Vector3 position)
    {
        var root = new GameObject("Manivela compartida");
        root.transform.position = position;
        var crank = root.AddComponent<CarryObject>();
        crank.displayName = "manivela";
        Shape("Brazo provisional", PrimitiveType.Cube, position + Vector3.up * 0.1f, new Vector3(0.38f, 0.055f, 0.07f), iron, root.transform);
        Shape("Mango provisional", PrimitiveType.Cylinder, position + new Vector3(0.16f, 0.18f, 0), new Vector3(0.065f, 0.11f, 0.065f), wood, root.transform);
        return crank;
    }

    static DemoMechanism Mechanism(int id, string symbol, Vector3 position, DemoGame game)
    {
        var root = new GameObject("Mecanismo " + symbol);
        root.transform.position = position;
        var mechanism = root.AddComponent<DemoMechanism>();
        mechanism.id = id;
        mechanism.symbol = symbol;
        mechanism.game = game;
        var model = Model("Plumbing", "Pipe Valve", position, 1.8f, 0, false);
        if (model) model.transform.SetParent(root.transform, true);
        var baseObject = Shape("Base interactuable", PrimitiveType.Cube, position + Vector3.up * 0.7f, new Vector3(1.2f, 1.4f, 0.65f), stone, root.transform);
        var wheel = Shape("Indicador de giro", PrimitiveType.Cylinder, position + new Vector3(0, 1.2f, -0.48f), new Vector3(0.48f, 0.06f, 0.48f), iron, root.transform);
        wheel.transform.rotation = Quaternion.Euler(90, 0, 0);
        mechanism.wheel = wheel.transform;
        mechanism.indicator = PointLight("Estado " + symbol, position + new Vector3(0, 1.7f, -0.7f), new Color(0.9f, 0.3f, 0.1f), 1.4f, 2.6f);
        mechanism.indicator.transform.SetParent(root.transform, true);
        var socket = new GameObject("Soporte manivela");
        socket.transform.SetParent(root.transform, false);
        socket.transform.localPosition = new Vector3(0f, 1.2f, -0.52f);
        socket.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        mechanism.crankSocket = socket.transform;

        return mechanism;
    }

    static GameObject Model(string pack, string name, Vector3 position, float size, float angle, bool collision)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/External/" + pack + "/" + name + ".fbx");
        if (!asset) { Debug.LogWarning("Falta modelo: " + name); return null; }
        var root = new GameObject(name);
        root.transform.SetParent(environment);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        model.transform.SetParent(root.transform, false);
        var renderers = model.GetComponentsInChildren<Renderer>();
        var bounds = new Bounds(model.transform.position, Vector3.zero);
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        float maximum = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        float factor = maximum > 0.001f ? size / maximum : 1f;
        model.transform.localScale *= factor;
        model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * factor;
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0, angle, 0);
        foreach (var renderer in renderers)
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Texture texture = materials[i] ? materials[i].mainTexture : null;
                if (!texture) texture = TextureForModel(name);
                materials[i] = Material(name + "_" + i, Color.white, texture);
            }
            renderer.sharedMaterials = materials;
        }
        if (collision)
        {
            foreach (var mesh in model.GetComponentsInChildren<MeshFilter>())
                if (mesh.sharedMesh) mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
        }
        return root;
    }

    static Texture TextureForModel(string name)
    {
        string key = name switch
        {
            "Pipe Valve" => "Pipe-Texture", "Circuit Breaker" => "Fuse-Box-Texture",
            "Generator" => "Generator-Texture", "Barrel" => "Barrel-Texture",
            "Pallet" => "Dark-Wood", "Concrete Barricade" => "Concrete",
            "Large Shed A" => "Rusty-Metal", "Large Shed B" => "Rusty-Metal",
            "Shack A" => "Rusty-Metal", _ => name + "-Texture"
        };
        return FindTexture(key);
    }

    static Texture2D FindTexture(string key)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/External" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path).IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        return null;
    }

    static Material Material(string name, Color color, Texture texture = null, bool emission = false)
    {
        string path = "Assets/Game/Materials/" + name.Replace(" ", "_") + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        material.mainTexture = texture;
        material.SetFloat("_Smoothness", 0.12f);
        material.SetFloat("_Cull", 0);
        if (emission)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2f);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    static GameObject Shape(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        var obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.position = position;
        obj.transform.localScale = scale;
        obj.transform.SetParent(parent, true);
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }

    static Light PointLight(string name, Vector3 position, Color color, float intensity, float range)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.transform.position = position;
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    static void Label(string text, Vector3 position, float angle, float size)
    {
        var label = new GameObject("Cartel " + text.Replace("\n", " ")).AddComponent<TextMesh>();
        label.transform.position = position;
        label.transform.rotation = Quaternion.Euler(0, angle, 0);
        label.transform.SetParent(environment);
        label.text = text;
        label.characterSize = size;
        label.fontSize = 48;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = new Color(0.9f, 0.83f, 0.65f);
    }

    [MenuItem("Gamejam/Compilar demo Windows")]
    public static void BuildWindows()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/Windows/gamejam2026.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Fallo la compilacion de Windows");
        Debug.Log("DEMO_BUILD_OK");
    }

    public static void CapturePreview()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var game = UnityEngine.Object.FindAnyObjectByType<DemoGame>();
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        for (int i = 0; i < 2; i++)
        {
            game.candles[i].transform.SetParent(game.players[i].hand, false);
            game.candles[i].transform.localPosition = Vector3.zero;
            var camera = game.players[i].view;
            var target = new RenderTexture(800, 900, 24);
            camera.rect = new Rect(0, 0, 1, 1);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 800, 900), i * 800, 0);
            camera.targetTexture = null;
            RenderTexture.active = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
        image.Apply();
        File.WriteAllBytes("../demo-preview.png", image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        Debug.Log("DEMO_PREVIEW_OK");
    }
}

#endif
