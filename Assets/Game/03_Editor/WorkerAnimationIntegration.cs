#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamejam2026.Editor
{
    // The existing Quaternius workers use a small Generic skeleton, not a Humanoid avatar.
    // Bake rotations from the CC0 library onto their actual bones and keep the hand-held clips.
    public static class WorkerAnimationIntegration
    {
        const string Library = "Assets/External/UniversalAnimationLibrary/AnimationLibrary_Unity_Standard.fbx";
        const string ScenePath = "Assets/Game/00_Scenes/Demo.unity";
        const string Output = "Assets/External/UniversalAnimationLibrary/Adapted";
        static readonly (string source, string target)[] Bones =
        {
            ("DEF-hips", "Hips"), ("DEF-spine.002", "Torso"),
            ("DEF-neck", "Neck"), ("DEF-head", "Head"),
            ("DEF-shoulder.L", "Shoulder.L"), ("DEF-upper_arm.L", "UpperArm.L"),
            ("DEF-forearm.L", "LowerArm.L"), ("DEF-shoulder.R", "Shoulder.R"),
            ("DEF-upper_arm.R", "UpperArm.R"), ("DEF-forearm.R", "LowerArm.R"),
            ("DEF-thigh.L", "UpperLeg.L"), ("DEF-shin.L", "LowerLeg.L"),
            ("DEF-foot.L", "Foot.L"), ("DEF-thigh.R", "UpperLeg.R"),
            ("DEF-shin.R", "LowerLeg.R"), ("DEF-foot.R", "Foot.R")
        };
        static readonly string[] ImportedMotions = { "Idle_Loop", "Walk_Loop", "Sprint_Loop", "Crouch_Idle_Loop", "Interact", "Hit_Chest" };

        public static void Verify()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var player in UnityEngine.Object.FindObjectsByType<DemoPlayer>())
            {
                int index = player.playerIndex;
                var motion = player.GetComponent<DemoMotion>();
                if (!motion || !motion.animator || !motion.grip || !player.hand)
                    throw new InvalidOperationException("Rig o mano incompletos en J" + (index + 1));
                var controller = motion.animator.runtimeAnimatorController as AnimatorController;
                if (!controller || !controller.name.Equals("Operario" + index))
                    throw new InvalidOperationException("Controlador incorrecto en J" + (index + 1));
                foreach (string name in new[] { "Idle_Loop", "Walk_Loop", "Sprint_Loop", "Crouch_Idle_Loop", "Hit_Chest" })
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Output + "/Operario" + index + "_" + name + ".anim");
                    var bindings = clip ? AnimationUtility.GetCurveBindings(clip) : Array.Empty<EditorCurveBinding>();
                    if (bindings.Length < Bones.Length * 4)
                        throw new InvalidOperationException("Clip incompleto " + name + " J" + (index + 1));
                    float motionRange = bindings.Select(b => AnimationUtility.GetEditorCurve(clip, b))
                        .Where(c => c != null && c.length > 1)
                        .Select(c => c.keys.Max(k => k.value) - c.keys.Min(k => k.value))
                        .DefaultIfEmpty(0f).Max();
                    if (motionRange < 0.02f)
                        throw new InvalidOperationException("Clip sin movimiento real " + name + " J" + (index + 1));
                }
                if (!controller.animationClips.Contains(Existing(index, "VelaIdle")) ||
                    !controller.animationClips.Contains(Existing(index, "VelaWalk")) ||
                    !controller.animationClips.Contains(Existing(index, "VelaRun")))
                    throw new InvalidOperationException("Faltan clips de agarre de vela en J" + (index + 1));
                Debug.Log("WorkerAnimationIntegration VERIFY J" + (index + 1) + ": controlador, huesos, clips y agarre de vela OK");
            }
        }

        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Sal de Play antes de integrar animaciones.");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Guarda la escena abierta antes de integrar animaciones.");
            var importer = AssetImporter.GetAtPath(Library) as ModelImporter;
            if (!importer) throw new InvalidOperationException("No está la biblioteca de animaciones.");
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
                importer.SaveAndReimport();
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Library);
            if (!prefab) throw new InvalidOperationException("Unity no importó el FBX de animaciones.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var source = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var players = UnityEngine.Object.FindObjectsByType<DemoPlayer>();
            if (players.Length != 2) throw new InvalidOperationException("Se esperaban dos jugadores en Demo.unity.");
            if (!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder("Assets/External/UniversalAnimationLibrary", "Adapted");
            try
            {
                foreach (var player in players)
                {
                    var motion = player.GetComponent<DemoMotion>();
                    if (!motion || !motion.animator || !motion.grip || !player.hand)
                        throw new InvalidOperationException("Falta rig o mano en " + player.name);
                    int index = player.playerIndex;
                    var baked = new Dictionary<string, AnimationClip>();
                    foreach (var name in ImportedMotions) baked[name] = Bake(source.transform, motion.visual, Clip(name), index, name);
                    var controller = CreateController(index, baked);
                    motion.animator.runtimeAnimatorController = controller;
                    motion.animator.applyRootMotion = false;
                    EditorUtility.SetDirty(motion.animator);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("WorkerAnimationIntegration: J1 y J2 usan clips adaptados; clips de vela y manivela conservados.");
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        static AnimationClip Clip(string token)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(Library).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name.EndsWith("|" + token, StringComparison.OrdinalIgnoreCase));
            if (!clip) throw new InvalidOperationException("Falta el clip " + token);
            return clip;
        }

        static Transform Bone(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name &&
                (name != "Body" || t.parent.name == "Bone"));
        }

        static string Path(Transform root, Transform child)
        {
            var parts = new List<string>();
            for (var t = child; t != root; t = t.parent)
            {
                if (!t) throw new InvalidOperationException("Hueso fuera del cuerpo");
                parts.Add(t.name);
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        static AnimationClip Bake(Transform sourceRoot, Transform targetRoot, AnimationClip input, int index, string name)
        {
            Clip("A_TPose").SampleAnimation(sourceRoot.gameObject, 0);
            var sourceBones = Bones.Select(b => Bone(sourceRoot, b.source)).ToArray();
            var targetBones = Bones.Select(b => Bone(targetRoot, b.target)).ToArray();
            for (int i = 0; i < Bones.Length; i++)
                if (!sourceBones[i] || !targetBones[i]) throw new InvalidOperationException("Falta hueso " + Bones[i]);

            var sourceRest = sourceBones.Select(b => Quaternion.Inverse(sourceRoot.rotation) * b.rotation).ToArray();
            var targetRest = targetBones.Select(b => Quaternion.Inverse(targetRoot.rotation) * b.rotation).ToArray();
            var targetLocals = targetBones.Select(b => b.localRotation).ToArray();
            var curves = Enumerable.Range(0, Bones.Length).Select(_ => new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() }).ToArray();
            int frames = Mathf.Max(2, Mathf.CeilToInt(input.length * 30));
            for (int frame = 0; frame <= frames; frame++)
            {
                float time = input.length * frame / frames;
                input.SampleAnimation(sourceRoot.gameObject, time);
                for (int i = 0; i < Bones.Length; i++) targetBones[i].localRotation = targetLocals[i];
                for (int i = 0; i < Bones.Length; i++)
                {
                    Quaternion sampled = Quaternion.Inverse(sourceRoot.rotation) * sourceBones[i].rotation;
                    Quaternion delta = sampled * Quaternion.Inverse(sourceRest[i]);
                    targetBones[i].rotation = targetRoot.rotation * delta * targetRest[i];
                    Quaternion q = targetBones[i].localRotation;
                    if (frame > 0)
                    {
                        var previous = new Quaternion(curves[i][0].keys[frame - 1].value, curves[i][1].keys[frame - 1].value,
                            curves[i][2].keys[frame - 1].value, curves[i][3].keys[frame - 1].value);
                        if (Quaternion.Dot(previous, q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    }
                    curves[i][0].AddKey(time, q.x); curves[i][1].AddKey(time, q.y);
                    curves[i][2].AddKey(time, q.z); curves[i][3].AddKey(time, q.w);
                }
            }
            for (int i = 0; i < Bones.Length; i++) targetBones[i].localRotation = targetLocals[i];
            var output = new AnimationClip { name = "Operario" + index + "_" + name, frameRate = 30 };
            for (int i = 0; i < Bones.Length; i++)
            {
                string path = Path(targetRoot, targetBones[i]);
                string[] components = { "x", "y", "z", "w" };
                for (int c = 0; c < 4; c++)
                    AnimationUtility.SetEditorCurve(output, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + components[c]), curves[i][c]);
            }
            bool loop = name.EndsWith("Loop", StringComparison.OrdinalIgnoreCase);
            var settings = AnimationUtility.GetAnimationClipSettings(output);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(output, settings);
            string pathOut = Output + "/" + output.name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(pathOut);
            if (existing)
            {
                EditorUtility.CopySerialized(output, existing);
                UnityEngine.Object.DestroyImmediate(output);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(output, pathOut);
            return output;
        }

        static AnimationClip Existing(int index, string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/External/AOscurasProduction/" + name + index + ".anim");
            if (!clip) throw new InvalidOperationException("Falta animación existente " + name + index);
            return clip;
        }

        static BlendTree Blend(AnimatorController controller, string name, params (AnimationClip clip, float speed)[] entries)
        {
            var tree = new BlendTree { name = name, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            foreach (var entry in entries) tree.AddChild(entry.clip, entry.speed);
            return tree;
        }

        static AnimatorController CreateController(int index, Dictionary<string, AnimationClip> baked)
        {
            string path = Output + "/Operario" + index + ".controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing) return existing;
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Candle", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Crank", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var locomotion = machine.AddState("Locomotion");
            locomotion.motion = Blend(controller, "Movimiento", (baked["Idle_Loop"], 0), (baked["Walk_Loop"], 3.2f), (baked["Sprint_Loop"], 5.6f));
            machine.defaultState = locomotion;
            var candle = machine.AddState("Candle");
            candle.motion = Blend(controller, "Vela", (Existing(index, "VelaIdle"), 0), (Existing(index, "VelaWalk"), 3.2f), (Existing(index, "VelaRun"), 5.6f));
            var crank = machine.AddState("Crank");
            crank.motion = Blend(controller, "Manivela", (Existing(index, "ManivelaIdle"), 0), (Existing(index, "ManivelaLenta"), 2.08f));
            var interact = machine.AddState("Interact");
            interact.motion = Existing(index, "UsoAgachado");
            var useEnter = machine.AddState("UseEnter");
            useEnter.motion = baked["Crouch_Idle_Loop"];
            var transition = useEnter.AddTransition(interact);
            transition.hasExitTime = true;
            transition.exitTime = 0.15f;
            transition.duration = 0.1f;
            var recoil = machine.AddState("Recoil");
            recoil.motion = baked["Hit_Chest"];
            EditorUtility.SetDirty(controller);
            return controller;
        }
    }
}
#endif
