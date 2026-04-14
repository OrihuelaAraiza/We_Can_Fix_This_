using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerMovement))]
public class FakeRagDoll : MonoBehaviour
{
    [Header("Impact")]
    [SerializeField] private float hardHitThreshold = 8f;
    [SerializeField] private float pushForce = 5.5f;
    [SerializeField] private float upwardForce = 1.1f;
    [SerializeField] private float impactCooldown = 0.35f;

    [Header("Recovery")]
    [SerializeField] private float fallDuration = 0.55f;
    [SerializeField] private float planarRecoveryDamping = 7f;

    [Header("Visual Reaction")]
    [SerializeField] private float heavyTiltAmount = 18f;

    [Header("Ignore Layers")]
    [SerializeField] private LayerMask ignoreLayers;

    private Rigidbody rb;
    private PlayerMovement movement;
    private PlayerVisualWobble wobble;

    private bool isRecovering;
    private float nextAllowedImpactTime;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnCollisionEnter(Collision collision)
    {
        ResolveReferences();

        if (isRecovering)
            return;

        if (movement == null || !movement.IsInitialized)
            return;

        if (Time.time < nextAllowedImpactTime)
            return;

        if (((1 << collision.gameObject.layer) & ignoreLayers.value) != 0)
            return;

        float impact = collision.relativeVelocity.magnitude;
        if (impact < hardHitThreshold)
            return;

        Vector3 hitDirection = collision.contactCount > 0
            ? -collision.GetContact(0).normal
            : (transform.position - collision.transform.position).normalized;

        StartCoroutine(KnockbackRoutine(hitDirection, impact));
    }

    private IEnumerator KnockbackRoutine(Vector3 hitDirection, float impact)
    {
        isRecovering = true;
        nextAllowedImpactTime = Time.time + impactCooldown;

        movement.SetExternalControlEnabled(false);

        Vector3 planarDirection = Vector3.ProjectOnPlane(hitDirection, Vector3.up);
        if (planarDirection.sqrMagnitude < 0.001f)
            planarDirection = transform.forward;

        planarDirection.Normalize();

        if (wobble != null)
            wobble.AddImpactTilt(planarDirection, heavyTiltAmount + impact * 0.4f);

        Vector3 knockback = planarDirection * Mathf.Max(pushForce, impact * 0.3f);
        knockback += Vector3.up * upwardForce;
        rb.AddForce(knockback, ForceMode.Impulse);

        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed += Time.fixedDeltaTime;

            Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.velocity, Vector3.up);
            Vector3 dampedPlanarVelocity = Vector3.Lerp(
                planarVelocity,
                Vector3.zero,
                planarRecoveryDamping * Time.fixedDeltaTime);

            rb.velocity = new Vector3(dampedPlanarVelocity.x, rb.velocity.y, dampedPlanarVelocity.z);
            rb.angularVelocity = Vector3.zero;

            yield return new WaitForFixedUpdate();
        }

        rb.angularVelocity = Vector3.zero;
        movement.SetExternalControlEnabled(true);
        isRecovering = false;
    }

    private void ResolveReferences()
    {
        rb ??= GetComponent<Rigidbody>();
        movement ??= GetComponent<PlayerMovement>();
        wobble ??= GetComponent<PlayerVisualWobble>();
    }
}
