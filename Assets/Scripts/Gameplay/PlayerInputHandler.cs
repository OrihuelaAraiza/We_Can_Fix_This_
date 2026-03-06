using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerInputHandler : MonoBehaviour
{
    private PlayerMovement _movement;
    private PlayerInput    _playerInput;
    private PlayerInteract _interact;
    private PlayerRole     _role;

    private void Awake()
    {
        _movement    = GetComponent<PlayerMovement>();
        _playerInput = GetComponent<PlayerInput>();
        _interact    = GetComponent<PlayerInteract>();
        _role        = GetComponent<PlayerRole>();

        if (_movement    == null) Debug.LogError("[Handler] No PlayerMovement!");
        if (_playerInput == null) Debug.LogError("[Handler] No PlayerInput!");
        if (_interact    == null) Debug.LogWarning("[Handler] No PlayerInteract - interact disabled");
        if (_role        == null) Debug.LogWarning("[Handler] No PlayerRole - ability disabled");
    }

    private void OnEnable()
    {
        _playerInput.actions["Move"].performed     += OnMovePerformed;
        _playerInput.actions["Move"].canceled      += OnMoveCanceled;
        _playerInput.actions["Jump"].performed     += OnJumpPerformed;
        _playerInput.actions["Interact"].performed += OnInteractPerformed;
        _playerInput.actions["Interact"].canceled  += OnInteractCanceled;
        _playerInput.actions["Ability"].performed  += OnAbilityPerformed;
    }

    private void OnDisable()
    {
        _playerInput.actions["Move"].performed     -= OnMovePerformed;
        _playerInput.actions["Move"].canceled      -= OnMoveCanceled;
        _playerInput.actions["Jump"].performed     -= OnJumpPerformed;
        _playerInput.actions["Interact"].performed -= OnInteractPerformed;
        _playerInput.actions["Interact"].canceled  -= OnInteractCanceled;
        _playerInput.actions["Ability"].performed  -= OnAbilityPerformed;
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx)
        => _movement?.OnMove(ctx.ReadValue<Vector2>());

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
        => _movement?.OnMove(Vector2.zero);

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
        => _movement?.OnJump();

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        Debug.Log("[Handler] Interact PRESSED");
        _interact?.SetInteractHeld(true);
    }

    private void OnInteractCanceled(InputAction.CallbackContext ctx)
    {
        Debug.Log("[Handler] Interact RELEASED");
        _interact?.SetInteractHeld(false);
    }

    private void OnAbilityPerformed(InputAction.CallbackContext ctx)
    {
        Debug.Log("[Handler] Ability PRESSED");
        if (_role != null) _role.UseAbility();
    }
}
