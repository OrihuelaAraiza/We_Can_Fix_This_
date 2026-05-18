using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Runtime Info (debug)")]
    [SerializeField] private int playerIndex;
    [SerializeField] private bool initialized;

    [Header("Runtime Motion")]
    [SerializeField] private bool externalControlEnabled = true;
    [SerializeField] private bool isGrounded;
    [SerializeField] private bool allowImpactReactions = false;

    [Header("Ground Check")]
    [SerializeField] private float groundProbePadding = 0.06f;
    [SerializeField] private float groundMaxSlope = 55f;

    public bool IsInitialized => initialized;
    public int PlayerIndex => playerIndex;
    public Rigidbody RB => rb;
    public bool IsGrounded => isGrounded;

    public float PlanarSpeed => rb != null
        ? new Vector3(rb.velocity.x, 0f, rb.velocity.z).magnitude
        : 0f;

    public float VerticalVelocity => rb != null ? rb.velocity.y : 0f;

    public float SpeedNormalized => rb != null && data != null
        ? Mathf.Clamp01(PlanarSpeed / Mathf.Max(0.01f, data.maxSpeed * speedMultiplier * tempBoostMultiplier))
        : 0f;
    public bool HasMoveInput => moveInput.sqrMagnitude >= 0.01f;
    public float MoveInputMagnitude => Mathf.Clamp01(moveInput.magnitude);
    public bool HasJumpQueued => jumpQueued;
    public float LastJumpRequestedTime => lastJumpRequestedTime;
    public float LastJumpStartedTime => lastJumpStartedTime;

    private PlayerData data;
    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Transform cameraTransform;
    private int playerLayer = -1;

    private float speedMultiplier = 1f;
    private float tempBoostMultiplier = 1f;
    private Vector2 moveInput;
    private bool jumpQueued;
    private float lastJumpRequestedTime = -10f;
    private float lastJumpStartedTime = -10f;
    private float lastGroundedTime = -10f;
    private Coroutine boostCoroutine;

    private const float CoyoteTime = 0.1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        playerLayer = LayerMask.NameToLayer("Player");
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

        if (capsule == null)
            capsule = GetComponent<CapsuleCollider>();

        rb.mass = Mathf.Max(0.01f, data.mass);
        rb.drag = 0f;
        rb.angularDrag = 0.05f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        externalControlEnabled = true;
        moveInput = Vector2.zero;
        jumpQueued = false;

        CheckGround();

        initialized = true;
    }

    public void OnMove(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    public void OnJump()
    {
        if (!initialized || !externalControlEnabled)
            return;

        if (isGrounded || Time.time - lastGroundedTime <= CoyoteTime)
        {
            jumpQueued = true;
            lastJumpRequestedTime = Time.time;
        }
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

    private void Update()
    {
        if (!initialized || data == null)
            return;

        CheckGround();
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

        rb.angularVelocity = Vector3.zero;

        if (jumpQueued)
        {
            DoJump();
            jumpQueued = false;
        }

        LimitPlanarSpeed();
    }

    private void ApplyMovementForce()
    {
        Vector3 planarVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        Vector3 targetVelocity = Vector3.zero;

        bool hasInput = moveInput.sqrMagnitude >= 0.01f;

        if (hasInput)
        {
            Vector3 dir = GetMoveDirection();

            if (dir.sqrMagnitude > 0.001f)
            {
                float targetSpeed = data.maxSpeed * speedMultiplier * tempBoostMultiplier;
                targetVelocity = dir * targetSpeed;
            }
        }

        float baseAcceleration = Mathf.Max(1f, data.moveForce);

        float acceleration = isGrounded
            ? baseAcceleration * 2.4f
            : baseAcceleration * Mathf.Clamp(data.airControlMultiplier, 0.35f, 1f) * 1.8f;

        float deceleration = isGrounded
            ? baseAcceleration * 3f
            : baseAcceleration * 1.5f;

        float step = (hasInput ? acceleration : deceleration) * Time.fixedDeltaTime;

        Vector3 nextPlanarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, step);
        rb.velocity = new Vector3(nextPlanarVelocity.x, rb.velocity.y, nextPlanarVelocity.z);
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

    private void DoJump()
    {
        isGrounded = false;
        lastGroundedTime = -10f;

        Vector3 v = rb.velocity;
        v.y = 0f;
        rb.velocity = v;

        rb.AddForce(Vector3.up * data.jumpForce, ForceMode.Impulse);
        lastJumpStartedTime = Time.time;
    }

    private void ApplyDrag()
    {
        rb.drag = 0f;
    }

    private void CheckGround()
    {
        if (data == null)
            return;

        LayerMask mask = data.groundLayer.value == 0 ? ~0 : data.groundLayer;

        Vector3 origin = transform.position + Vector3.up * 0.2f;
        float radius = 0.2f;
        float distance = data.groundCheckDistance + groundProbePadding;

        if (capsule != null)
        {
            float radiusScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            float heightScale = Mathf.Abs(transform.lossyScale.y);

            radius = Mathf.Max(0.05f, capsule.radius * radiusScale * 0.92f);

            float halfHeight = Mathf.Max(radius, capsule.height * heightScale * 0.5f);
            Vector3 worldCenter = transform.TransformPoint(capsule.center);

            origin = worldCenter - Vector3.up * Mathf.Max(0f, halfHeight - radius - groundProbePadding);
            distance = data.groundCheckDistance + groundProbePadding * 2f;
        }

        bool foundGround = HasGroundHit(origin, radius, distance, mask);

        if (!foundGround && data.groundLayer.value != 0)
            foundGround = HasGroundHit(origin, radius, distance, ~0);

        if (rb != null && rb.velocity.y > 0.15f)
            foundGround = false;

        isGrounded = foundGround;

        if (foundGround)
            lastGroundedTime = Time.time;
    }

    private bool HasGroundHit(Vector3 origin, float radius, float distance, LayerMask mask)
    {
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            radius,
            Vector3.down,
            distance,
            mask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider == null)
                continue;

            Transform hitTransform = hit.collider.transform;

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            if (playerLayer >= 0 && hit.collider.gameObject.layer == playerLayer)
                continue;

            if (Vector3.Angle(hit.normal, Vector3.up) > groundMaxSlope)
                continue;

            return true;
        }

        return false;
    }

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

    private void OnCollisionEnter(Collision collision)
    {
        if (!allowImpactReactions)
            return;

        if (!initialized || rb == null)
            return;

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

    public void AddImpactForce(Vector3 force, ForceMode mode = ForceMode.Impulse)
    {
        if (!allowImpactReactions || rb == null)
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

    // Lo dejo para que otros scripts no truene si todavía llaman BindAnimator().
    // Pero este PlayerMovement ya NO controla animaciones.
    public void BindAnimator(Animator targetAnimator)
    {
        if (targetAnimator == null)
            return;

        targetAnimator.applyRootMotion = false;
        targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    private void OnDrawGizmosSelected()
    {
        float rayDistance = 0.65f;

        if (data != null)
            rayDistance = data.groundCheckDistance + groundProbePadding * 2f;

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
