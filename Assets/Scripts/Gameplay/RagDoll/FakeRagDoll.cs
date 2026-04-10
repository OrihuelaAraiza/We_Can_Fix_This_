using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerMovement))]
public class FakeRagdoll : MonoBehaviour
{
    [Header("Only Big Objects")]
    [SerializeField] private string bigObjectTag = "BigObstacle";
    [SerializeField] private LayerMask bigObjectLayers;

    [Header("Impact")]
    [SerializeField] private float impactThreshold = 7f;

    [Header("Reaction")]
    [SerializeField] private float pushForce = 4f;
    [SerializeField] private float upwardForce = 1f;
    [SerializeField] private float stumbleDuration = 0.6f;

    [Header("Rotation")]
    [SerializeField] private float turnAmount = 55f;
    [SerializeField] private float turnSpeed = 12f;

    [Header("Cooldown")]
    [SerializeField] private float cooldown = 0.6f;

    private Rigidbody rb;
    private PlayerMovement movement;
    private bool isStumbling = false;
    private float lastTriggerTime = -999f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        movement = GetComponent<PlayerMovement>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isStumbling) return;
        if (Time.time - lastTriggerTime < cooldown) return;
        if (movement == null || !movement.IsInitialized) return;

        bool validByTag = !string.IsNullOrEmpty(bigObjectTag) && collision.gameObject.CompareTag(bigObjectTag);
        bool validByLayer = ((1 << collision.gameObject.layer) & bigObjectLayers) != 0;

        if (!validByTag && !validByLayer)
            return;

        float impact = collision.relativeVelocity.magnitude;
        if (impact < impactThreshold)
            return;

        Vector3 hitDir = collision.contactCount > 0
            ? -collision.GetContact(0).normal
            : (transform.position - collision.transform.position).normalized;

        StartCoroutine(StumbleRoutine(hitDir));
    }

    private IEnumerator StumbleRoutine(Vector3 hitDir)
    {
        isStumbling = true;
        lastTriggerTime = Time.time;

        movement.SetControlLocked(true);

        Vector3 push = (hitDir.normalized * pushForce) + (Vector3.up * upwardForce);
        movement.AddImpactForce(push, ForceMode.Impulse);

        float randomTurn = Random.Range(-turnAmount, turnAmount);
        Quaternion startRot = transform.rotation;
        Quaternion targetRot = Quaternion.Euler(
            0f,
            transform.eulerAngles.y + randomTurn,
            0f
        );

        float timer = 0f;
        while (timer < stumbleDuration)
        {
            timer += Time.deltaTime;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                turnSpeed * Time.deltaTime
            );

            yield return null;
        }

        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        movement.SetControlLocked(false);
        isStumbling = false;
    }
}