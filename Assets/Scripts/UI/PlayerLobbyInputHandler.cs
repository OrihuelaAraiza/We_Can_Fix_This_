using UnityEngine;
using UnityEngine.InputSystem;

// Se agrega dinámicamente por LobbyPlayerJoiner
// Maneja navegación de roles y botón Ready de un jugador específico
public class PlayerLobbyInputHandler : MonoBehaviour
{
    int playerIndex;
    InputDevice device;
    string controlScheme;

    // Cooldown para evitar navegación demasiado rápida
    float navCooldown = 0f;
    const float NAV_DELAY = 0.25f;

    public void Initialize(int index, InputDevice inputDevice, string assignedControlScheme)
    {
        playerIndex = index;
        device      = inputDevice;
        controlScheme = assignedControlScheme;
        Debug.Log($"[LobbyInputHandler] P{index} initialized with {inputDevice.name} ({controlScheme})");
    }

    void Update()
    {
        if (device == null) return;
        navCooldown -= Time.deltaTime;

        // ── GAMEPAD ─────────────────────────────────────────
        if (device is Gamepad gamepad)
        {
            // Navegar roles: D-Pad izquierda/derecha o stick izquierdo
            float horizontal = gamepad.leftStick.x.ReadValue();
            if (navCooldown <= 0f)
            {
                bool goLeft  = horizontal < -0.5f
                            || gamepad.dpad.left.wasPressedThisFrame
                            || gamepad.buttonWest.wasPressedThisFrame;   // X/Cuadrado

                bool goRight = horizontal > 0.5f
                            || gamepad.dpad.right.wasPressedThisFrame
                            || gamepad.buttonEast.wasPressedThisFrame;   // B/Círculo

                if (goLeft)  { NavigateRole(-1); navCooldown = NAV_DELAY; }
                if (goRight) { NavigateRole(1);  navCooldown = NAV_DELAY; }
            }

            // Ready: botón sur (A/Cruz)
            if (gamepad.buttonSouth.wasPressedThisFrame)
                ToggleReady();
        }

        // ── TECLADO (por jugador) ────────────────────────────
        else if (device is Keyboard keyboard)
        {
            if (navCooldown <= 0f)
            {
                bool left  = false;
                bool right = false;

                switch (controlScheme)
                {
                    case "KeyboardP1":
                        left  = keyboard.aKey.wasPressedThisFrame;
                        right = keyboard.dKey.wasPressedThisFrame;
                        break;
                    case "KeyboardP2":
                        left  = keyboard.leftArrowKey.wasPressedThisFrame;
                        right = keyboard.rightArrowKey.wasPressedThisFrame;
                        break;
                }

                if (left)  { NavigateRole(-1); navCooldown = NAV_DELAY; }
                if (right) { NavigateRole(1);  navCooldown = NAV_DELAY; }
            }

            bool pressedReady = controlScheme switch
            {
                "KeyboardP1" => keyboard.spaceKey.wasPressedThisFrame,
                "KeyboardP2" => keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame,
                _ => false
            };

            if (pressedReady) ToggleReady();
        }
    }

    void NavigateRole(int direction)
    {
        if (LobbyManager.Instance == null) return;

        var roles   = LobbyManager.Instance.GetAvailableRoles();
        if (roles == null || roles.Count == 0) return;
        var current = LobbyManager.Instance.GetSelectedRole(playerIndex);
        int idx     = roles.IndexOf(current);
        if (idx < 0) idx = 0;
        int next    = (idx + direction + roles.Count) % roles.Count;
        LobbyManager.Instance.SelectRole(playerIndex, roles[next]);
    }

    void ToggleReady()
    {
        LobbyManager.Instance?.ToggleReady(playerIndex);
    }
}
