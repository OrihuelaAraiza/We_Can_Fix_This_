using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(PlayerMovement))]
public class FakeRagDoll : MonoBehaviour
{
    [Header("Enable / Disable")]
    [SerializeField] private bool ragdollEnabled = true;

    [Header("Impact Detection")]
    [SerializeField] private float hardHitThreshold = 2.8f;
    [SerializeField] private float impactCooldown = 0.35f;
    [SerializeField] private float minimumPlayerSpeed = 1.25f;

    [Header("Visual Wobble")]
    [SerializeField] private float tiltAmount = 18f;
    [SerializeField] private float impactMultiplier = 0.85f;
    [SerializeField] private float maxTiltAmount = 34f;

    [Header("Clumsy Push")]
    [SerializeField] private float basePushForce = 1.8f;
    [SerializeField] private float impactPushMultiplier = 0.32f;
    [SerializeField] private float maxPushForce = 4.2f;
    [SerializeField] private float upwardLift = 0.18f;
    [SerializeField] private float stunDuration = 0.28f;

    [Header("Ignore Layers")]
    [SerializeField] private LayerMask ignoreLayers;

    private PlayerMovement movement;
    private PlayerVisualWobble wobble;
    private Rigidbody rb;
    private Coroutine stunCoroutine;

    private float nextAllowedImpactTime;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        wobble = GetComponent<PlayerVisualWobble>();
        rb = GetComponent<Rigidbody>();
    }

    private void OnDisable()
    {
        CancelStun();
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

        if (!IsNpcCollision(collision))
            return;

        float impact = GetImpactStrength(collision);

        if (impact < hardHitThreshold && movement.PlanarSpeed < minimumPlayerSpeed)
            return;

        Vector3 hitDirection = ResolveHitDirection(collision);

        ApplyClumsyPush(hitDirection, impact);

        if (wobble != null)
        {
            float finalTilt = Mathf.Min(maxTiltAmount, tiltAmount + impact * impactMultiplier);
            wobble.AddImpactTilt(hitDirection, finalTilt);
        }

        if (stunDuration > 0f)
        {
            if (stunCoroutine != null)
                StopCoroutine(stunCoroutine);

            stunCoroutine = StartCoroutine(StunBriefly());
        }

        nextAllowedImpactTime = Time.time + impactCooldown;
    }

    private bool IsNpcCollision(Collision collision)
    {
        if (collision == null)
            return false;

        if (IsNpcTransform(collision.transform))
            return true;

        if (collision.collider != null && IsNpcTransform(collision.collider.transform))
            return true;

        return collision.gameObject != null && IsNpcTransform(collision.gameObject.transform);
    }

    private bool IsNpcTransform(Transform hitTransform)
    {
        if (hitTransform == null)
            return false;

        if (hitTransform.GetComponentInParent<NPCBehaviourBase>() != null)
            return true;

        return hitTransform.GetComponentInParent<NavMeshAgent>() != null;
    }

    private float GetImpactStrength(Collision collision)
    {
        float relativeImpact = collision != null ? collision.relativeVelocity.magnitude : 0f;
        return Mathf.Max(relativeImpact, movement != null ? movement.PlanarSpeed : 0f);
    }

    private Vector3 ResolveHitDirection(Collision collision)
    {
        Vector3 hitDirection = Vector3.zero;

        if (collision != null && collision.contactCount > 0)
            hitDirection = transform.position - collision.GetContact(0).point;
        else if (collision != null && collision.transform != null)
            hitDirection = transform.position - collision.transform.position;

        hitDirection = Vector3.ProjectOnPlane(hitDirection, Vector3.up);

        if (hitDirection.sqrMagnitude < 0.001f && rb != null)
            hitDirection = Vector3.ProjectOnPlane(rb.velocity, Vector3.up);

        if (hitDirection.sqrMagnitude < 0.001f)
            hitDirection = -transform.forward;

        return hitDirection.normalized;
    }

    private void ApplyClumsyPush(Vector3 hitDirection, float impact)
    {
        if (rb == null)
            return;

        float pushForce = Mathf.Min(maxPushForce, basePushForce + impact * impactPushMultiplier);
        Vector3 push = hitDirection * pushForce;
        push.y = upwardLift;

        rb.AddForce(push, ForceMode.Impulse);
    }

    private IEnumerator StunBriefly()
    {
        movement.SetExternalControlEnabled(false);

        yield return new WaitForSeconds(stunDuration);

        movement.SetExternalControlEnabled(true);
        stunCoroutine = null;
    }

    public void SetRagdollEnabled(bool enabled)
    {
        ragdollEnabled = enabled;

        if (!enabled)
            CancelStun();
    }

    private void CancelStun()
    {
        if (stunCoroutine != null)
            StopCoroutine(stunCoroutine);

        stunCoroutine = null;

        if (movement != null)
            movement.SetExternalControlEnabled(true);
    }
}
