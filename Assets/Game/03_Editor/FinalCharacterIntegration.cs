using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gamejam2026.Editor
{
    public static class FinalCharacterIntegration
    {
        const string ScenePath = "Assets/Game/00_Scenes/Demo.unity";
        const string LibraryPath = "Assets/External/UniversalAnimationLibrary/AnimationLibrary_Unity_Standard.fbx";
        const string PlayersPath = "Assets/Resources/Players/";
        const string Output = "Assets/External/UniversalAnimationLibrary/Adapted/";

        [MenuItem("Gamejam/Producción/Instalar personajes finales")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Guarda la escena y sal de Play antes de instalar los personajes.");
            EnsureHuman(LibraryPath);
            EnsureHuman(PlayersPath + "male_casual.fbx");
            EnsureHuman(PlayersPath + "female_secretary.fbx");
            var controller = Controller();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var players = UnityEngine.Object.FindObjectsByType<DemoPlayer>();
            if (players.Length != 2) throw new InvalidOperationException("Demo.unity debe tener dos jugadores.");
            foreach (var player in players) Install(player, controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("FinalCharacterIntegration: J1 male_casual y J2 female_secretary instalados con animaciones Humanoid.");
        }

        static void EnsureHuman(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Falta " + path);
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (!avatar || !avatar.isValid || !avatar.isHuman)
                throw new InvalidOperationException("Avatar Humanoid inválido: " + path);
        }

        static AnimationClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(LibraryPath).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name.EndsWith("|" + name, StringComparison.OrdinalIgnoreCase));
            if (!clip) throw new InvalidOperationException("Falta clip " + name);
            return clip;
        }

        static BlendTree Blend(AnimatorController controller, string name, params (string clip, float speed)[] entries)
        {
            var tree = new BlendTree { name = name, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            foreach (var entry in entries) tree.AddChild(Clip(entry.clip), entry.speed);
            return tree;
        }

        static AnimatorController Controller()
        {
            string path = Output + "FinalCrew.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing) return existing;
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Candle", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Crank", AnimatorControllerParameterType.Bool);
            var layer = controller.layers[0];
            layer.iKPass = true;
            controller.layers = new[] { layer };
            var machine = layer.stateMachine;
            var locomotion = machine.AddState("Locomotion");
            locomotion.motion = Blend(controller, "Caminar y correr", ("Idle_Loop", 0), ("Walk_Loop", 3.2f), ("Sprint_Loop", 5.6f));
            machine.defaultState = locomotion;
            var candle = machine.AddState("Candle");
            candle.motion = Blend(controller, "Vela", ("Idle_Torch_Loop", 0), ("Walk_Loop", 3.2f), ("Sprint_Loop", 5.6f));
            var crank = machine.AddState("Crank");
            crank.motion = Blend(controller, "Manivela", ("Idle_Loop", 0), ("Walk_Loop", 2.08f), ("Sprint_Loop", 3.64f));
            var interact = machine.AddState("Interact");
            interact.motion = Clip("Interact");
            var useEnter = machine.AddState("UseEnter");
            useEnter.motion = Clip("Crouch_Idle_Loop");
            var transition = useEnter.AddTransition(interact);
            transition.hasExitTime = true;
            transition.exitTime = 0.15f;
            transition.duration = 0.1f;
            var recoil = machine.AddState("Recoil");
            recoil.motion = Clip("Hit_Chest");
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static Material Material(int index)
        {
            string path = Output + (index == 0 ? "Male" : "Secretary") + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayersPath + (index == 0 ? "man_tex" : "secretary_tex") + ".png");
            if (!texture) throw new InvalidOperationException("Falta textura del personaje " + index);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("Falta URP/Lit");
            var material = new Material(shader) { name = index == 0 ? "Male" : "Secretary" };
            material.SetTexture("_BaseMap", texture);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void Install(DemoPlayer player, AnimatorController controller)
        {
            int index = player.playerIndex;
            string name = index == 0 ? "male_casual" : "female_secretary";
            string path = PlayersPath + name + ".fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().First();
            var motion = player.GetComponent<DemoMotion>();
            if (!prefab || !motion || !motion.visual || !player.hand)
                throw new InvalidOperationException("Jugador incompleto: " + player.name);
            Transform old = motion.visual;
            Transform replacement = player.transform.Find(index == 0 ? "J1 - Male" : "J2 - Secretaria");
            if (!replacement)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
                instance.name = index == 0 ? "J1 - Male" : "J2 - Secretaria";
                replacement = instance.transform;
            }
            replacement.localPosition = Vector3.zero;
            replacement.localRotation = Quaternion.identity;
            replacement.localScale = Vector3.one;
            foreach (var collider in replacement.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            var renderer = replacement.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (!renderer) throw new InvalidOperationException("Modelo sin piel: " + name);
            var material = Material(index);
            renderer.sharedMaterials = Enumerable.Repeat(material, Math.Max(1, renderer.sharedMaterials.Length)).ToArray();
            var bounds = renderer.bounds;
            if (bounds.size.y < 0.1f) throw new InvalidOperationException("Altura inválida de " + name);
            replacement.localScale = Vector3.one * (1.7f / bounds.size.y);
            bounds = renderer.bounds;
            Vector3 bottom = player.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            replacement.localPosition = -bottom;

            var animator = replacement.GetComponent<Animator>() ?? replacement.gameObject.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (!rightHand) throw new InvalidOperationException("Falta mano derecha de " + name);
            var grip = rightHand.Find("Agarre de vela y herramientas");
            if (!grip)
            {
                grip = new GameObject("Agarre de vela y herramientas").transform;
                grip.SetParent(rightHand, false);
            }
            grip.localPosition = Vector3.zero;
            grip.localRotation = Quaternion.identity;
            if (old != replacement) old.gameObject.SetActive(false);
            motion.visual = replacement;
            motion.animator = animator;
            motion.grip = grip;
            motion.legs = new[] { animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.RightUpperLeg) };
            motion.arms = new[] { animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), animator.GetBoneTransform(HumanBodyBones.RightUpperArm) };
            EditorUtility.SetDirty(motion);
            EditorUtility.SetDirty(animator);
        }

        public static void Verify()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var player in UnityEngine.Object.FindObjectsByType<DemoPlayer>())
            {
                string expected = player.playerIndex == 0 ? "J1 - Male" : "J2 - Secretaria";
                var motion = player.GetComponent<DemoMotion>();
                if (!motion || !motion.visual || motion.visual.name != expected || !motion.animator ||
                    !motion.animator.avatar || !motion.animator.avatar.isValid || !motion.animator.avatar.isHuman ||
                    !motion.grip || !motion.grip.IsChildOf(motion.visual) ||
                    motion.animator.runtimeAnimatorController != AssetDatabase.LoadAssetAtPath<AnimatorController>(Output + "FinalCrew.controller"))
                    throw new InvalidOperationException("Personaje final incompleto: J" + (player.playerIndex + 1));
                Debug.Log("FinalCharacterIntegration VERIFY J" + (player.playerIndex + 1) + ": " + expected + " visible, Humanoid, mano y animaciones OK");
            }
        }
    }
}
