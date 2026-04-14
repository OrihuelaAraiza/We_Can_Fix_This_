using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class FixieAnimatorRebuilder
{
    private const string ControllerPath = "Assets/Art/Characters/Fixies/Fixie_AnimatorController.controller";
    private const string PreferredHumanoidModelPath = "Assets/Art/Models/Temp/Players/Astronaut_FinnTheFrog.fbx";
    private static readonly string[] CandidateModelPaths =
    {
        "Assets/Art/Models/Temp/Players/Astronaut_FinnTheFrog.fbx",
        "Assets/Art/Models/Temp/Players/Astronaut_FernandoTheFlamingo.fbx",
        "Assets/Art/Models/Temp/Players/Astronaut_BarbaraTheBee.fbx",
        "Assets/Art/Models/Astronaut_FinnTheFrog.fbx",
        "Assets/Art/Models/Astronaut_FernandoTheFlamingo.fbx",
        "Assets/Art/Models/Astronaut_BarbaraTheBee.fbx",
    };

    [InitializeOnLoadMethod]
    private static void AutoRepairOnLoad()
    {
        EditorApplication.delayCall += TryAutoRebuild;
    }

    [MenuItem("Tools/Fixies/Rebuild Animator From Imported FBX")]
    public static void RebuildFromImportedFbx()
    {
        if (!TryResolveClips(out AnimationClip idle, out AnimationClip walk, out AnimationClip run))
        {
            Debug.LogWarning("[FixieAnimatorRebuilder] No se encontraron clips Idle/Walk/Run en Assets/Art/Models.");
            return;
        }

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[FixieAnimatorRebuilder] Controller no encontrado: {ControllerPath}");
            return;
        }

        RebuildController(controller, idle, walk, run);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FixieAnimatorRebuilder] Controller reconstruido con clips: {idle.name}, {walk.name}, {run.name}");
    }

    private static void TryAutoRebuild()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || !NeedsRebuild(controller))
            return;

        RebuildFromImportedFbx();
    }

    private static bool NeedsRebuild(AnimatorController controller)
    {
        if (controller == null)
            return false;

        AnimationClip[] clips = controller.animationClips;
        if (clips == null || clips.Length < 2)
            return true;

        if (clips.All(clip => clip != null && clip.name.Contains("Ellen")))
            return true;

        string[] clipPaths = clips
            .Where(clip => clip != null)
            .Select(AssetDatabase.GetAssetPath)
            .Distinct()
            .ToArray();

        return clipPaths.Length == 0 || clipPaths.Any(path => path != PreferredHumanoidModelPath);
    }

    private static bool TryResolveClips(out AnimationClip idle, out AnimationClip walk, out AnimationClip run)
    {
        idle = null;
        walk = null;
        run = null;

        foreach (string modelPath in CandidateModelPaths)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            if (assets == null || assets.Length == 0)
                continue;

            idle ??= assets.OfType<AnimationClip>().FirstOrDefault(clip => clip.name.EndsWith("|Idle"));
            walk ??= assets.OfType<AnimationClip>().FirstOrDefault(clip => clip.name.EndsWith("|Walk"));
            run ??= assets.OfType<AnimationClip>().FirstOrDefault(clip => clip.name.EndsWith("|Run"));

            if (idle != null && walk != null && run != null)
                return true;
        }

        return idle != null && walk != null && run != null;
    }

    private static void RebuildController(AnimatorController controller, AnimationClip idle, AnimationClip walk, AnimationClip run)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
            controller.RemoveParameter(parameter);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states.ToArray())
            stateMachine.RemoveState(childState.state);

        foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines.ToArray())
            stateMachine.RemoveStateMachine(childStateMachine.stateMachine);

        BlendTree blendTree = AssetDatabase.LoadAllAssetsAtPath(ControllerPath)
            .OfType<BlendTree>()
            .FirstOrDefault(tree => tree != null && tree.name == "FixieLocomotion");

        if (blendTree == null)
        {
            blendTree = new BlendTree { name = "FixieLocomotion" };
            AssetDatabase.AddObjectToAsset(blendTree, controller);
        }

        blendTree.blendType = BlendTreeType.Simple1D;
        blendTree.blendParameter = "Speed";
        blendTree.useAutomaticThresholds = false;
        blendTree.children = new ChildMotion[0];
        blendTree.AddChild(idle, 0f);
        blendTree.AddChild(walk, 2.2f);
        blendTree.AddChild(run, 5.2f);

        AnimatorState locomotion = stateMachine.AddState("Locomotion");
        locomotion.motion = blendTree;
        locomotion.writeDefaultValues = true;
        stateMachine.defaultState = locomotion;
    }
}
