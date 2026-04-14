using UnityEngine;

/// <summary>
/// Reads movement state from PlayerMovement and drives the Animator.
/// Lives on the model child (same GameObject as Animator).
/// Does NOT touch physics or input.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    private Animator animator;
    private PlayerMovement movement;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        ResolveMovementReference();
    }

    private void Update()
    {
        if (movement == null)
            ResolveMovementReference();

        if (movement == null || animator == null)
            return;

        animator.SetFloat(SpeedHash, movement.PlanarSpeed, 0.1f, Time.deltaTime);
        animator.SetBool(IsGroundedHash, movement.IsGrounded);
    }

    public void BindMovement(PlayerMovement targetMovement)
    {
        movement = targetMovement;
    }

    private void ResolveMovementReference()
    {
        movement = GetComponentInParent<PlayerMovement>();
        if (movement == null)
            movement = GetComponent<PlayerMovement>();
    }
}
