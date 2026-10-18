#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Inspección del mapa para revisar el montaje: volcado de jerarquía y capturas a AgentOut/.
public static class MapProbe
{
    const string Out = "AgentOut";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static string F(float v) => v.ToString("0.00", Inv);
    static string V(Vector3 v) => "(" + F(v.x) + "," + F(v.y) + "," + F(v.z) + ")";

    public static string Dump()
    {
        Directory.CreateDirectory(Out);
        var sb = new StringBuilder();
        var scene = EditorSceneManager.GetActiveScene();
        sb.AppendLine("SCENE " + scene.path);
        foreach (var root in scene.GetRootGameObjects())
        {
            sb.AppendLine("ROOT " + root.name + " active=" + root.activeSelf + " children=" + root.transform.childCount);
            DumpNode(sb, root.transform, 1, root.name == "Recinto - definitivo" ? 3 : 2);
        }
        File.WriteAllText(Out + "/scene_dump.txt", sb.ToString());
        return "lines=" + sb.Length;
    }

    static void DumpNode(StringBuilder sb, Transform node, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;
        foreach (Transform child in node)
        {
            var b = Bounds(child.gameObject, out int renderers);
            var cols = child.GetComponentsInChildren<Collider>(true).Length;
            var comps = string.Join(",", child.GetComponents<Component>().Where(c => c && !(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer)).Select(c => c.GetType().Name));
            sb.Append(new string(' ', depth * 2)).Append(child.name).Append(child.gameObject.activeSelf ? "" : " [OFF]")
                .Append(" pos=").Append(V(child.position)).Append(" rotY=").Append(F(child.eulerAngles.y))
                .Append(" n=").Append(child.childCount);
            if (renderers > 0) sb.Append(" min=").Append(V(b.min)).Append(" max=").Append(V(b.max));
            if (cols > 0) sb.Append(" cols=").Append(cols);
            if (comps.Length > 0) sb.Append(" {").Append(comps).Append("}");
            sb.AppendLine();
            if (child.childCount <= 60) DumpNode(sb, child, depth + 1, maxDepth);
        }
    }

    static Bounds Bounds(GameObject obj, out int count)
    {
        var rs = obj.GetComponentsInChildren<Renderer>(false);
        count = rs.Length;
        if (rs.Length == 0) return new Bounds(obj.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // Planta sin techos, con luz plana para leer el montaje.
    public static string TopDown()
    {
        Directory.CreateDirectory(Out);
        var tiles = new[] { new Rect(-38f, -12f, 80f, 46f), new Rect(-38f, 32f, 80f, 46f), new Rect(-38f, 76f, 80f, 45f) };
        var hidden = new List<Renderer>();
        foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (!r.enabled) continue;
            if (r.bounds.min.y > 2.75f || (r.bounds.size.y < 0.6f && r.bounds.min.y > 2.4f)) { r.enabled = false; hidden.Add(r); }
        }
        string result;
        using (new FlatLight(true))
        {
            int i = 0;
            foreach (var tile in tiles)
            {
                var go = new GameObject("_probe_cam");
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = tile.height * 0.5f;
                cam.aspect = tile.width / tile.height;
                cam.transform.position = new Vector3(tile.center.x, 40f, tile.center.y);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 60f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.2f, 0.05f, 0.2f);
                Render(cam, Mathf.RoundToInt(tile.width * 20f), Mathf.RoundToInt(tile.height * 20f), Out + "/top_" + i + ".png");
                UnityEngine.Object.DestroyImmediate(go);
                i++;
            }
            result = "tiles=" + i;
        }
        foreach (var r in hidden) if (r) r.enabled = true;
        return result;
    }

    // Lee AgentOut/shots.txt: nombre x y z yaw pitch fov modo(lit|flat|candle)
    public static string Shots()
    {
        Directory.CreateDirectory(Out);
        string file = Out + "/shots.txt";
        if (!File.Exists(file)) return "sin shots.txt";
        int n = 0;
        foreach (var raw in File.ReadAllLines(file))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string name = p[0];
            var pos = new Vector3(float.Parse(p[1], Inv), float.Parse(p[2], Inv), float.Parse(p[3], Inv));
            float yaw = float.Parse(p[4], Inv), pitch = float.Parse(p[5], Inv), fov = float.Parse(p[6], Inv);
            string mode = p.Length > 7 ? p[7] : "flat";
            var go = new GameObject("_probe_cam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 120f;
            cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(pitch, yaw, 0f));
            GameObject lamp = null;
            if (mode == "candle")
            {
                lamp = new GameObject("_probe_candle");
                lamp.transform.position = pos + cam.transform.forward * 0.4f + Vector3.down * 0.35f + cam.transform.right * 0.25f;
                var l = lamp.AddComponent<Light>();
                l.type = LightType.Point; l.range = 7f; l.intensity = 2.2f; l.color = new Color(1f, 0.68f, 0.38f); l.shadows = LightShadows.Soft;
            }
            if (mode == "flat")
            {
                using (new FlatLight(false)) Render(cam, 960, 540, Out + "/shot_" + name + ".png");
            }
            else Render(cam, 960, 540, Out + "/shot_" + name + ".png");
            if (lamp) UnityEngine.Object.DestroyImmediate(lamp);
            UnityEngine.Object.DestroyImmediate(go);
            n++;
        }
        return "shots=" + n;
    }

    // Una imagen por modelo, aislado bajo el mapa. Rojo marca +X y azul +Z del objeto colocado.
    public static string Gallery()
    {
        Directory.CreateDirectory(Out + "/gallery");
        var list = new List<string>();
        string filter = File.Exists(Out + "/gallery.txt") ? File.ReadAllText(Out + "/gallery.txt").Trim() : "";
        bool raw = filter.StartsWith("raw:");
        if (raw) filter = filter.Substring(4);
        var paths = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Game/04_Atrezzo/FBX", "Assets/External/AOscurasProduction/Resources/Production", "Assets/Game/07_Cuaderno" })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".fbx") || p.EndsWith(".prefab")).Distinct().ToList();
        if (filter.Length > 0) paths = paths.Where(p => filter.Split(',').Any(f => Path.GetFileNameWithoutExtension(p).Contains(f))).ToList();
        using (new FlatLight(false))
        {
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                obj.transform.position = new Vector3(0f, -200f, 0f);
                obj.transform.rotation = path.EndsWith(".fbx") && !raw ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
                if (Path.GetFileNameWithoutExtension(path) == "shelf") obj.transform.localScale = Vector3.one * 0.1f;
                var b = Bounds(obj, out _);
                float size = Mathf.Max(b.size.x, b.size.y, b.size.z, 0.05f);
                var red = GameObject.CreatePrimitive(PrimitiveType.Cube);
                red.transform.localScale = Vector3.one * size * 0.06f;
                red.transform.position = new Vector3(b.max.x + size * 0.08f, b.center.y, b.center.z);
                var blue = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blue.transform.localScale = Vector3.one * size * 0.06f;
                blue.transform.position = new Vector3(b.center.x, b.center.y, b.max.z + size * 0.08f);
                var mr = new Material(Shader.Find("Universal Render Pipeline/Unlit")); mr.color = Color.red;
                var mb = new Material(Shader.Find("Universal Render Pipeline/Unlit")); mb.color = Color.blue;
                red.GetComponent<Renderer>().sharedMaterial = mr;
                blue.GetComponent<Renderer>().sharedMaterial = mb;
                var go = new GameObject("_probe_cam");
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = 35f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.35f, 0.38f, 0.42f);
                Vector3 dir = new Vector3(-0.75f, 0.55f, -1f).normalized;
                float dist = size * 0.5f / Mathf.Tan(17.5f * Mathf.Deg2Rad) * 1.35f;
                cam.transform.position = b.center + dir * dist;
                cam.transform.LookAt(b.center);
                cam.nearClipPlane = Mathf.Max(0.01f, dist * 0.05f);
                cam.farClipPlane = dist * 4f;
                string name = Path.GetFileNameWithoutExtension(path);
                Render(cam, 480, 360, Out + "/gallery/" + name + ".png");
                list.Add(name + " size=" + V(b.size) + " centerOff=" + V(b.center - obj.transform.position) + " minY=" + F(b.min.y - obj.transform.position.y));
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(red);
                UnityEngine.Object.DestroyImmediate(blue);
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }
        File.WriteAllLines(Out + "/gallery/sizes.txt", list);
        return "models=" + list.Count;
    }

    static void Render(Camera cam, int w, int h, string path)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
    }

    sealed class FlatLight : IDisposable
    {
        readonly AmbientMode mode;
        readonly Color ambient;
        readonly bool fog;
        readonly GameObject sun;
        readonly List<Light> disabled = new List<Light>();

        public FlatLight(bool fromAbove)
        {
            mode = RenderSettings.ambientMode;
            ambient = RenderSettings.ambientLight;
            fog = RenderSettings.fog;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);
            RenderSettings.fog = false;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.enabled && l.type == LightType.Directional) { l.enabled = false; disabled.Add(l); }
            sun = new GameObject("_probe_sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = fromAbove ? LightShadows.None : LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(fromAbove ? 75f : 50f, 30f, 0f);
        }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(sun);
            foreach (var l in disabled) if (l) l.enabled = true;
            RenderSettings.ambientMode = mode;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = fog;
        }
    }
}
#endif
