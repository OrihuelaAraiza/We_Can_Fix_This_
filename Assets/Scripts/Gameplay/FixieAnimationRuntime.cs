using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FixieAnimationRuntime : MonoBehaviour
{
    private enum AnimationState
    {
        None,
        Idle,
        Walk,
        Run,
        Air,
    }

    private struct ResolvedAnimationData
    {
        public string SourceId;
        public string AvailableClipNames;
        public Avatar Avatar;
        public AnimationClip IdleClip;
        public AnimationClip WalkClip;
        public AnimationClip RunClip;
        public AnimationClip JumpClip;
        public AnimationClip FallClip;

        public AnimationClip AirClip => FallClip != null ? FallClip : JumpClip;
        public bool HasAnyClip => IdleClip != null || WalkClip != null || RunClip != null || JumpClip != null || FallClip != null;
        public bool IsValid => IdleClip != null && WalkClip != null && RunClip != null;

        public string DescribeClips()
        {
            string airName = AirClip != null ? AirClip.name : "NONE";
            return $"idle={GetClipName(IdleClip)} walk={GetClipName(WalkClip)} run={GetClipName(RunClip)} air={airName} available=[{AvailableClipNames}]";
        }

        public void FillMissingFrom(in ResolvedAnimationData fallback)
        {
            if (Avatar == null)
                Avatar = fallback.Avatar;

            if (IdleClip == null)
                IdleClip = fallback.IdleClip;

            if (WalkClip == null)
                WalkClip = fallback.WalkClip;

            if (RunClip == null)
                RunClip = fallback.RunClip;

            if (JumpClip == null)
                JumpClip = fallback.JumpClip;

            if (FallClip == null)
                FallClip = fallback.FallClip;

            if (string.IsNullOrWhiteSpace(SourceId))
                SourceId = fallback.SourceId;

            if (string.IsNullOrWhiteSpace(AvailableClipNames))
                AvailableClipNames = fallback.AvailableClipNames;
        }

        private static string GetClipName(AnimationClip clip)
        {
            return clip != null ? clip.name : "NONE";
        }
    }

    private static readonly Dictionary<string, string> EditorAssetPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Fixie_P1"] = "Assets/Art/Models/Temp/Players/Astronaut_FinnTheFrog.fbx",
        ["Astronaut_FinnTheFrog"] = "Assets/Art/Models/Temp/Players/Astronaut_FinnTheFrog.fbx",
        ["Fixie_P2"] = "Assets/Art/Models/Temp/Players/Astronaut_FernandoTheFlamingo.fbx",
        ["Astronaut_FernandoTheFlamingo"] = "Assets/Art/Models/Temp/Players/Astronaut_FernandoTheFlamingo.fbx",
        ["Fixie_P3"] = "Assets/Art/Models/Temp/Players/Astronaut_BarbaraTheBee.fbx",
        ["Astronaut_BarbaraTheBee"] = "Assets/Art/Models/Temp/Players/Astronaut_BarbaraTheBee.fbx",
    };

    private static FixieAnimationSet[] cachedSets;

    [Header("Bindings")]
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Animator animator;
    [SerializeField] private FixieAnimationSet animationSet;
    [SerializeField] private string resolvedSource;

    [Header("Blend")]
    [SerializeField] private float walkThreshold = 0.45f;
    [SerializeField] private float blendSpeed = 8f;
    [SerializeField] private bool animationDebugLogs = true;

    private readonly float[] currentWeights = new float[4];

    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimationState currentState;
    private ResolvedAnimationData animationData;
    private string debugSourceName = "Unknown";
    private bool fatalErrorLogged;

    public static FixieAnimationSet ResolveAnimationSet(GameObject sourcePrefab, Transform modelRoot)
    {
        EnsureCache();

        if (cachedSets == null || cachedSets.Length == 0)
            return null;

        HashSet<string> candidates = BuildCandidateNames(sourcePrefab, modelRoot, null);
        foreach (FixieAnimationSet set in cachedSets)
        {
            if (set == null)
                continue;

            foreach (string candidate in candidates)
            {
                if (set.Matches(candidate))
                    return set;
            }
        }

        return null;
    }

    public void Bind(PlayerMovement targetMovement, Transform targetVisualRoot, GameObject sourcePrefab, Animator sourceAnimator, int slotIndex)
    {
        try
        {
            fatalErrorLogged = false;
            movement = targetMovement;
            visualRoot = targetVisualRoot != null ? targetVisualRoot : transform;
            animator = null;
            animationSet = null;
            debugSourceName = sourcePrefab != null ? sourcePrefab.name : (visualRoot != null ? visualRoot.name : name);

            animationData = ResolveAnimationData(sourcePrefab, visualRoot, sourceAnimator);
            resolvedSource = animationData.SourceId;

            if (!animationData.IsValid)
            {
                LogWarning($"slot={slotIndex} no pudo resolver clips validos para '{debugSourceName}'. {animationData.DescribeClips()}");
                DestroyGraph();
                enabled = false;
                return;
            }

            animator = EnsureAnimator(visualRoot, sourceAnimator, animationData.Avatar);
            if (animator == null)
            {
                LogWarning($"slot={slotIndex} no pudo crear/obtener Animator para '{debugSourceName}'.");
                DestroyGraph();
                enabled = false;
                return;
            }

            BuildGraph();
            enabled = true;
            LogInfo($"slot={slotIndex} source='{resolvedSource}' visual='{debugSourceName}' animator='{animator.name}' {animationData.DescribeClips()}");
        }
        catch (Exception exception)
        {
            HandleException("Bind", exception);
        }
    }

    private void Update()
    {
        if (fatalErrorLogged)
            return;

        try
        {
            if (!graph.IsValid() || movement == null || mixer.Equals(default(AnimationMixerPlayable)))
                return;

            float speed = Mathf.Clamp01(movement.SpeedNormalized);
            bool grounded = movement.IsGrounded;

            float targetIdle = 0f;
            float targetWalk = 0f;
            float targetRun = 0f;
            float targetAir = 0f;

            AnimationState nextState;

            if (!grounded && animationData.AirClip != null)
            {
                targetAir = 1f;
                nextState = AnimationState.Air;
            }
            else if (speed <= walkThreshold)
            {
                float t = walkThreshold <= 0.001f ? 1f : Mathf.InverseLerp(0f, walkThreshold, speed);
                targetIdle = 1f - t;
                targetWalk = t;
                nextState = t > 0.15f ? AnimationState.Walk : AnimationState.Idle;
            }
            else
            {
                float t = Mathf.InverseLerp(walkThreshold, 1f, speed);
                targetWalk = 1f - t;
                targetRun = t;
                nextState = t > 0.45f ? AnimationState.Run : AnimationState.Walk;
            }

            currentWeights[0] = Mathf.MoveTowards(currentWeights[0], targetIdle, blendSpeed * Time.deltaTime);
            currentWeights[1] = Mathf.MoveTowards(currentWeights[1], targetWalk, blendSpeed * Time.deltaTime);
            currentWeights[2] = Mathf.MoveTowards(currentWeights[2], targetRun, blendSpeed * Time.deltaTime);
            currentWeights[3] = Mathf.MoveTowards(currentWeights[3], targetAir, blendSpeed * Time.deltaTime);

            float totalWeight = currentWeights[0] + currentWeights[1] + currentWeights[2] + currentWeights[3];
            if (totalWeight <= 0.0001f)
            {
                currentWeights[0] = 1f;
                totalWeight = 1f;
            }

            for (int i = 0; i < currentWeights.Length; i++)
                mixer.SetInputWeight(i, currentWeights[i] / totalWeight);

            if (nextState != currentState)
            {
                currentState = nextState;
                LogInfo($"state={currentState} grounded={grounded} speed={speed:0.00}");
            }
        }
        catch (Exception exception)
        {
            HandleException("Update", exception);
        }
    }

    private ResolvedAnimationData ResolveAnimationData(GameObject sourcePrefab, Transform modelRoot, Animator sourceAnimator)
    {
        HashSet<string> candidates = BuildCandidateNames(sourcePrefab, modelRoot, sourceAnimator);
        ResolvedAnimationData resolved = default;

#if UNITY_EDITOR
        if (TryResolveEditorAnimationData(candidates, sourceAnimator, out ResolvedAnimationData editorData))
            resolved.FillMissingFrom(editorData);
#endif

        if (TryResolveAnimationSetData(sourcePrefab, modelRoot, out ResolvedAnimationData setData))
            resolved.FillMissingFrom(setData);

        if (TryResolveAnimatorControllerData(sourceAnimator, out ResolvedAnimationData controllerData))
            resolved.FillMissingFrom(controllerData);

        return resolved;
    }

    private Animator EnsureAnimator(Transform root, Animator preferredAnimator, Avatar avatar)
    {
        Animator targetAnimator = preferredAnimator;

        if (targetAnimator == null && root != null)
            targetAnimator = root.GetComponent<Animator>();

        if (targetAnimator == null && root != null)
            targetAnimator = root.GetComponentInChildren<Animator>(true);

        if (targetAnimator == null && root != null)
            targetAnimator = root.gameObject.AddComponent<Animator>();

        if (targetAnimator == null)
            return null;

        if (avatar != null)
            targetAnimator.avatar = avatar;

        targetAnimator.runtimeAnimatorController = null;
        targetAnimator.enabled = true;
        targetAnimator.applyRootMotion = false;
        targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        targetAnimator.Rebind();
        targetAnimator.Update(0f);
        return targetAnimator;
    }

    private void BuildGraph()
    {
        DestroyGraph();

        graph = PlayableGraph.Create($"{name}_FixieAnimationGraph");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        mixer = AnimationMixerPlayable.Create(graph, 4);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "FixieAnimation", animator);
        output.SetSourcePlayable(mixer);

        ConnectClip(0, animationData.IdleClip);
        ConnectClip(1, animationData.WalkClip);
        ConnectClip(2, animationData.RunClip);
        ConnectClip(3, animationData.AirClip);

        currentWeights[0] = 1f;
        currentWeights[1] = 0f;
        currentWeights[2] = 0f;
        currentWeights[3] = 0f;
        currentState = AnimationState.None;

        graph.Play();
    }

    private void ConnectClip(int index, AnimationClip clip)
    {
        if (clip == null)
        {
            mixer.SetInputWeight(index, 0f);
            return;
        }

        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, clip);
        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetApplyPlayableIK(false);

        graph.Connect(clipPlayable, 0, mixer, index);
        mixer.SetInputWeight(index, 0f);
    }

    private void OnDisable()
    {
        DestroyGraph();
    }

    private void OnDestroy()
    {
        DestroyGraph();
    }

    private void DestroyGraph()
    {
        if (graph.IsValid())
            graph.Destroy();

        mixer = default;
    }

    private void HandleException(string stage, Exception exception)
    {
        fatalErrorLogged = true;
        DestroyGraph();
        enabled = false;
        Debug.LogError($"[FixieAnimation] {stage} fallo en '{debugSourceName}': {exception.Message}\n{exception.StackTrace}");
    }

    private void LogInfo(string message)
    {
        if (animationDebugLogs)
            Debug.Log($"[FixieAnimation] {message}");
    }

    private void LogWarning(string message)
    {
        if (animationDebugLogs)
            Debug.LogWarning($"[FixieAnimation] {message}");
    }

    private static HashSet<string> BuildCandidateNames(GameObject sourcePrefab, Transform modelRoot, Animator sourceAnimator)
    {
        HashSet<string> candidates = new(StringComparer.OrdinalIgnoreCase);

        if (sourcePrefab != null && !string.IsNullOrWhiteSpace(sourcePrefab.name))
            candidates.Add(sourcePrefab.name);

        if (sourceAnimator != null)
        {
            if (!string.IsNullOrWhiteSpace(sourceAnimator.name))
                candidates.Add(sourceAnimator.name);

            if (sourceAnimator.avatar != null && !string.IsNullOrWhiteSpace(sourceAnimator.avatar.name))
                candidates.Add(sourceAnimator.avatar.name);
        }

        if (modelRoot == null)
            return candidates;

        if (!string.IsNullOrWhiteSpace(modelRoot.name))
            candidates.Add(modelRoot.name);

        foreach (Transform child in modelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && !string.IsNullOrWhiteSpace(child.name))
                candidates.Add(child.name);
        }

        return candidates;
    }

    private static bool TryResolveAnimationSetData(GameObject sourcePrefab, Transform modelRoot, out ResolvedAnimationData data)
    {
        data = default;
        FixieAnimationSet set = ResolveAnimationSet(sourcePrefab, modelRoot);
        if (set == null)
            return false;

        data.SourceId = $"AnimationSet/{set.SetId}";
        data.Avatar = set.Avatar;
        data.IdleClip = set.IdleClip;
        data.WalkClip = set.WalkClip;
        data.RunClip = set.RunClip;
        data.JumpClip = set.JumpClip;
        data.FallClip = set.FallClip;
        data.AvailableClipNames = set.DescribeClips();
        return data.HasAnyClip;
    }

    private static bool TryResolveAnimatorControllerData(Animator sourceAnimator, out ResolvedAnimationData data)
    {
        data = default;
        if (sourceAnimator == null || sourceAnimator.runtimeAnimatorController == null)
            return false;

        data.SourceId = $"Controller/{sourceAnimator.runtimeAnimatorController.name}";
        data.Avatar = sourceAnimator.avatar;

        List<string> clipNames = new();
        foreach (AnimationClip clip in sourceAnimator.runtimeAnimatorController.animationClips)
        {
            if (!IsUsableClip(clip))
                continue;

            clipNames.Add(clip.name);
            RegisterClip(ref data, clip);
        }

        data.AvailableClipNames = clipNames.Count > 0 ? string.Join(", ", clipNames) : "NONE";
        return data.HasAnyClip;
    }

#if UNITY_EDITOR
    private static bool TryResolveEditorAnimationData(HashSet<string> candidates, Animator sourceAnimator, out ResolvedAnimationData data)
    {
        data = default;
        if (!TryResolveEditorAssetPath(candidates, sourceAnimator, out string assetPath))
            return false;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (assets == null || assets.Length == 0)
            return false;

        List<string> clipNames = new();
        data.SourceId = assetPath;

        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Avatar avatar && data.Avatar == null)
            {
                data.Avatar = avatar;
                continue;
            }

            if (asset is not AnimationClip clip || !IsUsableClip(clip))
                continue;

            clipNames.Add(clip.name);
            RegisterClip(ref data, clip);
        }

        data.AvailableClipNames = clipNames.Count > 0 ? string.Join(", ", clipNames) : "NONE";
        return data.HasAnyClip;
    }

    private static bool TryResolveEditorAssetPath(HashSet<string> candidates, Animator sourceAnimator, out string assetPath)
    {
        assetPath = GetHumanoidAssetPath(sourceAnimator != null && sourceAnimator.avatar != null
            ? AssetDatabase.GetAssetPath(sourceAnimator.avatar)
            : null);

        if (!string.IsNullOrWhiteSpace(assetPath))
            return true;

        if (sourceAnimator != null && sourceAnimator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in sourceAnimator.runtimeAnimatorController.animationClips)
            {
                assetPath = GetHumanoidAssetPath(AssetDatabase.GetAssetPath(clip));
                if (!string.IsNullOrWhiteSpace(assetPath))
                    return true;
            }
        }

        foreach (string candidate in candidates)
        {
            if (!EditorAssetPaths.TryGetValue(candidate, out assetPath))
                continue;

            assetPath = GetHumanoidAssetPath(assetPath);
            if (!string.IsNullOrWhiteSpace(assetPath))
                return true;
        }

        assetPath = null;
        return false;
    }

    private static string GetHumanoidAssetPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        string fileName = Path.GetFileName(assetPath);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        string humanoidPath = $"Assets/Art/Models/Temp/Players/{fileName}";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(humanoidPath) != null)
            return humanoidPath;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
            return assetPath;

        return null;
    }
#endif

    private static void RegisterClip(ref ResolvedAnimationData data, AnimationClip clip)
    {
        if (clip == null)
            return;

        TryAssignClip(ref data.IdleClip, clip, 100, 20, "idle", "breathe", "breath", "rest");
        TryAssignClip(ref data.WalkClip, clip, 100, 20, "walk", "move", "locomotion");
        TryAssignClip(ref data.RunClip, clip, 100, 20, "run", "sprint", "jog");
        TryAssignClip(ref data.JumpClip, clip, 100, 20, "jump", "takeoff", "launch", "hop");
        TryAssignClip(ref data.FallClip, clip, 100, 20, "fall", "air", "airborne", "jumploop", "loop");

        if (data.IdleClip == null)
            data.IdleClip = clip;
        else if (data.WalkClip == null && !ReferenceEquals(clip, data.IdleClip))
            data.WalkClip = clip;
        else if (data.RunClip == null && !ReferenceEquals(clip, data.IdleClip) && !ReferenceEquals(clip, data.WalkClip))
            data.RunClip = clip;
    }

    private static void TryAssignClip(ref AnimationClip targetClip, AnimationClip candidateClip, int exactBonus, int partialBonus, params string[] tokens)
    {
        if (candidateClip == null)
            return;

        if (targetClip == null)
        {
            if (GetClipScore(candidateClip.name, exactBonus, partialBonus, tokens) > 0)
                targetClip = candidateClip;

            return;
        }

        int currentScore = GetClipScore(targetClip.name, exactBonus, partialBonus, tokens);
        int candidateScore = GetClipScore(candidateClip.name, exactBonus, partialBonus, tokens);
        if (candidateScore > currentScore)
            targetClip = candidateClip;
    }

    private static int GetClipScore(string clipName, int exactBonus, int partialBonus, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return 0;

        string normalized = NormalizeToken(clipName);
        int bestScore = 0;

        foreach (string token in tokens)
        {
            string normalizedToken = NormalizeToken(token);
            if (normalized == normalizedToken)
                bestScore = Mathf.Max(bestScore, exactBonus);
            else if (normalized.Contains(normalizedToken))
                bestScore = Mathf.Max(bestScore, partialBonus);
        }

        return bestScore;
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] buffer = new char[value.Length];
        int length = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char character = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(character))
                buffer[length++] = character;
        }

        return new string(buffer, 0, length);
    }

    private static bool IsUsableClip(AnimationClip clip)
    {
        return clip != null &&
            !clip.empty &&
            !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureCache()
    {
        if (cachedSets == null)
            cachedSets = Resources.LoadAll<FixieAnimationSet>("FixieAnimations");
    }
}
