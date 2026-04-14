using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Runtime Info (debug)")]
    [SerializeField] private int playerIndex;
    [SerializeField] private bool initialized;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Runtime Motion")]
    [SerializeField] private bool externalControlEnabled = true;
    [SerializeField] private bool isGrounded;

    public bool IsInitialized => initialized;
    public int PlayerIndex => playerIndex;
    public Rigidbody RB => rb;
    public bool IsGrounded => isGrounded;

    /// <summary>Normalized speed 0..1 for animator blend trees.</summary>
    public float SpeedNormalized => rb != null && data != null
        ? Mathf.Clamp01(PlanarSpeed / data.maxSpeed)
        : 0f;

    /// <summary>Raw planar speed in m/s.</summary>
    public float PlanarSpeed => rb != null
        ? new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude
        : 0f;

    // ── Internal state ──────────────────────────────────────────
    private PlayerData data;
    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Transform cameraTransform;

    private float speedMultiplier = 1f;
    private float tempBoostMultiplier = 1f;
    private Vector2 moveInput;
    private bool jumpQueued;
    private float lastGroundedTime = -10f;
    private Coroutine boostCoroutine;

    // ── Wobble constants ────────────────────────────────────────
    private const float CoyoteTime = 0.1f;
    private const float GroundProbePadding = 0.06f;
    private const float GroundMaxSlope = 55f;

    // ── Init ────────────────────────────────────────────────────
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        ResolveAnimatorReference();
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

        capsule ??= GetComponent<CapsuleCollider>();

        // Wobbly astronaut: unconstrained rotations, let physics tumble
        rb.mass = Mathf.Max(0.01f, data.mass);
        rb.drag = data.groundDrag;
        rb.angularDrag = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        // Keep character upright — visual wobble is handled by
        // PlayerVisualWobble and FixieProceduralAnimator.
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        ResolveAnimatorReference();
        externalControlEnabled = true;
        moveInput = Vector2.zero;
        jumpQueued = false;
        CheckGround();

        initialized = true;
        Debug.Log($"[PlayerMovement] Player {index} initialized. Mass:{rb.mass} Cam:{(cam != null ? cam.name : "NULL")}");
    }

    // ── Input (called by PlayerInputHandler) ────────────────────
    public void OnMove(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    public void OnJump()
    {
        if (!initialized || !externalControlEnabled)
            return;

        if (isGrounded || Time.time - lastGroundedTime <= CoyoteTime)
            jumpQueued = true;
    }

    public void SetExternalControlEnabled(bool enabled)
    {
        externalControlEnabled = enabled;
        if (!enabled)
        {
            jumpQueued = false;
            moveInput = Vector2.zero;
        }
    }

    // ── Update loops ────────────────────────────────────────────
    private void Update()
    {
        if (!initialized || data == null)
            return;

        CheckGround();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (!initialized || data == null || rb == null)
            return;

        CheckGround();
        ApplyDrag();

        if (externalControlEnabled)
        {
            ApplyMovementForce();
            ApplyFacing();
        }

        if (jumpQueued)
        {
            DoJump();
            jumpQueued = false;
        }

        LimitPlanarSpeed();
    }

    // ── Movement via AddForce ───────────────────────────────────
    private void ApplyMovementForce()
    {
        if (moveInput.sqrMagnitude < 0.01f)
            return;

        Vector3 dir = GetMoveDirection();
        if (dir.sqrMagnitude < 0.001f)
            return;

        float control = isGrounded ? 1f : Mathf.Clamp(data.airControlMultiplier, 0.1f, 1f);
        float force = data.moveForce * speedMultiplier * tempBoostMultiplier * control;
        rb.AddForce(dir * force, ForceMode.Force);
    }

    private void LimitPlanarSpeed()
    {
        Vector3 flat = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        float limit = data.maxSpeed * speedMultiplier * tempBoostMultiplier;
        if (flat.magnitude > limit)
        {
            Vector3 clamped = flat.normalized * limit;
            rb.velocity = new Vector3(clamped.x, rb.velocity.y, clamped.z);
        }
    }

    private void ApplyFacing()
    {
        Vector3 dir = GetMoveDirection();
        if (dir.sqrMagnitude < 0.01f)
            return;

        Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
        float turnSpeed = Mathf.Max(540f, data.rotationSpeed * 60f);
        Quaternion next = Quaternion.RotateTowards(rb.rotation, target, turnSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(next);
    }

    // ── Jump ────────────────────────────────────────────────────
    private void DoJump()
    {
        isGrounded = false;
        lastGroundedTime = -10f;

        Vector3 v = rb.velocity;
        v.y = 0f;
        rb.velocity = v;
        rb.AddForce(Vector3.up * data.jumpForce, ForceMode.Impulse);
    }

    // ── Collision bump (ragdoll-style) ──────────────────────────
    private void OnCollisionEnter(Collision collision)
    {
        if (!initialized || rb == null)
            return;

        // Don't bump on ground
        if (collision.contactCount == 0)
            return;

        Vector3 normal = collision.GetContact(0).normal;
        if (Vector3.Angle(normal, Vector3.up) < 45f)
            return;

        float impact = collision.relativeVelocity.magnitude;
        if (impact < 3f)
            return;

        Vector3 bumpDir = (transform.position - collision.GetContact(0).point).normalized;
        bumpDir.y = Mathf.Max(bumpDir.y, 0.15f);
        bumpDir.Normalize();
        float bumpForce = Mathf.Min(impact * 0.25f, 5f);
        rb.AddForce(bumpDir * bumpForce, ForceMode.Impulse);
    }

    // ── Drag ────────────────────────────────────────────────────
    private void ApplyDrag()
    {
        rb.drag = isGrounded ? data.groundDrag : data.airDrag;
    }

    // ── Ground check ────────────────────────────────────────────
    private void CheckGround()
    {
        if (data == null)
            return;

        LayerMask mask = data.groundLayer.value == 0 ? ~0 : data.groundLayer;
        Vector3 origin = transform.position + Vector3.up * 0.2f;
        float radius = 0.2f;
        float distance = data.groundCheckDistance + GroundProbePadding;

        if (capsule != null)
        {
            float radiusScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            float heightScale = Mathf.Abs(transform.lossyScale.y);
            radius = Mathf.Max(0.05f, capsule.radius * radiusScale * 0.92f);

            float halfHeight = Mathf.Max(radius, capsule.height * heightScale * 0.5f);
            Vector3 worldCenter = transform.TransformPoint(capsule.center);
            origin = worldCenter - Vector3.up * Mathf.Max(0f, halfHeight - radius - GroundProbePadding);
            distance = data.groundCheckDistance + GroundProbePadding * 2f;
        }

        RaycastHit[] hits = Physics.SphereCastAll(origin, radius, Vector3.down, distance, mask, QueryTriggerInteraction.Ignore);
        bool foundGround = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
                continue;

            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            if (Vector3.Angle(hit.normal, Vector3.up) > GroundMaxSlope)
                continue;

            foundGround = true;
            break;
        }

        isGrounded = foundGround;
        if (foundGround)
            lastGroundedTime = Time.time;
    }

    // ── Camera-relative direction ───────────────────────────────
    private Vector3 GetMoveDirection()
    {
        if (moveInput.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        Vector3 forward = cameraTransform != null
            ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
            : Vector3.forward;

        Vector3 right = cameraTransform != null
            ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized
            : Vector3.right;

        Vector3 dir = forward * moveInput.y + right * moveInput.x;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.zero;
    }

    // ── Animator ─────────────────────────────────────────────────
    private void UpdateAnimator()
    {
        if (animator == null || rb == null)
            return;

        animator.SetFloat("Speed", PlanarSpeed);
    }

    public void BindAnimator(Animator targetAnimator)
    {
        animator = targetAnimator;

        if (animator != null)
        {
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.SetFloat("Speed", 0f);
            animator.Rebind();
            animator.Update(0f);
        }
    }

    private void ResolveAnimatorReference()
    {
        if (animator != null)
            return;

        animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
        {
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.SetFloat("Speed", 0f);
        }
    }

    // ── Public API (used by PlayerRole, FakeRagDoll, etc.) ──────
    public void AddImpactForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
    {
        if (rb == null)
            return;

        rb.AddForce(force, mode);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void ApplyTemporarySpeedBoost(float multiplier, float duration)
    {
        tempBoostMultiplier = Mathf.Max(0.1f, multiplier);

        if (boostCoroutine != null)
            StopCoroutine(boostCoroutine);

        boostCoroutine = StartCoroutine(RemoveBoostAfter(duration));
    }

    private IEnumerator RemoveBoostAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        tempBoostMultiplier = 1f;
        boostCoroutine = null;
    }

    // ── Gizmos ──────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        float rayDistance = 0.65f;
        if (data != null)
            rayDistance = data.groundCheckDistance + GroundProbePadding * 2f;

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
