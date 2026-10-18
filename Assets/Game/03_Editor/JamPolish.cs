#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Gamejam2026;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Incremental editing of the saved scene; never invokes DemoBuilder.CreateScene.
public static class JamPolish
{
    const string ScenePath = "Assets/Game/00_Scenes/Demo.unity";
    public static DemoGame OpenGame()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Aplica el pulido fuera de Play.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (scene.isDirty) throw new InvalidOperationException("Guarda la escena abierta antes de aplicar el pulido.");
            EditorSceneManager.OpenScene(ScenePath);
        }
        return UnityEngine.Object.FindAnyObjectByType<DemoGame>();
    }

    [MenuItem("Gamejam/Pulido/1 - Atmósfera")]
    public static void Atmosphere()
    {
        var game = OpenGame();
        var atmosphere = game.GetComponent<DemoAtmosphere>();
        if (!atmosphere) atmosphere = game.gameObject.AddComponent<DemoAtmosphere>();
        foreach (var light in game.atmosphereLights) if (light) light.intensity = 0f;
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>())
            if (light.type == LightType.Directional) light.intensity = 0f;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.006f, 0.008f, 0.012f);
        RenderSettings.reflectionIntensity = 0f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.035f;
        RenderSettings.fogColor = new Color(0.004f, 0.006f, 0.009f);
        var candle = game.candles[0];
        candle.candleLight.range = 5.8f;
        candle.candleLight.color = new Color(1f, 0.65f, 0.30f);
        candle.candleLight.shadows = LightShadows.Soft;
        var shadowData = new SerializedObject(candle.candleLight.GetUniversalAdditionalLightData());
        shadowData.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = 0;
        shadowData.ApplyModifiedPropertiesWithoutUndo();
        for (int i = 1; i < game.candles.Length; i++)
        {
            if (game.candles[i].candleLight) game.candles[i].candleLight.enabled = false;
            game.candles[i].gameObject.SetActive(false);
        }
        var lightsRoot = GameObject.Find("Luces blancas de victoria");
        if (!lightsRoot) lightsRoot = new GameObject("Luces blancas de victoria");
        if (lightsRoot.transform.childCount == 0)
        {
            foreach (var point in new[] { new Vector3(0, 5, 0), new Vector3(-9, 4, 5), new Vector3(9, 4, 5), new Vector3(0, 4, 14) })
            {
                var light = new GameObject("Luz de emergencia blanca").AddComponent<Light>();
                light.transform.SetParent(lightsRoot.transform);
                light.transform.position = point;
                light.type = LightType.Point;
                light.range = 18f;
                light.color = Color.white;
                light.intensity = 0f;
            }
        }
        atmosphere.victoryLights = lightsRoot.GetComponentsInChildren<Light>();
        var clue = UnityEngine.Object.FindObjectsByType<DemoClue>().First(c => !c.nextSymbolOnly);
        clue.inscriptionLabel = UnityEngine.Object.FindObjectsByType<TextMesh>().FirstOrDefault(t => t.name.StartsWith("Cartel LEE"));
        if (clue.inscriptionLabel)
        {
            clue.inscriptionLabel.text = "INSCRIPCIÓN\n[E / U]";
            clue.inscriptionLabel.characterSize = 0.032f;
        }
        EditorUtility.SetDirty(clue);
        var material = GetMaterial("Chispas", new Color(1f, 0.55f, 0.1f), true);
        foreach (var mechanism in game.mechanisms)
        {
            mechanism.sparkMaterial = material;
            mechanism.indicator.range = 0.9f;
            mechanism.indicator.intensity = 0.4f;
            EditorUtility.SetDirty(mechanism);
        }
        foreach (var player in game.players) player.view.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        EditorUtility.SetDirty(atmosphere);
        Save();
    }

    public static Material GetMaterial(string name, Color color, bool unlit = false)
    {
        const string folder = "Assets/Game/02_Polish";
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        string path = folder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    public static void Save()
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Gamejam/Pulido/2 - Rutas y balance")]
    public static void Routes()
    {
        var game = OpenGame();
        game.dangerPerSecond = 0.04f;
        game.darknessPenalty = 0.03f;
        game.errorPenalty = 12f;
        game.successRelief = 10f;
        game.unstablePenalty = 1.25f;
        game.stabilizeRelief = 8f;
        game.destabilizeMin = 38f;
        game.destabilizeMax = 52f;
        EditorUtility.SetDirty(game);
        // These original barrels sat on the centre line to B. Keep them as landmarks at the side.
        foreach (var prop in UnityEngine.Object.FindObjectsByType<Transform>())
        {
            if (prop.name != "Barrel" || !prop.parent || prop.parent.name != "Escenario - editar libremente") continue;
            Vector3 point = prop.position;
            if (point.x >= 5f && point.x <= 9f && point.z > 1f && point.z < 4f)
                prop.position = new Vector3(point.x + 1f, point.y, -1.5f);
        }
        if (!GameObject.Find("Recorridos A B C"))
        {
            var root = new GameObject("Recorridos A B C").transform;
            // Open lanes: all props are outside the centre line, at least 3 m apart.
            var centre = new Vector3(0, 0, 0.5f);
            foreach (var mechanism in game.mechanisms)
            {
                Vector3 target = mechanism.transform.position;
                target.y = 0f;
                Vector3 direction = (target - centre).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                for (int i = 1; i <= 3; i++)
                {
                    Vector3 point = Vector3.Lerp(centre, target, i / 4f);
                    PlaceExisting("Pipes", "Assets/External/Plumbing/Pipes.fbx", point + side * 2.1f, 1.7f, angle, root, false);
                    if (i == 2)
                        PlaceExisting("Concrete Barricade", "Assets/External/Shacks/Concrete Barricade.fbx", point - side * 2.5f, 1.8f, angle, root, true);
                }
                var sign = new GameObject("Ruta " + mechanism.symbol).AddComponent<TextMesh>();
                sign.transform.SetParent(root, false);
                sign.transform.position = centre + direction * 3.2f + side * 1.7f + Vector3.up * 0.7f;
                sign.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                sign.text = mechanism.symbol + "  ↑";
                sign.fontSize = 48;
                sign.characterSize = 0.07f;
                sign.anchor = TextAnchor.MiddleCenter;
                sign.color = new Color(0.32f, 0.28f, 0.19f);
            }
        }
        Save();
    }

    static GameObject PlaceExisting(string modelName, string path, Vector3 position, float size, float angle, Transform parent, bool collision)
    {
        // Clone an existing textured instance when possible, otherwise use the source FBX.
        var source = UnityEngine.Object.FindObjectsByType<Transform>()
            .FirstOrDefault(t => t.name == modelName && t.GetComponentInChildren<MeshRenderer>() && t.parent != parent);
        GameObject asset = source ? source.gameObject : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!asset) throw new InvalidOperationException("Falta modelo de ruta: " + path);
        var obj = UnityEngine.Object.Instantiate(asset, parent);
        obj.name = "Ruta - " + modelName;
        var renderers = obj.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        float scale = size / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        obj.transform.localScale *= scale;
        obj.transform.rotation = Quaternion.Euler(0f, angle, 0f);
        bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        obj.transform.position += position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        if (!source)
        {
            var pipeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/02_Materials/Pipe_Valve_0.mat");
            if (pipeMaterial) foreach (var renderer in renderers) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => pipeMaterial).ToArray();
        }
        foreach (var collider in obj.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
        if (collision)
        {
            foreach (var mesh in obj.GetComponentsInChildren<MeshFilter>())
                if (mesh.sharedMesh) mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
        }
        return obj;
    }

}

#endif
