using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerManager : MonoBehaviour
{
    const int MaxSupportedPlayers = LobbyPlayerSessionData.MaxPlayers;
    const string AnimationDebugPrefix = "[FixieAnimation]";

    public static PlayerManager Instance { get; private set; }

    private static ILogHandler originalLogHandler;
    private static AnimationOnlyLogHandler animationOnlyLogHandler;
    private static bool animationLogFilterInstalled;

    [Header("Player Setup")]
    [SerializeField] private PlayerData playerData;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform cameraTransform;

    [Header("Fixie Models (randomly assigned on join)")]
    [Tooltip("All available Fixie mesh prefabs — one is picked randomly per player join")]
    [SerializeField] private GameObject[] fixieMeshPrefabs;

    [Header("Spawn Tuning")]
    [SerializeField] private float spawnHeightOffset = 0.15f;
    [SerializeField] private float minSpawnSeparation = 1.1f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    [Header("Temporary Player Stabilization")]
    [SerializeField] private bool disablePlayerImpactPhysics = true;
    [SerializeField] private bool disableProceduralPlayerVisuals = true;

    private readonly List<PlayerMovement> players = new();
    private readonly Dictionary<PlayerInput, int> activePlayerSlots = new();
    private readonly Dictionary<PlayerInput, int> pendingPlayerSlots = new();

    private PlayerInputManager playerInputManager;
    private int expectedLobbyPlayers;

    public static event System.Action OnPlayerCountChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BootstrapAnimationDebugging()
    {
        ClearEditorConsoleForAnimationDebug();
        InstallAnimationLogFilter();
    }

    private void Awake()
    {
        ClearEditorConsoleForAnimationDebug();
        InstallAnimationLogFilter();
        Instance = this;
        playerInputManager = GetComponent<PlayerInputManager>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        RestoreAnimationLogFilter();
    }

    private void OnApplicationQuit()
    {
        RestoreAnimationLogFilter();
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

        PrepareSpawnedPlayer(playerInput.gameObject);

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

        if (!ShipLayoutGenerator.IsReady || ShipLayoutGenerator.PlayerSpawnPositions == null || ShipLayoutGenerator.PlayerSpawnPositions.Count == 0)
        {
            // Only block if a ShipLayoutGenerator actually exists in the scene.
            // Without one (e.g. in test scenes) IsReady is permanently false,
            // which would freeze the Rigidbody for 10 s and block initialization.
            if (UnityEngine.Object.FindObjectOfType<ShipLayoutGenerator>() != null)
            {
                float elapsed = 0f;
                while (elapsed < 10f)
                {
                    bool layoutReady = ShipLayoutGenerator.IsReady;
                    bool spawnPointsReady = ShipLayoutGenerator.PlayerSpawnPositions != null
                        && ShipLayoutGenerator.PlayerSpawnPositions.Count > 0;

                    if (layoutReady && spawnPointsReady)
                        break;

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (!ShipLayoutGenerator.IsReady || ShipLayoutGenerator.PlayerSpawnPositions == null || ShipLayoutGenerator.PlayerSpawnPositions.Count == 0)
                    Debug.LogWarning("[PlayerManager] ShipLayout timeout — spawning with fallback positions.");
            }
        }

        string deviceName = GetDeviceDisplayName(playerInput);
        if (debugLog)
            Debug.Log($"[PlayerManager] OnPlayerJoined called - slot:{slotIndex} inputIndex:{playerInput.playerIndex} device:{deviceName} scheme:{playerInput.currentControlScheme}");

        Vector3 requestedSpawnPos = GetRequestedSpawnPosition(slotIndex);
        Vector3 spawnPos = ResolveSupportedSpawnPosition(playerInput.transform, slotIndex, requestedSpawnPos);
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

        try
        {
            SetupPlayerVisual(playerInput.transform, movement, slotIndex);
            EnsurePlayerCollider(playerInput.gameObject);
            movement.Initialize(slotIndex, playerData, cameraTransform);
        }
        finally
        {
            // Restore Rigidbody unconditionally — if Initialize() throws,
            // the player must not remain frozen/kinematic forever.
            if (hadRigidbody)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }

        pendingPlayerSlots.Remove(playerInput);
        activePlayerSlots[playerInput] = slotIndex;
        players.Add(movement);

        ApplyLobbyRole(playerInput, slotIndex);

        OnPlayerCountChanged?.Invoke();
        UpdateJoinAvailability();

        if (debugLog)
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
            if (debugLog)
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

    Vector3 ResolveSupportedSpawnPosition(Transform playerRoot, int slotIndex, Vector3 requestedPosition)
    {
        float spawnLift = GetRequiredSpawnLift(playerRoot);
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
            if (!TryResolveGroundedSpawn(candidate, spawnLift, out Vector3 grounded))
                continue;

            if (IsFarEnoughFromOtherPlayers(slotIndex, grounded))
                return grounded;
        }

        float fallbackLift = Mathf.Max(0f, spawnLift - spawnHeightOffset);
        return requestedPosition + Vector3.up * fallbackLift;
    }

    bool TryResolveGroundedSpawn(Vector3 requestedPosition, float spawnLift, out Vector3 groundedPosition)
    {
        if (TryRaycastSupportedSpawn(requestedPosition, 1.5f, spawnLift, out groundedPosition))
            return true;

        if (TryRaycastSupportedSpawn(requestedPosition, 2.5f, spawnLift, out groundedPosition))
            return true;

        groundedPosition = default;
        return false;
    }

    bool TryRaycastSupportedSpawn(Vector3 requestedPosition, float rayHeight, float spawnLift, out Vector3 groundedPosition)
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

            if (!IsSpawnSurfaceValid(hit.collider))
                continue;

            if (hit.distance >= bestDistance)
                continue;

            bestDistance = hit.distance;
            groundedPosition = hit.point + Vector3.up * spawnLift;
            found = true;
        }

        return found;
    }

    bool IsSpawnSurfaceValid(Collider collider)
    {
        if (collider == null)
            return false;

        int playerLayer = LayerMask.NameToLayer("Player");
        return playerLayer < 0 || collider.gameObject.layer != playerLayer;
    }

    float GetRequiredSpawnLift(Transform playerRoot)
    {
        if (playerRoot == null)
            return spawnHeightOffset;

        float lowestOffset = 0f;
        bool foundCollider = false;
        Collider[] colliders = playerRoot.GetComponents<Collider>();

        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;

            float offsetFromPivot = playerRoot.position.y - collider.bounds.min.y;
            if (!foundCollider || offsetFromPivot > lowestOffset)
            {
                lowestOffset = offsetFromPivot;
                foundCollider = true;
            }
        }

        if (!foundCollider)
            return spawnHeightOffset;

        return Mathf.Max(spawnHeightOffset, lowestOffset + 0.05f);
    }

    Vector3 GetRequestedSpawnPosition(int slotIndex)
    {
        if (ShipLayoutGenerator.PlayerSpawnPositions != null && ShipLayoutGenerator.PlayerSpawnPositions.Count > 0)
        {
            int runtimeIndex = Mathf.Clamp(slotIndex, 0, ShipLayoutGenerator.PlayerSpawnPositions.Count - 1);
            return ShipLayoutGenerator.PlayerSpawnPositions[runtimeIndex];
        }

        if (TryGetBridgeCenterSpawnPosition(slotIndex, out Vector3 bridgeSpawn))
            return bridgeSpawn;

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

    bool TryGetBridgeCenterSpawnPosition(int slotIndex, out Vector3 spawnPosition)
    {
        spawnPosition = default;

        if (ShipLayoutGenerator.RoomCenters == null
            || !ShipLayoutGenerator.RoomCenters.TryGetValue("Bridge", out Vector3 bridgeCenter))
        {
            return false;
        }

        Vector3[] bridgeOffsets =
        {
            new Vector3(-1.25f, 0f, -1.25f),
            new Vector3(1.25f, 0f, -1.25f),
            new Vector3(-1.25f, 0f, 1.25f),
            new Vector3(1.25f, 0f, 1.25f)
        };

        spawnPosition = bridgeCenter + bridgeOffsets[Mathf.Clamp(slotIndex, 0, bridgeOffsets.Length - 1)];
        return true;
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

    void PrepareSpawnedPlayer(GameObject playerObject)
    {
        if (playerObject == null)
            return;

        playerObject.tag = "Player";

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
            playerObject.layer = playerLayer;

        if (playerObject.GetComponent<PlayerRole>() == null)
            playerObject.AddComponent<PlayerRole>();

        if (playerObject.GetComponent<PlayerInteract>() == null)
            playerObject.AddComponent<PlayerInteract>();

        if (playerObject.GetComponent<PlayerInputHandler>() == null)
            playerObject.AddComponent<PlayerInputHandler>();

        RemovePlayerModelModifiers(playerObject);

        EnsurePlayerCollider(playerObject);
    }

    void RemovePlayerModelModifiers(GameObject playerObject)
    {
        if (playerObject == null)
            return;

        if (disablePlayerImpactPhysics)
        {
            DestroyComponentsInChildren<FakeRagDoll>(playerObject);
            DestroyComponentsInChildren<PlayerImpactReaction>(playerObject);
            DestroyComponentsInChildren<PlayerRagdoll>(playerObject);
        }

        if (disableProceduralPlayerVisuals)
        {
            DestroyComponentsInChildren<PlayerVisualWobble>(playerObject);
            DestroyComponentsInChildren<FixieProceduralAnimator>(playerObject);
        }
    }

    static void DestroyComponentsInChildren<T>(GameObject root) where T : Component
    {
        if (root == null)
            return;

        foreach (T component in root.GetComponentsInChildren<T>(true))
        {
            if (component == null)
                continue;

            if (component is Behaviour behaviour)
                behaviour.enabled = false;

            UnityEngine.Object.Destroy(component);
        }
    }

    void EnsurePlayerCollider(GameObject playerObject)
    {
        if (playerObject == null)
            return;

        CapsuleCollider capsule = playerObject.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = playerObject.AddComponent<CapsuleCollider>();

        capsule.direction = 1;
        // Keep physics stable with a fixed gameplay capsule.
        // The Fixie renderers have noisy bounds that are good for visuals,
        // but not reliable enough to size collision each spawn.
        capsule.radius = 0.34f;
        capsule.height = 1.6f;
        capsule.center = new Vector3(0f, 0.8f, 0f);
        capsule.contactOffset = 0.02f;
    }

    void SetupPlayerVisual(Transform playerRoot, PlayerMovement movement, int slotIndex)
    {
        if (playerRoot == null)
            return;

        RebuildPlayerVisual(playerRoot, movement, slotIndex);
    }

    void RebuildPlayerVisual(Transform playerRoot, PlayerMovement movement, int slotIndex)
    {
        movement?.BindAnimator(null);
        RemoveAvatarRig(playerRoot != null ? playerRoot.gameObject : null);

        Transform modelRoot = EnsureModelRoot(playerRoot);
        ClearModelRoot(modelRoot);

        GameObject modelPrefab = GetFixiePrefabForSlot(slotIndex);
        if (modelPrefab == null)
        {
            if (debugLog)
                Debug.LogWarning($"[PlayerManager] No Fixie model assigned for slot {slotIndex}. Assign Fixie prefabs in the Inspector.");
            return;
        }

        MeshRenderer capsuleMesh = playerRoot.GetComponent<MeshRenderer>();
        if (capsuleMesh != null)
            capsuleMesh.enabled = false;

        GameObject model = Instantiate(modelPrefab, modelRoot);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        Animator modelAnimator = FindBestAnimator(model);

        StripRuntimeComponents(model, preserveAnimator: modelAnimator != null);
        SetLayerRecursively(model.transform, playerRoot.gameObject.layer);
        NormalizeVisualScale(playerRoot, model.transform);
        AlignVisualToPlayer(playerRoot, model.transform);
        ForceRenderersVisible(model.transform);

        AttachRuntimeFixieAnimation(playerRoot.gameObject, movement, modelPrefab, model.transform, modelAnimator, slotIndex);
    }

    void AttachRuntimeFixieAnimation(GameObject playerObject, PlayerMovement movement, GameObject modelPrefab, Transform visualRoot, Animator sourceAnimator, int slotIndex)
    {
        if (visualRoot == null)
            return;

        RemoveAvatarRig(playerObject);

        FixieAnimationRuntime runtime = visualRoot.GetComponent<FixieAnimationRuntime>();
        if (runtime == null)
            runtime = visualRoot.gameObject.AddComponent<FixieAnimationRuntime>();

        runtime.Bind(movement, visualRoot, modelPrefab, sourceAnimator, slotIndex);
    }

    Transform EnsureModelRoot(Transform playerRoot)
    {
        Transform modelRoot = playerRoot.Find("ModelRoot");
        if (modelRoot != null)
            return modelRoot;

        var go = new GameObject("ModelRoot");
        go.transform.SetParent(playerRoot);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    void ClearModelRoot(Transform modelRoot)
    {
        if (modelRoot == null)
            return;

        for (int i = modelRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = modelRoot.GetChild(i);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    void AttachAvatarRig(GameObject playerObject, PlayerMovement movement, Transform visualRoot)
    {
        if (playerObject == null || disableProceduralPlayerVisuals)
            return;

        PlayerAvatarRig rig = playerObject.GetComponent<PlayerAvatarRig>();
        if (rig == null)
            rig = playerObject.AddComponent<PlayerAvatarRig>();

        rig.Bind(movement, visualRoot);
    }

    bool BindModelAnimator(GameObject playerObject, PlayerMovement movement, Transform visualRoot, Animator modelAnimator)
    {
        if (!HasWorkingAnimator(modelAnimator))
        {
            RemoveAvatarRig(playerObject);
            return false;
        }

        RemoveAvatarRig(playerObject);

        if (movement != null)
            movement.BindAnimator(modelAnimator);

        if (playerObject != null)
            BindProceduralAnimator(playerObject.transform, modelAnimator);

        PlayerAnimator playerAnimator = modelAnimator.GetComponent<PlayerAnimator>();
        if (playerAnimator == null)
            playerAnimator = modelAnimator.gameObject.AddComponent<PlayerAnimator>();

        playerAnimator.BindMovement(movement);
        return true;
    }

    static bool HasWorkingAnimator(Animator animator)
    {
        if (animator == null || animator.avatar == null)
            return false;

        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller == null)
            return false;

        AnimationClip[] clips = controller.animationClips;
        return clips != null && clips.Length > 0;
    }

    static Animator FindBestAnimator(GameObject root)
    {
        if (root == null)
            return null;

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        Animator bestAnimator = null;
        int bestScore = int.MinValue;

        foreach (Animator candidate in animators)
        {
            if (candidate == null)
                continue;

            int score = 0;

            if (candidate.runtimeAnimatorController != null)
                score += 100;

            if (candidate.avatar != null)
                score += 25;

            AnimationClip[] clips = candidate.runtimeAnimatorController != null
                ? candidate.runtimeAnimatorController.animationClips
                : null;

            if (clips != null)
                score += clips.Length;

            if (score > bestScore)
            {
                bestScore = score;
                bestAnimator = candidate;
            }
        }

        return bestAnimator;
    }

    void RemoveAvatarRig(GameObject playerObject)
    {
        if (playerObject == null)
            return;

        PlayerAvatarRig rig = playerObject.GetComponent<PlayerAvatarRig>();
        if (rig == null)
            return;

        rig.enabled = false;
        Destroy(rig);
    }

    bool TryUseExistingFixieVisual(Transform playerRoot, PlayerMovement movement, int slotIndex)
    {
        Renderer[] existingRenderers = playerRoot.GetComponentsInChildren<Renderer>(true);
        bool hasVisibleAvatar = false;

        foreach (Renderer renderer in existingRenderers)
        {
            if (renderer == null)
                continue;

            if (renderer.transform == playerRoot && renderer is MeshRenderer)
                continue;

            hasVisibleAvatar = true;
            break;
        }

        if (!hasVisibleAvatar)
            return false;

        if (slotIndex != 0)
        {
            DisableExistingAvatarRenderers(playerRoot);
            return false;
        }

        MeshRenderer rootCapsule = playerRoot.GetComponent<MeshRenderer>();
        if (rootCapsule != null)
            rootCapsule.enabled = false;

        Transform visualRoot = FindPrimaryVisualRoot(playerRoot);
        if (visualRoot != null)
        {
            NormalizeVisualScale(playerRoot, visualRoot);
            AlignVisualToPlayer(playerRoot, visualRoot);
            ForceRenderersVisible(visualRoot);
        }

        Animator existingAnimator = FindBestAnimator(playerRoot.gameObject);
        if (movement != null)
            movement.BindAnimator(existingAnimator);

        if (existingAnimator != null)
        {
            PlayerAnimator playerAnimator = existingAnimator.GetComponent<PlayerAnimator>();
            if (playerAnimator == null)
                playerAnimator = existingAnimator.gameObject.AddComponent<PlayerAnimator>();

            playerAnimator.BindMovement(movement);
        }

        if (debugLog)
            Debug.Log($"[PlayerManager] Using existing Fixie prefab as player visual for slot {slotIndex}.");

        return true;
    }

    void DisableExistingAvatarRenderers(Transform playerRoot)
    {
        if (playerRoot == null)
            return;

        foreach (Renderer renderer in playerRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            if (renderer.transform == playerRoot && renderer is MeshRenderer)
                continue;

            renderer.enabled = false;
        }
    }

    void AttachFixieModel(Transform playerRoot, PlayerMovement movement, int slotIndex)
    {
        GameObject modelPrefab = GetFixiePrefabForSlot(slotIndex);

        if (modelPrefab == null)
        {
            if (debugLog)
                Debug.LogWarning($"[PlayerManager] No Fixie model assigned for slot {slotIndex}. Assign Fixie prefabs in the Inspector.");
            return;
        }

        Transform modelRoot = playerRoot.Find("ModelRoot");
        if (modelRoot == null)
        {
            var go = new GameObject("ModelRoot");
            go.transform.SetParent(playerRoot);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            modelRoot = go.transform;
        }

        MeshRenderer capsuleMesh = playerRoot.GetComponent<MeshRenderer>();

        GameObject model = Instantiate(modelPrefab, modelRoot);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        SetLayerRecursively(model.transform, playerRoot.gameObject.layer);
        NormalizeVisualScale(playerRoot, model.transform);
        AlignVisualToPlayer(playerRoot, model.transform);
        ForceRenderersVisible(model.transform);

        Animator modelAnimator = FindBestAnimator(model);
        if (movement != null)
            movement.BindAnimator(modelAnimator);

        // Add PlayerAnimator to drive Animator from movement state
        if (modelAnimator != null)
        {
            PlayerAnimator playerAnimator = modelAnimator.GetComponent<PlayerAnimator>();
            if (playerAnimator == null)
                playerAnimator = modelAnimator.gameObject.AddComponent<PlayerAnimator>();

            playerAnimator.BindMovement(movement);
        }

        Renderer[] modelRenderers = model.GetComponentsInChildren<Renderer>(true);
        bool hasVisibleRenderer = modelRenderers != null && modelRenderers.Length > 0;

        if (capsuleMesh != null)
            capsuleMesh.enabled = !hasVisibleRenderer;

        // Fixie prefabs are visual-only children. Strip any physics or movement
        // components that Unity auto-added (e.g. Rigidbody via [RequireComponent])
        // — a second Rigidbody nested under the player's own causes chaotic physics.
        StripRuntimeComponents(model, preserveAnimator: true);

        if (!hasVisibleRenderer)
        {
            Debug.LogWarning($"[PlayerManager] Fixie '{modelPrefab.name}' se instanció sin renderers visibles. Se deja visible la cápsula fallback.");
        }
        else if (debugLog)
            Debug.Log($"[PlayerManager] Attached Fixie model '{modelPrefab.name}' to player slot {slotIndex}.");
    }

    GameObject GetFixiePrefabForSlot(int slotIndex)
    {
        if (fixieMeshPrefabs == null || fixieMeshPrefabs.Length == 0)
            return null;

        // Random assignment — any player can get any model
        return fixieMeshPrefabs[UnityEngine.Random.Range(0, fixieMeshPrefabs.Length)];
    }

    void StripRuntimeComponents(GameObject model, bool preserveAnimator = false)
    {
        if (model == null)
            return;

        DestroyComponentsInChildren<FakeRagDoll>(model);
        DestroyComponentsInChildren<PlayerImpactReaction>(model);
        DestroyComponentsInChildren<PlayerRagdoll>(model);
        DestroyComponentsInChildren<PlayerVisualWobble>(model);
        DestroyComponentsInChildren<FixieProceduralAnimator>(model);
        DestroyComponentsInChildren<FixieAnimationRuntime>(model);
        DestroyComponentsInChildren<Joint>(model);
        DestroyComponentsInChildren<PlayerAnimator>(model);

        if (!preserveAnimator)
            DestroyComponentsInChildren<Animator>(model);

        foreach (PlayerInput input in model.GetComponentsInChildren<PlayerInput>(true))
        {
            input.enabled = false;
            Destroy(input);
        }

        foreach (PlayerMovement movement in model.GetComponentsInChildren<PlayerMovement>(true))
        {
            movement.enabled = false;
            Destroy(movement);
        }

        foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
            Destroy(body);
        }

        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            Destroy(collider);
        }
    }

    void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
            return;

        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    void AlignVisualToPlayer(Transform playerRoot, Transform visualRoot)
    {
        if (playerRoot == null || visualRoot == null)
            return;

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        if (!TryGetCombinedRendererBounds(renderers, out Bounds visualBounds))
            return;

        float playerBottomOffset = GetPlayerBottomOffset(playerRoot);
        float visualBottomOffset = visualBounds.min.y - playerRoot.position.y;
        float requiredOffsetY = playerBottomOffset - visualBottomOffset;

        visualRoot.localPosition += Vector3.up * requiredOffsetY;
    }

    void NormalizeVisualScale(Transform playerRoot, Transform visualRoot)
    {
        if (playerRoot == null || visualRoot == null)
            return;

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        if (!TryGetCombinedRendererBounds(renderers, out Bounds visualBounds))
            return;

        float visualHeight = visualBounds.size.y;
        if (visualHeight <= 0.001f)
            return;

        float targetHeight = GetTargetVisualHeight(playerRoot);
        if (targetHeight <= 0.001f)
            return;

        float scaleFactor = Mathf.Clamp(targetHeight / visualHeight, 0.05f, 100f);
        visualRoot.localScale *= scaleFactor;

        if (debugLog)
            Debug.Log($"[PlayerManager] Normalized visual scale '{visualRoot.name}' x{scaleFactor:0.###} (height {visualHeight:0.###} -> {targetHeight:0.###}).");
    }

    void BindVisualWobble(Transform playerRoot, Transform visualRoot)
    {
        if (playerRoot == null || visualRoot == null)
            return;

        PlayerVisualWobble wobble = playerRoot.GetComponent<PlayerVisualWobble>();
        if (wobble != null)
            wobble.BindVisual(visualRoot);

        FixieProceduralAnimator proceduralAnimator = playerRoot.GetComponent<FixieProceduralAnimator>();
        if (proceduralAnimator != null)
            proceduralAnimator.BindVisual(visualRoot);
    }

    void BindProceduralAnimator(Transform playerRoot, Animator targetAnimator)
    {
        if (playerRoot == null)
            return;

        FixieProceduralAnimator proceduralAnimator = playerRoot.GetComponent<FixieProceduralAnimator>();
        if (proceduralAnimator != null)
            proceduralAnimator.BindAnimator(targetAnimator);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    static void ClearEditorConsoleForAnimationDebug()
    {
#if UNITY_EDITOR
        System.Type logEntriesType = System.Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
        System.Reflection.MethodInfo clearMethod = logEntriesType?.GetMethod(
            "Clear",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

        clearMethod?.Invoke(null, null);
#endif
    }

    static void InstallAnimationLogFilter()
    {
        if (animationLogFilterInstalled)
            return;

        originalLogHandler = Debug.unityLogger.logHandler;
        animationOnlyLogHandler = new AnimationOnlyLogHandler(originalLogHandler);
        Debug.unityLogger.logHandler = animationOnlyLogHandler;
        animationLogFilterInstalled = true;
    }

    static void RestoreAnimationLogFilter()
    {
        if (!animationLogFilterInstalled)
            return;

        if (ReferenceEquals(Debug.unityLogger.logHandler, animationOnlyLogHandler) && originalLogHandler != null)
            Debug.unityLogger.logHandler = originalLogHandler;

        animationOnlyLogHandler = null;
        originalLogHandler = null;
        animationLogFilterInstalled = false;
    }

    sealed class AnimationOnlyLogHandler : ILogHandler
    {
        private readonly ILogHandler innerHandler;

        public AnimationOnlyLogHandler(ILogHandler targetHandler)
        {
            innerHandler = targetHandler;
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            if (!ContainsAnimationDebugPrefix(format, args))
                return;

            innerHandler?.LogFormat(logType, context, format, args);
        }

        public void LogException(System.Exception exception, UnityEngine.Object context)
        {
            if (exception == null)
                return;

            if (!ContainsAnimationDebugPrefix(exception.Message, null) &&
                !ContainsAnimationDebugPrefix(exception.StackTrace, null))
            {
                return;
            }

            innerHandler?.LogException(exception, context);
        }

        static bool ContainsAnimationDebugPrefix(string message, object[] args)
        {
            if (!string.IsNullOrEmpty(message) &&
                message.IndexOf(AnimationDebugPrefix, System.StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            if (args == null)
                return false;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == null)
                    continue;

                string value = args[i].ToString();
                if (!string.IsNullOrEmpty(value) &&
                    value.IndexOf(AnimationDebugPrefix, System.StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    void ForceRenderersVisible(Transform visualRoot)
    {
        if (visualRoot == null)
            return;

        foreach (Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            renderer.enabled = true;
            renderer.forceRenderingOff = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            if (renderer is SkinnedMeshRenderer skinned)
            {
                skinned.quality = SkinQuality.Bone4;
                skinned.updateWhenOffscreen = true;
            }
        }
    }

    Transform FindPrimaryVisualRoot(Transform playerRoot)
    {
        if (playerRoot == null)
            return null;

        foreach (Renderer renderer in playerRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            if (renderer.transform == playerRoot && renderer is MeshRenderer)
                continue;

            Transform current = renderer.transform;
            while (current.parent != null && current.parent != playerRoot)
                current = current.parent;

            return current;
        }

        return playerRoot;
    }

    float GetPlayerBottomOffset(Transform playerRoot)
    {
        if (playerRoot == null)
            return -spawnHeightOffset;

        float bottomOffset = -spawnHeightOffset;
        bool foundCollider = false;

        foreach (Collider collider in playerRoot.GetComponents<Collider>())
        {
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;

            float candidate = collider.bounds.min.y - playerRoot.position.y;
            if (!foundCollider || candidate < bottomOffset)
            {
                bottomOffset = candidate;
                foundCollider = true;
            }
        }

        return bottomOffset;
    }

    float GetTargetVisualHeight(Transform playerRoot)
    {
        if (playerRoot == null)
            return 1.95f;

        CapsuleCollider capsule = playerRoot.GetComponent<CapsuleCollider>();
        if (capsule != null)
            return Mathf.Max(1.9f, capsule.height * 1.2f);

        Collider collider = playerRoot.GetComponent<Collider>();
        if (collider != null)
            return Mathf.Max(1.9f, collider.bounds.size.y * 1.1f);

        return 1.95f;
    }

    bool TryGetCombinedRendererBounds(Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool initialized = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return initialized;
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
