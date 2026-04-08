using UnityEngine;
using System.Collections;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerImpactReaction : MonoBehaviour
{
    [Header("Thresholds")]
    [SerializeField] private float lightHitThreshold = 3.5f;
    [SerializeField] private float mediumHitThreshold = 6.5f;
    [SerializeField] private float hardHitThreshold = 10.5f;

    [Header("Light Hit")]
    [SerializeField] private float lightPushForce = 1.2f;
    [SerializeField] private float lightTiltAmount = 6f;

    [Header("Medium Hit")]
    [SerializeField] private float mediumPushForce = 2.5f;
    [SerializeField] private float mediumTiltAmount = 12f;
    [SerializeField] private float mediumStunDuration = 0.35f;

    [Header("Hard Hit")]
    [SerializeField] private float ragdollDuration = 1.0f;
    [SerializeField] private float ragdollForceMultiplier = 1.2f;

    [Header("Cooldown")]
    [SerializeField] private float hitCooldown = 0.2f;

    private PlayerMovement movement;
    private Rigidbody rb;
    private PlayerRagdoll ragdoll;
    private PlayerVisualWobble wobble;

    private bool stunned;
    private float lastHitTime;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody>();
        ragdoll = GetComponent<PlayerRagdoll>();
        wobble = GetComponentInChildren<PlayerVisualWobble>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!movement.IsInitialized) return;
        if (Time.time - lastHitTime < hitCooldown) return;
        if (collision.contactCount == 0) return;

        float impact = collision.relativeVelocity.magnitude;
        Vector3 hitDir = -collision.GetContact(0).normal;

        if (impact >= hardHitThreshold)
        {
            lastHitTime = Time.time;

            if (ragdoll != null)
            {
                ragdoll.EnableRagdoll
                    (
                    ragdollDuration,
                    hitDir,
                    impact * ragdollForceMultiplier
                );
            }

            return;
        }

        if (impact >= mediumHitThreshold)
        {
            lastHitTime = Time.time;

            movement.AddImpactForce(hitDir * mediumPushForce, ForceMode.Impulse);

            if (wobble != null)
                wobble.AddImpactTilt(hitDir, mediumTiltAmount);

            StartCoroutine(DoStun(mediumStunDuration));
            return;
        }

        if (impact >= lightHitThreshold)
        {
            lastHitTime = Time.time;

            movement.AddImpactForce(hitDir * lightPushForce, ForceMode.Impulse);

            if (wobble != null)
                wobble.AddImpactTilt(hitDir, lightTiltAmount);
        }
    }

    private IEnumerator DoStun(float duration)
    {
        if (stunned) yield break;

        stunned = true;

        if (movement != null)
            movement.SetExternalControlEnabled(false);

        yield return new WaitForSeconds(duration);

        if (movement != null)
            movement.SetExternalControlEnabled(true);

        stunned = false;
    }
}