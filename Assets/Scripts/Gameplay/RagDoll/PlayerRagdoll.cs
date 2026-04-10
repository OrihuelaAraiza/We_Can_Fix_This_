using UnityEngine;
using System.Collections;

public class PlayerRagdoll : MonoBehaviour
{
    [Header("Main References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody mainRb;
    [SerializeField] private Collider mainCollider;
    [SerializeField] private Transform hips;

    [Header("Scripts to disable during ragdoll")]
    [SerializeField] private MonoBehaviour[] scriptsToDisable;

    private Rigidbody[] ragdollBodies;
    private Collider[] ragdollColliders;
    private bool isRagdoll;

    public bool IsRagdoll => isRagdoll;

    private void Awake()
    {
        if (mainRb == null)
            mainRb = GetComponent<Rigidbody>();

        if (mainCollider == null)
            mainCollider = GetComponent<Collider>();

        ragdollBodies = GetComponentsInChildren<Rigidbody>(true);
        ragdollColliders = GetComponentsInChildren<Collider>(true);

        DisableRagdollParts();
    }

    public void EnableRagdoll(float duration, Vector3 hitDirection, float force)
    {
        if (isRagdoll) return;
        StartCoroutine(RagdollRoutine(duration, hitDirection, force));
    }

    private IEnumerator RagdollRoutine(float duration, Vector3 hitDirection, float force)
    {
        isRagdoll = true;

        if (animator != null)
            animator.enabled = false;

        if (mainRb != null)
        {
            mainRb.velocity = Vector3.zero;
            mainRb.angularVelocity = Vector3.zero;
            mainRb.isKinematic = true;
        }

        if (mainCollider != null)
            mainCollider.enabled = false;

        foreach (var s in scriptsToDisable)
        {
            if (s != null)
                s.enabled = false;
        }

        foreach (var body in ragdollBodies)
        {
            if (body == mainRb) continue;
            body.isKinematic = false;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        foreach (var col in ragdollColliders)
        {
            if (col == mainCollider) continue;
            col.enabled = true;
        }

        if (hips != null)
        {
            Rigidbody hipsRb = hips.GetComponent<Rigidbody>();
            if (hipsRb != null)
            {
                hipsRb.AddForce(hitDirection.normalized * force, ForceMode.Impulse);
            }
        }

        yield return new WaitForSeconds(duration);

        RecoverFromRagdoll();
    }

    private void RecoverFromRagdoll()
    {
        Vector3 recoverPos = hips != null ? hips.position : transform.position;
        recoverPos.y += 0.15f;
        transform.position = recoverPos;

        DisableRagdollParts();

        if (mainCollider != null)
            mainCollider.enabled = true;

        if (mainRb != null)
        {
            mainRb.isKinematic = false;
            mainRb.velocity = Vector3.zero;
            mainRb.angularVelocity = Vector3.zero;
        }

        if (animator != null)
            animator.enabled = true;

        foreach (var s in scriptsToDisable)
        {
            if (s != null)
                s.enabled = true;
        }

        isRagdoll = false;
    }

    private void DisableRagdollParts()
    {
        foreach (var body in ragdollBodies)
        {
            if (body == mainRb) continue;
            body.isKinematic = true;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        foreach (var col in ragdollColliders)
        {
            if (col == mainCollider) continue;
            col.enabled = false;
        }
    }
}