using System;
using System.IO;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Gamejam2026.EditorTools
{
    [InitializeOnLoad]
    static class LanPrefabBuilder
    {
        const string AssetPath = "Assets/Resources/Lan/LanSession.prefab";

        static LanPrefabBuilder()
        {
            EditorApplication.delayCall += Ensure;
        }

        public static void EnsureNow()
        {
            try
            {
                Ensure();
                Debug.Log("LanPrefabBuilder: " + AssetPath);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                EditorApplication.Exit(0);
            }
        }

        static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string absolute = Path.Combine(Application.dataPath, "Resources/Lan/LanSession.prefab");
            if (!File.Exists(absolute))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(absolute));
                var body = new GameObject("LanSession");
                body.AddComponent<LanSession>();
                body.AddComponent<NetworkObject>();
                PrefabUtility.SaveAsPrefabAsset(body, AssetPath);
                UnityEngine.Object.DestroyImmediate(body);
                AssetDatabase.ImportAsset(AssetPath);
            }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
            var networkObject = asset ? asset.GetComponent<NetworkObject>() : null;
            if (!networkObject) return;
            var validate = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
            validate?.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
            AssetDatabase.SaveAssets();
        }
    }
}
