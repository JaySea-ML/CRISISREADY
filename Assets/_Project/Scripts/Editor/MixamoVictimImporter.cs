using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MRCrisisTrainer.EditorTools
{
    /// <summary>
    /// AssetPostprocessor który dla Mixamo Remy FBX-ów ustawia rig na Humanoid,
    /// generuje Avatar, oraz po imporcie buduje AnimatorController z stanami
    /// Idle -> FallDown -> OnGround -> Stabilized.
    /// </summary>
    public class MixamoVictimImporter : AssetPostprocessor
    {
        private const string VictimFolder = "Assets/_Project/Models/Victim";
        private const string IntruderFolder = "Assets/_Project/External/Act3/Intruder";

        void OnPreprocessModel()
        {
            var importer = (ModelImporter)assetImporter;

            if (assetPath.StartsWith(VictimFolder))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.optimizeGameObjects = false;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.External;
                importer.useFileScale = true;
                importer.globalScale = 1f;
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            }
            else if (assetPath.StartsWith(IntruderFolder))
            {
                // Chodzący intruz (Mixamo): generyczny rig, animacja zapętlona, bez materiałów (ciemna sylwetka w grze)
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.useFileScale = true;
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                var clips = importer.defaultClipAnimations;
                for (int i = 0; i < clips.Length; i++) clips[i].loopTime = true;
                if (clips.Length > 0) importer.clipAnimations = clips;
            }
        }

        void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!assetPath.StartsWith(VictimFolder)) return;
            clip.legacy = false;

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (fileName.Contains("DyingBackwards") || fileName.Contains("StandUp"))
            {
                clip.wrapMode = WrapMode.Once;
            }
        }
    }

    public static class VictimAnimatorBuilder
    {
        private const string VictimFolder = "Assets/_Project/Models/Victim";
        private const string FallFbxPath = VictimFolder + "/Remy_DyingBackwards.fbx";
        private const string StandFbxPath = VictimFolder + "/Remy_StandUp.fbx";
        private const string ControllerPath = VictimFolder + "/VictimAnimator.controller";

        [MenuItem("MRCrisis/Build Victim Animator Controller", priority = 10)]
        public static void BuildController()
        {
            if (!File.Exists(FallFbxPath) || !File.Exists(StandFbxPath))
            {
                EditorUtility.DisplayDialog(
                    "MR Crisis Trainer",
                    $"Brakuje FBX-ów Mixamo w {VictimFolder}.\nOczekiwane pliki:\n- Remy_DyingBackwards.fbx\n- Remy_StandUp.fbx",
                    "OK");
                return;
            }

            AnimationClip fallClip = ExtractAnimationClip(FallFbxPath);
            AnimationClip standClip = ExtractAnimationClip(StandFbxPath);
            if (fallClip == null || standClip == null)
            {
                Debug.LogError("[VictimAnimator] Nie mogę znaleźć animacji w FBX-ach.");
                return;
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Fall", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("OnGround", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Stabilized", AnimatorControllerParameterType.Trigger);

            var rootMachine = controller.layers[0].stateMachine;

            var fall = rootMachine.AddState("FallDown");
            fall.motion = fallClip;
            rootMachine.defaultState = fall;

            var onGround = rootMachine.AddState("OnGround");
            onGround.motion = fallClip;
            onGround.speed = 0f;

            var stabilized = rootMachine.AddState("Stabilized");
            stabilized.motion = standClip;

            // FallDown ends -> OnGround (lying on the ground, last frame frozen)
            var fallToGround = fall.AddTransition(onGround);
            fallToGround.hasExitTime = true;
            fallToGround.exitTime = 0.95f;
            fallToGround.duration = 0.1f;

            // After successful CPR, victim stands up
            AddTriggerTransition(onGround, stabilized, "Stabilized");
            AddTriggerTransition(fall, stabilized, "Stabilized");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[VictimAnimator] Controller built at {ControllerPath}");
            EditorUtility.DisplayDialog(
                "MR Crisis Trainer",
                "Victim Animator Controller zbudowany.\n\nMożesz teraz uruchomić MRCrisis -> Build Only Act 1 Scene, żeby podstawić Remy w scenie.",
                "OK");
        }

        private static void AddTriggerTransition(AnimatorState from, AnimatorState to, string triggerName)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.15f;
            transition.AddCondition(AnimatorConditionMode.If, 0, triggerName);
        }

        private static AnimationClip ExtractAnimationClip(string fbxPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }
    }
}
