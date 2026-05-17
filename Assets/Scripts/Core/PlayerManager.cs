using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerManager : MonoBehaviour
{
    const int MaxSupportedPlayers = LobbyPlayerSessionData.MaxPlayers;
    const string AnimationDebugPrefix = "[FixieAnimation]";
    const string FixieAnimationResourceFolder = "FixieAnimations";
    const string FixieAnimationSetPrefix = "Fixie_P";
    const int FixieAnimationSetCount = 3;

    public static PlayerManager Instance { get; private set; }

    [Header("Player Setup")]
    [SerializeField] private PlayerData playerData;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform cameraTransform;

    [Header("Fixie Visual Prefabs (index = player slot)")]
    [SerializeField] private GameObject[] fixieVisualPrefabs;

    [Header("Shared Animation Clips")]
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip walkClip;
    [SerializeField] private AnimationClip runClip;
    [SerializeField] private AnimationClip jumpClip;
    [SerializeField] private AnimationClip fallClip;

    [Header("Spawn Tuning")]
    [SerializeField] private float spawnHeightOffset = 0.15f;
    [SerializeField] private float minSpawnSeparation = 1.1f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    [Header("Temporary Player Stabilization")]
    [SerializeField] private bool disablePlayerImpactPhysics = false;
    [SerializeField] private bool disableProceduralPlayerVisuals = false;

    private readonly List<PlayerMovement> players = new();
    private readonly Dictionary<PlayerInput, int> activePlayerSlots = new();
    private readonly Dictionary<PlayerInput, int> pendingPlayerSlots = new();
    private readonly FixieAnimationSet[] runtimeAnimationSets = new FixieAnimationSet[FixieAnimationSetCount];

    private PlayerInputManager playerInputManager;
    private int expectedLobbyPlayers;

    public static event System.Action OnPlayerCountChanged;

    private void Awake()
    {
        Instance = this;
        playerInputManager = GetComponent<PlayerInputManager>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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
            EnsurePlayerCollider(playerInput.gameObject);
            movement.Initialize(slotIndex, playerData, cameraTransform);
            SetupPlayerVisual(playerInput.transform, movement, slotIndex);
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
        playerInputManager.EnableJoining();

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

            PlayerInput restoredInput = playerInputManager.JoinPlayer(
                playerIndex: entry.PlayerIndex,
                splitScreenIndex: -1,
                controlScheme: entry.ControlScheme,
                pairWithDevice: device
            );

            if (restoredInput != null)
                restoredPlayers++;
            else
                Debug.LogWarning($"[PlayerManager] Could not restore lobby player {entry.PlayerIndex} ({entry.ControlScheme}).");
        }

        if (debugLog)
            Debug.Log($"[PlayerManager] Restored {restoredPlayers}/{expectedLobbyPlayers} lobby players into gameplay.");
    }

    void ApplyLobbyRole(PlayerInput playerInput, int slotIndex)
    {
        PlayerRole playerRole = playerInput.GetComponent<PlayerRole>();
        if (playerRole == null)
        {
            Debug.LogWarning($"[PlayerManager] PlayerRole not found on P{slotIndex} — no role assigned");
            return;
        }

        RoleDefinition roleDef = RoleSelectionData.GetRole(slotIndex);
        if (roleDef != null)
        {
            playerRole.AssignRole(roleDef);
            Debug.Log($"[PlayerManager] Role '{roleDef.roleName}' applied to P{slotIndex}");
            return;
        }

        Debug.LogWarning($"[PlayerManager] No saved role for P{slotIndex} — no restrictions");
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

        if (playerObject.GetComponent<PlayerAudioController>() == null)
            playerObject.AddComponent<PlayerAudioController>();

        // Para que el jugador tenga tambaleo visual al chocar
        if (playerObject.GetComponent<PlayerVisualWobble>() == null)
            playerObject.AddComponent<PlayerVisualWobble>();

        // Para detectar choques y mandar el wobble visual
        if (playerObject.GetComponent<FakeRagDoll>() == null)
            playerObject.AddComponent<FakeRagDoll>();

        RemovePlayerModelModifiers(playerObject);

        EnsurePlayerCollider(playerObject);
    }

    void RemovePlayerModelModifiers(GameObject playerObject)
    {
        if (playerObject == null)
            return;

        // Ya NO borramos FakeRagDoll ni PlayerVisualWobble del player root.
        // Solo quitamos ragdolls físicos viejos si existen.
        if (disablePlayerImpactPhysics)
        {
            DestroyComponentsInChildren<PlayerImpactReaction>(playerObject);
            DestroyComponentsInChildren<PlayerRagdoll>(playerObject);
        }

        // Ya NO borramos PlayerVisualWobble porque lo necesitamos para el tambaleo.
        if (disableProceduralPlayerVisuals)
        {
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

            DestroyRuntimeObject(component);
        }
    }

    static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEngine.Object.DestroyImmediate(target);
            return;
        }
#endif

        UnityEngine.Object.Destroy(target);
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

        FixieAnimationSet animationSet = GetAnimationSetForSlot(slotIndex);
        GameObject modelPrefab = GetVisualPrefabForSlot(slotIndex, animationSet);
        if (modelPrefab == null)
        {
            Debug.LogWarning($"{AnimationDebugPrefix} Missing Fixie visual prefab for slot {slotIndex}. Configure Resources/FixieAnimations or assign fixieVisualPrefabs[] on PlayerManager.");
            return;
        }

        if (!HasAnimationSource(animationSet))
        {
            Debug.LogWarning($"{AnimationDebugPrefix} Missing animation clips for slot {slotIndex}. Configure Resources/FixieAnimations/Fixie_P1..P3 or assign idle/walk/run/jump/fall on PlayerManager.");
            return;
        }

        ClearModelRoot(modelRoot);

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
        // ForceRenderersVisible must run before NormalizeVisualScale so that
        // SkinnedMeshRenderer.updateWhenOffscreen = true and bounds are valid
        // even when the player spawns outside the camera frustum.
        ForceRenderersVisible(model.transform);
        NormalizeVisualScale(playerRoot, model.transform);
        AlignVisualToPlayer(playerRoot, model.transform);

        BindVisualWobble(playerRoot, model.transform);
        AttachRuntimeFixieAnimation(playerRoot.gameObject, movement, model.transform, modelAnimator, animationSet, slotIndex);
    }

    void AttachRuntimeFixieAnimation(GameObject playerObject, PlayerMovement movement, Transform visualRoot, Animator sourceAnimator, FixieAnimationSet animationSet, int slotIndex)
    {
        if (visualRoot == null)
            return;

        RemoveAvatarRig(playerObject);

        FixieAnimationRuntime runtime = visualRoot.GetComponent<FixieAnimationRuntime>();
        if (runtime == null)
            runtime = visualRoot.gameObject.AddComponent<FixieAnimationRuntime>();

        if (IsAnimationSetComplete(animationSet))
        {
            runtime.Bind(movement, visualRoot, sourceAnimator, animationSet, slotIndex);
        }
        else
        {
            runtime.Bind(
                movement,
                visualRoot,
                sourceAnimator,
                idleClip,
                walkClip,
                runClip,
                jumpClip,
                fallClip,
                slotIndex);
        }
    }

    bool HasAnimationSource(FixieAnimationSet animationSet)
    {
        return IsAnimationSetComplete(animationSet) || HasHardcodedAnimationClips();
    }

    static bool IsAnimationSetComplete(FixieAnimationSet animationSet)
    {
        return animationSet != null && animationSet.IsValid;
    }

    bool HasHardcodedAnimationClips()
    {
        return idleClip != null &&
            walkClip != null &&
            runClip != null &&
            jumpClip != null &&
            fallClip != null;
    }

    FixieAnimationSet GetAnimationSetForSlot(int slotIndex)
    {
        int index = Mathf.Clamp(slotIndex, 0, runtimeAnimationSets.Length - 1);
        FixieAnimationSet cached = runtimeAnimationSets[index];
        if (cached != null)
            return cached;

        string resourcePath = $"{FixieAnimationResourceFolder}/{FixieAnimationSetPrefix}{index + 1}";
        cached = Resources.Load<FixieAnimationSet>(resourcePath);
        runtimeAnimationSets[index] = cached;

        if (cached == null && debugLog)
            Debug.LogWarning($"{AnimationDebugPrefix} Runtime animation set not found at Resources/{resourcePath}.");

        return cached;
    }

    GameObject GetVisualPrefabForSlot(int slotIndex, FixieAnimationSet animationSet)
    {
        if (animationSet != null && animationSet.VisualPrefab != null)
            return animationSet.VisualPrefab;

        if (fixieVisualPrefabs == null || fixieVisualPrefabs.Length == 0)
            return null;

        int index = Mathf.Clamp(slotIndex, 0, fixieVisualPrefabs.Length - 1);
        return fixieVisualPrefabs[index];
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
            DestroyRuntimeObject(child.gameObject);
        }
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

            if (candidate.avatar != null)
                score += 100;

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
        DestroyRuntimeObject(rig);
    }

    void StripRuntimeComponents(GameObject model, bool preserveAnimator = false)
    {
        if (model == null)
            return;
        DestroyComponentsInChildren<FakeRagDoll>(model);
        DestroyComponentsInChildren<PlayerVisualWobble>(model);
        DestroyComponentsInChildren<PlayerImpactReaction>(model);
        DestroyComponentsInChildren<PlayerRagdoll>(model);
        DestroyComponentsInChildren<FixieProceduralAnimator>(model);
        DestroyComponentsInChildren<FixieAnimationRuntime>(model);
        DestroyComponentsInChildren<Joint>(model);
        DestroyComponentsInChildren<PlayerAnimator>(model);

        if (!preserveAnimator)
            DestroyComponentsInChildren<Animator>(model);

        foreach (PlayerInput input in model.GetComponentsInChildren<PlayerInput>(true))
        {
            input.enabled = false;
            DestroyRuntimeObject(input);
        }

        foreach (PlayerMovement movement in model.GetComponentsInChildren<PlayerMovement>(true))
        {
            movement.enabled = false;
            DestroyRuntimeObject(movement);
        }

        foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
            DestroyRuntimeObject(body);
        }

        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            DestroyRuntimeObject(collider);
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
