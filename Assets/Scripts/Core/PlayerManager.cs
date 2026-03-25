using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Player Setup")]
    [SerializeField] private PlayerData playerData;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform cameraTransform;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private readonly List<PlayerMovement> players = new();

    /// Fired whenever a player joins or leaves — PlayerSlotsUI listens to this.
    public static event System.Action OnPlayerCountChanged;

    // ── Singleton ──────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Debug.Log("[PlayerManager] Awake - Instance set");
    }

    // ── Llamado por PlayerInputManager automáticamente ─────────
    public void OnPlayerJoined(PlayerInput playerInput)
    {
        int index = playerInput.playerIndex;
        Debug.Log($"[PlayerManager] OnPlayerJoined called - index:{index} device:{playerInput.devices[0].displayName}");

        if (index >= 4)
        {
            Debug.LogWarning($"[PlayerManager] Max 4 players. Rejecting index {index}");
            Destroy(playerInput.gameObject);
            return;
        }

        // -- Posición spawn --
        Vector3 spawnPos = (spawnPoints != null && index < spawnPoints.Length)
            ? spawnPoints[index].position
            : new Vector3(index * 2f, 1f, 0f);
        playerInput.transform.position = spawnPos;

        // -- Buscar PlayerMovement con retry --
        PlayerMovement movement = playerInput.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            Debug.LogError($"[PlayerManager] PlayerMovement NOT FOUND on spawned player {index}! " +
                           "Make sure the Player prefab has PlayerMovement component.");
            return;
        }

        // -- Validar datos --
        if (playerData == null)
        {
            Debug.LogError("[PlayerManager] PlayerData is NULL! Assign it in the Inspector.");
            return;
        }

        // -- Inicializar --
        movement.Initialize(index, playerData, cameraTransform);
        players.Add(movement);
        OnPlayerCountChanged?.Invoke();

        // -- Aplicar rol guardado desde el lobby --
        var playerRole = playerInput.GetComponent<PlayerRole>();
        if (playerRole != null)
        {
            var roleDef = RoleSelectionData.GetRole(index);
            if (roleDef != null)
            {
                playerRole.AssignRole(roleDef);
                Debug.Log($"[PlayerManager] Rol '{roleDef.roleName}' aplicado a P{index}");
            }
            else
            {
                Debug.LogWarning($"[PlayerManager] Sin rol guardado para P{index} — sin restricciones");
            }
        }
        else
        {
            Debug.LogWarning($"[PlayerManager] PlayerRole no encontrado en P{index} — sin rol");
        }

        Debug.Log($"[PlayerManager] Player {index + 1} fully initialized. Total players: {players.Count}");
    }

    public void OnPlayerLeft(PlayerInput playerInput)
    {
        PlayerMovement movement = playerInput.GetComponent<PlayerMovement>();
        if (movement != null) players.Remove(movement);
        OnPlayerCountChanged?.Invoke();
        Debug.Log($"[PlayerManager] Player {playerInput.playerIndex + 1} left.");
    }

    // ── API pública ────────────────────────────────────────────
    public IReadOnlyList<PlayerMovement> GetPlayers() => players;
    public int PlayerCount => players.Count;

    /// Called by ShipLayoutGenerator at runtime to wire procedurally-placed spawn points.
    public void SetSpawnPoints(Transform[] points) => spawnPoints = points;

    // ── Gizmos ────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        if (spawnPoints == null) return;
        Color[] colors = { Color.red, Color.blue, Color.green, Color.yellow };
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null) continue;
            Gizmos.color = colors[i % colors.Length];
            Gizmos.DrawSphere(spawnPoints[i].position, 0.3f);
        }
    }
}
