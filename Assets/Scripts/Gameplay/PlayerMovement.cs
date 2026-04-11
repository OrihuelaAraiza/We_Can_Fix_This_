using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    // ── Inspector debug (readonly en runtime) ─────────────────
    [Header("Runtime Info (debug)")]
    [SerializeField] private int playerIndex;
    [SerializeField] private bool initialized;

    public bool IsInitialized => initialized;
    public int PlayerIndex => playerIndex;

    // ── Datos ─────────────────────────────────────────────────
    private PlayerData data;
    private Rigidbody rb;
    private Transform cameraTransform;

    // ── Speed multipliers (rol system) ───────────────────────
    private float speedMultiplier = 1f;
    private float tempBoostMultiplier = 1f;

    // ── Input state ───────────────────────────────────────────
    private Vector2 moveInput;
    private bool jumpPressed;
    private bool isGrounded;

    // ── Init ──────────────────────────────────────────────────
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

        Debug.Log($"[PlayerMovement] Player {index} initialized. " +
                  $"Mass:{rb.mass} Cam:{(cam != null ? cam.name : "NULL")}");
    }

    // ── Input callbacks ───────────────────────────────────────
    public void OnMove(Vector2 input) => moveInput = input;

    public void OnJump()
    {
        if (initialized) jumpPressed = true;
    }

    // ── Unity lifecycle ───────────────────────────────────────
    private void Update()
    {
        if (!initialized) return;

        CheckGround();
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

    // ── Ground ────────────────────────────────────────────────
    private void CheckGround()
    {
        isGrounded = Physics.Raycast(
            transform.position + Vector3.up * 0.1f,
            Vector3.down,
            data.groundCheckDistance + 0.6f,
            data.groundLayer
        );
    }

    // ── Movement ──────────────────────────────────────────────
    private void Move()
    {
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
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRot, data.rotationSpeed * Time.fixedDeltaTime);
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
        if (!isGrounded) return;

        rb.velocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        rb.AddForce(Vector3.up * data.jumpForce, ForceMode.Impulse);

        Debug.Log($"[PlayerMovement] Player {playerIndex} jumped!");
    }

    private void ApplyWobble()
    {
        if (!isGrounded || moveInput.sqrMagnitude < 0.1f) return;

        Vector3 wobble = new Vector3(
            Random.Range(-1f, 1f),
            0f,
            Random.Range(-1f, 1f)
        ) * data.wobbleTorque * Time.fixedDeltaTime;

        rb.AddTorque(wobble, ForceMode.Force);
    }

    // ── Color ─────────────────────────────────────────────────
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

    // ── Speed multiplier API (rol system) ────────────────────
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

    // ── Gizmos ────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, transform.position + Vector3.down * 0.65f);

        if (rb != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, rb.velocity);
        }
    }
}