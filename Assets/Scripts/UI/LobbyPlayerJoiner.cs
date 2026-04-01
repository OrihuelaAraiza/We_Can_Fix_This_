using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

public class LobbyPlayerJoiner : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] int maxPlayers = 4;

    LobbyUI lobbyUI;
    readonly Dictionary<int, int> deviceToPlayer = new();

    void Awake()
    {
        lobbyUI = FindObjectOfType<LobbyUI>();
        LobbyPlayerSessionData.Reset();
        deviceToPlayer.Clear();
    }

    void OnEnable()
    {
        InputSystem.onEvent += OnInputEvent;
    }

    void OnDisable()
    {
        InputSystem.onEvent -= OnInputEvent;
    }

    unsafe void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        if (LobbyPlayerSessionData.Count >= maxPlayers) return;

        if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
        if (device is Mouse) return;

        LobbyPlayerSessionEntry entry = TryCreateEntry(device);
        if (entry == null || LobbyManager.Instance == null) return;
        if (!LobbyManager.Instance.RegisterPlayer(entry.PlayerIndex)) return;

        if (!entry.IsKeyboard)
            deviceToPlayer[entry.DeviceId] = entry.PlayerIndex;

        var roles = LobbyManager.Instance.GetAvailableRoles();
        if (roles != null)
            lobbyUI?.ShowPanel(entry.PlayerIndex, roles);

        var handler = gameObject.AddComponent<PlayerLobbyInputHandler>();
        handler.Initialize(entry.PlayerIndex, device, entry.ControlScheme);

        Debug.Log($"[LobbyJoiner] Player {entry.PlayerIndex} joined with {entry.DeviceDisplayName} ({entry.ControlScheme})");
    }

    LobbyPlayerSessionEntry TryCreateEntry(InputDevice device)
    {
        if (device is Gamepad gamepad)
        {
            if (!WasPressedThisEvent(gamepad.buttonSouth)) return null;
            if (deviceToPlayer.ContainsKey(device.deviceId)) return null;

            return LobbyPlayerSessionData.TryRegisterGamepad(device, out LobbyPlayerSessionEntry gamepadEntry)
                ? gamepadEntry
                : null;
        }

        if (device is Keyboard keyboard)
        {
            if (WasPressedThisEvent(keyboard.spaceKey))
            {
                return LobbyPlayerSessionData.TryRegisterKeyboardPlayer("KeyboardP1", out LobbyPlayerSessionEntry keyboardP1Entry)
                    ? keyboardP1Entry
                    : null;
            }

            if (WasPressedThisEvent(keyboard.enterKey) || WasPressedThisEvent(keyboard.numpadEnterKey))
            {
                return LobbyPlayerSessionData.TryRegisterKeyboardPlayer("KeyboardP2", out LobbyPlayerSessionEntry keyboardP2Entry)
                    ? keyboardP2Entry
                    : null;
            }
        }

        return null;
    }

    static bool WasPressedThisEvent(ButtonControl button)
    {
        return button != null && button.IsPressed();
    }
}
