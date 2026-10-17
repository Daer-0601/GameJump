#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Gamejam2026;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class DaerSetup
{
    public const string RootName = "Daer - paso y segunda pista";
    const string Folder = "Assets/Game/06_Daer";

    [MenuItem("Gamejam/Daer/Preparar paso y segundo cartel")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play antes de preparar la escena.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Game/00_Scenes/Demo.unity") throw new InvalidOperationException("Abre Demo.unity antes de preparar el paso.");
        if (GameObject.Find(RootName)) { Debug.Log("DAER_SETUP: el paso ya existe; se conserva la edición actual."); return; }
        var game = Object.FindAnyObjectByType<DemoGame>();
        var map = GameObject.Find("Recinto - definitivo");
        var wall = map ? map.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Taller tabique oeste") : null;
        if (!game || !wall) throw new InvalidOperationException("Falta la partida o el tabique del taller del mapa actual.");
        var wallMaterial = wall.GetComponent<Renderer>().sharedMaterial;
        var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/05_Mapa/Acabados/Materials/Metal oxidado.mat");
        if (!wallMaterial || !metal) throw new InvalidOperationException("Faltan los materiales existentes del taller.");

        Directory.CreateDirectory(Folder + "/Meshes");
        AssetDatabase.Refresh();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Preparar parte de Daer");
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Crear paso y cartel");
        // Conservar la pared original desactivada permite restaurarla sin tocar el prefab de Julio.
        Undo.RecordObject(wall.gameObject, "Abrir hueco en tabique");
        wall.gameObject.SetActive(false);
        Box(root.transform, "Tabique conservado - oeste", new Vector3(-22f, 2.5f, 90f), new Vector3(12f, 5f, .5f), wallMaterial);
        Box(root.transform, "Tabique conservado - este", new Vector3(-9.5f, 2.5f, 90f), new Vector3(5f, 5f, .5f), wallMaterial);
        Box(root.transform, "Dintel del paso", new Vector3(-14f, 4.15f, 90f), new Vector3(4f, 1.7f, .5f), wallMaterial);
        var panel = Box(root.transform, "Compuerta al fondo - primer acierto", new Vector3(-14f, 1.65f, 90f), new Vector3(4f, 3.3f, .22f), metal);
        var passage = root.AddComponent<DemoPassage>();
        passage.game = game;
        passage.panel = panel.transform;
        passage.blocker = panel.GetComponent<Collider>();
        passage.openingSeconds = 1.5f;

        var sign = new GameObject("Segundo cartel - solo siguiente símbolo");
        sign.transform.SetParent(root.transform, false);
        sign.transform.position = new Vector3(-14f, 1.5f, 94.5f);
        Box(sign.transform, "Placa", sign.transform.position, new Vector3(1.35f, 1.15f, .12f), metal);
        Box(sign.transform, "Soporte", new Vector3(-14f, .47f, 94.55f), new Vector3(.12f, .94f, .12f), metal);
        var clue = sign.AddComponent<DemoClue>();
        clue.nextSymbolOnly = true;
        var labelObject = new GameObject("Símbolo pendiente - necesita vela");
        labelObject.transform.SetParent(sign.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0f, -.071f);
        var label = labelObject.AddComponent<TextMesh>();
        label.text = "";
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 96;
        label.characterSize = .095f;
        label.color = new Color(.85f, .8f, .65f);
        label.GetComponent<Renderer>().enabled = false;
        clue.inscriptionLabel = label;
        var recorder = root.AddComponent<DaerPlaytestRecorder>();
        recorder.game = game;
        Physics.SyncTransforms();
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("DAER_SETUP_OK: atajo del taller, cartel y registro local preparados; dificultad conservada.");
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = position;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = material;
        var filter = box.GetComponent<MeshFilter>();
        var mesh = Object.Instantiate(filter.sharedMesh);
        mesh.name = name;
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var uv = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = box.transform.TransformPoint(vertices[i]);
            var n = normals[i];
            uv[i] = Mathf.Abs(n.y) > .5f ? new Vector2(p.x, p.z) / 2f : Mathf.Abs(n.x) > .5f ? new Vector2(p.z, p.y) / 2f : new Vector2(p.x, p.y) / 2f;
        }
        mesh.uv = uv;
        mesh.RecalculateTangents();
        string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/Meshes/" + name + ".asset");
        AssetDatabase.CreateAsset(mesh, path);
        filter.sharedMesh = mesh;
        return box;
    }
}
#endif
