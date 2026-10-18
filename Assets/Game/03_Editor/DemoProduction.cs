#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Gamejam2026;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit local commands only; never rebuilds the map or runs legacy builders.
[InitializeOnLoad]
public static class DemoProduction
{
    const string Request = "Temp/Production.command";
    static double next;
    static DemoProduction() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(Request)) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try
        {
            if (command == "inspect") Inspect();
            else if (command == "models") InspectModels();
            else if (command == "refresh") AssetDatabase.Refresh();
            else if (command == "art") ApplyArt();
            else if (command == "materials") PolishMaterials();
            else if (command == "restore-settings") PlayerSettings.runInBackground = false;
            else if (command == "check") DemoProductionCheck.Run();
            else if (command == "build") BuildWindows();
            else if (command == "play") EditorApplication.isPlaying = true;
            else if (command == "stop") EditorApplication.isPlaying = false;
            else throw new InvalidOperationException("Comando de producción desconocido: " + command);
            File.WriteAllText("Temp/Production.result", "OK " + command);
        }
        catch (Exception ex) { File.WriteAllText("Temp/Production.result", ex.ToString()); Debug.LogException(ex); }
    }
    static void PolishMaterials()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Materiales: la escena debe estar guardada y fuera de Play.");
        var game = UnityEngine.Object.FindAnyObjectByType<DemoGame>();
        foreach (var mechanism in game.mechanisms) ConvertMaterials(mechanism.transform.Find("Carcasa industrial importada").gameObject);
        Flame(game.candles[0]);
        for (int i = 0; i < game.players.Length; i++)
        {
            var model = game.players[i].GetComponent<DemoMotion>().animator.gameObject;
            string path = "Assets/External/QuaterniusWorkers/Worker_" + (i == 0 ? "Female" : "Male") + ".fbx";
            HoldPose(model, Clip(path, "pickup"), "UsoAgachado" + i);
            Clip(path, "idle").SampleAnimation(model, 0);
        }
        BuildStation();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }
    static void BuildWindows()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play antes del build.");
        Directory.CreateDirectory("Build/AOscuras");
        var report = BuildPipeline.BuildPlayer(new[] { "Assets/Game/00_Scenes/Demo.unity" }, "Build/AOscuras/AOscuras.exe", BuildTarget.StandaloneWindows64, BuildOptions.CompressWithLz4);
        File.WriteAllText("Temp/Production.build", report.summary.result + " bytes=" + report.summary.totalSize + " errors=" + report.summary.totalErrors);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Build de Windows falló: " + report.summary.result);
    }
    static void InspectModels()
    {
        var paths = AssetDatabase.FindAssets("t:Model", new[] { "Assets/External/QuaterniusWorkers", "Assets/External/RecintoSpider" });
        File.WriteAllText("Temp/Production.models", string.Join("\n", paths.Select(AssetDatabase.GUIDToAssetPath).Select(p => p + "\n" +
            string.Join("\n", AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().Select(c => c.name + " " + c.length)) + "\n" +
            string.Join("\n", AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentsInChildren<Transform>().Select(t => t.name)))));
    }
    static void Inspect()
    {
        var scene = SceneManager.GetActiveScene();
        File.WriteAllText("Temp/Production.inspect", "Scene=" + scene.path + " dirty=" + scene.isDirty + " playing=" + EditorApplication.isPlaying + " paused=" + EditorApplication.isPaused + " scale=" + Time.timeScale + "\n" +
            string.Join("\n", UnityEngine.Object.FindObjectsByType<DemoPlayer>().Select(p =>
                p.name + " pos=" + p.transform.position + " cam=" + p.view.transform.localPosition + "\n" + string.Join("\n", p.GetComponentsInChildren<Transform>().Select(t => t.name)))));
    }
    [MenuItem("Gamejam/Producción/Aplicar arte sin reconstruir mapa")]
    public static void ApplyArt()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play antes de aplicar arte.");
        var scene = SceneManager.GetActiveScene();
        if (scene.isDirty) throw new InvalidOperationException("Hay cambios locales sin guardar en la escena; se conserva intacta.");
        if (scene.path != "Assets/Game/00_Scenes/Demo.unity")
            scene = EditorSceneManager.OpenScene("Assets/Game/00_Scenes/Demo.unity");
        var game = UnityEngine.Object.FindAnyObjectByType<DemoGame>();
        if (!game) throw new InvalidOperationException("DemoGame no está en Demo.");
        Directory.CreateDirectory(Art);
        AssetDatabase.Refresh();
        for (int i = 0; i < game.players.Length; i++) Crew(game.players[i], i);
        Candle(game.candles[0]);
        Flame(game.candles[0]);
        foreach (var carry in UnityEngine.Object.FindObjectsByType<CarryObject>())
            if (!carry.isCandle) Crank(carry);
        foreach (var mechanism in game.mechanisms)
        {
            Machine(mechanism);
            ConvertMaterials(mechanism.transform.Find("Carcasa industrial importada").gameObject);
        }
        BuildStation();
        BuildSpider();
        var slam = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/External/AudioFinal/MetalWood/metal_slam_01.ogg");
        foreach (var passage in UnityEngine.Object.FindObjectsByType<DemoPassage>()) { passage.openingClip = slam; passage.openingSeconds = 1.5f; EditorUtility.SetDirty(passage); }
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Inspect();
    }

    const string Art = "Assets/External/AOscurasProduction";
    static Material Material(string name, Color color, float metallic = 0)
    {
        string path = Art + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.color = color;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", .34f);
        return m;
    }
    static Bounds BoundsOf(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) throw new InvalidOperationException("Modelo sin renderers: " + root.name);
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }
    static GameObject Model(string path, Transform parent, string name, float height)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!source) throw new InvalidOperationException("Falta " + path);
        var obj = UnityEngine.Object.Instantiate(source, parent);
        obj.name = name;
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        var b = BoundsOf(obj);
        obj.transform.localScale *= height / Mathf.Max(.001f, b.size.y);
        b = BoundsOf(obj);
        obj.transform.position += parent.position - new Vector3(b.center.x, b.min.y, b.center.z);
        ConvertMaterials(obj);
        foreach (var collider in obj.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
        return obj;
    }
    static void ConvertMaterials(GameObject obj)
    {
        foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
            {
                if (original && original.shader && original.shader.name == "Universal Render Pipeline/Lit") return original;
                var converted = Material("Importado " + obj.name + " " + (original ? original.name : "metal"), original ? original.color : Color.gray, .5f);
                if (original && original.mainTexture) converted.SetTexture("_BaseMap", original.mainTexture);
                return converted;
            }).ToArray();
        }
    }
    static AnimationClip Clip(string path, params string[] candidates)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
        foreach (var key in candidates)
        {
            var found = clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains(key));
            if (found) return found;
        }
        throw new InvalidOperationException("Falta clip " + string.Join("/", candidates) + " en " + path);
    }
    static void ImportRig(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (!importer) throw new InvalidOperationException("Falta FBX: " + path);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        var clips = importer.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.loopTime = !c.name.ToLowerInvariant().Contains("death");
            c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
            c.keepOriginalOrientation = true; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }
    static void Crew(DemoPlayer player, int index)
    {
        string path = "Assets/External/QuaterniusWorkers/Worker_" + (index == 0 ? "Female" : "Male") + ".fbx";
        ImportRig(path);
        var old = player.transform.Find("Operario con rig");
        if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var model = Model(path, player.transform, "Operario con rig", 1.7f);
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            var originals = r.sharedMaterials;
            r.sharedMaterials = originals.Select((m, n) =>
            {
                var col = m ? m.color : Color.gray;
                // Keep skin/helmet detail, dull the overalls into the existing wet industrial palette.
                col = Color.Lerp(col, index == 0 ? new Color(.42f, .29f, .13f) : new Color(.17f, .32f, .32f), .35f);
                return Material("Operario" + index + "_" + n + "_" + (m ? m.name : "tela"), col);
            }).ToArray();
        }
        var animator = model.GetComponent<Animator>();
        if (!animator) animator = model.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var idle = Clip(path, "idle");
        var walk = Clip(path, "walk");
        var run = RunClip(model, walk, index);
        var carry = Clip(path, "walk_carry");
        string controllerPath = Art + "/Operario" + index + ".controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.parameters = new AnimatorControllerParameter[0];
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Crank", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Candle", AnimatorControllerParameterType.Bool);
        var machine = controller.layers[0].stateMachine;
        foreach (var state in machine.states) machine.RemoveState(state.state);
        Blend(controller, "Locomotion", idle, walk, run, player.moveSpeed, player.moveSpeed * player.runMultiplier);
        var candleIdle = Pose(model, idle, carry, "VelaIdle" + index);
        var candleWalk = Pose(model, walk, carry, "VelaWalk" + index);
        var candleRun = Pose(model, run, carry, "VelaRun" + index);
        Blend(controller, "Candle", candleIdle, candleWalk, candleRun, player.moveSpeed, player.moveSpeed * player.runMultiplier);
        var crankIdle = Pose(model, idle, carry, "ManivelaIdle" + index);
        var crankWalk = Pose(model, walk, carry, "ManivelaLenta" + index);
        Blend(controller, "Crank", crankIdle, crankWalk, crankWalk, player.moveSpeed * .65f, player.moveSpeed * .65f * player.runMultiplier);
        var interact = machine.AddState("Interact");
        interact.motion = HoldPose(model, Clip(path, "pickup"), "UsoAgachado" + index);
        var useEnter = machine.AddState("UseEnter"); useEnter.motion = Clip(path, "pickup");
        var settle = useEnter.AddTransition(interact); settle.hasExitTime = true; settle.exitTime = .65f; settle.duration = .1f;
        var recoil = machine.AddState("Recoil");
        recoil.motion = Clip(path, "recievehit", "receivehit");
        machine.defaultState = machine.states.First(s => s.state.name == "Locomotion").state;
        animator.runtimeAnimatorController = controller;
        idle.SampleAnimation(model, 0);
        var motion = player.GetComponent<DemoMotion>();
        if (!motion) motion = player.gameObject.AddComponent<DemoMotion>();
        motion.animator = animator;
        motion.visual = model.transform;
        var hand = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Fist.R");
        if (hand)
        {
            var grip = new GameObject("Agarre de herramientas").transform;
            grip.SetParent(hand, false);
            grip.localRotation = Quaternion.Euler(0, 0, 90);
            motion.grip = grip;
        }
        var primitiveSuit = player.transform.Find("Traje de mantenimiento");
        if (primitiveSuit) primitiveSuit.gameObject.SetActive(false);
        player.ConfigureCamera(new Vector3(.38f, 1.95f, -3.1f));
        player.view.fieldOfView = 65;
        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(motion);
    }
    static void Blend(AnimatorController controller, string name, AnimationClip idle, AnimationClip walk, AnimationClip run, float walking, float running)
    {
        var tree = new BlendTree { name = name, blendParameter = "Speed", useAutomaticThresholds = false, blendType = BlendTreeType.Simple1D };
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(idle, 0); tree.AddChild(walk, walking); tree.AddChild(run, running);
        var children = tree.children;
        children[1].timeScale = name == "Crank" ? .65f : 1f;
        children[2].timeScale = name == "Crank" ? 1.1f : 1.75f;
        tree.children = children;
        var s = controller.layers[0].stateMachine.AddState(name); s.motion = tree;
    }
    static AnimationClip HoldPose(GameObject model, AnimationClip source, string name)
    {
        source.SampleAnimation(model, source.length * .55f);
        var clip = new AnimationClip { name = name, frameRate = 30 };
        foreach (var bone in model.GetComponentsInChildren<Transform>())
        {
            if (bone == model.transform) continue;
            string path = AnimationUtility.CalculateTransformPath(bone, model.transform);
            Rotation(clip, path, bone.localRotation, 1);
            for (int axis = 0; axis < 3; axis++)
            {
                clip.SetCurve(path, typeof(Transform), "m_LocalPosition." + "xyz"[axis], AnimationCurve.Constant(0, 1, bone.localPosition[axis]));
                clip.SetCurve(path, typeof(Transform), "m_LocalScale." + "xyz"[axis], AnimationCurve.Constant(0, 1, bone.localScale[axis]));
            }
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip,settings);
        return SaveClip(clip,name);
    }
    static AnimationClip RunClip(GameObject model, AnimationClip walk, int index)
    {
        var clip = UnityEngine.Object.Instantiate(walk); clip.name = "Carrera" + index;
        var legs = model.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("UpperLeg") || t.name.StartsWith("LowerLeg") || t.name == "Torso").ToArray();
        var resting = legs.Select(t => t.localRotation).ToArray();
        var curves = legs.Select(t => new[] {new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()}).ToArray();
        for (int frame = 0; frame <= 30; frame++)
        {
            float time = walk.length * frame / 30;
            walk.SampleAnimation(model,time);
            for (int i = 0; i < legs.Length; i++)
            {
                Quaternion q = legs[i].name == "Torso" ? legs[i].localRotation * Quaternion.Euler(12,0,0) : Quaternion.SlerpUnclamped(resting[i],legs[i].localRotation,1.3f);
                for (int c = 0; c < 4; c++) curves[i][c].AddKey(time,q[c]);
            }
        }
        for (int i = 0; i < legs.Length; i++) for (int c = 0; c < 4; c++) clip.SetCurve(AnimationUtility.CalculateTransformPath(legs[i],model.transform),typeof(Transform),"m_LocalRotation."+"xyzw"[c],curves[i][c]);
        clip.EnsureQuaternionContinuity(); walk.SampleAnimation(model,0);
        return SaveClip(clip,clip.name);
    }
    static AnimationClip Pose(GameObject model, AnimationClip source, AnimationClip carry, string name)
    {
        var clip = UnityEngine.Object.Instantiate(source); clip.name = name;
        carry.SampleAnimation(model, carry.length * .2f);
        foreach (var bone in model.GetComponentsInChildren<Transform>())
        {
            string lower = bone.name.ToLowerInvariant();
            bool upper = lower.Contains("upperarm") || lower.Contains("upper_arm") || lower.Contains("arm.") || lower.StartsWith("arm_");
            bool fore = lower.Contains("forearm") || lower.Contains("lowerarm") || lower.Contains("lower_arm");
            if ((!upper && !fore) || !lower.EndsWith(".r")) continue;
            string path = AnimationUtility.CalculateTransformPath(bone, model.transform);
            Quaternion q = bone.localRotation;
            Rotation(clip, path, q, source.length);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, settings);
        return SaveClip(clip, name);
    }
    static AnimationClip Recoil(GameObject model, AnimationClip idle, int index)
    {
        var clip = UnityEngine.Object.Instantiate(idle); string name = "Respingo" + index; clip.name = name;
        foreach (var bone in model.GetComponentsInChildren<Transform>().Where(t => t.name.ToLowerInvariant().Contains("spine")))
        {
            string path = AnimationUtility.CalculateTransformPath(bone, model.transform);
            var a = bone.localRotation; var b = a * Quaternion.Euler(-18, 0, 5);
            for (int c = 0; c < 4; c++) clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[c], new AnimationCurve(new Keyframe(0, a[c]), new Keyframe(.1f, b[c]), new Keyframe(.35f, a[c])));
        }
        return SaveClip(clip, name);
    }
    static void Rotation(AnimationClip clip, string path, Quaternion rotation, float duration)
    {
        for (int c = 0; c < 4; c++) clip.SetCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[c], AnimationCurve.Constant(0, Mathf.Max(.1f, duration), rotation[c]));
        clip.EnsureQuaternionContinuity();
    }
    static AnimationClip SaveClip(AnimationClip clip, string name)
    {
        string path = Art + "/" + name + ".anim";
        var previous = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (previous) { EditorUtility.CopySerialized(clip, previous); UnityEngine.Object.DestroyImmediate(clip); return previous; }
        AssetDatabase.CreateAsset(clip, path); return clip;
    }
    static void Candle(CarryObject candle)
    {
        var old = candle.transform.Find("Vela importada");
        if (old) return;
        var model = Model("Assets/External/QuaterniusDungeon/Candle.fbx", candle.transform, "Vela importada", .32f);
        foreach (var r in model.GetComponentsInChildren<Renderer>())
            r.sharedMaterials = r.sharedMaterials.Select((m, n) => Material("Cera y soporte " + n, m ? m.color : new Color(.65f, .55f, .36f), n == 0 ? .4f : 0)).ToArray();
        foreach (var r in candle.GetComponentsInChildren<Renderer>())
            if (!r.transform.IsChildOf(model.transform) && !r.name.StartsWith("Llama")) r.enabled = false;
        candle.candleLight.range = 7;
        candle.candleLight.shadows = LightShadows.Soft;
        EditorUtility.SetDirty(candle);
    }
    static void Crank(CarryObject crank)
    {
        if (crank.transform.Find("Manivela industrial")) return;
        var mesh = Model("Assets/External/Plumbing/Pipe Valve.fbx", crank.transform, "Manivela industrial", .38f);
        foreach (var r in crank.GetComponentsInChildren<Renderer>()) if (!r.transform.IsChildOf(mesh.transform)) r.enabled = false;
        foreach (var r in mesh.GetComponentsInChildren<Renderer>()) r.sharedMaterials = r.sharedMaterials.Select(m => Material("Acero oxidado", new Color(.24f,.19f,.13f), .72f)).ToArray();
    }
    static void Flame(CarryObject candle)
    {
        var animation = candle.GetComponent<DemoCandleFlame>();
        if (!animation || candle.transform.Find("Llama animada")) return;
        Material warm = animation.flame.GetComponent<Renderer>().sharedMaterial;
        animation.flame.gameObject.SetActive(false);
        string path = Art + "/Llama.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!mesh)
        {
            var vertices = new Vector3[32]; var triangles = new int[8 * 3 * 6];
            float[] heights = { 0, .025f, .052f, .08f }, radii = { .008f, .014f, .007f, .0003f };
            for (int ring = 0; ring < 4; ring++) for (int side = 0; side < 8; side++)
            {
                float a = side * Mathf.PI / 4;
                vertices[ring * 8 + side] = new Vector3(Mathf.Cos(a) * radii[ring] + ring * .0015f, heights[ring], Mathf.Sin(a) * radii[ring]);
            }
            int at = 0;
            for (int ring = 0; ring < 3; ring++) for (int side = 0; side < 8; side++)
            {
                int a = ring * 8 + side, b = ring * 8 + (side + 1) % 8, c = a + 8, d = b + 8;
                triangles[at++] = a; triangles[at++] = c; triangles[at++] = b;
                triangles[at++] = b; triangles[at++] = c; triangles[at++] = d;
            }
            mesh = new Mesh { name = "Llama de cera de 8 cm", vertices = vertices, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path);
        }
        var flame = new GameObject("Llama animada"); flame.transform.SetParent(candle.transform,false); flame.transform.localPosition = Vector3.up * .32f;
        flame.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = flame.AddComponent<MeshRenderer>(); renderer.sharedMaterial = warm; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        animation.flame = flame.transform;
        candle.candleLight.transform.localPosition = new Vector3(0,.4f,0);
        EditorUtility.SetDirty(animation); EditorUtility.SetDirty(candle);
    }
    static void Machine(DemoMechanism mechanism)
    {
        if (mechanism.transform.Find("Carcasa industrial importada")) return;
        var mesh = Model("Assets/External/Plumbing/Circuit Breaker.fbx", mechanism.transform, "Carcasa industrial importada", 1.45f);
        mesh.transform.localPosition += Vector3.up * .05f;
        // Keep existing wheel, socket, collider and label references.
        foreach (var r in mechanism.GetComponentsInChildren<Renderer>())
            if (!r.transform.IsChildOf(mesh.transform) && r.transform != mechanism.wheel && !r.GetComponent<TextMesh>() && !r.transform.IsChildOf(mechanism.wheel)) r.enabled = false;
    }
    static void BuildStation()
    {
        var root = new GameObject("Panel industrial");
        Model("Assets/External/Plumbing/Circuit Breaker.fbx", root.transform, "Carcasa", 1.45f);
        Directory.CreateDirectory(Art + "/Resources/Production");
        PrefabUtility.SaveAsPrefabAsset(root, Art + "/Resources/Production/Station.prefab");
        UnityEngine.Object.DestroyImmediate(root);
    }
    static void BuildSpider()
    {
        const string path = "Assets/External/RecintoSpider/Spider.fbx";
        ImportRig(path);
        var root = new GameObject("Sombra que gatea");
        var model = Model(path, root.transform, "Criatura con rig", .48f);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/External/RecintoSpider/Spider.png");
        var skin = Material("Piel mojada de la sombra", new Color(.24f,.28f,.22f), .12f); skin.SetTexture("_BaseMap", texture);
        foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = skin;
        var animator = model.GetComponent<Animator>(); if (!animator) animator = model.AddComponent<Animator>();
        string controllerPath = Art + "/Sombra.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        foreach (var state in machine.states) machine.RemoveState(state.state);
        var crawl = machine.AddState("Crawl"); crawl.motion = Clip(path, "walk"); machine.defaultState = crawl;
        var idle = machine.AddState("Idle"); idle.motion = Clip(path, "idle");
        animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
        PrefabUtility.SaveAsPrefabAsset(root, Art + "/Resources/Production/Shadow.prefab");
        UnityEngine.Object.DestroyImmediate(root);
    }
}
#endif
