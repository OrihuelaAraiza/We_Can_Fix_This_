using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerInputHandler : MonoBehaviour
{
    private PlayerMovement _movement;
    private PlayerInput    _playerInput;
    private PlayerInteract _interact;
    private PlayerRole     _role;
    private InputAction     _moveAction;
    private InputAction     _jumpAction;
    private InputAction     _interactAction;
    private InputAction     _abilityAction;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (_playerInput == null)
            return;

        _moveAction = FindRequiredAction("Move");
        _jumpAction = FindRequiredAction("Jump");
        _interactAction = FindRequiredAction("Interact");
        _abilityAction = FindOptionalAction("Ability");

        if (_moveAction != null)
        {
            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
        }

        if (_jumpAction != null)
            _jumpAction.performed += OnJumpPerformed;

        if (_interactAction != null)
        {
            _interactAction.performed += OnInteractPerformed;
            _interactAction.canceled += OnInteractCanceled;
        }

        if (_abilityAction != null)
            _abilityAction.performed += OnAbilityPerformed;
    }

    private void OnDisable()
    {
        if (_moveAction != null)
        {
            _moveAction.performed -= OnMovePerformed;
            _moveAction.canceled -= OnMoveCanceled;
        }

        if (_jumpAction != null)
            _jumpAction.performed -= OnJumpPerformed;

        if (_interactAction != null)
        {
            _interactAction.performed -= OnInteractPerformed;
            _interactAction.canceled -= OnInteractCanceled;
        }

        if (_abilityAction != null)
            _abilityAction.performed -= OnAbilityPerformed;

        _moveAction = null;
        _jumpAction = null;
        _interactAction = null;
        _abilityAction = null;
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        ResolveReferences();
        _movement?.OnMove(ctx.ReadValue<Vector2>());
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        ResolveReferences();
        _movement?.OnMove(Vector2.zero);
    }

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        ResolveReferences();
        _movement?.OnJump();
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        ResolveReferences();
        _interact?.SetInteractHeld(true);
    }

    private void OnInteractCanceled(InputAction.CallbackContext ctx)
    {
        ResolveReferences();
        _interact?.SetInteractHeld(false);
    }

    private void OnAbilityPerformed(InputAction.CallbackContext ctx)
    {
        ResolveReferences();
        if (_role != null) _role.UseAbility();
    }

    InputAction FindRequiredAction(string actionName)
    {
        InputAction action = _playerInput.actions?.FindAction(actionName, throwIfNotFound: false);
        if (action == null)
            Debug.LogError($"[Handler] Required action '{actionName}' not found in InputActions asset.");

        return action;
    }

    InputAction FindOptionalAction(string actionName)
    {
        InputAction action = _playerInput.actions?.FindAction(actionName, throwIfNotFound: false);
        if (action == null)
            Debug.LogWarning($"[Handler] Optional action '{actionName}' not found in InputActions asset.");

        return action;
    }

    private void ResolveReferences()
    {
        _movement ??= GetComponent<PlayerMovement>();
        _playerInput ??= GetComponent<PlayerInput>();
        _interact ??= GetComponent<PlayerInteract>();
        _role ??= GetComponent<PlayerRole>();

        if (_movement == null) Debug.LogError("[Handler] No PlayerMovement!");
        if (_playerInput == null) Debug.LogError("[Handler] No PlayerInput!");
    }
}
