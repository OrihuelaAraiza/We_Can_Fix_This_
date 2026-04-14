using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class FixieAnimatorRebuilder
{
    private const string ControllerPath = "Assets/Art/Characters/Fixies/Fixie_AnimatorController.controller";
    private const string PreferredHumanoidModelPath = "Assets/Art/Models/Temp/Players/Astronaut_FinnTheFrog.fbx";
    private static readonly FixieBinding[] FixieBindings =
    {
        new FixieBinding(
            "Fixie_P1",
            "Assets/Art/Models/Temp/Players/Astronaut_FinnTheFrog.fbx",
            "Assets/Art/Characters/Fixies/Prefabs/Fixie_P1.prefab",
            "Assets/Art/Characters/Fixies/Fixie_P1_Override.overrideController"),
        new FixieBinding(
            "Fixie_P2",
            "Assets/Art/Models/Temp/Players/Astronaut_FernandoTheFlamingo.fbx",
            "Assets/Art/Characters/Fixies/Prefabs/Fixie_P2.prefab",
            "Assets/Art/Characters/Fixies/Fixie_P2_Override.overrideController"),
        new FixieBinding(
            "Fixie_P3",
            "Assets/Art/Models/Temp/Players/Astronaut_BarbaraTheBee.fbx",
            "Assets/Art/Characters/Fixies/Prefabs/Fixie_P3.prefab",
            "Assets/Art/Characters/Fixies/Fixie_P3_Override.overrideController"),
    };
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

        TryResolveOptionalClips(out AnimationClip jump, out AnimationClip fall);

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[FixieAnimatorRebuilder] Controller no encontrado: {ControllerPath}");
            return;
        }

        RebuildController(controller, idle, walk, run, jump, fall);
        EnsurePerFixieOverrides(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[FixieAnimatorRebuilder] Controller reconstruido con clips: {idle.name}, {walk.name}, {run.name}");
    }

    private static void TryAutoRebuild()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            return;

        if (NeedsRebuild(controller))
        {
            RebuildFromImportedFbx();
            return;
        }

        EnsurePerFixieOverrides(controller);
        AssetDatabase.SaveAssets();
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

        if (clipPaths.Length == 0 || clipPaths.Any(path => path != PreferredHumanoidModelPath))
            return true;

        // Rebuild if any state still has writeDefaultValues enabled (causes build deformation)
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state.writeDefaultValues)
                return true;
        }

        // Rebuild if IsGrounded parameter is missing (new architecture needs it)
        bool hasIsGrounded = controller.parameters.Any(p => p.name == "IsGrounded");
        if (!hasIsGrounded)
            return true;

        return false;
    }

    private static void EnsurePerFixieOverrides(AnimatorController baseController)
    {
        if (baseController == null)
            return;

        foreach (FixieBinding binding in FixieBindings)
            EnsureOverrideAndPrefabBinding(baseController, binding);
    }

    private static void EnsureOverrideAndPrefabBinding(AnimatorController baseController, FixieBinding binding)
    {
        if (!TryResolveClipsFromModel(binding.ModelPath, out AnimationClip idle, out AnimationClip walk, out AnimationClip run))
        {
            Debug.LogWarning($"[FixieAnimatorRebuilder] Clips faltantes para {binding.ModelPath}");
            return;
        }

        AnimatorOverrideController overrideController =
            AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(binding.OverrideControllerPath);

        if (overrideController == null)
        {
            overrideController = new AnimatorOverrideController(baseController);
            AssetDatabase.CreateAsset(overrideController, binding.OverrideControllerPath);
        }

        if (overrideController.runtimeAnimatorController != baseController)
            overrideController.runtimeAnimatorController = baseController;

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrideController.GetOverrides(overrides);

        // Also resolve optional clips from this model
        TryResolveOptionalClipsFromModel(binding.ModelPath, out AnimationClip jump, out AnimationClip fall);

        bool updated = false;
        for (int i = 0; i < overrides.Count; i++)
        {
            AnimationClip originalClip = overrides[i].Key;
            AnimationClip replacement = ResolveReplacementClip(originalClip, idle, walk, run, jump, fall);
            if (replacement == null || overrides[i].Value == replacement)
                continue;

            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(originalClip, replacement);
            updated = true;
        }

        if (updated)
            overrideController.ApplyOverrides(overrides);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(binding.PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[FixieAnimatorRebuilder] Prefab no encontrado: {binding.PrefabPath}");
            return;
        }

        Animator animator = prefab.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning($"[FixieAnimatorRebuilder] Animator no encontrado en {binding.PrefabPath}");
            return;
        }

        if (animator.runtimeAnimatorController != overrideController)
        {
            animator.runtimeAnimatorController = overrideController;
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(prefab);
        }
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

    private static void TryResolveOptionalClips(out AnimationClip jump, out AnimationClip fall)
    {
        jump = null;
        fall = null;

        foreach (string modelPath in CandidateModelPaths)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            if (assets == null || assets.Length == 0)
                continue;

            jump ??= assets.OfType<AnimationClip>().FirstOrDefault(clip => clip.name.EndsWith("|Jump"));
            fall ??= assets.OfType<AnimationClip>().FirstOrDefault(clip => clip.name.EndsWith("|Jump_Idle"));

            if (jump != null && fall != null)
                return;
        }
    }

    private static void TryResolveOptionalClipsFromModel(string modelPath, out AnimationClip jump, out AnimationClip fall)
    {
        jump = null;
        fall = null;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
        if (assets == null || assets.Length == 0)
            return;

        jump = assets.OfType<AnimationClip>().FirstOrDefault(clip => clip.name.EndsWith("|Jump"));
        fall = assets.OfType<AnimationClip>().FirstOrDefault(clip => clip.name.EndsWith("|Jump_Idle"));
    }

    private static bool TryResolveClipsFromModel(string modelPath, out AnimationClip idle, out AnimationClip walk, out AnimationClip run)
    {
        idle = null;
        walk = null;
        run = null;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
        if (assets == null || assets.Length == 0)
            return false;

        idle = assets.OfType<AnimationClip>().FirstOrDefault(clip => clip.name.EndsWith("|Idle"));
        walk = assets.OfType<AnimationClip>().FirstOrDefault(clip => clip.name.EndsWith("|Walk"));
        run = assets.OfType<AnimationClip>().FirstOrDefault(clip => clip.name.EndsWith("|Run"));

        return idle != null && walk != null && run != null;
    }

    private static AnimationClip ResolveReplacementClip(AnimationClip originalClip,
        AnimationClip idle, AnimationClip walk, AnimationClip run,
        AnimationClip jump, AnimationClip fall)
    {
        if (originalClip == null)
            return null;

        if (originalClip.name.EndsWith("|Idle"))
            return idle;
        if (originalClip.name.EndsWith("|Walk"))
            return walk;
        if (originalClip.name.EndsWith("|Run"))
            return run;
        if (originalClip.name.EndsWith("|Jump"))
            return jump;
        if (originalClip.name.EndsWith("|Jump_Idle"))
            return fall;

        return null;
    }

    private static void RebuildController(AnimatorController controller,
        AnimationClip idle, AnimationClip walk, AnimationClip run,
        AnimationClip jump, AnimationClip fall)
    {
        // Clear all parameters
        foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
            controller.RemoveParameter(parameter);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        // Clear all states
        foreach (ChildAnimatorState childState in stateMachine.states.ToArray())
            stateMachine.RemoveState(childState.state);
        foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines.ToArray())
            stateMachine.RemoveStateMachine(childStateMachine.stateMachine);

        // Locomotion BlendTree (Idle → Walk → Run)
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
        locomotion.writeDefaultValues = false;
        stateMachine.defaultState = locomotion;

        // Fall state (if we have a jump/fall clip)
        AnimationClip fallClip = fall ?? jump;
        if (fallClip != null)
        {
            AnimatorState fallState = stateMachine.AddState("Fall");
            fallState.motion = fallClip;
            fallState.writeDefaultValues = false;

            // Locomotion → Fall : IsGrounded == false
            AnimatorStateTransition toFall = locomotion.AddTransition(fallState);
            toFall.hasExitTime = false;
            toFall.duration = 0.15f;
            toFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");

            // Fall → Locomotion : IsGrounded == true
            AnimatorStateTransition toLoco = fallState.AddTransition(locomotion);
            toLoco.hasExitTime = false;
            toLoco.duration = 0.1f;
            toLoco.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
        }
    }

    private readonly struct FixieBinding
    {
        public FixieBinding(string name, string modelPath, string prefabPath, string overrideControllerPath)
        {
            Name = name;
            ModelPath = modelPath;
            PrefabPath = prefabPath;
            OverrideControllerPath = overrideControllerPath;
        }

        public string Name { get; }
        public string ModelPath { get; }
        public string PrefabPath { get; }
        public string OverrideControllerPath { get; }
    }
}
