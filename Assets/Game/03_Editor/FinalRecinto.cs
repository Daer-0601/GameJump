#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Gamejam2026;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class FinalRecinto
{
    const string Folder = "Assets/Game/05_Mapa";
    static Transform decor;
    static DemoGame game;

    [MenuItem("Gamejam/Mapa definitivo/Montar recinto completo")]
    public static void Build()
    {
        game = JamPolish.OpenGame();
        if (GameObject.Find("Recinto - definitivo")) throw new Exception("el recinto ya esta montado; editar la escena guardada");
        var clue = Object.FindAnyObjectByType<DemoClue>();
        var label = clue.inscriptionLabel;
        Vector3 offset = label ? label.transform.position - clue.transform.position : Vector3.zero;
        // conserva el mapa actual completo antes de tocar la escena
        Directory.CreateDirectory(Folder);
        File.WriteAllText("../reglas-antes-mapa.json", EditorJsonUtility.ToJson(game, true));
        FinalMapBuilder.Build();
        if (label)
        {
            label.transform.SetParent(clue.transform, true);
            label.transform.position = clue.transform.position + offset;
        }
        FinalTexturePass.Apply();
        Finish();
    }

    [MenuItem("Gamejam/Mapa definitivo/Actualizar acabado ambiental")]
    public static void Finish()
    {
        game = JamPolish.OpenGame();
        var map = GameObject.Find("Recinto - definitivo");
        ConfigureSky();
        DressSigns(map.transform);
        var previous = map.transform.Find("Detalles reutilizados del mapa nuevo");
        if (previous) Object.DestroyImmediate(previous.gameObject);
        var pumpMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Acabados/Materials/Piso bombas.mat");
        pumpMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/External/SBSHorror/256x256/Floor/Horror_Floor_09-256x256.png"));
        decor = new GameObject("Detalles reutilizados del mapa nuevo").transform;
        decor.SetParent(map.transform);
        Model("Barrel", new Vector3(-22, 0, 21), 1.1f, 0);
        Model("Pallet", new Vector3(-23, 0, 35), 2.3f, 0);
        Model("Generator", new Vector3(-23, 0, 45), 2.4f, 90);
        Model("Pipes", new Vector3(2.9f, 0, 35), 5.5f, 0);
        Model("Barrel", new Vector3(-16, 0, 41), 1.2f, 25);
        Model("Concrete Barricade", new Vector3(3, 0, 14), 3, 0);
        Model("Pallet", new Vector3(26, 0, 65), 2, 15);
        Model("Barrel", new Vector3(27, 0, 78), 1.2f, 0);
        Model("Generator", new Vector3(-25, 0, 81), 2.4f, 90);
        Model("Pipes", new Vector3(-26.8f, 0, 101), 5.5f, 90);
        Model("Pallet", new Vector3(-23, 0, 91), 2.1f, 0);
        Model("Pipe Valve", new Vector3(28, 0, 109), 2.1f, 0);
        Model("Generator", new Vector3(37.5f, 0, 113), 2.1f, 180);
        Model("Barrel", new Vector3(14.5f, 0, 112), 1.1f, 10);
        Model("Pipes", new Vector3(38.5f, 0, 94), 4.8f, 0);
        // los faroles dejan oscuridad entre los puntos de referencia
        foreach (var light in map.GetComponentsInChildren<Light>())
        {
            light.shadows = LightShadows.Hard;
            light.shadowBias = 0.015f;
            light.shadowNormalBias = 0.1f;
            ShadowTier(light, 0);
            light.intensity = 0.08f;
            light.range = 2.8f;
            var existingBulb = light.transform.Find("Difusor de farol");
            var bulb = existingBulb ? existingBulb.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            bulb.name = "Difusor de farol";
            bulb.transform.SetParent(light.transform, false);
            bulb.transform.localScale = new Vector3(0.22f, 0.12f, 0.16f);
            if (bulb.GetComponent<Collider>()) Object.DestroyImmediate(bulb.GetComponent<Collider>());
            bulb.GetComponent<Renderer>().sharedMaterial = JamPolish.GetMaterial("Farol guia oscuro", new Color(0.16f, 0.075f, 0.025f), true);
        }
        game.atmosphereLights = Array.Empty<Light>();
        var victory = GameObject.Find("Luces blancas de victoria");
        var victoryPositions = new[] { new Vector3(0, 4, 4), new Vector3(-12, 4, 24), new Vector3(-10, 4, 40), new Vector3(21, 4, 72), new Vector3(-4, 4, 80), new Vector3(-20, 4, 105), new Vector3(21, 4, 104), new Vector3(34, 4, 109) };
        var lamps = victory.GetComponentsInChildren<Light>();
        for (int i = 0; i < victoryPositions.Length; i++)
        {
            var light = i < lamps.Length ? lamps[i] : new GameObject("Emergencia blanca").AddComponent<Light>();
            light.transform.SetParent(victory.transform);
            light.transform.position = victoryPositions[i];
            light.type = LightType.Point;
            light.color = Color.white;
            light.range = 16;
            light.intensity = 0;
            light.shadows = LightShadows.None;
        }
        game.GetComponent<DemoAtmosphere>().victoryLights = victory.GetComponentsInChildren<Light>();
        game.GetComponent<DemoAtmosphere>().navigationLights = map.GetComponentsInChildren<Light>();
        EditorUtility.SetDirty(game.GetComponent<DemoAtmosphere>());
        foreach (var candle in game.candles)
        {
            if (!candle.candleLight) continue;
            candle.candleLight.shadows = LightShadows.Soft;
            candle.candleLight.shadowBias = 0.015f;
            candle.candleLight.shadowNormalBias = 0.1f;
            ShadowTier(candle.candleLight, 1);
        }
        foreach (var player in game.players)
        {
            player.view.farClipPlane = 190;
            player.view.nearClipPlane = 0.08f;
        }
        // cada ruido se escucha desde la posicion mas cercana del equipo
        string metals = "Assets/External/AudioFinal/MetalWood/";
        string wind = "Assets/External/AudioFinal/Wind/Free PSX Wind Ambience/";
        Sound("Viento entrada", new Vector3(0, 3, 4), new[] { wind + "Wind 1.wav" }, true, 0.14f, 26);
        Sound("Viento patio", new Vector3(23, 4, 74), new[] { wind + "Wind 2.wav" }, true, 0.12f, 25);
        Sound("Metal en deposito", new Vector3(-22, 4.7f, 35), new[] { metals + "metal_sheet_02.ogg", metals + "metal_spring_01.ogg", metals + "metal_falling_01.ogg" }, false, 0.33f, 30);
        Sound("Crujidos taller", new Vector3(-22, 4.7f, 102), new[] { metals + "wood_squeak_01.ogg", metals + "wood_cracking_02.ogg", metals + "metal_hit_03.ogg" }, false, 0.32f, 28);
        Sound("Tuberias al fondo", new Vector3(38, 3, 108), new[] { metals + "metal_sheet_05.ogg", metals + "tools_05.ogg", metals + "metal_spring_02.ogg" }, false, 0.3f, 28);
        game.walkingClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/External/AudioFinal/Footsteps/40 Free PSX Footsteps/Concrete/Footstep Concrete 1.ogg");
        var footsteps = game.GetComponent<RecintoFootsteps>();
        if (!footsteps) footsteps = game.gameObject.AddComponent<RecintoFootsteps>();
        string stepFolder = "Assets/External/AudioFinal/Footsteps/40 Free PSX Footsteps/";
        footsteps.concrete = Enumerable.Range(1, 5).Select(i => AssetDatabase.LoadAssetAtPath<AudioClip>(stepFolder + "Concrete/Footstep Concrete " + i + ".ogg")).ToArray();
        footsteps.metal = Enumerable.Range(1, 5).Select(i => AssetDatabase.LoadAssetAtPath<AudioClip>(stepFolder + "Metal/Foodstep Metal " + i + ".ogg")).ToArray();
        var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        if (pipeline)
        {
            var settings = new SerializedObject(pipeline);
            settings.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = 4096;
            settings.FindProperty("m_ShadowDistance").floatValue = 100;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        var mix = map.GetComponent<RecintoSound>();
        if (!mix) mix = map.AddComponent<RecintoSound>();
        mix.game = game;
        mix.mixBackground = true;
        mix.clips = Array.Empty<AudioClip>();
        // evita dos fondos musicales tocando a la vez
        foreach (var source in Object.FindObjectsByType<AudioSource>())
            if (source != game.ambience && source.playOnAwake && !source.GetComponentInParent<DemoPlayer>()) source.playOnAwake = false;
        Physics.SyncTransforms();
        FinalMapBuilder.Validate();
        PrefabUtility.SaveAsPrefabAsset(map, Folder + "/RecintoDefinitivo.prefab");
        JamPolish.Save();
        Debug.Log("FINAL_MAP_READY funcionalidades conservadas");
    }

    static void Model(string name, Vector3 position, float size, float yaw)
    {
        var source = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
            .FirstOrDefault(t => (t.name == name || t.name == "Ruta - " + name) && t.GetComponentsInChildren<MeshRenderer>(true).Length > 0 && !t.IsChildOf(decor));
        if (!source) throw new Exception("falta modelo reutilizable " + name);
        var obj = Object.Instantiate(source.gameObject, decor);
        obj.name = "Reutilizado - " + name;
        obj.SetActive(true);
        var renderers = obj.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        obj.transform.localScale *= size / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        obj.transform.rotation = Quaternion.Euler(0, yaw, 0);
        bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        obj.transform.position += position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        foreach (var collider in obj.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
        // una caja estable evita engancharse en las tuberias pequenas
        var box = obj.AddComponent<BoxCollider>();
        bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        box.center = obj.transform.InverseTransformPoint(bounds.center);
        var x = obj.transform.InverseTransformVector(Vector3.right * bounds.size.x);
        var y = obj.transform.InverseTransformVector(Vector3.up * bounds.size.y);
        var z = obj.transform.InverseTransformVector(Vector3.forward * bounds.size.z);
        box.size = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x), Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
    }

    static void ShadowTier(Light light, int tier)
    {
        var settings = new SerializedObject(light.GetUniversalAdditionalLightData());
        settings.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = tier;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ConfigureSky()
    {
        string path = Folder + "/Cielo nocturno.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!sky) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, path); }
        sky.SetColor("_SkyTint", new Color(0.18f, 0.25f, 0.4f));
        sky.SetColor("_GroundColor", new Color(0.015f, 0.02f, 0.03f));
        sky.SetFloat("_Exposure", 0.15f);
        sky.SetFloat("_AtmosphereThickness", 0.65f);
        sky.SetFloat("_SunSize", 0.015f);
        RenderSettings.skybox = sky;
        RenderSettings.sun = GameObject.Find("Luz nocturna").GetComponent<Light>();
        EditorUtility.SetDirty(sky);
    }

    static void DressSigns(Transform root)
    {
        foreach (var text in root.GetComponentsInChildren<TextMesh>())
        {
            if (text.name == "Cartel PISTA") { Object.DestroyImmediate(text.gameObject); continue; }
            text.characterSize = 0.045f;
            text.color = new Color(0.7f, 0.67f, 0.55f);
            if (text.text == "ENTRADA") text.transform.position = new Vector3(-8, 2.2f, 9.70f);
            if (text.text == "CARGA / DEPOSITO") text.transform.position = new Vector3(-12, 3.7f, 17.70f);
            if (text.text == "PATIO INTERIOR") text.transform.position = new Vector3(21, 2.5f, 79.70f);
            if (text.text == "BOMBAS") text.transform.position = new Vector3(34, 3.7f, 99.70f);
            if (text.text == "TALLER")
            {
                text.transform.position = new Vector3(6.3f, 2.5f, 82);
                text.transform.rotation = Quaternion.Euler(0, -90, 0);
            }
            var board = text.transform.Find("Chapa de senal");
            if (!board)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = "Chapa de senal";
                obj.transform.SetParent(text.transform, false);
                obj.transform.localPosition = new Vector3(0, 0, 0.035f);
                obj.transform.localScale = new Vector3(text.text.Length * 0.18f + 0.35f, 0.6f, 0.04f);
                Object.DestroyImmediate(obj.GetComponent<Collider>());
                obj.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Acabados/Materials/Metal oxidado.mat");
            }
            var renderer = text.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            string path = Folder + "/Acabados/Materials/Texto senales.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader);
                material.SetTexture("_BaseMap", text.font ? text.font.material.mainTexture : renderer.sharedMaterial.mainTexture);
                material.SetFloat("_AlphaClip", 1);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Cutoff", 0.4f);
                material.SetFloat("_Cull", 0);
                AssetDatabase.CreateAsset(material, path);
            }
            renderer.sharedMaterial = material;
        }
    }

    static void Sound(string name, Vector3 position, string[] paths, bool loop, float volume, float radius)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(decor);
        obj.transform.position = position;
        var sound = obj.AddComponent<RecintoSound>();
        sound.game = game;
        sound.loop = loop;
        sound.volume = volume;
        sound.radius = radius;
        sound.startDelay = 10 + decor.childCount;
        sound.clips = paths.Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p)).ToArray();
        if (sound.clips.Any(c => !c)) throw new Exception("falta audio " + name);
    }
}
#endif
