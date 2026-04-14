using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class FixieProceduralAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Animator animator;

    [Header("Timing")]
    [SerializeField] private float idleFrequency = 1.8f;
    [SerializeField] private float walkFrequency = 9f;
    [SerializeField] private float moveBlendSpeed = 8f;

    [Header("Idle")]
    [SerializeField] private float idleBodyBob = 0.015f;
    [SerializeField] private float idleHeadPitch = 3f;
    [SerializeField] private float idleArmSway = 4f;

    [Header("Walk")]
    [SerializeField] private float walkArmSwing = 28f;
    [SerializeField] private float walkForearmSwing = 16f;
    [SerializeField] private float walkLegSwing = 24f;
    [SerializeField] private float walkLowerLegSwing = 18f;
    [SerializeField] private float walkHipBob = 0.05f;
    [SerializeField] private float walkHeadBob = 6f;

    private readonly Dictionary<Transform, Quaternion> baseRotations = new();
    private readonly Dictionary<Transform, Vector3> basePositions = new();

    private Transform hips;
    private Transform neck;
    private Transform head;
    private Transform upperArmLeft;
    private Transform lowerArmLeft;
    private Transform upperArmRight;
    private Transform lowerArmRight;
    private Transform upperLegLeft;
    private Transform lowerLegLeft;
    private Transform upperLegRight;
    private Transform lowerLegRight;

    private float moveBlend;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponentInParent<Rigidbody>();

        if (animator == null)
            animator = GetComponentInParent<Animator>();

        if (visualRoot == null)
            visualRoot = transform;

        CacheRig();
    }

    public void BindVisual(Transform targetVisual)
    {
        if (targetVisual == null)
            return;

        visualRoot = targetVisual;
        CacheRig();
    }

    private void LateUpdate()
    {
        if (visualRoot == null || rb == null || hips == null || HasWorkingAnimator())
            return;

        float planarSpeed = new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude;
        float targetBlend = Mathf.Clamp01(planarSpeed / 3.5f);
        moveBlend = Mathf.MoveTowards(moveBlend, targetBlend, moveBlendSpeed * Time.deltaTime);

        float frequency = Mathf.Lerp(idleFrequency, walkFrequency, moveBlend);
        float phase = Time.time * frequency;
        float oppositePhase = phase + Mathf.PI;

        float idleBob = Mathf.Sin(Time.time * idleFrequency) * idleBodyBob * (1f - moveBlend);
        float strideBob = Mathf.Abs(Mathf.Sin(phase)) * walkHipBob * moveBlend;
        ApplyLocalPosition(hips, new Vector3(0f, idleBob + strideBob, 0f));

        ApplyLocalRotation(head, new Vector3(
            Mathf.Sin(Time.time * idleFrequency) * idleHeadPitch * (1f - moveBlend) + Mathf.Sin(phase) * walkHeadBob * moveBlend,
            Mathf.Sin(phase * 0.5f) * 4f * moveBlend,
            0f));

        ApplyLocalRotation(neck, new Vector3(
            Mathf.Sin(Time.time * idleFrequency) * idleHeadPitch * 0.45f * (1f - moveBlend),
            Mathf.Sin(phase * 0.5f) * 2.5f * moveBlend,
            0f));

        ApplyLimb(upperArmLeft, lowerArmLeft, phase, 1f);
        ApplyLimb(upperArmRight, lowerArmRight, oppositePhase, -1f);
        ApplyLeg(upperLegLeft, lowerLegLeft, oppositePhase);
        ApplyLeg(upperLegRight, lowerLegRight, phase);
    }

    private void ApplyLimb(Transform upper, Transform lower, float phase, float side)
    {
        float idleSway = Mathf.Sin(Time.time * idleFrequency + side) * idleArmSway * (1f - moveBlend);
        float upperSwing = Mathf.Sin(phase) * walkArmSwing * moveBlend;
        float lowerSwing = Mathf.Max(0f, Mathf.Sin(phase)) * walkForearmSwing * moveBlend;

        ApplyLocalRotation(upper, new Vector3(idleSway + upperSwing, 0f, side * 4f * moveBlend));
        ApplyLocalRotation(lower, new Vector3(lowerSwing, 0f, 0f));
    }

    private void ApplyLeg(Transform upper, Transform lower, float phase)
    {
        float upperSwing = Mathf.Sin(phase) * walkLegSwing * moveBlend;
        float lowerSwing = Mathf.Max(0f, -Mathf.Sin(phase)) * walkLowerLegSwing * moveBlend;

        ApplyLocalRotation(upper, new Vector3(upperSwing, 0f, 0f));
        ApplyLocalRotation(lower, new Vector3(lowerSwing, 0f, 0f));
    }

    private void ApplyLocalRotation(Transform bone, Vector3 eulerOffset)
    {
        if (bone == null || !baseRotations.TryGetValue(bone, out Quaternion baseRotation))
            return;

        bone.localRotation = baseRotation * Quaternion.Euler(eulerOffset);
    }

    private void ApplyLocalPosition(Transform bone, Vector3 localOffset)
    {
        if (bone == null || !basePositions.TryGetValue(bone, out Vector3 basePosition))
            return;

        bone.localPosition = basePosition + localOffset;
    }

    private void CacheRig()
    {
        baseRotations.Clear();
        basePositions.Clear();

        if (visualRoot == null)
            return;

        hips = FindBone("Hips");
        neck = FindBone("Neck");
        head = FindBone("Head");
        upperArmLeft = FindBone("UpperArm.L");
        lowerArmLeft = FindBone("LowerArm.L");
        upperArmRight = FindBone("UpperArm.R");
        lowerArmRight = FindBone("LowerArm.R");
        upperLegLeft = FindBone("UpperLeg.L");
        lowerLegLeft = FindBone("LowerLeg.L");
        upperLegRight = FindBone("UpperLeg.R");
        lowerLegRight = FindBone("LowerLeg.R");

        RegisterBaseTransform(hips, true);
        RegisterBaseTransform(neck);
        RegisterBaseTransform(head);
        RegisterBaseTransform(upperArmLeft);
        RegisterBaseTransform(lowerArmLeft);
        RegisterBaseTransform(upperArmRight);
        RegisterBaseTransform(lowerArmRight);
        RegisterBaseTransform(upperLegLeft);
        RegisterBaseTransform(lowerLegLeft);
        RegisterBaseTransform(upperLegRight);
        RegisterBaseTransform(lowerLegRight);
    }

    private void RegisterBaseTransform(Transform target, bool trackPosition = false)
    {
        if (target == null)
            return;

        baseRotations[target] = target.localRotation;
        if (trackPosition)
            basePositions[target] = target.localPosition;
    }

    private Transform FindBone(string boneName)
    {
        if (visualRoot == null)
            return null;

        foreach (Transform child in visualRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == boneName)
                return child;
        }

        return null;
    }

    private bool HasWorkingAnimator()
    {
        if (animator == null)
            return false;

        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller == null)
            return false;

        AnimationClip[] clips = controller.animationClips;
        return clips != null && clips.Distinct().Count() >= 2;
    }
}
