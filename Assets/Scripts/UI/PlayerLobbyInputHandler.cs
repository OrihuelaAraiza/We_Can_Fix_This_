using UnityEngine;
using UnityEngine.InputSystem;

// Se agrega dinámicamente por LobbyPlayerJoiner
// Maneja navegación de roles y botón Ready de un jugador específico
public class PlayerLobbyInputHandler : MonoBehaviour
{
    int playerIndex;
    InputDevice device;

    // Cooldown para evitar navegación demasiado rápida
    float navCooldown = 0f;
    const float NAV_DELAY = 0.25f;

    public void Initialize(int index, InputDevice inputDevice)
    {
        playerIndex = index;
        device      = inputDevice;
        Debug.Log($"[LobbyInputHandler] P{index} initialized with {inputDevice.name}");
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
                // P0: A/D    P1: ←/→    P2: F/H    P3: J/L
                bool left  = false;
                bool right = false;

                switch (playerIndex)
                {
                    case 0:
                        left  = keyboard.aKey.wasPressedThisFrame;
                        right = keyboard.dKey.wasPressedThisFrame;
                        break;
                    case 1:
                        left  = keyboard.leftArrowKey.wasPressedThisFrame;
                        right = keyboard.rightArrowKey.wasPressedThisFrame;
                        break;
                    case 2:
                        left  = keyboard.fKey.wasPressedThisFrame;
                        right = keyboard.hKey.wasPressedThisFrame;
                        break;
                    case 3:
                        left  = keyboard.jKey.wasPressedThisFrame;
                        right = keyboard.lKey.wasPressedThisFrame;
                        break;
                }

                if (left)  { NavigateRole(-1); navCooldown = NAV_DELAY; }
                if (right) { NavigateRole(1);  navCooldown = NAV_DELAY; }
            }

            // Ready por jugador
            // P0: Espacio  P1: Enter  P2: R  P3: P
            bool pressedReady = playerIndex switch
            {
                0 => keyboard.spaceKey.wasPressedThisFrame,
                1 => keyboard.enterKey.wasPressedThisFrame,
                2 => keyboard.rKey.wasPressedThisFrame,
                3 => keyboard.pKey.wasPressedThisFrame,
                _ => false
            };

            if (pressedReady) ToggleReady();
        }
    }

    void NavigateRole(int direction)
    {
        if (LobbyManager.Instance == null) return;

        var roles   = LobbyManager.Instance.GetAvailableRoles();
        var current = LobbyManager.Instance.GetSelectedRole(playerIndex);
        int idx     = roles.IndexOf(current);
        int next    = (idx + direction + roles.Count) % roles.Count;
        LobbyManager.Instance.SelectRole(playerIndex, roles[next]);
    }

    void ToggleReady()
    {
        LobbyManager.Instance?.ToggleReady(playerIndex);
    }
}
