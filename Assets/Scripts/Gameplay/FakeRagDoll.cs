using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class FakeRagDoll : MonoBehaviour
{
    [Header("Enable / Disable")]
    [SerializeField] private bool ragdollEnabled = true;

    [Header("Impact Detection")]
    [SerializeField] private float hardHitThreshold = 4f;
    [SerializeField] private float impactCooldown = 0.25f;

    [Header("Visual Wobble")]
    [SerializeField] private float tiltAmount = 18f;
    [SerializeField] private float impactMultiplier = 0.6f;

    [Header("Ignore Layers")]
    [SerializeField] private LayerMask ignoreLayers;

    private PlayerMovement movement;
    private PlayerVisualWobble wobble;

    private float nextAllowedImpactTime;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        wobble = GetComponent<PlayerVisualWobble>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!ragdollEnabled)
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

        Vector3 hitDirection;

        if (collision.contactCount > 0)
        {
            hitDirection = -collision.GetContact(0).normal;
        }
        else
        {
            hitDirection = transform.position - collision.transform.position;
        }

        hitDirection = Vector3.ProjectOnPlane(hitDirection, Vector3.up);

        if (hitDirection.sqrMagnitude < 0.001f)
            hitDirection = -transform.forward;

        hitDirection.Normalize();

        if (wobble != null)
        {
            float finalTilt = tiltAmount + impact * impactMultiplier;
            wobble.AddImpactTilt(hitDirection, finalTilt);
        }

        nextAllowedImpactTime = Time.time + impactCooldown;
    }

    public void SetRagdollEnabled(bool enabled)
    {
        ragdollEnabled = enabled;
    }
}