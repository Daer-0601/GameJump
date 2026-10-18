#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
static class AtrezzoAutorun
{
    const string Flag = "AtrezzoPendiente.txt";
    const string Log = "AtrezzoAutorun.log";

    static AtrezzoAutorun()
    {
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (!File.Exists(Flag)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Game/04_Atrezzo/FBX/barrel.fbx") == null) return;
        EditorApplication.update -= Tick;
        if (!File.Exists(Flag)) return;
        File.Delete(Flag);
        try
        {
            EditorSceneManager.SaveOpenScenes();
            MapAtrezzo.Build();
            File.WriteAllText(Log, "OK " + DateTime.Now.ToString("o"));
        }
        catch (Exception ex)
        {
            File.WriteAllText(Log, "FAIL\n" + ex);
            DebugFail(ex);
        }
    }

    static void DebugFail(Exception ex)
    {
        UnityEngine.Debug.LogError("ATREZZO_AUTORUN " + ex.Message);
    }
}
#endif
