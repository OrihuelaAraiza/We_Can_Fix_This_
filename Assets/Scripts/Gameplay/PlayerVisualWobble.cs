using UnityEngine;

public class PlayerVisualWobble : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visual;
    [SerializeField] private Rigidbody rb;

    [Header("Movement Wobble")]
    [SerializeField] private float tiltAmount = 8f;
    [SerializeField] private float tiltSmooth = 10f;

    [Header("Idle Motion")]
    [SerializeField] private float idleBreathAmount = 0.015f;
    [SerializeField] private float idleBreathSpeed = 2f;

    [Header("Stride Motion")]
    [SerializeField] private float strideBounceAmount = 0.035f;
    [SerializeField] private float stridePitchAmount = 3f;
    [SerializeField] private float strideRollAmount = 2.5f;
    [SerializeField] private float strideFrequency = 9f;

    [Header("Impact Reaction")]
    [SerializeField] private float impactTiltMultiplier = 1f;
    [SerializeField] private float impactRecoverySpeed = 7f;
    [SerializeField] private float maxImpactTilt = 25f;

    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;
    private Quaternion extraImpactRotation = Quaternion.identity;

    private bool initialized;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        ResolveVisual();

        if (visual != null)
        {
            initialLocalPos = visual.localPosition;
            initialLocalRot = visual.localRotation;
            initialized = true;
        }
    }

    private void LateUpdate()
    {
        if (!initialized || visual == null || rb == null)
            return;

        Vector3 planarVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        float planarSpeed = planarVelocity.magnitude;
        float moveBlend = Mathf.Clamp01(planarSpeed / 4f);

        Vector3 localVel = transform.InverseTransformDirection(planarVelocity);

        float stridePhase = Time.time * Mathf.Lerp(idleBreathSpeed, strideFrequency, moveBlend);

        float movementTiltX = -localVel.z * tiltAmount * 0.08f;
        float movementTiltZ = localVel.x * tiltAmount * 0.08f;

        float stridePitch = Mathf.Sin(stridePhase) * stridePitchAmount * moveBlend;
        float strideRoll = Mathf.Cos(stridePhase) * strideRollAmount * moveBlend;

        Quaternion movementRotation = Quaternion.Euler(
            movementTiltX + stridePitch,
            0f,
            movementTiltZ + strideRoll
        );

        Quaternion targetRotation = initialLocalRot * movementRotation * extraImpactRotation;

        visual.localRotation = Quaternion.Slerp(
            visual.localRotation,
            targetRotation,
            tiltSmooth * Time.deltaTime
        );

        float idleBounce = Mathf.Sin(Time.time * idleBreathSpeed) * idleBreathAmount;
        float strideBounce = Mathf.Abs(Mathf.Sin(stridePhase)) * strideBounceAmount * moveBlend;

        Vector3 targetPosition = initialLocalPos + new Vector3(0f, idleBounce + strideBounce, 0f);

        visual.localPosition = Vector3.Lerp(
            visual.localPosition,
            targetPosition,
            tiltSmooth * Time.deltaTime
        );

        extraImpactRotation = Quaternion.Slerp(
            extraImpactRotation,
            Quaternion.identity,
            impactRecoverySpeed * Time.deltaTime
        );
    }

    public void BindVisual(Transform targetVisual)
    {
        if (targetVisual == null)
            return;

        visual = targetVisual;
        initialLocalPos = visual.localPosition;
        initialLocalRot = visual.localRotation;
        initialized = true;
    }

    public void AddImpactTilt(Vector3 worldDirection, float amount)
    {
        if (!initialized || visual == null)
            return;

        Vector3 localDir = transform.InverseTransformDirection(worldDirection.normalized);

        float clampedAmount = Mathf.Clamp(amount * impactTiltMultiplier, 0f, maxImpactTilt);

        float tiltX = localDir.z * clampedAmount;
        float tiltZ = -localDir.x * clampedAmount;

        extraImpactRotation = Quaternion.Euler(tiltX, 0f, tiltZ);
    }

    private void ResolveVisual()
    {
        if (visual != null && visual != transform)
            return;

        Transform modelRoot = transform.Find("ModelRoot");

        if (modelRoot != null && modelRoot.childCount > 0)
        {
            visual = modelRoot.GetChild(0);
            return;
        }

        Animator animator = GetComponentInChildren<Animator>(true);

        if (animator != null && animator.transform != transform)
        {
            visual = animator.transform;
            return;
        }

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            if (renderer.transform == transform)
                continue;

            visual = renderer.transform;
            return;
        }

        Debug.LogWarning("[PlayerVisualWobble] No se encontró un visual hijo. No se aplicará wobble.", this);
        visual = null;
    }
}