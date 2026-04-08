using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerMovement))]
public class FakeRagDoll : MonoBehaviour
{
    [Header("Impact")]
    [SerializeField] private float hardHitThreshold = 8f;
    [SerializeField] private float pushForce = 4f;
    [SerializeField] private float spinForce = 7f;

    [Header("Fake Fall")]
    [SerializeField] private float fallDuration = 0.8f;
    [SerializeField] private float recoverSpeed = 8f;

    [Header("Ignore Layers")]
    [SerializeField] private LayerMask ignoreLayers;

    private Rigidbody rb;
    private PlayerMovement movement;

    private bool isFalling = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        movement = GetComponent<PlayerMovement>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isFalling) return;
        if (movement == null || !movement.IsInitialized) return;

        // Ignorar capas que no quieres que activen el fake fall
        if (((1 << collision.gameObject.layer) & ignoreLayers) != 0)
            return;

        float impact = collision.relativeVelocity.magnitude;

        if (impact < hardHitThreshold)
            return;

        Vector3 hitDir = Vector3.zero;

        if (collision.contactCount > 0)
            hitDir = -collision.GetContact(0).normal;
        else
            hitDir = (transform.position - collision.transform.position).normalized;

        StartCoroutine(FakeFallRoutine(hitDir));
    }

    private IEnumerator FakeFallRoutine(Vector3 hitDir)
    {
        isFalling = true;

        // Bloquear control del jugador
        movement.SetExternalControlEnabled(false);

        // Quitar frenos de rotación para que se "tambalee/caiga"
        RigidbodyConstraints oldConstraints = rb.constraints;
        rb.constraints = RigidbodyConstraints.None;

        // Empujón
        rb.AddForce((hitDir.normalized + Vector3.up * 0.2f) * pushForce, ForceMode.Impulse);

        // Giro medio torpe
        Vector3 randomTorque = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-0.3f, 0.3f),
            Random.Range(-1f, 1f)
        ).normalized * spinForce;

        rb.AddTorque(randomTorque, ForceMode.Impulse);

        yield return new WaitForSeconds(fallDuration);

        // Enderezar jugador
        Vector3 euler = transform.eulerAngles;
        Quaternion upright = Quaternion.Euler(0f, euler.y, 0f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * recoverSpeed;
            transform.rotation = Quaternion.Slerp(transform.rotation, upright, t);
            yield return null;
        }

        // Restaurar velocidades para que no siga rodando raro
        rb.velocity = new Vector3(rb.velocity.x * 0.4f, 0f, rb.velocity.z * 0.4f);
        rb.angularVelocity = Vector3.zero;

        // Volver a congelar X y Z, dejando Y libre
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Reactivar control
        movement.SetExternalControlEnabled(true);

        isFalling = false;
    }
}