using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerInputHandler : MonoBehaviour
{
    // ── References ────────────────────────────────────────────
    private PlayerMovement _movement;
    private PlayerInput _playerInput;

    // ── Lifecycle ─────────────────────────────────────────────
    private void Awake()
    {
        _movement   = GetComponent<PlayerMovement>();
        _playerInput = GetComponent<PlayerInput>();

        if (_movement == null)
            Debug.LogError("[PlayerInputHandler] PlayerMovement component not found!");
        if (_playerInput == null)
            Debug.LogError("[PlayerInputHandler] PlayerInput component not found!");
    }

    private void OnEnable()
    {
        _playerInput.actions["Move"].performed += OnMovePerformed;
        _playerInput.actions["Move"].canceled  += OnMoveCanceled;
        _playerInput.actions["Jump"].performed  += OnJumpPerformed;
    }

    private void OnDisable()
    {
        _playerInput.actions["Move"].performed -= OnMovePerformed;
        _playerInput.actions["Move"].canceled  -= OnMoveCanceled;
        _playerInput.actions["Jump"].performed  -= OnJumpPerformed;
    }

    // ── Callbacks ─────────────────────────────────────────────
    private void OnMovePerformed(InputAction.CallbackContext ctx)
        => _movement?.OnMove(ctx.ReadValue<Vector2>());

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
        => _movement?.OnMove(Vector2.zero);

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
        => _movement?.OnJump();
}
