using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Prefab-based ship layout generator.
/// Places modules by reading actual socket world positions — zero assumptions.
/// Corridor rotation computed from the vector between two connecting sockets.
/// </summary>
[DefaultExecutionOrder(-100)]
public class ShipLayoutGenerator : MonoBehaviour
{
    // ── Prefab References ────────────────────────────────────────────────

    [Header("Room Modules")]
    [SerializeField] GameObject prefabBridge;
    [SerializeField] GameObject prefabRepairPower;
    [SerializeField] GameObject prefabRepairComms;
    [SerializeField] GameObject prefabRepairGravity;
    [SerializeField] GameObject prefabRepairHull;

    [Header("Extra Room Modules")]
    [SerializeField] GameObject prefabCrewBunks;
    [SerializeField] GameObject prefabGunStation;
    [SerializeField] GameObject prefabStorage;

    [Header("Corridor Modules (long axis = local Z)")]
    [SerializeField] GameObject prefabCorridorStraight;
    [SerializeField] GameObject prefabCorridorStraight_v2;
    [SerializeField] GameObject prefabCorridorStraight_v3;
    [SerializeField] GameObject prefabCorridorTurn;
    [SerializeField] GameObject prefabCorridorT;

    [Header("Settings")]
    [SerializeField] int seed = 0;
    [SerializeField] float corridorGap = 4f;
    [SerializeField] Transform shipInteriorParent;
    [SerializeField] bool logValidation = true;

    // ── Public State ─────────────────────────────────────────────────────

    public static bool IsReady     { get; private set; }
    public static int  CurrentSeed { get; private set; }

    // ── Internal ──────────────────────────────────────────────────────────

    System.Random rng;
    Transform root;
    GameObject bridgeInstance;

    readonly Dictionary<RepairStation.StationType, GameObject> repairInstances = new();
    readonly List<PlacedInfo> allPlaced = new();
    // Track which sockets have been used (instanceID + socketName)
    readonly HashSet<string> usedSockets = new();

    struct PlacedInfo
    {
        public GameObject instance;
        public Bounds     bounds;
    }

    static string OppositeSocket(string name) => name switch
    {
        "Socket_North" => "Socket_South",
        "Socket_South" => "Socket_North",
        "Socket_East"  => "Socket_West",
        "Socket_West"  => "Socket_East",
        _ => name,
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        IsReady = false;

        int usedSeed = seed == 0 ? Random.Range(1, 99999) : seed;
        CurrentSeed = usedSeed;
        rng = new System.Random(usedSeed);

        if (shipInteriorParent == null)
            shipInteriorParent = new GameObject("ShipInterior").transform;
        root = shipInteriorParent;

        var assembler = GetComponent<ShipAssembler>();
        if (assembler != null) assembler.enabled = false;

        if (prefabBridge == null)
        {
            Debug.LogError("[ShipLayout] prefabBridge not assigned!");
            IsReady = true;
            return;
        }

        try
        {
            BuildShip();
            WireRepairStations();
            WireSpawnPoints();
            if (logValidation)
                Debug.Log($"[ShipLayout] Done | Seed={CurrentSeed} | Modules={allPlaced.Count}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ShipLayout] FAILED: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            IsReady = true;
            Debug.Log("[ShipLayout] IsReady = true");
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // SHIP CONSTRUCTION
    // ══════════════════════════════════════════════════════════════════════

    void BuildShip()
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            DestroyImmediate(root.GetChild(i).gameObject);

        // ── Phase 1: Bridge ──────────────────────────────────────────
        bridgeInstance = Instantiate(prefabBridge, Vector3.zero, Quaternion.identity, root);
        bridgeInstance.name = "Bridge";
        Register(bridgeInstance);

        // ── Phase 2: Repair Stations ─────────────────────────────────
        var socketNames = new List<string>
            { "Socket_North", "Socket_South", "Socket_East", "Socket_West" };
        Shuffle(socketNames);

        var repairs = new[]
        {
            (prefab: prefabRepairPower,   type: RepairStation.StationType.Energy),
            (prefab: prefabRepairComms,   type: RepairStation.StationType.Communications),
            (prefab: prefabRepairGravity, type: RepairStation.StationType.Gravity),
            (prefab: prefabRepairHull,    type: RepairStation.StationType.Hull),
        };

        for (int i = 0; i < 4; i++)
        {
            string bridgeSocket = socketNames[i];
            var (prefab, stationType) = repairs[i];
            if (prefab == null) continue;

            GameObject instance = ConnectModule(
                bridgeInstance, bridgeSocket, prefab, $"Repair_{stationType}");

            if (instance != null)
                repairInstances[stationType] = instance;
        }

        // ── Phase 3: Extra rooms ─────────────────────────────────────
        PlaceExtraRooms();
    }

    // ══════════════════════════════════════════════════════════════════════
    // MODULE CONNECTION (the core algorithm)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Connect a new module to an existing one.
    /// 1. Read the source socket's world position
    /// 2. Compute the outward direction (from module center toward socket)
    /// 3. Connection point = sourceSocket + outward * corridorGap
    /// 4. Place new module so its opposite socket lands at connectionPoint
    /// 5. Place corridor between the two socket positions
    /// </summary>
    GameObject ConnectModule(
        GameObject sourceInstance, string sourceSocketName,
        GameObject newPrefab, string label)
    {
        // Mark source socket as used
        MarkSocketUsed(sourceInstance, sourceSocketName);

        // 1. Source socket world position
        Transform srcSocket = FindSocket(sourceInstance, sourceSocketName);
        if (srcSocket == null)
        {
            Log($"Socket {sourceSocketName} not found in {sourceInstance.name}");
            return null;
        }
        Vector3 srcSocketWorld = srcSocket.position;

        // 2. Outward direction = from module center toward this socket (XZ only)
        Vector3 moduleCenter = GetModuleCenter(sourceInstance);
        Vector3 outward = (srcSocketWorld - moduleCenter);
        outward.y = 0;
        outward = outward.normalized;

        // 3. Connection point
        Vector3 connectionPoint = srcSocketWorld + outward * corridorGap;

        // 4. New module placement
        // Read the opposite socket's LOCAL position in the prefab
        string newSocketName = OppositeSocket(sourceSocketName);
        Vector3 newSocketLocal = GetPrefabSocketLocalPos(newPrefab, newSocketName);

        // The new module's pivot = connectionPoint - socketLocalOffset
        Vector3 newPivot = connectionPoint - newSocketLocal;

        // Overlap check
        Bounds newBounds = EstimateBoundsFromPrefab(newPrefab, newPivot);
        if (CheckOverlap(newBounds))
        {
            Log($"Overlap for {label} at {newPivot} — skipping");
            return null;
        }

        // Instantiate
        GameObject instance = Instantiate(newPrefab, newPivot, Quaternion.identity, root);
        instance.name = label;
        Register(instance);
        MarkSocketUsed(instance, newSocketName);

        // 5. Verify: read the actual world position of the new socket after placement
        Transform newSocket = FindSocket(instance, newSocketName);
        Vector3 newSocketWorld = newSocket != null ? newSocket.position : connectionPoint;

        // Place corridor between the two sockets
        PlaceCorridor(srcSocketWorld, newSocketWorld);

        Log($"Placed {label} at {newPivot}");
        return instance;
    }

    // ── Corridor ──────────────────────────────────────────────────────────

    void PlaceCorridor(Vector3 from, Vector3 to)
    {
        GameObject prefab = GetRandomCorridorStraight();
        if (prefab == null) return;

        Vector3 midpoint = (from + to) / 2f;

        // Direction from 'from' to 'to' — this is the axis the corridor must span
        Vector3 direction = (to - from);
        direction.y = 0;

        if (direction.sqrMagnitude < 0.01f) return;

        // Corridor long axis = local Z.
        // LookRotation aligns local Z with 'direction'
        Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        GameObject corr = Instantiate(prefab, midpoint, rotation, root);
        corr.name = $"Corridor_{allPlaced.Count}";

        Log($"Corridor at {midpoint}, rot={rotation.eulerAngles}");
    }

    // ── Extra Rooms ───────────────────────────────────────────────────────

    void PlaceExtraRooms()
    {
        var extraPrefabs = new List<GameObject>();
        if (prefabCrewBunks  != null) extraPrefabs.Add(prefabCrewBunks);
        if (prefabGunStation != null) extraPrefabs.Add(prefabGunStation);
        if (prefabStorage    != null) extraPrefabs.Add(prefabStorage);
        if (extraPrefabs.Count == 0) return;

        // Collect all unused sockets from repair station instances
        var candidates = new List<(GameObject instance, string socketName)>();

        foreach (var kvp in repairInstances)
        {
            GameObject inst = kvp.Value;
            foreach (string sName in GetAllSocketNames(inst))
            {
                if (IsSocketUsed(inst, sName)) continue;
                candidates.Add((inst, sName));
            }
        }

        Shuffle(candidates);

        int count = Mathf.Min(2 + rng.Next(3), candidates.Count);
        int placed = 0;

        foreach (var (inst, socketName) in candidates)
        {
            if (placed >= count) break;

            GameObject prefab = extraPrefabs[rng.Next(extraPrefabs.Count)];
            GameObject result = ConnectModule(inst, socketName, prefab,
                $"Extra_{prefab.name.Replace("Module_", "")}_{placed}");

            if (result != null) placed++;
        }

        Log($"Placed {placed} extra rooms");
    }

    // ══════════════════════════════════════════════════════════════════════
    // WIRING
    // ══════════════════════════════════════════════════════════════════════

    void WireRepairStations()
    {
#pragma warning disable CS0618
        var allStations = FindObjectsOfType<RepairStation>();
#pragma warning restore CS0618

        foreach (var station in allStations)
        {
            if (!repairInstances.TryGetValue(station.Type, out GameObject moduleInst))
                continue;

            string anchorName = station.Type switch
            {
                RepairStation.StationType.Gravity        => "projector",
                RepairStation.StationType.Energy         => "Battery_medium",
                RepairStation.StationType.Communications => "console",
                RepairStation.StationType.Hull           => "generator",
                _ => null,
            };

            Transform anchor = null;
            if (anchorName != null)
            {
                anchor = moduleInst.GetComponentsInChildren<Transform>()
                    .FirstOrDefault(t => t.name.Equals(anchorName,
                        System.StringComparison.OrdinalIgnoreCase));
            }

            Vector3 pos = anchor != null
                ? anchor.position + Vector3.up * 0.5f
                : GetModuleCenter(moduleInst) + Vector3.up;

            station.transform.position = pos;
            station.SetGeneratedLocationLabel(moduleInst.name);
            Log($"{station.Type} → {moduleInst.name}" +
                (anchor != null ? $" (anchor: {anchorName})" : " (fallback)"));
        }
    }

    void WireSpawnPoints()
    {
        if (bridgeInstance == null) return;

        Transform spawnParent = bridgeInstance.transform.Find("SpawnPoints");
        if (spawnParent == null) { Log("No SpawnPoints in Bridge"); return; }

        var points = new List<Transform>();
        foreach (Transform child in spawnParent) points.Add(child);

        if (points.Count > 0 && PlayerManager.Instance != null)
        {
            PlayerManager.Instance.SetSpawnPoints(points.ToArray());
            Log($"Wired {points.Count} spawn points");
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Compute the XZ center of a module from its socket positions.</summary>
    Vector3 GetModuleCenter(GameObject instance)
    {
        Transform socketsParent = instance.transform.Find("Sockets");
        if (socketsParent == null || socketsParent.childCount == 0)
            return instance.transform.position;

        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (Transform child in socketsParent)
        {
            sum += child.position;
            count++;
        }
        Vector3 center = sum / count;
        center.y = 0;
        return center;
    }

    Vector3 GetPrefabSocketLocalPos(GameObject prefab, string socketName)
    {
        Transform socketsParent = prefab.transform.Find("Sockets");
        if (socketsParent == null) return new Vector3(6, 0, 6);
        Transform socket = socketsParent.Find(socketName);
        return socket != null ? socket.localPosition : new Vector3(6, 0, 6);
    }

    Transform FindSocket(GameObject instance, string socketName)
    {
        Transform socketsParent = instance.transform.Find("Sockets");
        return socketsParent != null ? socketsParent.Find(socketName) : null;
    }

    List<string> GetAllSocketNames(GameObject instance)
    {
        var result = new List<string>();
        Transform socketsParent = instance.transform.Find("Sockets");
        if (socketsParent == null) return result;
        foreach (Transform child in socketsParent) result.Add(child.name);
        return result;
    }

    void MarkSocketUsed(GameObject instance, string socketName)
        => usedSockets.Add($"{instance.GetInstanceID()}_{socketName}");

    bool IsSocketUsed(GameObject instance, string socketName)
        => usedSockets.Contains($"{instance.GetInstanceID()}_{socketName}");

    Bounds EstimateBoundsFromPrefab(GameObject prefab, Vector3 pivot)
    {
        // Compute bounds from socket spread
        Transform socketsParent = prefab.transform.Find("Sockets");
        if (socketsParent == null)
            return new Bounds(pivot + new Vector3(6, 1.5f, 6), new Vector3(12, 3, 12));

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (Transform child in socketsParent)
        {
            Vector3 lp = child.localPosition;
            if (lp.x < minX) minX = lp.x;
            if (lp.x > maxX) maxX = lp.x;
            if (lp.z < minZ) minZ = lp.z;
            if (lp.z > maxZ) maxZ = lp.z;
        }

        // Sockets are at/near edges, so use them as bounds
        Vector3 min = pivot + new Vector3(minX, 0, minZ);
        Vector3 max = pivot + new Vector3(maxX, 3, maxZ);
        Vector3 size = max - min;
        Vector3 center = min + size / 2f;
        return new Bounds(center, size);
    }

    bool CheckOverlap(Bounds newBounds)
    {
        Bounds shrunk = newBounds;
        shrunk.Expand(-0.5f); // allow touching edges

        foreach (var placed in allPlaced)
        {
            if (placed.bounds.Intersects(shrunk))
                return true;
        }
        return false;
    }

    void Register(GameObject instance)
    {
        var renderers = instance.GetComponentsInChildren<MeshRenderer>();
        Bounds b;
        if (renderers.Length > 0)
        {
            b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
        }
        else
        {
            b = new Bounds(instance.transform.position + new Vector3(6, 1.5f, 6),
                           new Vector3(12, 3, 12));
        }
        allPlaced.Add(new PlacedInfo { instance = instance, bounds = b });
    }

    GameObject GetRandomCorridorStraight()
    {
        var options = new List<GameObject>();
        if (prefabCorridorStraight    != null) options.Add(prefabCorridorStraight);
        if (prefabCorridorStraight_v2 != null) options.Add(prefabCorridorStraight_v2);
        if (prefabCorridorStraight_v3 != null) options.Add(prefabCorridorStraight_v3);
        return options.Count == 0 ? null : options[rng.Next(options.Count)];
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    void Log(string msg)
    {
        if (logValidation) Debug.Log($"[ShipLayout] {msg}");
    }

    // ── Gizmos ────────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (allPlaced == null) return;

        foreach (var placed in allPlaced)
        {
            if (placed.instance == null) continue;

            Gizmos.color = new Color(0, 1, 0, 0.12f);
            Gizmos.DrawWireCube(placed.bounds.center, placed.bounds.size);

            Transform socketsParent = placed.instance.transform.Find("Sockets");
            if (socketsParent == null) continue;

            Vector3 moduleCenter = GetModuleCenter(placed.instance);

            foreach (Transform sock in socketsParent)
            {
                bool used = usedSockets.Contains(
                    $"{placed.instance.GetInstanceID()}_{sock.name}");

                Gizmos.color = used ? Color.green : Color.red;
                Gizmos.DrawSphere(sock.position, 0.3f);

                // Draw outward direction (from center toward socket)
                Vector3 outward = (sock.position - moduleCenter).normalized;
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(sock.position, sock.position + outward * 2f);
            }
        }
    }

    // ── Public API ────────────────────────────────────────────────────────

    public void RegenerateWithSeed(int newSeed)
    {
        seed = newSeed;
        CurrentSeed = newSeed;
    }
}
