using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class FixieAnimationBuildSafe : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private Animator animator;
    [SerializeField] private Avatar avatar;

    [Header("Animation Clips")]
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip walkClip;
    [SerializeField] private AnimationClip runClip;
    [SerializeField] private AnimationClip jumpClip;
    [SerializeField] private AnimationClip fallClip;

    [Header("Blend")]
    [SerializeField] private float walkThreshold = 0.45f;
    [SerializeField] private float blendSpeed = 10f;

    [Header("Playback Speed")]
    [SerializeField] private float idleSpeed = 1f;
    [SerializeField] private float walkSpeed = 1f;
    [SerializeField] private float runSpeed = 1f;
    [SerializeField] private float jumpSpeed = 1f;
    [SerializeField] private float fallSpeed = 1f;

    [Header("Build Debug")]
    [SerializeField] private bool showDebugOnScreen = true;

    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;

    private AnimationClipPlayable idlePlayable;
    private AnimationClipPlayable walkPlayable;
    private AnimationClipPlayable runPlayable;
    private AnimationClipPlayable jumpPlayable;
    private AnimationClipPlayable fallPlayable;

    private float idleTime;
    private float walkTime;
    private float runTime;
    private float jumpTime;
    private float fallTime;

    private float idleWeight = 1f;
    private float walkWeight;
    private float runWeight;
    private float jumpWeight;
    private float fallWeight;

    private bool graphReady;
    private string status = "Starting animation system...";

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        AutoFindReferences();
        BuildGraph();
    }

    private void OnEnable()
    {
        if (!graphReady)
        {
            AutoFindReferences();
            BuildGraph();
        }
    }

    private void Update()
    {
        if (!graphReady)
        {
            AutoFindReferences();
            BuildGraph();
            return;
        }

        if (movement == null)
        {
            AutoFindReferences();
            return;
        }

        UpdateAnimationState();
        UpdateClipTimes();
        ApplyWeights();
    }

    private void AutoFindReferences()
    {
        if (movement == null)
            movement = GetComponentInParent<PlayerMovement>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator == null)
            animator = GetComponentInParent<Animator>();

        if (animator != null)
        {
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (animator.avatar == null && avatar != null)
                animator.avatar = avatar;
        }
    }

    private void BuildGraph()
    {
        if (graphReady)
            return;

        if (movement == null)
        {
            status = "Missing PlayerMovement";
            return;
        }

        if (animator == null)
        {
            status = "Missing Animator";
            return;
        }

        // Solo Idle, Walk y Run son obligatorias.
        if (idleClip == null || walkClip == null || runClip == null)
        {
            status =
                "Missing essential clips: " +
                "Idle=" + ClipName(idleClip) + " " +
                "Walk=" + ClipName(walkClip) + " " +
                "Run=" + ClipName(runClip);

            return;
        }

        // Fallbacks para que el sistema NO se detenga si faltan Jump o Fall.
        if (jumpClip == null)
            jumpClip = idleClip;

        if (fallClip == null)
            fallClip = jumpClip != null ? jumpClip : idleClip;

        DestroyGraph();

        animator.runtimeAnimatorController = null;
        animator.enabled = true;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (animator.avatar == null && avatar != null)
            animator.avatar = avatar;

        animator.Rebind();
        animator.Update(0f);

        graph = PlayableGraph.Create(gameObject.name + "_BuildSafeAnimationGraph");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        mixer = AnimationMixerPlayable.Create(graph, 5);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "BuildSafeAnimationOutput", animator);
        output.SetSourcePlayable(mixer);

        idlePlayable = CreateClipPlayable(idleClip);
        walkPlayable = CreateClipPlayable(walkClip);
        runPlayable = CreateClipPlayable(runClip);
        jumpPlayable = CreateClipPlayable(jumpClip);
        fallPlayable = CreateClipPlayable(fallClip);

        graph.Connect(idlePlayable, 0, mixer, 0);
        graph.Connect(walkPlayable, 0, mixer, 1);
        graph.Connect(runPlayable, 0, mixer, 2);
        graph.Connect(jumpPlayable, 0, mixer, 3);
        graph.Connect(fallPlayable, 0, mixer, 4);

        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);
        mixer.SetInputWeight(2, 0f);
        mixer.SetInputWeight(3, 0f);
        mixer.SetInputWeight(4, 0f);

        idleWeight = 1f;
        walkWeight = 0f;
        runWeight = 0f;
        jumpWeight = 0f;
        fallWeight = 0f;

        idleTime = 0f;
        walkTime = 0f;
        runTime = 0f;
        jumpTime = 0f;
        fallTime = 0f;

        graph.Play();
        graph.Evaluate(0f);

        graphReady = true;
        status = "Animation OK";
    }

    private AnimationClipPlayable CreateClipPlayable(AnimationClip clip)
    {
        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);

        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);

        // Controlamos el tiempo manualmente para asegurar el loop en build.
        playable.SetSpeed(0f);
        playable.SetTime(0f);

        return playable;
    }

    private void UpdateAnimationState()
    {
        float speed = Mathf.Clamp01(movement.SpeedNormalized);
        bool grounded = movement.IsGrounded;
        float verticalVelocity = movement.RB != null ? movement.RB.velocity.y : 0f;

        float targetIdle = 0f;
        float targetWalk = 0f;
        float targetRun = 0f;
        float targetJump = 0f;
        float targetFall = 0f;

        if (!grounded)
        {
            if (verticalVelocity > 0.5f)
                targetJump = 1f;
            else
                targetFall = 1f;
        }
        else
        {
            if (speed <= 0.02f)
            {
                targetIdle = 1f;
            }
            else if (speed <= walkThreshold)
            {
                float t = Mathf.InverseLerp(0f, walkThreshold, speed);

                targetIdle = 1f - t;
                targetWalk = t;
            }
            else
            {
                float t = Mathf.InverseLerp(walkThreshold, 1f, speed);

                targetWalk = 1f - t;
                targetRun = t;
            }
        }

        float step = blendSpeed * Time.deltaTime;

        idleWeight = Mathf.MoveTowards(idleWeight, targetIdle, step);
        walkWeight = Mathf.MoveTowards(walkWeight, targetWalk, step);
        runWeight = Mathf.MoveTowards(runWeight, targetRun, step);
        jumpWeight = Mathf.MoveTowards(jumpWeight, targetJump, step);
        fallWeight = Mathf.MoveTowards(fallWeight, targetFall, step);
    }

    private void UpdateClipTimes()
    {
        idleTime = AdvanceLoop(idlePlayable, idleClip, idleTime, idleSpeed);
        walkTime = AdvanceLoop(walkPlayable, walkClip, walkTime, walkSpeed);
        runTime = AdvanceLoop(runPlayable, runClip, runTime, runSpeed);

        jumpTime = AdvanceOnce(jumpPlayable, jumpClip, jumpTime, jumpSpeed);
        fallTime = AdvanceLoop(fallPlayable, fallClip, fallTime, fallSpeed);
    }

    private float AdvanceLoop(AnimationClipPlayable playable, AnimationClip clip, float currentTime, float playbackSpeed)
    {
        if (!playable.IsValid() || clip == null)
            return 0f;

        float length = Mathf.Max(0.01f, clip.length);

        currentTime += Time.deltaTime * Mathf.Max(0f, playbackSpeed);
        currentTime = Mathf.Repeat(currentTime, length);

        playable.SetTime(currentTime);

        return currentTime;
    }

    private float AdvanceOnce(AnimationClipPlayable playable, AnimationClip clip, float currentTime, float playbackSpeed)
    {
        if (!playable.IsValid() || clip == null)
            return 0f;

        float length = Mathf.Max(0.01f, clip.length);

        currentTime += Time.deltaTime * Mathf.Max(0f, playbackSpeed);
        currentTime = Mathf.Min(currentTime, length);

        playable.SetTime(currentTime);

        return currentTime;
    }

    private void ApplyWeights()
    {
        float total = idleWeight + walkWeight + runWeight + jumpWeight + fallWeight;

        if (total <= 0.0001f)
        {
            idleWeight = 1f;
            total = 1f;
        }

        mixer.SetInputWeight(0, idleWeight / total);
        mixer.SetInputWeight(1, walkWeight / total);
        mixer.SetInputWeight(2, runWeight / total);
        mixer.SetInputWeight(3, jumpWeight / total);
        mixer.SetInputWeight(4, fallWeight / total);
    }

    private string ClipName(AnimationClip clip)
    {
        return clip != null ? clip.name : "NONE";
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

        graphReady = false;
    }

    private void OnGUI()
    {
        if (!showDebugOnScreen)
            return;

        string text =
            "Anim Status: " + status + "\n" +
            "Movement: " + (movement != null ? movement.name : "NONE") + "\n" +
            "Animator: " + (animator != null ? animator.name : "NONE") + "\n" +
            "Speed: " + (movement != null ? movement.SpeedNormalized.ToString("0.00") : "0") + "\n" +
            "Grounded: " + (movement != null ? movement.IsGrounded.ToString() : "false") + "\n" +
            "Idle: " + ClipName(idleClip) + "\n" +
            "Walk: " + ClipName(walkClip) + "\n" +
            "Run: " + ClipName(runClip) + "\n" +
            "Jump: " + ClipName(jumpClip) + "\n" +
            "Fall: " + ClipName(fallClip);

        GUI.Label(new Rect(20, 20, 800, 210), text);
    }
}