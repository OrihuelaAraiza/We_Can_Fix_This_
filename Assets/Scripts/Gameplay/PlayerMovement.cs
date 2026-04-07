using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Runtime Info (debug)")]
    [SerializeField] private int playerIndex;
    [SerializeField] private bool initialized;

    public bool IsInitialized => initialized;
    public int PlayerIndex => playerIndex;

    private PlayerData data;
    private Rigidbody rb;
    private Transform cameraTransform;

    private float speedMultiplier = 1f;
    private float tempBoostMultiplier = 1f;

    private Vector2 moveInput;
    private bool jumpPressed;
    private bool isGrounded;

    private bool externalControlEnabled = true;
    private Coroutine boostCoroutine;

    public Rigidbody RB => rb;
    public bool IsGrounded => isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

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

        if (rb == null)
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
        if (!externalControlEnabled) return;
        moveInput = input;
    }

    public void OnJump()
    {
        if (!initialized || !externalControlEnabled) return;
        jumpPressed = true;
    }

    public void SetExternalControlEnabled(bool enabled)
    {
        externalControlEnabled = enabled;

        if (!enabled)
        {
            moveInput = Vector2.zero;
            jumpPressed = false;
        }
    }

    private void Update()
    {
        if (!initialized) return;

        CheckGround();
        rb.drag = isGrounded ? data.groundDrag : data.airDrag;
    }

    private void FixedUpdate()
    {
        if (!initialized) return;

        if (externalControlEnabled)
        {
            Move();

            if (jumpPressed)
            {
                TryJump();
                jumpPressed = false;
            }
        }

        LimitSpeed();
    }

    private void CheckGround()
    {
        if (data == null) return;

        isGrounded = Physics.Raycast(
            transform.position + Vector3.up * 0.1f,
            Vector3.down,
            data.groundCheckDistance + 0.6f,
            data.groundLayer
        );
    }

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
        float finalForce = data.moveForce * control * 2f * speedMultiplier * tempBoostMultiplier;

        rb.AddForce(dir * finalForce, ForceMode.Force);

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
        if (data == null) return;

        float currentMaxSpeed = data.maxSpeed * speedMultiplier * tempBoostMultiplier;

        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);

        if (flatVel.magnitude > currentMaxSpeed)
        {
            Vector3 capped = flatVel.normalized * currentMaxSpeed;
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

    public void AddImpactForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
    {
        if (rb == null) return;
        rb.AddForce(force, mode);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    public void ApplyTemporarySpeedBoost(float multiplier, float duration)
    {
        tempBoostMultiplier = multiplier;

        if (boostCoroutine != null)
            StopCoroutine(boostCoroutine);

        boostCoroutine = StartCoroutine(RemoveBoostAfter(duration));
    }

    private IEnumerator RemoveBoostAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        tempBoostMultiplier = 1f;
        boostCoroutine = null;
        Debug.Log("[PlayerMovement] Speed boost expired");
    }

    private void ApplyColor(int index)
    {
        if (data == null || data.playerColors == null || data.playerColors.Length == 0) return;

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

    private void OnDrawGizmosSelected()
    {
        float rayDistance = 0.65f;

        if (data != null)
            rayDistance = data.groundCheckDistance + 0.6f;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(
            transform.position + Vector3.up * 0.1f,
            transform.position + Vector3.up * 0.1f + Vector3.down * rayDistance
        );

        if (rb != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, rb.velocity);
        }
    }
}