using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

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

    private struct HardcodedAnimationData
    {
        public AnimationClip IdleClip;
        public AnimationClip WalkClip;
        public AnimationClip RunClip;
        public AnimationClip JumpClip;
        public AnimationClip FallClip;

        public AnimationClip AirClip => FallClip != null ? FallClip : JumpClip;
        public bool IsValid => IdleClip != null && WalkClip != null && RunClip != null && JumpClip != null && FallClip != null;

        public string DescribeClips()
        {
            return $"idle={GetClipName(IdleClip)} walk={GetClipName(WalkClip)} run={GetClipName(RunClip)} jump={GetClipName(JumpClip)} fall={GetClipName(FallClip)}";
        }

        private static string GetClipName(AnimationClip clip)
        {
            return clip != null ? clip.name : "NONE";
        }
    }

    [Header("Bindings")]
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Animator animator;
    [SerializeField] private FixieAnimationSet animationSet;

    [Header("Clips")]
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip walkClip;
    [SerializeField] private AnimationClip runClip;
    [SerializeField] private AnimationClip jumpClip;
    [SerializeField] private AnimationClip fallClip;

    [Header("Blend")]
    [SerializeField] private float walkThreshold = 0.45f;
    [SerializeField] private float blendSpeed = 8f;
    [SerializeField] private bool animationDebugLogs = false;

    private readonly float[] currentWeights = new float[4];

    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimationState currentState;
    private HardcodedAnimationData animationData;
    private string debugSourceName = "Unknown";
    private bool fatalErrorLogged;

    public void Bind(
        PlayerMovement targetMovement,
        Transform targetVisualRoot,
        Animator sourceAnimator,
        FixieAnimationSet runtimeAnimationSet,
        int slotIndex)
    {
        BindInternal(
            targetMovement,
            targetVisualRoot,
            sourceAnimator,
            runtimeAnimationSet,
            runtimeAnimationSet != null ? runtimeAnimationSet.Avatar : null,
            runtimeAnimationSet != null ? runtimeAnimationSet.IdleClip : null,
            runtimeAnimationSet != null ? runtimeAnimationSet.WalkClip : null,
            runtimeAnimationSet != null ? runtimeAnimationSet.RunClip : null,
            runtimeAnimationSet != null ? runtimeAnimationSet.JumpClip : null,
            runtimeAnimationSet != null ? runtimeAnimationSet.FallClip : null,
            slotIndex);
    }

    public void Bind(
        PlayerMovement targetMovement,
        Transform targetVisualRoot,
        Animator sourceAnimator,
        AnimationClip hardcodedIdleClip,
        AnimationClip hardcodedWalkClip,
        AnimationClip hardcodedRunClip,
        AnimationClip hardcodedJumpClip,
        AnimationClip hardcodedFallClip,
        int slotIndex)
    {
        BindInternal(
            targetMovement,
            targetVisualRoot,
            sourceAnimator,
            null,
            null,
            hardcodedIdleClip,
            hardcodedWalkClip,
            hardcodedRunClip,
            hardcodedJumpClip,
            hardcodedFallClip,
            slotIndex);
    }

    private void BindInternal(
        PlayerMovement targetMovement,
        Transform targetVisualRoot,
        Animator sourceAnimator,
        FixieAnimationSet runtimeAnimationSet,
        Avatar fallbackAvatar,
        AnimationClip hardcodedIdleClip,
        AnimationClip hardcodedWalkClip,
        AnimationClip hardcodedRunClip,
        AnimationClip hardcodedJumpClip,
        AnimationClip hardcodedFallClip,
        int slotIndex)
    {
        try
        {
            fatalErrorLogged = false;
            movement = targetMovement;
            visualRoot = targetVisualRoot != null ? targetVisualRoot : transform;
            animator = null;
            animationSet = runtimeAnimationSet;
            idleClip = hardcodedIdleClip;
            walkClip = hardcodedWalkClip;
            runClip = hardcodedRunClip;
            jumpClip = hardcodedJumpClip;
            fallClip = hardcodedFallClip;
            debugSourceName = visualRoot != null ? visualRoot.name : name;

            animationData = new HardcodedAnimationData
            {
                IdleClip = idleClip,
                WalkClip = walkClip,
                RunClip = runClip,
                JumpClip = jumpClip,
                FallClip = fallClip,
            };

            if (!animationData.IsValid)
            {
                LogWarning($"slot={slotIndex} animation clips incomplete for '{debugSourceName}'. {animationData.DescribeClips()}");
                DestroyGraph();
                enabled = false;
                return;
            }

            animator = EnsureAnimator(visualRoot, sourceAnimator, fallbackAvatar);
            if (animator == null)
            {
                LogWarning($"slot={slotIndex} could not find or create Animator for '{debugSourceName}'.");
                DestroyGraph();
                enabled = false;
                return;
            }

            BuildGraph();
            enabled = true;
            LogInfo($"slot={slotIndex} visual='{debugSourceName}' animator='{animator.name}' avatar='{(animator.avatar != null ? animator.avatar.name : "none")}' {animationData.DescribeClips()}");
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

            if (!grounded)
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

    private Animator EnsureAnimator(Transform root, Animator preferredAnimator, Avatar fallbackAvatar)
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

        if (root != null)
        {
            foreach (PlayerAnimator controllerDriver in root.GetComponentsInChildren<PlayerAnimator>(true))
                controllerDriver.enabled = false;
        }

        // Disable every other Animator in the hierarchy. Some prefabs (e.g. Fixie_P3) have
        // an extra Animator on the root AND one from the FBX child. If both run, they fight
        // over the same bones and the controller-driven one overrides the PlayableGraph.
        if (root != null)
        {
            foreach (Animator other in root.GetComponentsInChildren<Animator>(true))
            {
                if (other == targetAnimator)
                    continue;

                other.runtimeAnimatorController = null;
                other.enabled = false;
            }
        }

        // Prefer the model's own avatar. If the selected Animator lacks one, use the
        // build-safe avatar stored on the runtime animation set.
        if (targetAnimator.avatar == null && fallbackAvatar != null)
            targetAnimator.avatar = fallbackAvatar;

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

        graph = PlayableGraph.Create($"{name}_FixieHardcodedAnimationGraph");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        mixer = AnimationMixerPlayable.Create(graph, 4);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "FixieHardcodedAnimation", animator);
        output.SetSourcePlayable(mixer);

        ConnectClip(0, animationData.IdleClip);
        ConnectClip(1, animationData.WalkClip);
        ConnectClip(2, animationData.RunClip);
        ConnectClip(3, animationData.AirClip);

        // Start in idle so the character animates from frame 0, not T-pose.
        currentWeights[0] = 1f;
        currentWeights[1] = 0f;
        currentWeights[2] = 0f;
        currentWeights[3] = 0f;
        mixer.SetInputWeight(0, 1f);
        currentState = AnimationState.None;

        graph.Play();
    }

    private void ConnectClip(int index, AnimationClip clip)
    {
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
        Debug.LogWarning($"[FixieAnimation] {message}");
    }
}
