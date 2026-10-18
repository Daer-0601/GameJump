using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Gamejam2026.EditorTools
{
    // Órdenes de prueba en Play leídas de AgentOut/play.txt (una por línea).
    public static class PlayProbe
    {
        const string Out = "AgentOut";

        public static string Enter()
        {
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            return "entrando";
        }

        public static string Exit()
        {
            EditorApplication.isPlaying = false;
            return "saliendo";
        }

        public static string Run()
        {
            var log = new StringBuilder();
            Application.runInBackground = true;
            var game = Object.FindAnyObjectByType<DemoGame>();
            if (!game) return "sin DemoGame";
            foreach (var raw in File.ReadAllLines(Out + "/play.txt"))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var p = line.Split(' ');
                switch (p[0])
                {
                    case "start":
                        game.Begin();
                        game.SkipIntro();
                        log.AppendLine("start playing=" + game.Playing);
                        break;
                    case "nb":
                        {
                            var player = game.players[int.Parse(p[1])];
                            player.Notebook.Toggle();
                            log.AppendLine("nb " + p[1] + " blocking=" + player.Notebook.Blocking);
                            break;
                        }
                    case "preview":
                        game.players[int.Parse(p[1])].Notebook.Preview(p[2], F(p[3]));
                        log.AppendLine("preview " + p[2] + " " + p[3]);
                        break;
                    case "turn":
                        {
                            var player = game.players[int.Parse(p[1])];
                            player.Notebook.Turn(int.Parse(p[2]));
                            log.AppendLine("turn page=" + player.Notebook.Page);
                            break;
                        }
                    case "tp":
                        {
                            var player = game.players[int.Parse(p[1])];
                            var cc = player.GetComponent<CharacterController>();
                            if (cc) cc.enabled = false;
                            player.transform.SetPositionAndRotation(new Vector3(F(p[2]), F(p[3]), F(p[4])), Quaternion.Euler(0f, F(p[5]), 0f));
                            if (cc) cc.enabled = true;
                            log.AppendLine("tp " + p[1]);
                            break;
                        }
                    case "note":
                        game.Notify(line.Substring(5));
                        break;
                    case "shot":
                        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Out + "/play_" + p[1] + ".png"));
                        log.AppendLine("shot " + p[1]);
                        break;
                    case "render":
                        {
                            var cam = game.players[int.Parse(p[1])].view;
                            var rt = new RenderTexture(960, 1080, 24, RenderTextureFormat.ARGB32);
                            var rect = cam.rect;
                            var target = cam.targetTexture;
                            cam.targetTexture = rt;
                            cam.rect = new Rect(0, 0, 1, 1);
                            cam.Render();
                            cam.targetTexture = target;
                            cam.rect = rect;
                            Save(rt, Out + "/play_" + p[2] + ".png");
                            rt.Release();
                            Object.DestroyImmediate(rt);
                            log.AppendLine("render " + p[2]);
                            break;
                        }
                    case "rendernb":
                        {
                            var cam = game.players[int.Parse(p[1])].Notebook.OverlayCamera;
                            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
                            var rt = new RenderTexture(960, 1080, 24, RenderTextureFormat.ARGB32);
                            bool enabled = cam.enabled;
                            data.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Base;
                            cam.clearFlags = CameraClearFlags.SolidColor;
                            cam.backgroundColor = new Color(0.1f, 0.12f, 0.1f);
                            cam.targetTexture = rt;
                            cam.Render();
                            cam.targetTexture = null;
                            cam.clearFlags = CameraClearFlags.Depth;
                            data.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Overlay;
                            cam.enabled = enabled;
                            Save(rt, Out + "/play_" + p[2] + ".png");
                            rt.Release();
                            Object.DestroyImmediate(rt);
                            log.AppendLine("rendernb " + p[2]);
                            break;
                        }
                    case "pages":
                        {
                            var content = CuadernoContenido.Instance;
                            if (!content) { log.AppendLine("sin contenido"); break; }
                            content.Refresh();
                            for (int i = 0; i < content.Pages.Length; i++) Save(content.Pages[i], Out + "/page_" + i + ".png");
                            log.AppendLine("pages " + content.Pages.Length);
                            break;
                        }
                    case "state":
                        foreach (var player in game.players)
                            log.AppendLine("J" + (player.playerIndex + 1) + " pos=" + player.transform.position + " reading=" + player.Reading + " focus=" + player.HasFocus + " hint=" + player.Hint
                                + " nb=" + (player.Notebook ? player.Notebook.Describe() : "-") + " time=" + Time.time.ToString("0.0"));
                        break;
                    case "stations":
                        foreach (var station in Object.FindObjectsByType<RecintoStation>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                            log.AppendLine(station.name + " pos=" + station.transform.position + " yaw=" + station.transform.eulerAngles.y.ToString("0"));
                        break;
                }
            }
            return log.ToString();
        }

        static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        static void Save(RenderTexture rt, string path)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
