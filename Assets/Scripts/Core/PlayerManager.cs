using System.Collections;
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
        StartCoroutine(InitializeJoinedPlayer(playerInput));
    }

    IEnumerator InitializeJoinedPlayer(PlayerInput playerInput)
    {
        if (playerInput == null) yield break;

        Rigidbody rb = playerInput.GetComponent<Rigidbody>();
        bool hadRigidbody = rb != null;
        bool originalKinematic = hadRigidbody && rb.isKinematic;
        bool originalUseGravity = hadRigidbody && rb.useGravity;

        if (hadRigidbody)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (playerInput == null) yield break;

        // Wait for layout with 10-second timeout
        if (!ShipLayoutGenerator.IsReady)
        {
            float elapsed = 0f;
            while (!ShipLayoutGenerator.IsReady && elapsed < 10f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (!ShipLayoutGenerator.IsReady)
                Debug.LogWarning("[PlayerManager] ShipLayout timeout — spawning anyway.");
        }

        int index = playerInput.playerIndex;
        string deviceName = playerInput.devices.Count > 0 ? playerInput.devices[0].displayName : "Unknown";
        Debug.Log($"[PlayerManager] OnPlayerJoined called - index:{index} device:{deviceName}");

        if (index >= 4)
        {
            Debug.LogWarning($"[PlayerManager] Max 4 players. Rejecting index {index}");
            Destroy(playerInput.gameObject);
            yield break;
        }

        // -- Posición spawn --
        Transform spawnAnchor = (spawnPoints != null && index < spawnPoints.Length)
            ? spawnPoints[index]
            : null;
        Vector3 requestedSpawnPos = spawnAnchor != null
            ? spawnAnchor.position
            : new Vector3(index * 2f, 1f, 0f);
        Vector3 spawnPos = ResolveSupportedSpawnPosition(requestedSpawnPos);
        playerInput.transform.position = spawnPos;

        // -- Buscar PlayerMovement con retry --
        PlayerMovement movement = playerInput.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            if (hadRigidbody)
            {
                rb.isKinematic = originalKinematic;
                rb.useGravity = originalUseGravity;
            }
            Debug.LogError($"[PlayerManager] PlayerMovement NOT FOUND on spawned player {index}! " +
                           "Make sure the Player prefab has PlayerMovement component.");
            yield break;
        }

        // -- Validar datos --
        if (playerData == null)
        {
            if (hadRigidbody)
            {
                rb.isKinematic = originalKinematic;
                rb.useGravity = originalUseGravity;
            }
            Debug.LogError("[PlayerManager] PlayerData is NULL! Assign it in the Inspector.");
            yield break;
        }

        // -- Inicializar --
        movement.Initialize(index, playerData, cameraTransform);
        if (hadRigidbody)
        {
            // Always ensure player is physically active after initialization
            rb.isKinematic = false;
            rb.useGravity  = true;
        }

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
    public void SetSpawnPoints(Transform[] points)
    {
        spawnPoints = points;
        if (debugLog)
            Debug.Log($"[PlayerManager] Spawn points wired: {(spawnPoints != null ? spawnPoints.Length : 0)}");
    }

    Vector3 ResolveSupportedSpawnPosition(Vector3 requestedPosition)
    {
        if (TryRaycastSupportedSpawn(requestedPosition, 1.5f, out Vector3 grounded))
            return grounded;

        if (TryRaycastSupportedSpawn(requestedPosition, 2.5f, out grounded))
            return grounded;

        return requestedPosition + Vector3.up * 0.15f;
    }

    bool TryRaycastSupportedSpawn(Vector3 requestedPosition, float rayHeight, out Vector3 groundedPosition)
    {
        groundedPosition = default;
        float maxAcceptedY = requestedPosition.y + 0.75f;
        Ray ray = new Ray(requestedPosition + Vector3.up * rayHeight, Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, 12f, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        float bestDistance = float.PositiveInfinity;
        bool found = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.normal.y < 0.5f || hit.point.y > maxAcceptedY)
                continue;

            if (hit.distance >= bestDistance)
                continue;

            bestDistance = hit.distance;
            groundedPosition = hit.point + Vector3.up * 0.15f;
            found = true;
        }

        return found;
    }

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
