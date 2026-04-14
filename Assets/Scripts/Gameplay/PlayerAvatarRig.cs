using System.Collections.Generic;
using UnityEngine;

public class PlayerAvatarRig : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private Transform visualRoot;

    [Header("Idle")]
    [SerializeField] private float idleBreathAmount = 0.012f;
    [SerializeField] private float idleBreathSpeed = 2f;
    [SerializeField] private float idleHeadPitch = 1.5f;

    [Header("Locomotion")]
    [SerializeField] private float cycleFrequency = 9f;
    [SerializeField] private float moveBlendSpeed = 8f;
    [SerializeField] private float hipBounce = 0.045f;
    [SerializeField] private float torsoLean = 7f;
    [SerializeField] private float upperArmSwing = 30f;
    [SerializeField] private float lowerArmSwing = 12f;
    [SerializeField] private float upperLegSwing = 32f;
    [SerializeField] private float lowerLegSwing = 20f;

    [Header("Air")]
    [SerializeField] private float airArmPitch = 16f;
    [SerializeField] private float airLegPitch = 18f;
    [SerializeField] private float poseLerpSpeed = 12f;

    private readonly Dictionary<Transform, Quaternion> baseRotations = new();
    private readonly Dictionary<Transform, Vector3> basePositions = new();

    private Transform hips;
    private Transform spine;
    private Transform chest;
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
        movement ??= GetComponent<PlayerMovement>();
        if (visualRoot == null)
            visualRoot = transform;

        CacheRig();
    }

    public void Bind(PlayerMovement targetMovement, Transform targetVisual)
    {
        movement = targetMovement;
        if (targetVisual != null)
            visualRoot = targetVisual;

        CacheRig();
    }

    private void LateUpdate()
    {
        if (movement == null || visualRoot == null || hips == null)
            return;

        float speed = movement.PlanarSpeed;
        float targetBlend = movement.IsGrounded ? Mathf.Clamp01(speed / 4.5f) : 0f;
        moveBlend = Mathf.MoveTowards(moveBlend, targetBlend, moveBlendSpeed * Time.deltaTime);

        if (!movement.IsGrounded)
        {
            ApplyAirPose();
            return;
        }

        float breath = Mathf.Sin(Time.time * idleBreathSpeed) * idleBreathAmount;
        float cycle = Time.time * Mathf.Lerp(idleBreathSpeed, cycleFrequency, moveBlend);
        float oppositeCycle = cycle + Mathf.PI;
        float bounce = Mathf.Abs(Mathf.Sin(cycle)) * hipBounce * moveBlend;

        ApplyLocalPosition(hips, new Vector3(0f, breath + bounce, 0f));

        float torsoPitch = -torsoLean * moveBlend;
        float headPitch = Mathf.Sin(Time.time * idleBreathSpeed) * idleHeadPitch * (1f - moveBlend);
        ApplyLocalRotation(spine, new Vector3(torsoPitch * 0.6f, 0f, 0f));
        ApplyLocalRotation(chest, new Vector3(torsoPitch, 0f, 0f));
        ApplyLocalRotation(neck, new Vector3(headPitch * 0.5f, 0f, 0f));
        ApplyLocalRotation(head, new Vector3(headPitch, 0f, 0f));

        ApplyArmPose(upperArmLeft, lowerArmLeft, cycle, 1f);
        ApplyArmPose(upperArmRight, lowerArmRight, oppositeCycle, -1f);
        ApplyLegPose(upperLegLeft, lowerLegLeft, oppositeCycle);
        ApplyLegPose(upperLegRight, lowerLegRight, cycle);
    }

    private void ApplyArmPose(Transform upper, Transform lower, float cycle, float side)
    {
        float upperPitch = Mathf.Sin(cycle) * upperArmSwing * moveBlend;
        float lowerPitch = Mathf.Max(0f, Mathf.Sin(cycle)) * lowerArmSwing * moveBlend;
        float upperRoll = side * 4f * moveBlend;

        ApplyLocalRotation(upper, new Vector3(upperPitch, 0f, upperRoll));
        ApplyLocalRotation(lower, new Vector3(lowerPitch, 0f, 0f));
    }

    private void ApplyLegPose(Transform upper, Transform lower, float cycle)
    {
        float upperPitch = Mathf.Sin(cycle) * upperLegSwing * moveBlend;
        float lowerPitch = Mathf.Max(0f, -Mathf.Sin(cycle)) * lowerLegSwing * moveBlend;

        ApplyLocalRotation(upper, new Vector3(upperPitch, 0f, 0f));
        ApplyLocalRotation(lower, new Vector3(lowerPitch, 0f, 0f));
    }

    private void ApplyAirPose()
    {
        ApplyLocalPosition(hips, new Vector3(0f, idleBreathAmount, 0f));
        ApplyLocalRotation(spine, new Vector3(-6f, 0f, 0f));
        ApplyLocalRotation(chest, new Vector3(-10f, 0f, 0f));
        ApplyLocalRotation(neck, new Vector3(3f, 0f, 0f));
        ApplyLocalRotation(head, new Vector3(5f, 0f, 0f));

        ApplySmoothedRotation(upperArmLeft, new Vector3(-airArmPitch, 0f, 4f));
        ApplySmoothedRotation(lowerArmLeft, new Vector3(airArmPitch * 0.4f, 0f, 0f));
        ApplySmoothedRotation(upperArmRight, new Vector3(-airArmPitch, 0f, -4f));
        ApplySmoothedRotation(lowerArmRight, new Vector3(airArmPitch * 0.4f, 0f, 0f));

        ApplySmoothedRotation(upperLegLeft, new Vector3(airLegPitch, 0f, 0f));
        ApplySmoothedRotation(lowerLegLeft, new Vector3(-airLegPitch * 0.35f, 0f, 0f));
        ApplySmoothedRotation(upperLegRight, new Vector3(airLegPitch, 0f, 0f));
        ApplySmoothedRotation(lowerLegRight, new Vector3(-airLegPitch * 0.35f, 0f, 0f));
    }

    private void CacheRig()
    {
        baseRotations.Clear();
        basePositions.Clear();

        if (visualRoot == null)
            return;

        hips = FindBone("Hips");
        spine = FindBone("Spine");
        chest = FindBone("Chest");
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

        RegisterTransform(hips, true);
        RegisterTransform(spine);
        RegisterTransform(chest);
        RegisterTransform(neck);
        RegisterTransform(head);
        RegisterTransform(upperArmLeft);
        RegisterTransform(lowerArmLeft);
        RegisterTransform(upperArmRight);
        RegisterTransform(lowerArmRight);
        RegisterTransform(upperLegLeft);
        RegisterTransform(lowerLegLeft);
        RegisterTransform(upperLegRight);
        RegisterTransform(lowerLegRight);
    }

    private void RegisterTransform(Transform target, bool trackPosition = false)
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

    private void ApplyLocalRotation(Transform bone, Vector3 eulerOffset)
    {
        if (bone == null || !baseRotations.TryGetValue(bone, out Quaternion baseRotation))
            return;

        bone.localRotation = baseRotation * Quaternion.Euler(eulerOffset);
    }

    private void ApplySmoothedRotation(Transform bone, Vector3 eulerOffset)
    {
        if (bone == null || !baseRotations.TryGetValue(bone, out Quaternion baseRotation))
            return;

        Quaternion target = baseRotation * Quaternion.Euler(eulerOffset);
        bone.localRotation = Quaternion.Slerp(bone.localRotation, target, poseLerpSpeed * Time.deltaTime);
    }

    private void ApplyLocalPosition(Transform bone, Vector3 offset)
    {
        if (bone == null || !basePositions.TryGetValue(bone, out Vector3 basePosition))
            return;

        bone.localPosition = Vector3.Lerp(bone.localPosition, basePosition + offset, poseLerpSpeed * Time.deltaTime);
    }
}
