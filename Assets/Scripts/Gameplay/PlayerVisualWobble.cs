using UnityEngine;

public class PlayerVisualWobble : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform visual;
    [SerializeField] private Rigidbody rb;

    [Header("Tilt")]
    [SerializeField] private float tiltAmount = 10f;
    [SerializeField] private float tiltSmooth = 8f;

    [Header("Bounce")]
    [SerializeField] private float bounceAmount = 0.04f;
    [SerializeField] private float bounceSpeed = 10f;

    [Header("Impact Reaction")]
    [SerializeField] private float impactTiltMultiplier = 1.5f;
    [SerializeField] private float impactRecoverySpeed = 6f;

    private Vector3 initialLocalPos;
    private Quaternion extraImpactRotation = Quaternion.identity;

    private void Awake()
    {
        if (visual == null)
            visual = transform;

        if (rb == null)
            rb = GetComponentInParent<Rigidbody>();

        initialLocalPos = visual.localPosition;
    }

    public void BindVisual(Transform targetVisual)
    {
        if (targetVisual == null)
            return;

        visual = targetVisual;
        initialLocalPos = visual.localPosition;
    }

    private void LateUpdate()
    {
        if (visual == null || rb == null) return;

        Vector3 localVel = transform.InverseTransformDirection(rb.velocity);

        float tiltX = -localVel.z * tiltAmount * 0.1f;
        float tiltZ = localVel.x * tiltAmount * 0.1f;

        Quaternion moveTilt = Quaternion.Euler(tiltX, 0f, tiltZ);
        Quaternion targetRot = moveTilt * extraImpactRotation;

        visual.localRotation = Quaternion.Slerp(
            visual.localRotation,
            targetRot,
            tiltSmooth * Time.deltaTime
        );

        float speedFactor = Mathf.Clamp01(new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude * 0.12f);
        float bounce = Mathf.Sin(Time.time * bounceSpeed) * speedFactor * bounceAmount;

        Vector3 targetPos = initialLocalPos + new Vector3(0f, bounce, 0f);
        visual.localPosition = Vector3.Lerp(
            visual.localPosition,
            targetPos,
            tiltSmooth * Time.deltaTime
        );

        extraImpactRotation = Quaternion.Slerp(
            extraImpactRotation,
            Quaternion.identity,
            impactRecoverySpeed * Time.deltaTime
        );
    }

    public void AddImpactTilt(Vector3 worldDirection, float amount)
    {
        if (visual == null) return;

        Vector3 localDir = transform.InverseTransformDirection(worldDirection.normalized);

        float tiltX = localDir.z * amount * impactTiltMultiplier;
        float tiltZ = -localDir.x * amount * impactTiltMultiplier;

        extraImpactRotation *= Quaternion.Euler(tiltX, 0f, tiltZ);
    }
}
