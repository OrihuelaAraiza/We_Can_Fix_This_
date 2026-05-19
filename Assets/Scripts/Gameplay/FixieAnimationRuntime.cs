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
        Jump,
        Fall,
    }

    private struct HardcodedAnimationData
    {
        public AnimationClip IdleClip;
        public AnimationClip WalkClip;
        public AnimationClip RunClip;
        public AnimationClip JumpClip;
        public AnimationClip FallClip;

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
    [SerializeField] private float blendSpeed = 16f;
    [SerializeField] private float airborneBlendSpeed = 28f;
    [SerializeField] private float jumpRequestWindow = 0.16f;
    [SerializeField] private float jumpSnapWeight = 0.85f;
    [SerializeField] private bool animationDebugLogs = false;

    [Header("Loop Control")]
    [SerializeField] private bool forceLoopByCode = true;
    [SerializeField] private float idlePlaybackSpeed = 1f;
    [SerializeField] private float walkPlaybackSpeed = 1f;
    [SerializeField] private float runPlaybackSpeed = 1f;
    [SerializeField] private float jumpPlaybackSpeed = 1f;
    [SerializeField] private float fallPlaybackSpeed = 1f;

    // 0=Idle, 1=Walk, 2=Run, 3=Jump, 4=Fall
    private readonly float[] currentWeights = new float[5];
    private readonly AnimationClipPlayable[] clipPlayables = new AnimationClipPlayable[5];
    private readonly float[] clipTimes = new float[5];

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

            Debug.Log($"[FixieAnimation] Bind OK slot={slotIndex} visual='{debugSourceName}' animator='{animator.name}' avatar='{(animator.avatar != null ? animator.avatar.name : "NONE")}' {animationData.DescribeClips()}");
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
            bool jumpRequested = movement.HasJumpQueued ||
                Time.time - movement.LastJumpRequestedTime <= jumpRequestWindow ||
                Time.time - movement.LastJumpStartedTime <= jumpRequestWindow;

            // Lo hacemos así para evitar cualquier error de nombre con VerticalVelocity.
            float verticalVelocity = movement.RB != null ? movement.RB.velocity.y : 0f;

            float targetIdle = 0f;
            float targetWalk = 0f;
            float targetRun = 0f;
            float targetJump = 0f;
            float targetFall = 0f;

            AnimationState nextState;

            if (jumpRequested)
            {
                targetJump = 1f;
                nextState = AnimationState.Jump;
            }
            else if (!grounded)
            {
                if (verticalVelocity > 0.5f)
                {
                    targetJump = 1f;
                    nextState = AnimationState.Jump;
                }
                else
                {
                    targetFall = 1f;
                    nextState = AnimationState.Fall;
                }
            }
            else if (speed <= 0.02f)
            {
                targetIdle = 1f;
                nextState = AnimationState.Idle;
            }
            else if (speed <= walkThreshold)
            {
                float t = walkThreshold <= 0.001f ? 1f : Mathf.InverseLerp(0f, walkThreshold, speed);

                targetWalk = movement.HasMoveInput ? Mathf.Max(t, movement.MoveInputMagnitude) : t;
                targetIdle = 1f - targetWalk;

                nextState = movement.HasMoveInput || t > 0.15f ? AnimationState.Walk : AnimationState.Idle;
            }
            else
            {
                float t = Mathf.InverseLerp(walkThreshold, 1f, speed);

                targetWalk = 1f - t;
                targetRun = t;

                nextState = t > 0.45f ? AnimationState.Run : AnimationState.Walk;
            }

            if (nextState != currentState)
                ApplyStateTransition(nextState);

            float activeBlendSpeed = nextState == AnimationState.Jump || nextState == AnimationState.Fall
                ? airborneBlendSpeed
                : blendSpeed;
            float step = activeBlendSpeed * Time.deltaTime;

            currentWeights[0] = Mathf.MoveTowards(currentWeights[0], targetIdle, step);
            currentWeights[1] = Mathf.MoveTowards(currentWeights[1], targetWalk, step);
            currentWeights[2] = Mathf.MoveTowards(currentWeights[2], targetRun, step);
            currentWeights[3] = Mathf.MoveTowards(currentWeights[3], targetJump, step);
            currentWeights[4] = Mathf.MoveTowards(currentWeights[4], targetFall, step);

            NormalizeAndApplyWeights();

            if (forceLoopByCode)
                UpdateClipLoopTimes();

            if (nextState != currentState)
            {
                currentState = nextState;

                if (animationDebugLogs)
                    Debug.Log($"[FixieAnimation] state={currentState} grounded={grounded} speed={speed:0.00} vy={verticalVelocity:0.00}");
            }
        }
        catch (Exception exception)
        {
            HandleException("Update", exception);
        }
    }

    private void ApplyStateTransition(AnimationState nextState)
    {
        if (nextState == AnimationState.Jump)
        {
            ResetClipTime(3);
            currentWeights[3] = Mathf.Max(currentWeights[3], Mathf.Clamp01(jumpSnapWeight));
            currentWeights[0] *= 0.25f;
            currentWeights[1] *= 0.35f;
            currentWeights[2] *= 0.35f;
            currentWeights[4] = 0f;
        }
        else if (nextState == AnimationState.Fall)
        {
            ResetClipTime(4);
        }
    }

    private void ResetClipTime(int index)
    {
        if (index < 0 || index >= clipTimes.Length)
            return;

        clipTimes[index] = 0f;

        if (clipPlayables[index].IsValid())
            clipPlayables[index].SetTime(0f);
    }

    private void NormalizeAndApplyWeights()
    {
        float totalWeight =
            currentWeights[0] +
            currentWeights[1] +
            currentWeights[2] +
            currentWeights[3] +
            currentWeights[4];

        if (totalWeight <= 0.0001f)
        {
            currentWeights[0] = 1f;
            totalWeight = 1f;
        }

        for (int i = 0; i < currentWeights.Length; i++)
            mixer.SetInputWeight(i, currentWeights[i] / totalWeight);
    }

    private void UpdateClipLoopTimes()
    {
        UpdateLoopingClip(0, animationData.IdleClip, idlePlaybackSpeed, true);
        UpdateLoopingClip(1, animationData.WalkClip, walkPlaybackSpeed, true);
        UpdateLoopingClip(2, animationData.RunClip, runPlaybackSpeed, true);

        // Jump y Fall normalmente no necesitan loop perfecto,
        // pero los dejamos controlados para que no se queden congelados raro.
        UpdateLoopingClip(3, animationData.JumpClip, jumpPlaybackSpeed, false);
        UpdateLoopingClip(4, animationData.FallClip, fallPlaybackSpeed, true);
    }

    private void UpdateLoopingClip(int index, AnimationClip clip, float playbackSpeed, bool shouldLoop)
    {
        if (clip == null)
            return;

        if (!clipPlayables[index].IsValid())
            return;

        float length = Mathf.Max(0.01f, clip.length);
        float weight = currentWeights[index];

        // Si el clip casi no tiene peso, no hace falta avanzar tanto su tiempo.
        if (weight <= 0.001f)
            return;

        clipTimes[index] += Time.deltaTime * Mathf.Max(0f, playbackSpeed);

        if (shouldLoop)
        {
            // Aquí está el loop real por código.
            // Cuando termina el clip, regresa a 0 automáticamente.
            clipTimes[index] = Mathf.Repeat(clipTimes[index], length);
        }
        else
        {
            clipTimes[index] = Mathf.Min(clipTimes[index], length);
        }

        clipPlayables[index].SetTime(clipTimes[index]);
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

        mixer = AnimationMixerPlayable.Create(graph, 5);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "FixieHardcodedAnimation", animator);
        output.SetSourcePlayable(mixer);

        ConnectClip(0, animationData.IdleClip);
        ConnectClip(1, animationData.WalkClip);
        ConnectClip(2, animationData.RunClip);
        ConnectClip(3, animationData.JumpClip);
        ConnectClip(4, animationData.FallClip);

        ResetWeightsAndTimes();

        graph.Play();
        graph.Evaluate(0f);
    }

    private void ConnectClip(int index, AnimationClip clip)
    {
        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(graph, clip);

        clipPlayable.SetApplyFootIK(false);
        clipPlayable.SetApplyPlayableIK(false);

        // El tiempo lo vamos a controlar nosotros.
        clipPlayable.SetSpeed(0f);
        clipPlayable.SetTime(0f);

        clipPlayables[index] = clipPlayable;

        graph.Connect(clipPlayable, 0, mixer, index);
        mixer.SetInputWeight(index, 0f);
    }

    private void ResetWeightsAndTimes()
    {
        for (int i = 0; i < currentWeights.Length; i++)
        {
            currentWeights[i] = 0f;
            clipTimes[i] = 0f;

            if (clipPlayables[i].IsValid())
                clipPlayables[i].SetTime(0f);
        }

        currentWeights[0] = 1f;
        mixer.SetInputWeight(0, 1f);

        currentState = AnimationState.None;
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

        for (int i = 0; i < clipPlayables.Length; i++)
            clipPlayables[i] = default;
    }

    private void HandleException(string stage, Exception exception)
    {
        fatalErrorLogged = true;
        DestroyGraph();
        enabled = false;

        Debug.LogError($"[FixieAnimation] {stage} fallo en '{debugSourceName}': {exception.Message}\n{exception.StackTrace}");
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[FixieAnimation] {message}");
    }
}
