#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Puente para herramientas externas: ejecuta un método estático pedido en AgentBridge.request
// y deja el resultado en AgentBridge.result. También fuerza un refresco si existe AgentBridge.refresh.
[InitializeOnLoad]
static class AgentBridge
{
    const string Request = "AgentBridge.request";
    const string Result = "AgentBridge.result";
    const string RefreshFlag = "AgentBridge.refresh";
    static double nextPoll;

    static AgentBridge()
    {
        EditorApplication.update += Tick;
        File.WriteAllText("AgentBridge.alive", DateTime.Now.ToString("o"));
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 1.0;
        if (File.Exists(RefreshFlag) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            File.Delete(RefreshFlag);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return;
        }
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string spec = File.ReadAllText(Request).Trim();
        bool wantsPlay = spec.StartsWith("play:");
        if (!wantsPlay && EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        if (wantsPlay) spec = spec.Substring(5);
        try
        {
            int dot = spec.LastIndexOf('.');
            string typeName = spec.Substring(0, dot);
            string method = spec.Substring(dot + 1);
            Type type = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) break;
            }
            if (type == null) throw new Exception("tipo no encontrado " + typeName);
            var info = type.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (info == null) throw new Exception("método no encontrado " + spec);
            object value = info.Invoke(null, null);
            File.WriteAllText(Result, "OK " + spec + "\n" + (value ?? ""));
        }
        catch (Exception ex)
        {
            File.WriteAllText(Result, "FAIL " + spec + "\n" + (ex.InnerException ?? ex));
            Debug.LogError("AGENT_BRIDGE " + ex);
        }
    }
}
#endif
