using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerManager : MonoBehaviour
{
    const int MaxSupportedPlayers = LobbyPlayerSessionData.MaxPlayers;

    public static PlayerManager Instance { get; private set; }

    [Header("Player Setup")]
    [SerializeField] private PlayerData playerData;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform cameraTransform;

    [Header("Spawn Tuning")]
    [SerializeField] private float spawnHeightOffset = 0.15f;
    [SerializeField] private float minSpawnSeparation = 1.1f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private readonly List<PlayerMovement> players = new();
    private readonly Dictionary<PlayerInput, int> activePlayerSlots = new();
    private readonly Dictionary<PlayerInput, int> pendingPlayerSlots = new();

    private PlayerInputManager playerInputManager;
    private int expectedLobbyPlayers;

    public static event System.Action OnPlayerCountChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }

        Instance = this;
        playerInputManager = GetComponent<PlayerInputManager>();

        // maxPlayerCount se configura en el Inspector del PlayerInputManager component;
        // no es asignable en runtime (read-only property).

        Debug.Log("[PlayerManager] Awake - Instance set");
    }

    IEnumerator Start()
    {
        yield return null;
        RestoreLobbyRosterIfNeeded();
        UpdateJoinAvailability();
    }

    public void OnPlayerJoined(PlayerInput playerInput)
    {
        if (playerInput == null)
            return;

        StartCoroutine(InitializeJoinedPlayer(playerInput));
    }

    IEnumerator InitializeJoinedPlayer(PlayerInput playerInput)
    {
        if (playerInput == null)
            yield break;

        if (!TryReservePlayerSlot(playerInput, out int slotIndex))
        {
            RejectJoin(playerInput, "No free player slot available or duplicate device detected.");
            yield break;
        }

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

        string deviceName = GetDeviceDisplayName(playerInput);
        Debug.Log($"[PlayerManager] OnPlayerJoined called - slot:{slotIndex} inputIndex:{playerInput.playerIndex} device:{deviceName} scheme:{playerInput.currentControlScheme}");

        Vector3 requestedSpawnPos = GetRequestedSpawnPosition(slotIndex);
        Vector3 spawnPos = ResolveSupportedSpawnPosition(slotIndex, requestedSpawnPos);
        playerInput.transform.position = spawnPos;

        PlayerMovement movement = playerInput.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            ReleasePlayerReservation(playerInput);
            RestoreRigidbodyState(rb, hadRigidbody, originalKinematic, originalUseGravity);
            Debug.LogError($"[PlayerManager] PlayerMovement NOT FOUND on spawned player {slotIndex}! Make sure the Player prefab has PlayerMovement component.");
            yield break;
        }

        if (playerData == null)
        {
            ReleasePlayerReservation(playerInput);
            RestoreRigidbodyState(rb, hadRigidbody, originalKinematic, originalUseGravity);
            Debug.LogError("[PlayerManager] PlayerData is NULL! Assign it in the Inspector.");
            yield break;
        }

        movement.Initialize(slotIndex, playerData, cameraTransform);

        if (hadRigidbody)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        pendingPlayerSlots.Remove(playerInput);
        activePlayerSlots[playerInput] = slotIndex;
        players.Add(movement);

        ApplyLobbyRole(playerInput, slotIndex);

        OnPlayerCountChanged?.Invoke();
        UpdateJoinAvailability();

        Debug.Log($"[PlayerManager] Player {slotIndex + 1} fully initialized. Total players: {players.Count}");
    }

    public void OnPlayerLeft(PlayerInput playerInput)
    {
        if (playerInput != null)
        {
            PlayerMovement movement = playerInput.GetComponent<PlayerMovement>();
            if (movement != null)
                players.Remove(movement);

            ReleasePlayerReservation(playerInput);
            Debug.Log($"[PlayerManager] Player {playerInput.playerIndex + 1} left.");
        }

        OnPlayerCountChanged?.Invoke();
        UpdateJoinAvailability();
    }

    public IReadOnlyList<PlayerMovement> GetPlayers() => players;
    public int PlayerCount => players.Count;

    public void SetSpawnPoints(Transform[] points)
    {
        spawnPoints = points;
        if (debugLog)
            Debug.Log($"[PlayerManager] Spawn points wired: {(spawnPoints != null ? spawnPoints.Length : 0)}");
    }

    void RestoreLobbyRosterIfNeeded()
    {
        if (!LobbyPlayerSessionData.HasEntries)
            return;

        if (playerInputManager == null || playerInputManager.playerPrefab == null)
        {
            Debug.LogError("[PlayerManager] PlayerInputManager or player prefab missing. Cannot restore lobby roster.");
            return;
        }

        expectedLobbyPlayers = LobbyPlayerSessionData.Count;
        int restoredPlayers = 0;

        foreach (LobbyPlayerSessionEntry entry in LobbyPlayerSessionData.Entries)
        {
            InputDevice device = entry.IsKeyboard
                ? Keyboard.current
                : InputSystem.GetDeviceById(entry.DeviceId);

            if (device == null)
            {
                Debug.LogWarning($"[PlayerManager] Missing device for lobby slot {entry.PlayerIndex} ({entry.ControlScheme}).");
                continue;
            }

            PlayerInput restoredInput = PlayerInput.Instantiate(
                playerInputManager.playerPrefab,
                playerIndex: entry.PlayerIndex,
                controlScheme: entry.ControlScheme,
                splitScreenIndex: -1,
                pairWithDevice: device
            );

            OnPlayerJoined(restoredInput);
            restoredPlayers++;
        }

        if (debugLog)
            Debug.Log($"[PlayerManager] Restored {restoredPlayers}/{expectedLobbyPlayers} lobby players into gameplay.");
    }

    void ApplyLobbyRole(PlayerInput playerInput, int slotIndex)
    {
        PlayerRole playerRole = playerInput.GetComponent<PlayerRole>();
        if (playerRole == null)
        {
            Debug.LogWarning($"[PlayerManager] PlayerRole no encontrado en P{slotIndex} — sin rol");
            return;
        }

        RoleDefinition roleDef = RoleSelectionData.GetRole(slotIndex);
        if (roleDef != null)
        {
            playerRole.AssignRole(roleDef);
            Debug.Log($"[PlayerManager] Rol '{roleDef.roleName}' aplicado a P{slotIndex}");
            return;
        }

        Debug.LogWarning($"[PlayerManager] Sin rol guardado para P{slotIndex} — sin restricciones");
    }

    Vector3 ResolveSupportedSpawnPosition(int slotIndex, Vector3 requestedPosition)
    {
        Vector3[] candidateOffsets =
        {
            Vector3.zero,
            new Vector3(0.75f, 0f, 0f),
            new Vector3(-0.75f, 0f, 0f),
            new Vector3(0f, 0f, 0.75f),
            new Vector3(0f, 0f, -0.75f),
            new Vector3(0.55f, 0f, 0.55f),
            new Vector3(-0.55f, 0f, 0.55f),
            new Vector3(0.55f, 0f, -0.55f),
            new Vector3(-0.55f, 0f, -0.55f)
        };

        for (int i = 0; i < candidateOffsets.Length; i++)
        {
            Vector3 candidate = requestedPosition + candidateOffsets[i];
            if (!TryResolveGroundedSpawn(candidate, out Vector3 grounded))
                continue;

            if (IsFarEnoughFromOtherPlayers(slotIndex, grounded))
                return grounded;
        }

        return requestedPosition + Vector3.up * spawnHeightOffset;
    }

    bool TryResolveGroundedSpawn(Vector3 requestedPosition, out Vector3 groundedPosition)
    {
        if (TryRaycastSupportedSpawn(requestedPosition, 1.5f, out groundedPosition))
            return true;

        if (TryRaycastSupportedSpawn(requestedPosition, 2.5f, out groundedPosition))
            return true;

        groundedPosition = default;
        return false;
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
            groundedPosition = hit.point + Vector3.up * spawnHeightOffset;
            found = true;
        }

        return found;
    }

    Vector3 GetRequestedSpawnPosition(int slotIndex)
    {
        if (spawnPoints != null && slotIndex >= 0 && slotIndex < spawnPoints.Length && spawnPoints[slotIndex] != null)
            return spawnPoints[slotIndex].position;

        Vector3 basePosition = transform.position;
        if (spawnPoints != null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null)
                {
                    basePosition = spawnPoints[i].position;
                    break;
                }
            }
        }

        Vector3[] fallbackOffsets =
        {
            new Vector3(-1.25f, 0f, 0f),
            new Vector3(1.25f, 0f, 0f),
            new Vector3(0f, 0f, -1.25f),
            new Vector3(0f, 0f, 1.25f)
        };

        return basePosition + fallbackOffsets[Mathf.Clamp(slotIndex, 0, fallbackOffsets.Length - 1)];
    }

    bool IsFarEnoughFromOtherPlayers(int slotIndex, Vector3 candidatePosition)
    {
        for (int i = 0; i < players.Count; i++)
        {
            PlayerMovement other = players[i];
            if (other == null || other.PlayerIndex == slotIndex)
                continue;

            Vector3 flatDelta = other.transform.position - candidatePosition;
            flatDelta.y = 0f;

            if (flatDelta.sqrMagnitude < minSpawnSeparation * minSpawnSeparation)
                return false;
        }

        return true;
    }

    bool TryReservePlayerSlot(PlayerInput playerInput, out int slotIndex)
    {
        slotIndex = -1;

        if (playerInput == null)
            return false;

        if (activePlayerSlots.Count + pendingPlayerSlots.Count >= MaxSupportedPlayers)
            return false;

        if (HasConflictingDevice(playerInput))
            return false;

        slotIndex = ResolveSlotIndex(playerInput);
        if (slotIndex < 0)
            return false;

        pendingPlayerSlots[playerInput] = slotIndex;
        return true;
    }

    int ResolveSlotIndex(PlayerInput playerInput)
    {
        LobbyPlayerSessionEntry sessionEntry = FindMatchingSessionEntry(playerInput);
        if (sessionEntry != null && !IsSlotReserved(sessionEntry.PlayerIndex))
            return sessionEntry.PlayerIndex;

        if (playerInput.playerIndex >= 0 && playerInput.playerIndex < MaxSupportedPlayers && !IsSlotReserved(playerInput.playerIndex))
            return playerInput.playerIndex;

        for (int i = 0; i < MaxSupportedPlayers; i++)
        {
            if (!IsSlotReserved(i))
                return i;
        }

        return -1;
    }

    LobbyPlayerSessionEntry FindMatchingSessionEntry(PlayerInput playerInput)
    {
        if (!LobbyPlayerSessionData.HasEntries)
            return null;

        string controlScheme = playerInput.currentControlScheme;
        int primaryDeviceId = GetPrimaryDeviceId(playerInput);

        foreach (LobbyPlayerSessionEntry entry in LobbyPlayerSessionData.Entries)
        {
            if (entry.ControlScheme != controlScheme)
                continue;

            if (entry.IsKeyboard || entry.DeviceId == primaryDeviceId)
                return entry;
        }

        return null;
    }

    bool IsSlotReserved(int slotIndex)
    {
        foreach (int pendingSlot in pendingPlayerSlots.Values)
        {
            if (pendingSlot == slotIndex)
                return true;
        }

        foreach (int activeSlot in activePlayerSlots.Values)
        {
            if (activeSlot == slotIndex)
                return true;
        }

        return false;
    }

    bool HasConflictingDevice(PlayerInput playerInput)
    {
        foreach (PlayerInput pending in pendingPlayerSlots.Keys)
        {
            if (HasDeviceConflict(pending, playerInput))
                return true;
        }

        foreach (PlayerInput active in activePlayerSlots.Keys)
        {
            if (HasDeviceConflict(active, playerInput))
                return true;
        }

        return false;
    }

    bool HasDeviceConflict(PlayerInput existing, PlayerInput incoming)
    {
        if (existing == null || incoming == null)
            return false;

        int existingDeviceId = GetPrimaryDeviceId(existing);
        int incomingDeviceId = GetPrimaryDeviceId(incoming);

        if (existingDeviceId == InputDevice.InvalidDeviceId || incomingDeviceId == InputDevice.InvalidDeviceId)
            return false;

        if (existingDeviceId != incomingDeviceId)
            return false;

        return !CanShareDevice(existing, incoming);
    }

    static bool CanShareDevice(PlayerInput left, PlayerInput right)
    {
        if (left == null || right == null)
            return false;

        return left.currentControlScheme == "KeyboardP1" && right.currentControlScheme == "KeyboardP2"
            || left.currentControlScheme == "KeyboardP2" && right.currentControlScheme == "KeyboardP1";
    }

    static int GetPrimaryDeviceId(PlayerInput playerInput)
    {
        return playerInput != null && playerInput.devices.Count > 0
            ? playerInput.devices[0].deviceId
            : InputDevice.InvalidDeviceId;
    }

    static string GetDeviceDisplayName(PlayerInput playerInput)
    {
        return playerInput != null && playerInput.devices.Count > 0
            ? playerInput.devices[0].displayName
            : "Unknown";
    }

    void RejectJoin(PlayerInput playerInput, string reason)
    {
        Debug.LogWarning($"[PlayerManager] Rejecting player join. {reason}");
        ReleasePlayerReservation(playerInput);

        if (playerInput != null)
            Destroy(playerInput.gameObject);

        UpdateJoinAvailability();
    }

    void ReleasePlayerReservation(PlayerInput playerInput)
    {
        if (playerInput == null)
            return;

        pendingPlayerSlots.Remove(playerInput);
        activePlayerSlots.Remove(playerInput);
    }

    void RestoreRigidbodyState(Rigidbody rb, bool hadRigidbody, bool originalKinematic, bool originalUseGravity)
    {
        if (!hadRigidbody || rb == null)
            return;

        rb.isKinematic = originalKinematic;
        rb.useGravity = originalUseGravity;
    }

    void UpdateJoinAvailability()
    {
        if (playerInputManager == null)
            return;

        bool underCap = activePlayerSlots.Count + pendingPlayerSlots.Count < MaxSupportedPlayers;
        bool rosterFilledOrPending = expectedLobbyPlayers > 0
            && activePlayerSlots.Count + pendingPlayerSlots.Count >= expectedLobbyPlayers;
        bool shouldAllowJoining = !rosterFilledOrPending && underCap;

        if (shouldAllowJoining)
            playerInputManager.EnableJoining();
        else
            playerInputManager.DisableJoining();
    }

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
