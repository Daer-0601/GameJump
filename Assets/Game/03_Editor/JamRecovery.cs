#if UNITY_EDITOR
using System.Linq;
using Gamejam2026;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class JamRecovery
{
    [MenuItem("Gamejam/Pulido/Recuperar oscuridad y mezcla de audio")]
    public static void Presentation()
    {
        var game = JamPolish.OpenGame();
        var atmosphere = game.GetComponent<DemoAtmosphere>();
        var map = GameObject.Find("Recinto - definitivo");
        atmosphere.navigationLights = map.GetComponentsInChildren<Light>();
        foreach (var light in atmosphere.navigationLights) { light.intensity = .08f; light.range = 2.8f; }
        foreach (var light in Object.FindObjectsByType<Light>())
            if (light.type == LightType.Directional) light.intensity = 0;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.003f, .0045f, .007f);
        RenderSettings.fogDensity = .03f;
        foreach (var player in game.players)
        {
            player.view.clearFlags = CameraClearFlags.SolidColor;
            player.view.backgroundColor = new Color(.004f, .006f, .012f);
        }
        var candle = game.candles[0];
        candle.candleLight.range = 5.8f;
        candle.candleLight.intensity = 4.9f;
        foreach (var bulb in map.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Difusor de farol"))
            bulb.sharedMaterial = JamPolish.GetMaterial("Farol guia oscuro", new Color(.16f, .075f, .025f), true);
        EditorUtility.SetDirty(atmosphere);
        JamPolish.Save();
        Debug.Log("RECOVERY_PRESENTATION_OK");
    }
}
#endif
