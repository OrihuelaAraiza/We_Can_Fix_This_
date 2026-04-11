using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerFakeRagdoll : MonoBehaviour
{
    [Header("References")]
    public Rigidbody rb;
    public PlayerMovement playerMovement;
    public Transform visualModel;

    [Header("Impact Settings")]
    public float minImpactForce = 7f;
    public float minHeavyBodyMass = 80f;

    [Header("Fake Fall")]
    public float stunDuration = 0.35f;
    public float recoverDuration = 0.25f;
    public float pushForce = 3f;
    public float upwardForce = 1f;
    public float tiltAngle = 35f;

    [Header("Protection")]
    public float fallCooldown = 1.2f;

    private bool isFalling = false;
    private bool onCooldown = false;
    private Quaternion initialVisualRotation;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();

        if (visualModel != null)
            initialVisualRotation = visualModel.localRotation;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isFalling || onCooldown) return;
        if (playerMovement == null || !playerMovement.IsInitialized) return;

        float impactForce = collision.relativeVelocity.magnitude;

        Rigidbody otherRb = collision.rigidbody;
        bool heavyBody = otherRb != null && otherRb.mass >= minHeavyBodyMass;

        // Solo caer si el impacto es fuerte Y el objeto es pesado
        if (!heavyBody || impactForce < minImpactForce) return;

        Vector3 hitDir;

        if (collision.contactCount > 0)
        {
            hitDir = transform.position - collision.GetContact(0).point;
            hitDir.y = 0f;
        }
        else
        {
            hitDir = -transform.forward;
        }

        if (hitDir.sqrMagnitude < 0.01f)
            hitDir = -transform.forward;

        hitDir.Normalize();

        StartCoroutine(FakeFallRoutine(hitDir));
    }

    private IEnumerator FakeFallRoutine(Vector3 hitDir)
    {
        isFalling = true;
        onCooldown = true;

        playerMovement.enabled = false;

        rb.velocity = new Vector3(rb.velocity.x * 0.35f, rb.velocity.y, rb.velocity.z * 0.35f);
        rb.AddForce((hitDir * pushForce) + (Vector3.up * upwardForce), ForceMode.Impulse);

        if (visualModel != null)
        {
            Vector3 tiltAxis = Vector3.Cross(Vector3.up, hitDir).normalized;
            Quaternion targetFallRotation = Quaternion.AngleAxis(tiltAngle, tiltAxis) * initialVisualRotation;

            float t = 0f;
            while (t < stunDuration)
            {
                t += Time.deltaTime;
                float lerp = Mathf.Clamp01(t / stunDuration);
                visualModel.localRotation = Quaternion.Slerp(initialVisualRotation, targetFallRotation, lerp);
                yield return null;
            }

            t = 0f;
            Quaternion currentRot = visualModel.localRotation;

            while (t < recoverDuration)
            {
                t += Time.deltaTime;
                float lerp = Mathf.Clamp01(t / recoverDuration);
                visualModel.localRotation = Quaternion.Slerp(currentRot, initialVisualRotation, lerp);
                yield return null;
            }

            visualModel.localRotation = initialVisualRotation;
        }
        else
        {
            yield return new WaitForSeconds(stunDuration + recoverDuration);
        }

        playerMovement.enabled = true;
        isFalling = false;

        yield return new WaitForSeconds(fallCooldown);
        onCooldown = false;
    }

    public bool IsFalling()
    {
        return isFalling;
    }
}