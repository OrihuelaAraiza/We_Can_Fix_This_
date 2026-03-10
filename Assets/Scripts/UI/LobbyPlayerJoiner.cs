using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class LobbyPlayerJoiner : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] int maxPlayers = 4;

    LobbyUI lobbyUI;
    int joinedCount = 0;

    Dictionary<int, int> deviceToPlayer = new();

    // Para evitar detectar el mismo press múltiples veces
    bool listeningForJoin = true;

    void Awake()
    {
        lobbyUI = FindObjectOfType<LobbyUI>();
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
        if (!listeningForJoin) return;
        if (joinedCount >= maxPlayers) return;

        // Solo procesar StateEvents (botones presionados)
        if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;

        // Ignorar mouse para evitar joins accidentales
        if (device is Mouse) return;

        // Verificar que haya algún botón presionado en este evento
        bool anyButtonPressed = false;
        foreach (var control in device.allControls)
        {
            if (control is UnityEngine.InputSystem.Controls.ButtonControl button)
            {
                if (button.IsPressed())
                {
                    anyButtonPressed = true;
                    break;
                }
            }
        }

        if (!anyButtonPressed) return;

        // Dispositivo ya registrado — ignorar
        if (deviceToPlayer.ContainsKey(device.deviceId)) return;

        // Registrar nuevo jugador
        int playerIndex = joinedCount;
        deviceToPlayer[device.deviceId] = playerIndex;
        joinedCount++;

        // Registrar en LobbyManager
        LobbyManager.Instance?.RegisterPlayer(playerIndex);

        // Mostrar panel en UI
        var roles = LobbyManager.Instance?.GetAvailableRoles();
        if (roles != null)
            lobbyUI?.ShowPanel(playerIndex, roles);

        Debug.Log($"[LobbyJoiner] Player {playerIndex} joined with {device.name}");

        // Agregar handler de input para este jugador
        var handler = gameObject.AddComponent<PlayerLobbyInputHandler>();
        handler.Initialize(playerIndex, device);

        // Si llegamos al máximo, dejar de escuchar joins
        if (joinedCount >= maxPlayers)
            listeningForJoin = false;
    }
}
