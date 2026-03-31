using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Variant-aware ship layout generator.
/// Builds a fixed cross topology, selects the correct variant (_N / _NE / _NS)
/// and places every room by aligning resolved door openings instead of assuming
/// centered pivots, fixed cell offsets, or perfectly placed socket markers.
/// </summary>
[DefaultExecutionOrder(-100)]
public class ShipLayoutGenerator : MonoBehaviour
{
    [Serializable]
    public class ModuleSet
    {
        public GameObject variantN;
        public GameObject variantNE;
        public GameObject variantNS;
    }

    internal enum Dir { N, S, E, W }
    internal enum ModuleVariantSlot { N, NE, NS }

    internal readonly struct EdgeOpening
    {
        public EdgeOpening(Dir direction, Vector3 centerLocal, float spanMin, float spanMax, float hintDistance, bool isAmbiguous)
        {
            Direction = direction;
            CenterLocal = centerLocal;
            SpanMin = Mathf.Min(spanMin, spanMax);
            SpanMax = Mathf.Max(spanMin, spanMax);
            HintDistance = hintDistance;
            IsAmbiguous = isAmbiguous;
        }

        public Dir Direction { get; }
        public Vector3 CenterLocal { get; }
        public float SpanMin { get; }
        public float SpanMax { get; }
        public float Width => SpanMax - SpanMin;
        public float HintDistance { get; }
        public bool IsAmbiguous { get; }

        public EdgeOpening WithHintDistance(float hintDistance) =>
            new EdgeOpening(Direction, CenterLocal, SpanMin, SpanMax, hintDistance, IsAmbiguous);

        public EdgeOpening WithAmbiguous(bool isAmbiguous) =>
            new EdgeOpening(Direction, CenterLocal, SpanMin, SpanMax, HintDistance, isAmbiguous);

        public bool SupportsLocalPoint(Vector3 localPoint, float tolerance)
        {
            float edgeDelta = Mathf.Abs(GetEdgeCoordinate(localPoint, Direction) - GetEdgeCoordinate(CenterLocal, Direction));
            if (edgeDelta > tolerance)
                return false;

            float lateral = GetLateralCoordinate(localPoint, Direction);
            return lateral >= SpanMin - tolerance && lateral <= SpanMax + tolerance;
        }

        public override string ToString() =>
            $"{Direction} Center:{CenterLocal} Span:[{SpanMin:F2}, {SpanMax:F2}] HintΔ:{HintDistance:F2} Ambiguous:{IsAmbiguous}";
    }

    internal readonly struct WorldRect
    {
        public WorldRect(Vector2 min, Vector2 max)
        {
            Min = min;
            Max = max;
        }

        public Vector2 Min { get; }
        public Vector2 Max { get; }
        public Vector2 Size => Max - Min;
        public Vector2 Center => (Min + Max) * 0.5f;

        public bool Overlaps(WorldRect other, float minimumPenetration)
        {
            float xOverlap = Mathf.Min(Max.x, other.Max.x) - Mathf.Max(Min.x, other.Min.x);
            float zOverlap = Mathf.Min(Max.y, other.Max.y) - Mathf.Max(Min.y, other.Min.y);
            return xOverlap > minimumPenetration && zOverlap > minimumPenetration;
        }

        public override string ToString() => $"Min:{Min} Max:{Max}";
    }

    internal sealed class PrefabLayoutInfo
    {
        readonly Dictionary<Dir, Vector3> socketHints;
        readonly Dictionary<Dir, Vector3> resolvedDoorCenters;
        readonly Dictionary<Dir, EdgeOpening> resolvedEdgeOpenings;
        readonly HashSet<Dir> resolvedOpenDoorDirs;

        public PrefabLayoutInfo(
            GameObject prefab,
            Bounds localBounds,
            Bounds localWalkableBounds,
            Dictionary<Dir, Vector3> socketHints,
            Dictionary<Dir, Vector3> resolvedDoorCenters,
            Dictionary<Dir, EdgeOpening> resolvedEdgeOpenings,
            HashSet<Dir> resolvedOpenDoorDirs,
            Dir straightEntryDir,
            Dir straightExitDir,
            bool isCorridor)
        {
            Prefab = prefab;
            LocalBounds = localBounds;
            LocalWalkableBounds = localWalkableBounds;
            this.socketHints = socketHints;
            this.resolvedDoorCenters = resolvedDoorCenters;
            this.resolvedEdgeOpenings = resolvedEdgeOpenings;
            this.resolvedOpenDoorDirs = resolvedOpenDoorDirs;
            StraightEntryDir = straightEntryDir;
            StraightExitDir = straightExitDir;
            IsCorridor = isCorridor;
        }

        public GameObject Prefab { get; }
        public Bounds LocalBounds { get; }
        public Bounds LocalWalkableBounds { get; }
        public bool IsCorridor { get; }
        public IReadOnlyDictionary<Dir, Vector3> SocketHints => socketHints;
        public IReadOnlyDictionary<Dir, Vector3> ResolvedDoorCenters => resolvedDoorCenters;
        public IReadOnlyDictionary<Dir, EdgeOpening> ResolvedEdgeOpenings => resolvedEdgeOpenings;
        public IReadOnlyCollection<Dir> ResolvedOpenDoorDirs => resolvedOpenDoorDirs;
        public IReadOnlyDictionary<Dir, Vector3> LocalSockets => socketHints;
        public IReadOnlyDictionary<Dir, Vector3> LocalDoorCenters => resolvedDoorCenters;
        public IReadOnlyCollection<Dir> OpenDoorDirs => resolvedOpenDoorDirs;
        public Vector3 LocalWalkableCenter => LocalWalkableBounds.center;
        public Dir StraightEntryDir { get; }
        public Dir StraightExitDir { get; }
        public float StraightLength => Vector3.Distance(StraightEntryLocal, StraightExitLocal);
        public Vector3 StraightEntryLocal => GetDoorCenter(StraightEntryDir);
        public Vector3 StraightExitLocal => GetDoorCenter(StraightExitDir);

        public bool HasOpenDoor(Dir dir) => resolvedOpenDoorDirs.Contains(dir);

        public Vector3 GetDoorCenter(Dir dir)
        {
            if (resolvedDoorCenters.TryGetValue(dir, out Vector3 center))
                return center;

            throw new InvalidOperationException($"{Prefab.name} is missing local door center {dir}");
        }

        public bool TryGetEdgeOpening(Dir dir, out EdgeOpening opening) =>
            resolvedEdgeOpenings.TryGetValue(dir, out opening);
    }

    internal sealed class PlacedModule
    {
        readonly Dictionary<Dir, Vector3> worldSocketHints = new Dictionary<Dir, Vector3>();
        readonly Dictionary<Dir, Vector3> worldDoorCenters = new Dictionary<Dir, Vector3>();
        readonly HashSet<Dir> openDoorDirsWorld = new HashSet<Dir>();

        public PlacedModule(
            string name,
            GameObject instance,
            PrefabLayoutInfo layout,
            IEnumerable<Dir> openings,
            bool isBridge)
        {
            Name = name;
            Instance = instance;
            Layout = layout;
            IsBridge = isBridge;
            Openings = new HashSet<Dir>(openings);

            foreach (var socket in layout.SocketHints)
            {
                Dir worldDir = RotateDir(socket.Key, instance.transform.rotation);
                worldSocketHints[worldDir] = TransformPoint(instance.transform, socket.Value);
            }

            foreach (var door in layout.ResolvedDoorCenters)
            {
                Dir worldDir = RotateDir(door.Key, instance.transform.rotation);
                worldDoorCenters[worldDir] = TransformPoint(instance.transform, door.Value);
            }

            foreach (var localDir in layout.ResolvedOpenDoorDirs)
                openDoorDirsWorld.Add(RotateDir(localDir, instance.transform.rotation));

            WorldRect = ComputeWorldRect(layout.LocalBounds, instance.transform);
            WalkableRect = ComputeWorldRect(layout.LocalWalkableBounds, instance.transform);
            WalkableCenterWorld = TransformPoint(instance.transform, layout.LocalWalkableCenter);
        }

        public string Name { get; }
        public GameObject Instance { get; }
        public PrefabLayoutInfo Layout { get; }
        public bool IsBridge { get; }
        public HashSet<Dir> Openings { get; }
        public IReadOnlyDictionary<Dir, Vector3> WorldSocketHints => worldSocketHints;
        public IReadOnlyDictionary<Dir, Vector3> WorldSockets => worldSocketHints;
        public IReadOnlyDictionary<Dir, Vector3> WorldDoorCenters => worldDoorCenters;
        public IReadOnlyCollection<Dir> OpenDoorDirsWorld => openDoorDirsWorld;
        public WorldRect WorldRect { get; }
        public WorldRect WalkableRect { get; }
        public Vector3 WalkableCenterWorld { get; }

        public bool HasOpenDoor(Dir dir) => openDoorDirsWorld.Contains(dir);

        public bool SupportsDoorCenter(Dir worldDir, Vector3 worldPoint, float tolerance, out string failureReason)
        {
            failureReason = null;
            Dir localDir = InverseRotateDir(worldDir, Instance.transform.rotation);
            if (!Layout.TryGetEdgeOpening(localDir, out EdgeOpening opening))
            {
                failureReason = $"{Name} has no resolved opening for {worldDir}";
                return false;
            }

            if (opening.IsAmbiguous)
            {
                failureReason = $"{Name} opening {worldDir} is ambiguous";
                return false;
            }

            Vector3 localPoint = Quaternion.Inverse(Instance.transform.rotation) * (worldPoint - Instance.transform.position);
            if (opening.SupportsLocalPoint(localPoint, tolerance))
                return true;

            failureReason = $"{Name} connection point for {worldDir} falls outside resolved opening span";
            return false;
        }
    }

    internal sealed class PlacedConnection
    {
        public PlacedConnection(
            string name,
            GameObject instance,
            PrefabLayoutInfo layout,
            PlacedModule from,
            PlacedModule to,
            Dir direction,
            Vector3 fromSocket,
            Vector3 toSocket,
            Vector3 actualEntry,
            Vector3 actualExit)
        {
            Name = name;
            Instance = instance;
            Layout = layout;
            From = from;
            To = to;
            Direction = direction;
            FromSocket = fromSocket;
            ToSocket = toSocket;
            ActualEntry = actualEntry;
            ActualExit = actualExit;
            WorldRect = ComputeWorldRect(layout.LocalBounds, instance.transform);
            WalkableRect = ComputeWorldRect(layout.LocalWalkableBounds, instance.transform);
        }

        public string Name { get; }
        public GameObject Instance { get; }
        public PrefabLayoutInfo Layout { get; }
        public PlacedModule From { get; }
        public PlacedModule To { get; }
        public Dir Direction { get; }
        public Vector3 FromSocket { get; }
        public Vector3 ToSocket { get; }
        public Vector3 ActualEntry { get; }
        public Vector3 ActualExit { get; }
        public WorldRect WorldRect { get; }
        public WorldRect WalkableRect { get; }
    }

    [Header("Modules")]
    [SerializeField] GameObject prefabBridge;
    [SerializeField] ModuleSet setPower;
    [SerializeField] ModuleSet setComms;
    [SerializeField] ModuleSet setGravity;
    [SerializeField] ModuleSet setHull;
    [SerializeField] ModuleSet setCrewBunks;
    [SerializeField] ModuleSet setGunStation;
    [SerializeField] ModuleSet setStorage;

    [Header("Corridors (long axis = local Z)")]
    [SerializeField] GameObject[] prefabsCorridorStraight;
    [SerializeField] GameObject prefabCorridorTurn;
    [SerializeField] GameObject prefabCorridorT;

    [Header("Settings")]
    [SerializeField] int seed = 0;
    [SerializeField] Transform shipInteriorParent;
    [SerializeField] bool logValidation = true;
    [SerializeField, Min(1)] int maxGenerationAttempts = 8;
    [SerializeField] float overlapTolerance = 0.05f;
    [SerializeField] float passageHalfWidth = 0.75f;

    public static bool IsReady { get; private set; }
    public static int CurrentSeed { get; private set; }

    static readonly Dir[] AllDirs = { Dir.N, Dir.S, Dir.E, Dir.W };
    const float DoorwayBlockerMinHeight = 0.65f;
    const float DoorwayProbeHeight = 1.1f;
    const float DoorwayProbeDepth = 0.2f;
    const float DoorwaySampleStep = 0.05f;
    const float DoorwayHintWarningDistance = 0.35f;
    const float DoorwayAmbiguityDistance = 0.1f;

    System.Random rng;
    Transform root;
    GameObject bridgeInstance;
    readonly Dictionary<RepairStation.StationType, GameObject> repairInstances = new Dictionary<RepairStation.StationType, GameObject>();
    readonly List<GameObject> allPlaced = new List<GameObject>();
    readonly List<PlacedModule> placedModules = new List<PlacedModule>();
    readonly List<PlacedConnection> placedConnections = new List<PlacedConnection>();
    readonly Dictionary<GameObject, PrefabLayoutInfo> layoutCache = new Dictionary<GameObject, PrefabLayoutInfo>();

    internal IReadOnlyList<PlacedModule> DebugPlacedModules => placedModules;
    internal IReadOnlyList<PlacedConnection> DebugPlacedConnections => placedConnections;

    void Awake()
    {
        IsReady = false;
        EnsureRoot();

        var assembler = GetComponent<ShipAssembler>();
        if (assembler != null) assembler.enabled = false;

        if (prefabBridge == null)
        {
            Debug.LogError("[ShipLayout] prefabBridge not assigned!");
            IsReady = true;
            return;
        }

        int baseSeed = seed == 0 ? UnityEngine.Random.Range(1, 99999) : seed;

        try
        {
            if (!BuildShip(baseSeed, out int successfulSeed, out string failureReason))
            {
                CurrentSeed = baseSeed;
                Debug.LogError($"[ShipLayout] FAILED after {maxGenerationAttempts} attempt(s): {failureReason}");
            }
            else
            {
                CurrentSeed = successfulSeed;
                WireRepairStations();
                WireSpawnPoints();

                if (logValidation)
                    Debug.Log($"[ShipLayout] Done | Seed={CurrentSeed} | Objects={allPlaced.Count}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ShipLayout] FAILED: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            IsReady = true;
            Debug.Log("[ShipLayout] IsReady = true");
        }
    }

    internal bool GenerateLayoutForTesting(int baseSeed, out string failureReason)
    {
        EnsureRoot();
        bool success = BuildShip(baseSeed, out int successfulSeed, out failureReason);
        if (success) CurrentSeed = successfulSeed;
        return success;
    }

    internal bool ValidateGeneratedLayout(out string failureReason)
    {
        Physics.SyncTransforms();
        return ValidateLayout(out failureReason);
    }

    bool BuildShip(int baseSeed, out int successfulSeed, out string failureReason)
    {
        successfulSeed = baseSeed;
        failureReason = "unknown";

        for (int attempt = 0; attempt < Mathf.Max(1, maxGenerationAttempts); attempt++)
        {
            int attemptSeed = baseSeed + attempt;
            rng = new System.Random(attemptSeed);

            ResetGeneratedState();

            if (TryBuildShipAttempt(out failureReason))
            {
                Physics.SyncTransforms();
                successfulSeed = attemptSeed;
                return true;
            }

            Log($"Attempt {attempt + 1}/{maxGenerationAttempts} failed | Seed={attemptSeed} | {failureReason}");
        }

        return false;
    }

    bool TryBuildShipAttempt(out string failureReason)
    {
        failureReason = null;

        PrefabLayoutInfo bridgeInfo;
        try
        {
            bridgeInfo = GetPrefabLayout(prefabBridge, false);
        }
        catch (Exception e)
        {
            failureReason = $"bridge analysis failed: {e.Message}";
            return false;
        }

        var repairDefs = new[]
        {
            (set: setPower,   type: RepairStation.StationType.Energy),
            (set: setComms,   type: RepairStation.StationType.Communications),
            (set: setGravity, type: RepairStation.StationType.Gravity),
            (set: setHull,    type: RepairStation.StationType.Hull),
        };

        var dirs = new List<Dir>(AllDirs);
        Shuffle(dirs);

        var dirToRepair = new Dictionary<Dir, (ModuleSet set, RepairStation.StationType type)>();
        for (int i = 0; i < repairDefs.Length; i++)
            dirToRepair[dirs[i]] = (repairDefs[i].set, repairDefs[i].type);

        var extraModuleSets = new List<ModuleSet>();
        if (setCrewBunks?.variantN != null) extraModuleSets.Add(setCrewBunks);
        if (setGunStation?.variantN != null) extraModuleSets.Add(setGunStation);
        if (setStorage?.variantN != null) extraModuleSets.Add(setStorage);

        var dirsWithExtra = new HashSet<Dir>();
        if (extraModuleSets.Count > 0)
        {
            int extraCount = Mathf.Min(2 + rng.Next(3), AllDirs.Length);
            var shuffledDirs = new List<Dir>(dirs);
            Shuffle(shuffledDirs);
            for (int i = 0; i < extraCount; i++)
                dirsWithExtra.Add(shuffledDirs[i]);
        }

        var repairOpenings = new Dictionary<Dir, HashSet<Dir>>();
        foreach (var dir in AllDirs)
        {
            var openings = new HashSet<Dir> { Opposite(dir) };
            if (dirsWithExtra.Contains(dir)) openings.Add(dir);
            repairOpenings[dir] = openings;
        }

        bridgeInstance = Instantiate(prefabBridge, Vector3.zero, Quaternion.identity, root);
        bridgeInstance.name = "Bridge";
        allPlaced.Add(bridgeInstance);

        var bridgePlaced = new PlacedModule("Bridge", bridgeInstance, bridgeInfo, AllDirs, true);
        placedModules.Add(bridgePlaced);

        Log("Bridge placed via prefab bounds/sockets");

        foreach (var dir in dirs)
        {
            var (set, stationType) = dirToRepair[dir];
            if (!TrySelectVariant(set, repairOpenings[dir], out GameObject repairPrefab, out float repairRotY, out string variantFailure))
            {
                failureReason = $"{stationType} variant selection failed: {variantFailure}";
                return false;
            }

            if (!TryPlaceConnectedModule(
                    parent: bridgePlaced,
                    outwardDir: dir,
                    modulePrefab: repairPrefab,
                    moduleName: $"Repair_{stationType}",
                    openings: repairOpenings[dir],
                    rotationY: repairRotY,
                    out PlacedModule repairPlaced,
                    out string placementFailure))
            {
                failureReason = $"{stationType} placement failed: {placementFailure}";
                return false;
            }

            repairInstances[stationType] = repairPlaced.Instance;

            if (!dirsWithExtra.Contains(dir))
                continue;

            ModuleSet extraSet = extraModuleSets[rng.Next(extraModuleSets.Count)];
            var extraOpenings = new HashSet<Dir> { Opposite(dir) };

            if (!TrySelectVariant(extraSet, extraOpenings, out GameObject extraPrefab, out float extraRotY, out variantFailure))
            {
                failureReason = $"extra room on {dir} variant selection failed: {variantFailure}";
                return false;
            }

            if (!TryPlaceConnectedModule(
                    parent: repairPlaced,
                    outwardDir: dir,
                    modulePrefab: extraPrefab,
                    moduleName: $"Extra_{dir}",
                    openings: extraOpenings,
                    rotationY: extraRotY,
                    out _,
                    out placementFailure))
            {
                failureReason = $"extra room on {dir} placement failed: {placementFailure}";
                return false;
            }
        }

        Physics.SyncTransforms();
        return ValidateLayout(out failureReason);
    }

    bool TryPlaceConnectedModule(
        PlacedModule parent,
        Dir outwardDir,
        GameObject modulePrefab,
        string moduleName,
        HashSet<Dir> openings,
        float rotationY,
        out PlacedModule placedModule,
        out string failureReason)
    {
        placedModule = null;
        failureReason = null;

        if (!TryPickCorridor(out GameObject corridorPrefab, out PrefabLayoutInfo corridorInfo, out failureReason))
            return false;

        PrefabLayoutInfo moduleInfo;
        try
        {
            moduleInfo = GetPrefabLayout(modulePrefab, false);
        }
        catch (Exception e)
        {
            failureReason = e.Message;
            return false;
        }

        Quaternion moduleRotation = Quaternion.Euler(0f, rotationY, 0f);
        Dir inboundDir = Opposite(outwardDir);
        Dir localInboundDir = InverseRotateDir(inboundDir, moduleRotation);
        if (!moduleInfo.HasOpenDoor(localInboundDir))
        {
            failureReason = $"{modulePrefab.name} local door {localInboundDir} is closed after selecting {moduleName}";
            return false;
        }

        Vector3 inboundDoorLocal = moduleInfo.GetDoorCenter(localInboundDir);

        if (!parent.HasOpenDoor(outwardDir))
        {
            failureReason = $"{parent.Name} world door {DirToSocketName(outwardDir)} is closed";
            return false;
        }

        if (!parent.WorldDoorCenters.TryGetValue(outwardDir, out Vector3 parentSocketWorld))
        {
            failureReason = $"{parent.Name} is missing world door center {DirToSocketName(outwardDir)}";
            return false;
        }

        if (!TryResolveStraightCorridor(corridorInfo, outwardDir, out Dir corridorEntryDir, out Dir corridorExitDir, out Quaternion corridorRotation))
        {
            failureReason = $"corridor {corridorPrefab.name} has no valid straight door pair for {outwardDir}";
            return false;
        }

        float corridorLength = corridorInfo.StraightLength;
        Vector3 desiredDoorWorld = parentSocketWorld + DirToVec(outwardDir) * corridorLength;
        Vector3 moduleWorldPosition = desiredDoorWorld - moduleRotation * inboundDoorLocal;

        GameObject moduleInstance = Instantiate(modulePrefab, moduleWorldPosition, moduleRotation, root);
        moduleInstance.name = moduleName;

        var candidate = new PlacedModule(moduleName, moduleInstance, moduleInfo, openings, false);

        if (!candidate.WorldDoorCenters.TryGetValue(inboundDir, out Vector3 actualInboundDoor))
        {
            DestroyImmediate(moduleInstance);
            failureReason = $"{moduleName} lost inbound door {DirToSocketName(inboundDir)} after placement";
            return false;
        }

        if ((actualInboundDoor - desiredDoorWorld).sqrMagnitude > overlapTolerance * overlapTolerance)
        {
            DestroyImmediate(moduleInstance);
            failureReason = $"{moduleName} inbound doorway misaligned by {(actualInboundDoor - desiredDoorWorld).magnitude:F3}";
            return false;
        }

        Vector3 corridorWorldPosition = parentSocketWorld - corridorRotation * corridorInfo.GetDoorCenter(corridorEntryDir);

        GameObject corridorInstance = Instantiate(corridorPrefab, corridorWorldPosition, corridorRotation, root);
        corridorInstance.name = $"Corridor_{placedConnections.Count}_{parent.Name}_{moduleName}";

        Vector3 actualEntry = TransformPoint(corridorInstance.transform, corridorInfo.GetDoorCenter(corridorEntryDir));
        Vector3 actualExit = TransformPoint(corridorInstance.transform, corridorInfo.GetDoorCenter(corridorExitDir));

        var connection = new PlacedConnection(
            corridorInstance.name,
            corridorInstance,
            corridorInfo,
            parent,
            candidate,
            outwardDir,
            parentSocketWorld,
            actualInboundDoor,
            actualEntry,
            actualExit);

        if (!ValidateConnectionGeometry(connection, out failureReason))
        {
            DestroyImmediate(corridorInstance);
            DestroyImmediate(moduleInstance);
            return false;
        }

        allPlaced.Add(moduleInstance);
        allPlaced.Add(corridorInstance);
        placedModules.Add(candidate);
        placedConnections.Add(connection);
        placedModule = candidate;

        Log($"{moduleName} connected {outwardDir} | CorridorLen={corridorLength:F2}");
        return true;
    }

    bool ValidateLayout(out string failureReason)
    {
        if (!ValidateNoOverlaps(out failureReason)) return false;
        if (!ValidateDoorOpenings(out failureReason)) return false;
        if (!ValidateSocketAlignment(out failureReason)) return false;
        if (!ValidateConnectivity(out failureReason)) return false;
        if (!ValidatePassages(out failureReason)) return false;

        failureReason = null;
        return true;
    }

    bool ValidateDoorOpenings(out string failureReason)
    {
        foreach (var connection in placedConnections)
        {
            if (!connection.From.HasOpenDoor(connection.Direction))
            {
                failureReason = $"blocked doorway | {connection.From.Name} {connection.Direction} is not an open doorway";
                return false;
            }

            Dir inbound = Opposite(connection.Direction);
            if (!connection.To.HasOpenDoor(inbound))
            {
                failureReason = $"blocked doorway | {connection.To.Name} {inbound} is not an open doorway";
                return false;
            }

            if (!connection.From.SupportsDoorCenter(connection.Direction, connection.FromSocket, overlapTolerance, out failureReason))
            {
                failureReason = $"blocked doorway | {failureReason}";
                return false;
            }

            if (!connection.To.SupportsDoorCenter(inbound, connection.ToSocket, overlapTolerance, out failureReason))
            {
                failureReason = $"blocked doorway | {failureReason}";
                return false;
            }
        }

        failureReason = null;
        return true;
    }

    bool ValidateNoOverlaps(out string failureReason)
    {
        var items = new List<(string name, GameObject go, WorldRect rect)>();
        items.AddRange(placedModules.Select(m => (m.Name, m.Instance, m.WalkableRect)));
        items.AddRange(placedConnections.Select(c => (c.Name, c.Instance, c.WalkableRect)));

        var allowedPairs = new HashSet<(int a, int b)>();
        foreach (var connection in placedConnections)
        {
            allowedPairs.Add(GetPairKey(connection.Instance.GetInstanceID(), connection.From.Instance.GetInstanceID()));
            allowedPairs.Add(GetPairKey(connection.Instance.GetInstanceID(), connection.To.Instance.GetInstanceID()));
        }

        for (int i = 0; i < items.Count; i++)
        {
            for (int j = i + 1; j < items.Count; j++)
            {
                if (allowedPairs.Contains(GetPairKey(items[i].go.GetInstanceID(), items[j].go.GetInstanceID())))
                    continue;

                if (!items[i].rect.Overlaps(items[j].rect, overlapTolerance))
                    continue;

                failureReason = $"overlap | {items[i].name} {items[i].rect} vs {items[j].name} {items[j].rect}";
                return false;
            }
        }

        failureReason = null;
        return true;
    }

    bool ValidateSocketAlignment(out string failureReason)
    {
        foreach (var connection in placedConnections)
        {
            if ((connection.ActualEntry - connection.FromSocket).sqrMagnitude > overlapTolerance * overlapTolerance)
            {
                failureReason = $"gap | {connection.Name} entry offset {(connection.ActualEntry - connection.FromSocket).magnitude:F3}";
                return false;
            }

            if ((connection.ActualExit - connection.ToSocket).sqrMagnitude > overlapTolerance * overlapTolerance)
            {
                failureReason = $"gap | {connection.Name} exit offset {(connection.ActualExit - connection.ToSocket).magnitude:F3}";
                return false;
            }

            Vector3 delta = connection.ToSocket - connection.FromSocket;
            Vector3 axis = DirToVec(connection.Direction);
            float axialDistance = Vector3.Dot(delta, axis);
            Vector3 lateralDelta = delta - axis * axialDistance;

            if (lateralDelta.sqrMagnitude > overlapTolerance * overlapTolerance)
            {
                failureReason = $"gap | {connection.Name} lateral offset {lateralDelta.magnitude:F3}";
                return false;
            }

            if (Mathf.Abs(axialDistance - connection.Layout.StraightLength) > overlapTolerance)
            {
                failureReason = $"gap | {connection.Name} length {axialDistance:F3} expected {connection.Layout.StraightLength:F3}";
                return false;
            }
        }

        failureReason = null;
        return true;
    }

    bool ValidateConnectivity(out string failureReason)
    {
        var bridge = placedModules.FirstOrDefault(m => m.IsBridge);
        if (bridge == null)
        {
            failureReason = "unreachable module | bridge missing";
            return false;
        }

        var adjacency = new Dictionary<PlacedModule, List<PlacedModule>>();
        foreach (var module in placedModules)
            adjacency[module] = new List<PlacedModule>();

        foreach (var connection in placedConnections)
        {
            adjacency[connection.From].Add(connection.To);
            adjacency[connection.To].Add(connection.From);
        }

        var visited = new HashSet<PlacedModule> { bridge };
        var queue = new Queue<PlacedModule>();
        queue.Enqueue(bridge);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in adjacency[current])
            {
                if (!visited.Add(next))
                    continue;

                queue.Enqueue(next);
            }
        }

        if (visited.Count != placedModules.Count)
        {
            var unreachable = placedModules
                .Where(m => !visited.Contains(m))
                .Select(m => m.Name)
                .ToArray();

            failureReason = $"unreachable module | {string.Join(", ", unreachable)}";
            return false;
        }

        failureReason = null;
        return true;
    }

    bool ValidatePassages(out string failureReason)
    {
        const float passageCenterHeight = 1f;
        const float passageHalfHeight = 1f;

        foreach (var connection in placedConnections)
        {
            Vector3 center = (connection.FromSocket + connection.ToSocket) * 0.5f + Vector3.up * passageCenterHeight;
            float distance = Vector3.Distance(connection.FromSocket, connection.ToSocket);
            float halfLength = Mathf.Max(0.1f, distance * 0.5f - overlapTolerance);

            Vector3 halfExtents = (connection.Direction == Dir.E || connection.Direction == Dir.W)
                ? new Vector3(halfLength, passageHalfHeight, passageHalfWidth)
                : new Vector3(passageHalfWidth, passageHalfHeight, halfLength);

            var colliders = Physics.OverlapBox(center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in colliders)
            {
                if (hit == null || !hit.enabled || hit.isTrigger)
                    continue;

                if (!hit.transform.IsChildOf(root))
                    continue;

                if (hit.transform.IsChildOf(connection.Instance.transform))
                    continue;

                if (hit.transform.IsChildOf(connection.From.Instance.transform))
                    continue;

                if (hit.transform.IsChildOf(connection.To.Instance.transform))
                    continue;

                if (hit.bounds.max.y <= 0.5f)
                    continue;

                failureReason = $"blocked doorway | {connection.Name} hit {hit.name}";
                return false;
            }
        }

        failureReason = null;
        return true;
    }

    bool ValidateConnectionGeometry(PlacedConnection connection, out string failureReason)
    {
        if ((connection.ActualEntry - connection.FromSocket).sqrMagnitude > overlapTolerance * overlapTolerance)
        {
            failureReason = $"{connection.Name} entry misaligned";
            return false;
        }

        if ((connection.ActualExit - connection.ToSocket).sqrMagnitude > overlapTolerance * overlapTolerance)
        {
            failureReason = $"{connection.Name} exit misaligned";
            return false;
        }

        failureReason = null;
        return true;
    }

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
                RepairStation.StationType.Energy         => "Battery_medium",
                RepairStation.StationType.Communications => "console",
                RepairStation.StationType.Gravity        => "projector",
                RepairStation.StationType.Hull           => "generator",
                _ => null,
            };

            Transform anchor = anchorName != null
                ? moduleInst.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name.Equals(anchorName, StringComparison.OrdinalIgnoreCase))
                : null;

            Vector3 pos = anchor != null
                ? anchor.position + Vector3.up * 0.5f
                : moduleInst.transform.position + Vector3.up;

            station.transform.position = pos;
            station.SetGeneratedLocationLabel(moduleInst.name);
            Log($"RepairStation {station.Type} → {moduleInst.name}" +
                (anchor != null ? $" (anchor: {anchorName})" : " (fallback center)"));
        }
    }

    void WireSpawnPoints()
    {
        if (bridgeInstance == null) return;

        PlacedModule bridgePlaced = placedModules.FirstOrDefault(module => module.IsBridge);
        if (bridgePlaced == null)
        {
            Log("Bridge placed module metadata missing for spawn wiring");
            return;
        }

        Transform spawnParent = bridgeInstance.transform.Find("SpawnPoints");
        var runtimeParent = new GameObject("RuntimeSpawnPoints").transform;
        runtimeParent.SetParent(bridgeInstance.transform, false);

        var points = new List<Transform>();
        if (spawnParent != null)
        {
            foreach (Transform child in spawnParent)
            {
                Transform safeAnchor = CreateSafeSpawnAnchor(runtimeParent, child.name, child.position, bridgePlaced);
                points.Add(safeAnchor);
            }
        }

        if (points.Count == 0)
        {
            Log("No 'SpawnPoints' child found in Bridge prefab, generating fallback bridge spawns");
            foreach (Vector3 fallback in BuildFallbackBridgeSpawnPositions(bridgePlaced))
            {
                Transform safeAnchor = CreateSafeSpawnAnchor(runtimeParent, $"FallbackSpawn_{points.Count + 1}", fallback, bridgePlaced);
                points.Add(safeAnchor);
            }
        }

        if (points.Count > 0 && PlayerManager.Instance != null)
        {
            PlayerManager.Instance.SetSpawnPoints(points.ToArray());
            Log($"Wired {points.Count} spawn points from Bridge");
        }
    }

    Transform CreateSafeSpawnAnchor(Transform parent, string name, Vector3 desiredWorldPosition, PlacedModule bridgePlaced)
    {
        var anchor = new GameObject(name).transform;
        anchor.SetParent(parent, false);
        anchor.position = ResolveSupportedBridgeSpawn(desiredWorldPosition, bridgePlaced);
        return anchor;
    }

    IEnumerable<Vector3> BuildFallbackBridgeSpawnPositions(PlacedModule bridgePlaced)
    {
        Vector2 min = bridgePlaced.WalkableRect.Min + Vector2.one * 1.25f;
        Vector2 max = bridgePlaced.WalkableRect.Max - Vector2.one * 1.25f;
        Vector2 center = bridgePlaced.WalkableRect.Center;

        yield return new Vector3(Mathf.Lerp(min.x, center.x, 0.5f), bridgeInstance.transform.position.y + 1f, Mathf.Lerp(min.y, center.y, 0.5f));
        yield return new Vector3(Mathf.Lerp(max.x, center.x, 0.5f), bridgeInstance.transform.position.y + 1f, Mathf.Lerp(min.y, center.y, 0.5f));
        yield return new Vector3(Mathf.Lerp(min.x, center.x, 0.5f), bridgeInstance.transform.position.y + 1f, Mathf.Lerp(max.y, center.y, 0.5f));
        yield return new Vector3(Mathf.Lerp(max.x, center.x, 0.5f), bridgeInstance.transform.position.y + 1f, Mathf.Lerp(max.y, center.y, 0.5f));
    }

    Vector3 ResolveSupportedBridgeSpawn(Vector3 desiredWorldPosition, PlacedModule bridgePlaced)
    {
        if (TryRaycastBridgeFloor(desiredWorldPosition, 1.5f, out RaycastHit hit))
            return hit.point + Vector3.up * 0.15f;

        Vector2 clampedXZ = new Vector2(
            Mathf.Clamp(desiredWorldPosition.x, bridgePlaced.WalkableRect.Min.x + 0.75f, bridgePlaced.WalkableRect.Max.x - 0.75f),
            Mathf.Clamp(desiredWorldPosition.z, bridgePlaced.WalkableRect.Min.y + 0.75f, bridgePlaced.WalkableRect.Max.y - 0.75f));

        if (TryRaycastBridgeFloor(new Vector3(clampedXZ.x, desiredWorldPosition.y, clampedXZ.y), 2f, out hit))
            return hit.point + Vector3.up * 0.15f;

        return new Vector3(clampedXZ.x, desiredWorldPosition.y + 0.15f, clampedXZ.y);
    }

    bool TryRaycastBridgeFloor(Vector3 requestedWorldPosition, float rayHeight, out RaycastHit bestHit)
    {
        bestHit = default;
        float bestDistance = float.PositiveInfinity;
        bool found = false;

        float maxAcceptedY = requestedWorldPosition.y + 0.75f;
        Ray ray = new Ray(requestedWorldPosition + Vector3.up * rayHeight, Vector3.down);
        foreach (var collider in bridgeInstance.GetComponentsInChildren<Collider>(true))
        {
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;

            if (!collider.Raycast(ray, out RaycastHit hit, 10f))
                continue;

            if (hit.normal.y < 0.5f || hit.point.y > maxAcceptedY)
                continue;

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        return found;
    }

    void ResetGeneratedState()
    {
        EnsureRoot();

        for (int i = root.childCount - 1; i >= 0; i--)
            DestroyImmediate(root.GetChild(i).gameObject);

        bridgeInstance = null;
        repairInstances.Clear();
        allPlaced.Clear();
        placedModules.Clear();
        placedConnections.Clear();
    }

    Transform EnsureRoot()
    {
        if (shipInteriorParent == null)
            shipInteriorParent = new GameObject("ShipInterior").transform;

        root = shipInteriorParent;
        return root;
    }

    bool TryPickCorridor(out GameObject prefab, out PrefabLayoutInfo info, out string failureReason)
    {
        prefab = null;
        info = null;
        failureReason = null;

        if (prefabsCorridorStraight == null || prefabsCorridorStraight.Length == 0)
        {
            failureReason = "no straight corridor prefabs assigned";
            return false;
        }

        var options = prefabsCorridorStraight.Where(p => p != null).ToArray();
        if (options.Length == 0)
        {
            failureReason = "straight corridor array only contains null prefabs";
            return false;
        }

        prefab = options[rng.Next(options.Length)];

        try
        {
            info = GetPrefabLayout(prefab, true);
        }
        catch (Exception e)
        {
            failureReason = $"corridor analysis failed: {e.Message}";
            return false;
        }

        if (info.StraightLength <= overlapTolerance)
        {
            failureReason = $"corridor {prefab.name} has invalid straight length {info.StraightLength:F3}";
            return false;
        }

        return true;
    }

    PrefabLayoutInfo GetPrefabLayout(GameObject prefab, bool treatAsCorridor)
    {
        if (layoutCache.TryGetValue(prefab, out PrefabLayoutInfo cached))
            return cached;

        var layout = AnalyzePrefabLayout(prefab, treatAsCorridor);
        layoutCache[prefab] = layout;
        return layout;
    }

    internal static PrefabLayoutInfo AnalyzePrefabLayout(GameObject prefab, bool treatAsCorridor)
    {
        if (prefab == null)
            throw new ArgumentNullException(nameof(prefab));

        Transform root = prefab.transform;

        if (!TryCollectLocalBounds(root, walkableOnly: false, out Bounds localBounds))
            throw new InvalidOperationException($"{prefab.name} has no colliders or render meshes to analyze");

        Bounds walkableBounds = localBounds;
        if (TryCollectLocalBounds(root, walkableOnly: true, out Bounds candidateWalkable))
            walkableBounds = candidateWalkable;

        List<Bounds> blockingBounds = CollectBlockingLocalBounds(root);

        var socketHints = new Dictionary<Dir, Vector3>();
        foreach (var dir in AllDirs)
        {
            string socketName = DirToSocketName(dir);
            Transform socket = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name.Equals(socketName, StringComparison.OrdinalIgnoreCase));

            if (socket != null)
                socketHints[dir] = root.InverseTransformPoint(socket.position);
        }

        if (!treatAsCorridor && socketHints.Count == 0)
            throw new InvalidOperationException($"{prefab.name} has no sockets");

        Dictionary<Dir, EdgeOpening> resolvedEdgeOpenings = ResolveEdgeOpenings(prefab, walkableBounds, socketHints, blockingBounds, treatAsCorridor);
        Dictionary<Dir, Vector3> resolvedDoorCenters = resolvedEdgeOpenings
            .Where(pair => !pair.Value.IsAmbiguous)
            .ToDictionary(pair => pair.Key, pair => pair.Value.CenterLocal);
        HashSet<Dir> resolvedOpenDoorDirs = new HashSet<Dir>(resolvedDoorCenters.Keys);

        if (!TryResolveStraightDoorPair(resolvedEdgeOpenings, resolvedOpenDoorDirs, out Dir straightEntryDir, out Dir straightExitDir))
        {
            if (treatAsCorridor)
                throw new InvalidOperationException($"{prefab.name} has no unambiguous straight doorway pair");

            straightEntryDir = default;
            straightExitDir = default;
        }

        return new PrefabLayoutInfo(
            prefab,
            localBounds,
            walkableBounds,
            socketHints,
            resolvedDoorCenters,
            resolvedEdgeOpenings,
            resolvedOpenDoorDirs,
            straightEntryDir,
            straightExitDir,
            treatAsCorridor);
    }

    static Dictionary<Dir, EdgeOpening> ResolveEdgeOpenings(
        GameObject prefab,
        Bounds walkableBounds,
        IReadOnlyDictionary<Dir, Vector3> socketHints,
        IReadOnlyList<Bounds> blockingBounds,
        bool treatAsCorridor)
    {
        var resolved = new Dictionary<Dir, EdgeOpening>();
        foreach (var dir in AllDirs)
        {
            List<EdgeOpening> candidates = DetectEdgeOpeningCandidates(walkableBounds, blockingBounds, dir, treatAsCorridor);
            if (!TryResolveEdgeOpening(candidates, socketHints, dir, out EdgeOpening opening))
                continue;

            resolved[dir] = opening;

            if (!opening.IsAmbiguous &&
                socketHints.TryGetValue(dir, out Vector3 hint) &&
                Mathf.Abs(GetLateralCoordinate(hint, dir) - GetLateralCoordinate(opening.CenterLocal, dir)) > DoorwayHintWarningDistance)
            {
                Debug.LogWarning($"[ShipLayout] {prefab.name} {dir} socket hint is offset from resolved doorway center by {opening.HintDistance:F2}");
            }
        }

        return resolved;
    }

    static List<EdgeOpening> DetectEdgeOpeningCandidates(
        Bounds walkableBounds,
        IReadOnlyList<Bounds> blockingBounds,
        Dir dir,
        bool treatAsCorridor)
    {
        var openings = new List<EdgeOpening>();

        float lateralMin = GetLateralMin(walkableBounds, dir);
        float lateralMax = GetLateralMax(walkableBounds, dir);
        float span = lateralMax - lateralMin;
        if (span <= 0.01f)
            return openings;

        int sampleCount = Mathf.Max(2, Mathf.CeilToInt(span / DoorwaySampleStep) + 1);
        float sampleSpacing = span / (sampleCount - 1);
        int runStart = -1;

        for (int i = 0; i < sampleCount; i++)
        {
            float lateral = lateralMin + sampleSpacing * i;
            Vector3 probeDoorCenter = CreateEdgeDoorCenter(walkableBounds, dir, lateral);
            bool isOpen = IsDoorwayOpenLocal(walkableBounds, blockingBounds, dir, probeDoorCenter, treatAsCorridor);

            if (isOpen)
            {
                if (runStart < 0)
                    runStart = i;
            }

            bool isRunEnd = runStart >= 0 && (!isOpen || i == sampleCount - 1);
            if (!isRunEnd)
                continue;

            int runEnd = isOpen && i == sampleCount - 1 ? i : i - 1;
            float spanMin = Mathf.Max(lateralMin, lateralMin + runStart * sampleSpacing - sampleSpacing * 0.5f);
            float spanMax = Mathf.Min(lateralMax, lateralMin + runEnd * sampleSpacing + sampleSpacing * 0.5f);

            if (spanMax - spanMin > sampleSpacing * 0.5f)
            {
                float centerLateral = (spanMin + spanMax) * 0.5f;
                openings.Add(new EdgeOpening(
                    dir,
                    CreateEdgeDoorCenter(walkableBounds, dir, centerLateral),
                    spanMin,
                    spanMax,
                    float.PositiveInfinity,
                    false));
            }

            runStart = -1;
        }

        return openings;
    }

    static bool TryResolveEdgeOpening(
        IReadOnlyList<EdgeOpening> candidates,
        IReadOnlyDictionary<Dir, Vector3> socketHints,
        Dir dir,
        out EdgeOpening resolvedOpening)
    {
        resolvedOpening = default;
        if (candidates == null || candidates.Count == 0)
            return false;

        if (socketHints.TryGetValue(dir, out Vector3 hint))
        {
            EdgeOpening[] ranked = candidates
                .Select(candidate =>
                    candidate.WithHintDistance(Mathf.Abs(GetLateralCoordinate(candidate.CenterLocal, dir) - GetLateralCoordinate(hint, dir))))
                .OrderBy(candidate => candidate.HintDistance)
                .ThenByDescending(candidate => candidate.Width)
                .ToArray();

            bool isAmbiguous = ranked.Length > 1 &&
                               Mathf.Abs(ranked[1].HintDistance - ranked[0].HintDistance) <= DoorwayAmbiguityDistance;
            resolvedOpening = ranked[0].WithAmbiguous(isAmbiguous);
            return true;
        }

        if (candidates.Count == 1)
        {
            resolvedOpening = candidates[0];
            return true;
        }

        resolvedOpening = candidates
            .OrderByDescending(candidate => candidate.Width)
            .ThenBy(candidate => GetLateralCoordinate(candidate.CenterLocal, dir))
            .First()
            .WithAmbiguous(true);
        return true;
    }

    internal static bool TryResolveStraightDoorPair(
        IReadOnlyDictionary<Dir, EdgeOpening> resolvedEdgeOpenings,
        IReadOnlyCollection<Dir> openDoorDirs,
        out Dir entryDir,
        out Dir exitDir)
    {
        entryDir = Dir.N;
        exitDir = Dir.S;

        bool hasNorth = resolvedEdgeOpenings.TryGetValue(Dir.N, out EdgeOpening north) && !north.IsAmbiguous && openDoorDirs.Contains(Dir.N);
        bool hasSouth = resolvedEdgeOpenings.TryGetValue(Dir.S, out EdgeOpening south) && !south.IsAmbiguous && openDoorDirs.Contains(Dir.S);
        bool hasEast = resolvedEdgeOpenings.TryGetValue(Dir.E, out EdgeOpening east) && !east.IsAmbiguous && openDoorDirs.Contains(Dir.E);
        bool hasWest = resolvedEdgeOpenings.TryGetValue(Dir.W, out EdgeOpening west) && !west.IsAmbiguous && openDoorDirs.Contains(Dir.W);

        if (hasNorth && hasSouth && openDoorDirs.Count == 2)
        {
            entryDir = Dir.N;
            exitDir = Dir.S;
            return true;
        }

        if (hasWest && hasEast && openDoorDirs.Count == 2)
        {
            entryDir = Dir.W;
            exitDir = Dir.E;
            return true;
        }

        return false;
    }

    static Vector3 CreateEdgeDoorCenter(Bounds walkableBounds, Dir dir) =>
        CreateEdgeDoorCenter(walkableBounds, dir, dir == Dir.N || dir == Dir.S ? walkableBounds.center.x : walkableBounds.center.z);

    static Vector3 CreateEdgeDoorCenter(Bounds walkableBounds, Dir dir, float lateralCoordinate)
    {
        Vector3 center = walkableBounds.center;
        center.y = 0f;

        switch (dir)
        {
            case Dir.N:
                center.x = lateralCoordinate;
                center.z = walkableBounds.min.z;
                break;
            case Dir.S:
                center.x = lateralCoordinate;
                center.z = walkableBounds.max.z;
                break;
            case Dir.E:
                center.z = lateralCoordinate;
                center.x = walkableBounds.max.x;
                break;
            case Dir.W:
                center.z = lateralCoordinate;
                center.x = walkableBounds.min.x;
                break;
        }

        return center;
    }

    static bool IsDoorwayOpenLocal(
        Bounds walkableBounds,
        IReadOnlyList<Bounds> blockingBounds,
        Dir dir,
        Vector3 doorCenter,
        bool treatAsCorridor)
    {
        float floorY = walkableBounds.min.y;
        float doorwayMidY = floorY + DoorwayProbeHeight;
        Vector3 inward = -DirToVec(dir);
        Vector3 probeCenter = new Vector3(doorCenter.x, doorwayMidY, doorCenter.z) + inward * DoorwayProbeDepth;

        Vector3 halfExtents = (dir == Dir.E || dir == Dir.W)
            ? new Vector3(0.30f, 1.0f, treatAsCorridor ? 0.75f : 1.05f)
            : new Vector3(treatAsCorridor ? 0.75f : 1.05f, 1.0f, 0.30f);

        var probe = new Bounds(probeCenter, halfExtents * 2f);
        foreach (Bounds blocker in blockingBounds)
        {
            if (blocker.max.y <= floorY + DoorwayBlockerMinHeight)
                continue;

            if (!blocker.Intersects(probe))
                continue;

            return false;
        }

        return true;
    }

    internal static bool TryGetCanonicalPlacement(
        IEnumerable<Dir> requestedOpenings,
        out ModuleVariantSlot slot,
        out float rotationY)
    {
        var openings = new HashSet<Dir>(requestedOpenings);
        slot = ModuleVariantSlot.N;
        rotationY = 0f;

        if (openings.Count == 1)
        {
            slot = ModuleVariantSlot.N;
            rotationY = openings.First() switch
            {
                Dir.N =>   0f,
                Dir.E => 270f,
                Dir.S => 180f,
                Dir.W =>  90f,
                _     =>   0f,
            };
            return true;
        }

        if (openings.Count == 2)
        {
            bool n = openings.Contains(Dir.N);
            bool s = openings.Contains(Dir.S);
            bool e = openings.Contains(Dir.E);
            bool w = openings.Contains(Dir.W);

            if (n && s)
            {
                slot = ModuleVariantSlot.NS;
                rotationY = 0f;
                return true;
            }

            if (e && w)
            {
                slot = ModuleVariantSlot.NS;
                rotationY = 90f;
                return true;
            }

            slot = ModuleVariantSlot.NE;
            if (n && e) { rotationY =   0f; return true; }
            if (s && e) { rotationY = 270f; return true; }
            if (s && w) { rotationY = 180f; return true; }
            if (n && w) { rotationY =  90f; return true; }
        }

        return false;
    }

    bool TrySelectVariant(
        ModuleSet set,
        IEnumerable<Dir> openings,
        out GameObject prefab,
        out float rotY,
        out string failureReason)
    {
        prefab = null;
        rotY = 0f;
        failureReason = null;

        if (set == null)
        {
            failureReason = "module set is null";
            return false;
        }

        HashSet<Dir> requestedOpenings = new HashSet<Dir>(openings);
        foreach (GameObject candidatePrefab in EnumerateDistinctVariants(set))
        {
            PrefabLayoutInfo candidateInfo;
            try
            {
                candidateInfo = GetPrefabLayout(candidatePrefab, false);
            }
            catch (Exception e)
            {
                failureReason = $"analysis failed for {candidatePrefab.name}: {e.Message}";
                return false;
            }

            foreach (float candidateRotY in EnumerateQuarterTurns())
            {
                Quaternion rotation = Quaternion.Euler(0f, candidateRotY, 0f);
                var rotatedOpenings = new HashSet<Dir>(candidateInfo.OpenDoorDirs.Select(dir => RotateDir(dir, rotation)));
                if (!rotatedOpenings.SetEquals(requestedOpenings))
                    continue;

                prefab = candidatePrefab;
                rotY = candidateRotY;
                failureReason = null;
                return true;
            }
        }

        if (!TryGetCanonicalPlacement(requestedOpenings, out ModuleVariantSlot slot, out rotY))
        {
            failureReason = $"unsupported openings [{OpeningsToKey(requestedOpenings)}]";
            return false;
        }

        prefab = slot switch
        {
            ModuleVariantSlot.N  => set.variantN,
            ModuleVariantSlot.NE => set.variantNE,
            ModuleVariantSlot.NS => set.variantNS,
            _                    => null,
        };

        if (prefab == null)
            prefab = FallbackAny(set);

        if (prefab == null)
        {
            failureReason = $"no prefab assigned for slot {slot}";
            return false;
        }

        return true;
    }

    static IEnumerable<GameObject> EnumerateDistinctVariants(ModuleSet set)
    {
        if (set == null) yield break;

        var seen = new HashSet<GameObject>();
        foreach (GameObject prefab in new[] { set.variantN, set.variantNE, set.variantNS })
        {
            if (prefab == null || !seen.Add(prefab))
                continue;

            yield return prefab;
        }
    }

    static IEnumerable<float> EnumerateQuarterTurns()
    {
        yield return 0f;
        yield return 90f;
        yield return 180f;
        yield return 270f;
    }

    static GameObject FallbackAny(ModuleSet set)
    {
        if (set == null) return null;
        if (set.variantN != null) return set.variantN;
        if (set.variantNE != null) return set.variantNE;
        return set.variantNS;
    }

    static bool TryCollectLocalBounds(Transform root, bool walkableOnly, out Bounds bounds)
    {
        bool initialized = false;
        bounds = new Bounds();

        foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
        {
            var localBounds = new Bounds(box.center, box.size);
            if (walkableOnly && !IsWalkableSource(box.transform, localBounds))
                continue;

            EncapsulateBounds(root, box.transform, localBounds, ref bounds, ref initialized);
        }

        foreach (var capsule in root.GetComponentsInChildren<CapsuleCollider>(true))
        {
            Bounds localBounds = CapsuleToBounds(capsule);
            if (walkableOnly && !IsWalkableSource(capsule.transform, localBounds))
                continue;

            EncapsulateBounds(root, capsule.transform, localBounds, ref bounds, ref initialized);
        }

        foreach (var sphere in root.GetComponentsInChildren<SphereCollider>(true))
        {
            float diameter = sphere.radius * 2f;
            var localBounds = new Bounds(sphere.center, new Vector3(diameter, diameter, diameter));
            if (walkableOnly && !IsWalkableSource(sphere.transform, localBounds))
                continue;

            EncapsulateBounds(root, sphere.transform, localBounds, ref bounds, ref initialized);
        }

        foreach (var meshCollider in root.GetComponentsInChildren<MeshCollider>(true))
        {
            if (meshCollider.sharedMesh == null) continue;

            Bounds localBounds = meshCollider.sharedMesh.bounds;
            if (walkableOnly && !IsWalkableSource(meshCollider.transform, localBounds))
                continue;

            EncapsulateBounds(root, meshCollider.transform, localBounds, ref bounds, ref initialized);
        }

        foreach (var meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null) continue;

            Bounds localBounds = meshFilter.sharedMesh.bounds;
            if (walkableOnly && !IsWalkableSource(meshFilter.transform, localBounds))
                continue;

            EncapsulateBounds(root, meshFilter.transform, localBounds, ref bounds, ref initialized);
        }

        return initialized;
    }

    static List<Bounds> CollectBlockingLocalBounds(Transform root)
    {
        var results = new List<Bounds>();

        foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
        {
            Bounds localBounds = new Bounds(box.center, box.size);
            if (IsWalkableSource(box.transform, localBounds))
                continue;

            results.Add(TransformBoundsToRootLocal(root, box.transform, localBounds));
        }

        foreach (var capsule in root.GetComponentsInChildren<CapsuleCollider>(true))
        {
            Bounds localBounds = CapsuleToBounds(capsule);
            if (IsWalkableSource(capsule.transform, localBounds))
                continue;

            results.Add(TransformBoundsToRootLocal(root, capsule.transform, localBounds));
        }

        foreach (var sphere in root.GetComponentsInChildren<SphereCollider>(true))
        {
            float diameter = sphere.radius * 2f;
            Bounds localBounds = new Bounds(sphere.center, new Vector3(diameter, diameter, diameter));
            if (IsWalkableSource(sphere.transform, localBounds))
                continue;

            results.Add(TransformBoundsToRootLocal(root, sphere.transform, localBounds));
        }

        foreach (var meshCollider in root.GetComponentsInChildren<MeshCollider>(true))
        {
            if (meshCollider.sharedMesh == null)
                continue;

            Bounds localBounds = meshCollider.sharedMesh.bounds;
            if (IsWalkableSource(meshCollider.transform, localBounds))
                continue;

            results.Add(TransformBoundsToRootLocal(root, meshCollider.transform, localBounds));
        }

        foreach (var meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null)
                continue;

            Bounds localBounds = meshFilter.sharedMesh.bounds;
            if (IsWalkableSource(meshFilter.transform, localBounds))
                continue;

            results.Add(TransformBoundsToRootLocal(root, meshFilter.transform, localBounds));
        }

        return results;
    }

    static Bounds CapsuleToBounds(CapsuleCollider capsule)
    {
        float radius = capsule.radius;
        Vector3 size = capsule.direction switch
        {
            0 => new Vector3(capsule.height, radius * 2f, radius * 2f),
            1 => new Vector3(radius * 2f, capsule.height, radius * 2f),
            2 => new Vector3(radius * 2f, radius * 2f, capsule.height),
            _ => Vector3.one * radius * 2f,
        };
        return new Bounds(capsule.center, size);
    }

    static bool IsWalkableSource(Transform source, Bounds localBounds)
    {
        for (Transform current = source; current != null; current = current.parent)
        {
            if (current.name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        float maxY = localBounds.center.y + localBounds.extents.y;
        return localBounds.size.y <= 0.6f && maxY <= 0.6f;
    }

    static void EncapsulateBounds(
        Transform root,
        Transform source,
        Bounds localBounds,
        ref Bounds combined,
        ref bool initialized)
    {
        foreach (var corner in GetBoundsCorners(localBounds))
        {
            Vector3 rootLocalPoint = root.InverseTransformPoint(source.TransformPoint(corner));
            if (!initialized)
            {
                combined = new Bounds(rootLocalPoint, Vector3.zero);
                initialized = true;
            }
            else
            {
                combined.Encapsulate(rootLocalPoint);
            }
        }
    }

    static Bounds TransformBoundsToRootLocal(Transform root, Transform source, Bounds localBounds)
    {
        bool initialized = false;
        Bounds combined = new Bounds();
        EncapsulateBounds(root, source, localBounds, ref combined, ref initialized);
        return combined;
    }

    static IEnumerable<Vector3> GetBoundsCorners(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        yield return new Vector3(min.x, min.y, min.z);
        yield return new Vector3(min.x, min.y, max.z);
        yield return new Vector3(min.x, max.y, min.z);
        yield return new Vector3(min.x, max.y, max.z);
        yield return new Vector3(max.x, min.y, min.z);
        yield return new Vector3(max.x, min.y, max.z);
        yield return new Vector3(max.x, max.y, min.z);
        yield return new Vector3(max.x, max.y, max.z);
    }

    static WorldRect ComputeWorldRect(Bounds localBounds, Transform transform)
    {
        var corners = new[]
        {
            new Vector3(localBounds.min.x, 0f, localBounds.min.z),
            new Vector3(localBounds.min.x, 0f, localBounds.max.z),
            new Vector3(localBounds.max.x, 0f, localBounds.min.z),
            new Vector3(localBounds.max.x, 0f, localBounds.max.z),
        };

        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        foreach (var corner in corners)
        {
            Vector3 world = TransformPoint(transform, corner);
            min.x = Mathf.Min(min.x, world.x);
            min.y = Mathf.Min(min.y, world.z);
            max.x = Mathf.Max(max.x, world.x);
            max.y = Mathf.Max(max.y, world.z);
        }

        return new WorldRect(min, max);
    }

    static Vector3 TransformPoint(Transform transform, Vector3 localPoint) =>
        transform.position + transform.rotation * localPoint;

    static Dir RotateDir(Dir dir, Quaternion rotation)
    {
        Vector3 rotated = rotation * DirToVec(dir);
        return VecToDir(rotated);
    }

    static Dir InverseRotateDir(Dir worldDir, Quaternion rotation) =>
        RotateDir(worldDir, Quaternion.Inverse(rotation));

    static Dir VecToDir(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return Dir.N;

        direction.Normalize();

        if (Vector3.Dot(direction, Vector3.back) > 0.707f) return Dir.N;
        if (Vector3.Dot(direction, Vector3.forward) > 0.707f) return Dir.S;
        if (Vector3.Dot(direction, Vector3.right) > 0.707f) return Dir.E;
        if (Vector3.Dot(direction, Vector3.left) > 0.707f) return Dir.W;

        throw new InvalidOperationException($"Direction {direction} does not map to a cardinal axis");
    }

    static bool TryResolveStraightCorridor(
        PrefabLayoutInfo corridorInfo,
        Dir outwardDir,
        out Dir entryDir,
        out Dir exitDir,
        out Quaternion rotation)
    {
        rotation = Quaternion.identity;

        if (!TryResolveStraightDoorPair(corridorInfo.ResolvedEdgeOpenings, corridorInfo.ResolvedOpenDoorDirs, out entryDir, out exitDir))
            return false;

        foreach (float candidateRotY in EnumerateQuarterTurns())
        {
            Quaternion candidateRotation = Quaternion.Euler(0f, candidateRotY, 0f);
            if (RotateDir(entryDir, candidateRotation) != Opposite(outwardDir))
                continue;

            if (RotateDir(exitDir, candidateRotation) != outwardDir)
                continue;

            rotation = candidateRotation;
            return true;
        }

        return false;
    }

    static (int a, int b) GetPairKey(int left, int right) =>
        left < right ? (left, right) : (right, left);

    static float GetLateralMin(Bounds bounds, Dir dir) =>
        dir == Dir.N || dir == Dir.S ? bounds.min.x : bounds.min.z;

    static float GetLateralMax(Bounds bounds, Dir dir) =>
        dir == Dir.N || dir == Dir.S ? bounds.max.x : bounds.max.z;

    static float GetLateralCoordinate(Vector3 point, Dir dir) =>
        dir == Dir.N || dir == Dir.S ? point.x : point.z;

    static float GetEdgeCoordinate(Vector3 point, Dir dir) =>
        dir == Dir.N || dir == Dir.S ? point.z : point.x;

    static Vector3 DirToVec(Dir d) => d switch
    {
        Dir.N => Vector3.back,
        Dir.S => Vector3.forward,
        Dir.E => Vector3.right,
        Dir.W => Vector3.left,
        _     => Vector3.zero,
    };

    static Dir Opposite(Dir d) => d switch
    {
        Dir.N => Dir.S,
        Dir.S => Dir.N,
        Dir.E => Dir.W,
        Dir.W => Dir.E,
        _     => d,
    };

    internal static string DirToSocketName(Dir dir) => dir switch
    {
        Dir.N => "Socket_North",
        Dir.S => "Socket_South",
        Dir.E => "Socket_East",
        Dir.W => "Socket_West",
        _     => "Socket_Unknown",
    };

    internal static string OpeningsToKey(IEnumerable<Dir> openings) =>
        string.Join(",", openings.OrderBy(d => d).Select(d => d.ToString()));

    void Shuffle<T>(IList<T> list)
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

    void OnDrawGizmos()
    {
        if (placedModules.Count == 0 && placedConnections.Count == 0)
            return;

        foreach (var module in placedModules)
        {
            if (module?.Instance == null) continue;

            DrawRectGizmo(module.WorldRect, new Color(0f, 1f, 0f, 0.15f));

            foreach (var socket in module.WorldSocketHints)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(socket.Value + Vector3.up * 0.25f, 0.2f);

                if (module.WorldDoorCenters.TryGetValue(socket.Key, out Vector3 resolvedDoor))
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawSphere(resolvedDoor + Vector3.up * 0.25f, 0.17f);

                    if ((resolvedDoor - socket.Value).sqrMagnitude > 0.0001f)
                    {
                        Gizmos.color = new Color(1f, 1f, 0f, 0.75f);
                        Gizmos.DrawLine(socket.Value + Vector3.up * 0.25f, resolvedDoor + Vector3.up * 0.25f);
                    }
                }
            }

            foreach (var door in module.WorldDoorCenters)
            {
                if (module.WorldSocketHints.ContainsKey(door.Key))
                    continue;

                Gizmos.color = Color.green;
                Gizmos.DrawSphere(door.Value + Vector3.up * 0.25f, 0.17f);
            }
        }

        foreach (var connection in placedConnections)
        {
            if (connection?.Instance == null) continue;

            DrawRectGizmo(connection.WorldRect, new Color(0f, 0.5f, 1f, 0.25f));
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(connection.FromSocket + Vector3.up * 0.15f, connection.ToSocket + Vector3.up * 0.15f);
        }
    }

    static void DrawRectGizmo(WorldRect rect, Color color)
    {
        Gizmos.color = color;

        Vector3 center = new Vector3(rect.Center.x, 1.5f, rect.Center.y);
        Vector3 size = new Vector3(rect.Size.x, 3f, rect.Size.y);
        Gizmos.DrawWireCube(center, size);
    }

    public void RegenerateWithSeed(int newSeed)
    {
        seed = newSeed;
        CurrentSeed = newSeed;
    }
}
