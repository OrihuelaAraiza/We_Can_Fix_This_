using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Runtime Info (debug)")]
    [SerializeField] private int playerIndex;
    [SerializeField] private bool initialized;
    [SerializeField] private bool isGrounded;

    public bool IsInitialized => initialized;
    public int PlayerIndex => playerIndex;

    private PlayerData data;
    private Rigidbody rb;
    private Transform cameraTransform;

    private float speedMultiplier = 1f;
    private float tempBoostMultiplier = 1f;

    private Vector2 moveInput;
    private bool jumpPressed;
    private bool controlLocked = false;

    public void Initialize(int index, PlayerData playerData, Transform cam)
    {
        playerIndex = index;
        data = playerData;
        cameraTransform = cam;

        if (data == null)
        {
            Debug.LogError($"[PlayerMovement] PlayerData NULL on player {index}!");
            return;
        }

        rb = GetComponent<Rigidbody>();
        rb.mass = data.mass;
        rb.drag = data.groundDrag;
        rb.angularDrag = 0.05f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        ApplyColor(index);

        initialized = true;
        Debug.Log($"[PlayerMovement] Player {index} initialized. Mass:{rb.mass} Cam:{(cam != null ? cam.name : "NULL")}");
    }

    public void OnMove(Vector2 input)
    {
        if (controlLocked) return;
        moveInput = input;
    }

    public void OnJump()
    {
        if (initialized && !controlLocked)
            jumpPressed = true;
    }

    public void SetControlLocked(bool locked)
    {
        controlLocked = locked;

        if (locked)
        {
            moveInput = Vector2.zero;
            jumpPressed = false;
        }
    }

    public void AddImpactForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        rb.AddForce(force, mode);
    }

    private void Update()
    {
        if (!initialized) return;
        rb.drag = isGrounded ? data.groundDrag : data.airDrag;
    }

    private void FixedUpdate()
    {
        if (!initialized) return;

        Move();

        if (jumpPressed)
        {
            TryJump();
            jumpPressed = false;
        }

        LimitSpeed();
        ApplyWobble();
    }

    private void Move()
    {
        if (controlLocked) return;
        if (moveInput.sqrMagnitude < 0.01f) return;

        Vector3 forward = cameraTransform != null
            ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
            : Vector3.forward;

        Vector3 right = cameraTransform != null
            ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized
            : Vector3.right;

        Vector3 dir = (forward * moveInput.y + right * moveInput.x).normalized;
        float control = isGrounded ? 1f : data.airControlMultiplier;

        rb.AddForce(
            dir * (data.moveForce * control * 2f * speedMultiplier * tempBoostMultiplier),
            ForceMode.Force
        );

        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            rb.rotation = Quaternion.Slerp(
                rb.rotation,
                targetRot,
                data.rotationSpeed * Time.fixedDeltaTime
            );
        }
    }

    private void LimitSpeed()
    {
        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        if (flatVel.magnitude > data.maxSpeed)
        {
            Vector3 capped = flatVel.normalized * data.maxSpeed;
            rb.velocity = new Vector3(capped.x, rb.velocity.y, capped.z);
        }
    }

    private void TryJump()
    {
        if (controlLocked) return;
        if (!isGrounded) return;

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(Vector3.up * data.jumpForce, ForceMode.Impulse);

        isGrounded = false;

        Debug.Log($"[PlayerMovement] Player {playerIndex} jumped!");
    }

    private void ApplyWobble()
    {
        if (controlLocked) return;
        if (!isGrounded || moveInput.sqrMagnitude < 0.1f) return;

        Vector3 wobble = new Vector3(
            Random.Range(-1f, 1f),
            0f,
            Random.Range(-1f, 1f)
        ) * data.wobbleTorque * Time.fixedDeltaTime;

        rb.AddTorque(wobble, ForceMode.Force);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!initialized || data == null) return;

        if (((1 << collision.gameObject.layer) & data.groundLayer) == 0)
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;

            if (Vector3.Dot(normal, Vector3.up) > 0.4f)
            {
                isGrounded = true;
                return;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (!initialized || data == null) return;

        if (((1 << collision.gameObject.layer) & data.groundLayer) == 0)
            return;

        isGrounded = false;
    }

    private void ApplyColor(int index)
    {
        if (data.playerColors == null || data.playerColors.Length == 0) return;

        Color color = data.playerColors[index % data.playerColors.Length];

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[PlayerMovement] No renderers found on player {index}");
            return;
        }

        foreach (Renderer r in renderers)
        {
            r.material = new Material(r.sharedMaterial) { color = color };
        }

        Debug.Log($"[PlayerMovement] Color applied: {color} to {renderers.Length} renderers");
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    public void ApplyTemporarySpeedBoost(float multiplier, float duration)
    {
        tempBoostMultiplier = multiplier;
        StartCoroutine(RemoveBoostAfter(duration));
    }

    System.Collections.IEnumerator RemoveBoostAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        tempBoostMultiplier = 1f;
        Debug.Log("[PlayerMovement] Speed boost expired");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.down * 0.9f, 0.2f);

        if (rb != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, rb.velocity);
        }
    }
}