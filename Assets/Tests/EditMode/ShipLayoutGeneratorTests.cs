using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ShipLayoutGeneratorTests
{
    const string BridgePath = "Assets/Art/Modules/Bridge/Module_Bridge_OOOO.prefab";
    const string CorridorPath = "Assets/Art/Modules/Corridors_Straight/Module_Corridor_Straight.prefab";
    const string CorridorV2Path = "Assets/Art/Modules/Corridors_Straight/Module_Corridor_Straight_v2.prefab";
    const string CorridorTPath = "Assets/Art/Modules/Corridors_T/Module_Corridor_T.prefab";
    const string CommsNPath = "Assets/Art/Modules/Comms/Module_RepairStation_COMMS_N.prefab";

    static readonly int[] SampleSeeds = { 101, 202, 303 };

    readonly List<GameObject> createdRoots = new List<GameObject>();

    static Type GeneratorType => Type.GetType("ShipLayoutGenerator, Assembly-CSharp");
    static Type PlayerManagerType => Type.GetType("PlayerManager, Assembly-CSharp");
    static Type DirType => GeneratorType.GetNestedType("Dir", BindingFlags.NonPublic);
    static Type VariantSlotType => GeneratorType.GetNestedType("ModuleVariantSlot", BindingFlags.NonPublic);
    static Type ModuleSetType => GeneratorType.GetNestedType("ModuleSet", BindingFlags.Public);

    [SetUp]
    public void SetUp()
    {
        Assert.That(GeneratorType, Is.Not.Null, "ShipLayoutGenerator type");
        Assert.That(PlayerManagerType, Is.Not.Null, "PlayerManager type");
        Assert.That(DirType, Is.Not.Null, "ShipLayoutGenerator.Dir type");
        Assert.That(VariantSlotType, Is.Not.Null, "ShipLayoutGenerator.ModuleVariantSlot type");
        Assert.That(ModuleSetType, Is.Not.Null, "ShipLayoutGenerator.ModuleSet type");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var root in createdRoots)
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        createdRoots.Clear();
        SetStaticProperty(PlayerManagerType, "Instance", null);
    }

    [Test]
    public void AnalyzePrefabLayout_ExtractsExpectedSocketsAndBounds()
    {
        object bridgeInfo = InvokeStatic("AnalyzePrefabLayout", LoadPrefab(BridgePath), false);
        Assert.That(GetSocketHint(bridgeInfo, "N").z, Is.EqualTo(0f).Within(0.05f));
        Assert.That(GetSocketHint(bridgeInfo, "S").z, Is.EqualTo(12f).Within(0.05f));
        Assert.That(GetSocketHint(bridgeInfo, "E").x, Is.EqualTo(10f).Within(0.05f));
        Assert.That(GetDoorCenter(bridgeInfo, "N").z, Is.EqualTo(0f).Within(0.2f));

        Bounds bridgeBounds = Get<Bounds>(bridgeInfo, "LocalBounds");
        Assert.That(bridgeBounds.size.x, Is.GreaterThan(11f));
        Assert.That(bridgeBounds.size.z, Is.GreaterThan(11f));

        object repairInfo = InvokeStatic("AnalyzePrefabLayout", LoadPrefab(CommsNPath), false);
        Assert.That(GetSocketHint(repairInfo, "W").x, Is.EqualTo(-2f).Within(0.05f));
        Assert.That(GetSocketHint(repairInfo, "N").z, Is.EqualTo(-2f).Within(0.05f));
        Assert.That(ContainsDir(repairInfo, "ResolvedOpenDoorDirs", "N"), Is.True);

        Bounds repairBounds = Get<Bounds>(repairInfo, "LocalBounds");
        Assert.That(repairBounds.size.x, Is.GreaterThan(11f));
        Assert.That(repairBounds.size.z, Is.GreaterThan(11f));

        object corridorInfo = InvokeStatic("AnalyzePrefabLayout", LoadPrefab(CorridorPath), true);
        Assert.That(Get<float>(corridorInfo, "StraightLength"), Is.EqualTo(4f).Within(0.2f));
        Assert.That(Get<Bounds>(corridorInfo, "LocalBounds").size.x, Is.GreaterThan(3.5f));
    }

    [Test]
    public void AnalyzePrefabLayout_ResolvesDoorCenterFromActualGapNotOffsetSocket()
    {
        GameObject module = CreateSyntheticModule(
            "Synthetic_OffsetHint",
            width: 8f,
            depth: 8f,
            openings: new[] { ("N", 2.5f, 5.5f) },
            socketHints: new[] { ("N", 2.15f) });

        object layout = InvokeStatic("AnalyzePrefabLayout", module, false);

        Assert.That(GetSocketHint(layout, "N").x, Is.EqualTo(2.15f).Within(0.01f));
        Assert.That(GetDoorCenter(layout, "N").x, Is.EqualTo(4f).Within(0.15f));
        Assert.That(ContainsDir(layout, "ResolvedOpenDoorDirs", "N"), Is.True);
    }

    [Test]
    public void AnalyzePrefabLayout_PicksOpeningClosestToHintWhenMultipleGaps()
    {
        GameObject module = CreateSyntheticModule(
            "Synthetic_MultiGapHinted",
            width: 10f,
            depth: 8f,
            openings: new[] { ("N", 0.75f, 3.75f), ("N", 6.25f, 9.25f) },
            socketHints: new[] { ("N", 7.6f) });

        object layout = InvokeStatic("AnalyzePrefabLayout", module, false);
        object opening = GetEdgeOpening(layout, "N");

        Assert.That(GetDoorCenter(layout, "N").x, Is.EqualTo(7.75f).Within(0.15f));
        Assert.That(Get<bool>(opening, "IsAmbiguous"), Is.False);
        Assert.That(Get<float>(opening, "HintDistance"), Is.LessThan(0.2f));
    }

    [Test]
    public void AnalyzePrefabLayout_MarksEdgeAmbiguousWithoutHintWhenMultipleGaps()
    {
        GameObject module = CreateSyntheticModule(
            "Synthetic_AmbiguousNoHint",
            width: 10f,
            depth: 8f,
            openings: new[] { ("N", 0.75f, 3.75f), ("N", 6.25f, 9.25f) },
            socketHints: new[] { ("S", 5f) });

        object layout = InvokeStatic("AnalyzePrefabLayout", module, false);
        object opening = GetEdgeOpening(layout, "N");

        Assert.That(Get<bool>(opening, "IsAmbiguous"), Is.True);
        Assert.That(ContainsDir(layout, "ResolvedOpenDoorDirs", "N"), Is.False);
    }

    [Test]
    public void AnalyzePrefabLayout_ResolvesStraightCorridorFromOppositeOpenings()
    {
        GameObject corridor = CreateSyntheticModule(
            "Synthetic_Corridor_NS",
            width: 4f,
            depth: 6f,
            openings: new[] { ("N", 1f, 3f), ("S", 1f, 3f) },
            socketHints: Array.Empty<(string dirName, float lateral)>());

        object layout = InvokeStatic("AnalyzePrefabLayout", corridor, true);

        Assert.That(ContainsDir(layout, "ResolvedOpenDoorDirs", "N"), Is.True);
        Assert.That(ContainsDir(layout, "ResolvedOpenDoorDirs", "S"), Is.True);
        Assert.That(ContainsDir(layout, "ResolvedOpenDoorDirs", "E"), Is.False);
        Assert.That(ContainsDir(layout, "ResolvedOpenDoorDirs", "W"), Is.False);
        Assert.That(Get<float>(layout, "StraightLength"), Is.EqualTo(6f).Within(0.15f));
    }

    [Test]
    public void TryGetCanonicalPlacement_MapsOpeningsToVariantAndRotation()
    {
        AssertPlacement(new[] { "N" }, "N", 0f);
        AssertPlacement(new[] { "E" }, "N", 270f);
        AssertPlacement(new[] { "S" }, "N", 180f);
        AssertPlacement(new[] { "W" }, "N", 90f);

        AssertPlacement(new[] { "N", "E" }, "NE", 0f);
        AssertPlacement(new[] { "S", "E" }, "NE", 270f);
        AssertPlacement(new[] { "S", "W" }, "NE", 180f);
        AssertPlacement(new[] { "N", "W" }, "NE", 90f);

        AssertPlacement(new[] { "N", "S" }, "NS", 0f);
        AssertPlacement(new[] { "E", "W" }, "NS", 90f);
    }

    [Test]
    public void GenerateLayout_SampleSeeds_HaveNoUnexpectedOverlaps()
    {
        foreach (int seed in SampleSeeds)
        {
            Component generator = CreateConfiguredGenerator();
            Assert.That(GenerateLayout(generator, seed, out string failureReason), Is.True, $"Seed {seed}: {failureReason}");
            AssertNoUnexpectedOverlaps(generator, 0.05f);
        }
    }

    [Test]
    public void GenerateLayout_SampleSeeds_AlwaysIncludeAllConfiguredRooms()
    {
        foreach (int seed in SampleSeeds)
        {
            Component generator = CreateConfiguredGenerator();
            Assert.That(GenerateLayout(generator, seed, out string failureReason), Is.True, $"Seed {seed}: {failureReason}");

            List<object> modules = GetObjects(generator, "DebugPlacedModules");
            string[] names = modules.Select(module => Get<string>(module, "Name")).ToArray();

            Assert.That(modules.Count, Is.EqualTo(8), $"Seed {seed}");
            Assert.That(names.Count(name => name.StartsWith("Repair_", StringComparison.Ordinal)), Is.EqualTo(4), $"Seed {seed}");
            Assert.That(names.Count(name => name.StartsWith("Extra_", StringComparison.Ordinal)), Is.EqualTo(3), $"Seed {seed}");
            Assert.That(names, Does.Contain("Extra_CrewBunks"), $"Seed {seed}");
            Assert.That(names, Does.Contain("Extra_GunStation"), $"Seed {seed}");
            Assert.That(names, Does.Contain("Extra_Storage"), $"Seed {seed}");
        }
    }

    [Test]
    public void GenerateLayout_SampleSeeds_HaveAlignedConnectionsWithoutGaps()
    {
        foreach (int seed in SampleSeeds)
        {
            Component generator = CreateConfiguredGenerator();
            Assert.That(GenerateLayout(generator, seed, out string failureReason), Is.True, $"Seed {seed}: {failureReason}");

            foreach (object connection in GetObjects(generator, "DebugPlacedConnections"))
            {
                Vector3 fromSocket = Get<Vector3>(connection, "FromSocket");
                Vector3 toSocket = Get<Vector3>(connection, "ToSocket");
                Vector3 actualEntry = Get<Vector3>(connection, "ActualEntry");
                Vector3 actualExit = Get<Vector3>(connection, "ActualExit");

                Assert.That(Vector3.Distance(fromSocket, actualEntry), Is.LessThanOrEqualTo(0.05f), $"{Get<string>(connection, "Name")} entry");
                Assert.That(Vector3.Distance(toSocket, actualExit), Is.LessThanOrEqualTo(0.05f), $"{Get<string>(connection, "Name")} exit");

                Vector3 axis = DirToVec(GetEnumName(Get<object>(connection, "Direction")));
                Vector3 delta = toSocket - fromSocket;
                float axialDistance = Vector3.Dot(delta, axis);
                float lateralDistance = (delta - axis * axialDistance).magnitude;
                float expectedLength = Get<float>(Get<object>(connection, "Layout"), "StraightLength");

                Assert.That(lateralDistance, Is.LessThanOrEqualTo(0.05f), $"{Get<string>(connection, "Name")} lateral");
                Assert.That(axialDistance, Is.EqualTo(expectedLength).Within(0.05f), $"{Get<string>(connection, "Name")} length");
            }
        }
    }

    [Test]
    public void GenerateLayout_IgnoresMisassignedTCorridorInStraightPool()
    {
        Component generator = CreateConfiguredGenerator();
        SetField(generator, "prefabsCorridorStraight", new[]
        {
            LoadPrefab(CorridorPath),
            LoadPrefab(CorridorV2Path),
            LoadPrefab(CorridorTPath),
        });

        foreach (int seed in Enumerable.Range(5000, 16))
        {
            Assert.That(GenerateLayout(generator, seed, out string failureReason), Is.True, $"Seed {seed}: {failureReason}");
            Assert.That(GetObjects(generator, "DebugPlacedConnections").Count, Is.GreaterThanOrEqualTo(7), $"Seed {seed}");
        }
    }

    [Test]
    public void GenerateLayout_SampleSeeds_ReachAllModulesFromBridge()
    {
        foreach (int seed in SampleSeeds)
        {
            Component generator = CreateConfiguredGenerator();
            Assert.That(GenerateLayout(generator, seed, out string failureReason), Is.True, $"Seed {seed}: {failureReason}");
            Assert.That(ValidateLayout(generator, out failureReason), Is.True, $"Seed {seed}: {failureReason}");

            List<object> modules = GetObjects(generator, "DebugPlacedModules");
            List<object> connections = GetObjects(generator, "DebugPlacedConnections");
            object bridge = modules.Single(m => Get<bool>(m, "IsBridge"));

            var adjacency = modules.ToDictionary(module => module, _ => new List<object>());
            foreach (object connection in connections)
            {
                object from = Get<object>(connection, "From");
                object to = Get<object>(connection, "To");
                adjacency[from].Add(to);
                adjacency[to].Add(from);
            }

            var visited = new HashSet<object> { bridge };
            var queue = new Queue<object>();
            queue.Enqueue(bridge);

            while (queue.Count > 0)
            {
                object current = queue.Dequeue();
                foreach (object next in adjacency[current])
                {
                    if (!visited.Add(next))
                        continue;

                    queue.Enqueue(next);
                }
            }

            Assert.That(visited.Count, Is.EqualTo(modules.Count), $"Seed {seed}");
        }
    }

    [Test]
    public void GenerateLayout_WiresSupportedSpawnPointsInsideBridge()
    {
        var playerManagerRoot = new GameObject("PlayerManager_Test");
        createdRoots.Add(playerManagerRoot);
        Component playerManager = playerManagerRoot.AddComponent(PlayerManagerType);
        SetStaticProperty(PlayerManagerType, "Instance", playerManager);

        Component generator = CreateConfiguredGenerator();
        Assert.That(GenerateLayout(generator, 101, out string failureReason), Is.True, failureReason);

        GetInstanceMethod(generator, "WireSpawnPoints").Invoke(generator, null);
        Physics.SyncTransforms();

        Transform[] spawnPoints = Get<Transform[]>(playerManager, "spawnPoints");
        Assert.That(spawnPoints, Is.Not.Null);
        Assert.That(spawnPoints.Length, Is.GreaterThanOrEqualTo(4));

        object bridge = GetObjects(generator, "DebugPlacedModules").Single(module => Get<bool>(module, "IsBridge"));
        GameObject bridgeInstance = Get<GameObject>(bridge, "Instance");
        object bridgeRect = Get<object>(bridge, "WalkableRect");
        Vector2 min = Get<Vector2>(bridgeRect, "Min");
        Vector2 max = Get<Vector2>(bridgeRect, "Max");

        foreach (Transform spawnPoint in spawnPoints)
        {
            Assert.That(spawnPoint, Is.Not.Null);
            Assert.That(spawnPoint.IsChildOf(bridgeInstance.transform), Is.True, spawnPoint.name);
            Assert.That(spawnPoint.position.x, Is.InRange(min.x, max.x));
            Assert.That(spawnPoint.position.z, Is.InRange(min.y, max.y));
            Assert.That(Physics.Raycast(spawnPoint.position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore), Is.True, spawnPoint.name);
            Assert.That(hit.normal.y, Is.GreaterThan(0.5f), spawnPoint.name);
            Assert.That(spawnPoint.position.y - hit.point.y, Is.InRange(0.05f, 0.3f), spawnPoint.name);
        }
    }

    Component CreateConfiguredGenerator()
    {
        var root = new GameObject("ShipLayoutGeneratorTests_Root");
        createdRoots.Add(root);

        var shipInterior = new GameObject("ShipInterior_Test");
        shipInterior.transform.SetParent(root.transform, false);

        Component generator = root.AddComponent(GeneratorType);

        SetField(generator, "prefabBridge", LoadPrefab(BridgePath));
        SetField(generator, "setPower", CreateModuleSet(
            "Assets/Art/Modules/Power/Module_RepairStation_POWER_N.prefab",
            "Assets/Art/Modules/Power/Module_RepairStation_POWER_NE.prefab",
            "Assets/Art/Modules/Power/Module_RepairStation_POWER_NS.prefab"));
        SetField(generator, "setComms", CreateModuleSet(
            "Assets/Art/Modules/Comms/Module_RepairStation_COMMS_N.prefab",
            "Assets/Art/Modules/Comms/Module_RepairStation_COMMS_NE.prefab",
            "Assets/Art/Modules/Comms/Module_RepairStation_COMMS_NS.prefab"));
        SetField(generator, "setGravity", CreateModuleSet(
            "Assets/Art/Modules/Gravity/Module_RepairStation_GRAVITY_N.prefab",
            "Assets/Art/Modules/Gravity/Module_RepairStation_GRAVITY_NE.prefab",
            "Assets/Art/Modules/Gravity/Module_RepairStation_GRAVITY_NS.prefab"));
        SetField(generator, "setHull", CreateModuleSet(
            "Assets/Art/Modules/Hull/Module_RepairStation_HULL_N.prefab",
            "Assets/Art/Modules/Hull/Module_RepairStation_HULL_NE.prefab",
            "Assets/Art/Modules/Hull/Module_RepairStation_HULL_NS.prefab"));
        SetField(generator, "setCrewBunks", CreateModuleSet(
            "Assets/Art/Modules/CrewBunks/Module_CrewBunks_N.prefab",
            "Assets/Art/Modules/CrewBunks/Module_CrewBunks_NE.prefab",
            "Assets/Art/Modules/CrewBunks/Module_CrewBunks_NS.prefab"));
        SetField(generator, "setGunStation", CreateModuleSet(
            "Assets/Art/Modules/GunStation/Module_GunStation_N.prefab",
            "Assets/Art/Modules/GunStation/Module_GunStation_NE.prefab",
            "Assets/Art/Modules/GunStation/Module_GunStation_NS.prefab"));
        SetField(generator, "setStorage", CreateModuleSet(
            "Assets/Art/Modules/Storage/Module_Storage_N.prefab",
            "Assets/Art/Modules/Storage/Module_Storage_NE.prefab",
            "Assets/Art/Modules/Storage/Module_Storage_NS.prefab"));
        SetField(generator, "prefabsCorridorStraight", new[]
        {
            LoadPrefab(CorridorPath),
            LoadPrefab(CorridorV2Path),
            LoadPrefab("Assets/Art/Modules/Corridors_Straight/Module_Corridor_Straight_v3.prefab"),
        });
        SetField(generator, "shipInteriorParent", shipInterior.transform);
        SetField(generator, "logValidation", false);
        SetField(generator, "maxGenerationAttempts", 8);
        SetField(generator, "overlapTolerance", 0.05f);
        SetField(generator, "passageHalfWidth", 0.75f);

        return generator;
    }

    object CreateModuleSet(string pathN, string pathNE, string pathNS)
    {
        object set = Activator.CreateInstance(ModuleSetType);
        SetField(set, "variantN", LoadPrefab(pathN));
        SetField(set, "variantNE", LoadPrefab(pathNE));
        SetField(set, "variantNS", LoadPrefab(pathNS));
        return set;
    }

    static void AssertPlacement(string[] dirNames, string expectedSlotName, float expectedRotation)
    {
        MethodInfo method = GetStaticMethod("TryGetCanonicalPlacement");
        Array openings = Array.CreateInstance(DirType, dirNames.Length);

        for (int i = 0; i < dirNames.Length; i++)
            openings.SetValue(Enum.Parse(DirType, dirNames[i]), i);

        object[] args = { openings, Activator.CreateInstance(VariantSlotType), 0f };
        bool result = (bool)method.Invoke(null, args);

        Assert.That(result, Is.True);
        Assert.That(GetEnumName(args[1]), Is.EqualTo(expectedSlotName));
        Assert.That((float)args[2], Is.EqualTo(expectedRotation).Within(0.01f));
    }

    static void AssertNoUnexpectedOverlaps(Component generator, float tolerance)
    {
        var items = new List<(string name, GameObject go, object rect)>();
        items.AddRange(GetObjects(generator, "DebugPlacedModules")
            .Select(module => (Get<string>(module, "Name"), Get<GameObject>(module, "Instance"), Get<object>(module, "WalkableRect"))));
        items.AddRange(GetObjects(generator, "DebugPlacedConnections")
            .Select(connection => (Get<string>(connection, "Name"), Get<GameObject>(connection, "Instance"), Get<object>(connection, "WalkableRect"))));

        var allowedPairs = new HashSet<(int a, int b)>();
        foreach (object connection in GetObjects(generator, "DebugPlacedConnections"))
        {
            allowedPairs.Add(GetPairKey(Get<GameObject>(connection, "Instance").GetInstanceID(), Get<GameObject>(Get<object>(connection, "From"), "Instance").GetInstanceID()));
            allowedPairs.Add(GetPairKey(Get<GameObject>(connection, "Instance").GetInstanceID(), Get<GameObject>(Get<object>(connection, "To"), "Instance").GetInstanceID()));
        }

        for (int i = 0; i < items.Count; i++)
        {
            for (int j = i + 1; j < items.Count; j++)
            {
                if (allowedPairs.Contains(GetPairKey(items[i].go.GetInstanceID(), items[j].go.GetInstanceID())))
                    continue;

                Assert.That(RectOverlaps(items[i].rect, items[j].rect, tolerance), Is.False, $"{items[i].name} vs {items[j].name}");
            }
        }
    }

    static bool GenerateLayout(Component generator, int seed, out string failureReason)
    {
        MethodInfo method = GetInstanceMethod(generator, "GenerateLayoutForTesting");
        object[] args = { seed, null };
        bool result = (bool)method.Invoke(generator, args);
        failureReason = args[1] as string;
        return result;
    }

    static bool ValidateLayout(Component generator, out string failureReason)
    {
        MethodInfo method = GetInstanceMethod(generator, "ValidateGeneratedLayout");
        object[] args = { null };
        bool result = (bool)method.Invoke(generator, args);
        failureReason = args[0] as string;
        return result;
    }

    static List<object> GetObjects(object target, string propertyName)
    {
        IEnumerable enumerable = Get<IEnumerable>(target, propertyName);
        return enumerable.Cast<object>().ToList();
    }

    GameObject CreateSyntheticModule(
        string name,
        float width,
        float depth,
        IEnumerable<(string dirName, float min, float max)> openings,
        IEnumerable<(string dirName, float lateral)> socketHints)
    {
        var root = new GameObject(name);
        createdRoots.Add(root);

        CreateFloor(root.transform, width, depth);

        var openingsByDir = openings
            .GroupBy(opening => opening.dirName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(opening => (opening.min, opening.max)).OrderBy(opening => opening.min).ToList());

        foreach (string dirName in new[] { "N", "S", "E", "W" })
        {
            openingsByDir.TryGetValue(dirName, out List<(float min, float max)> dirOpenings);
            CreateEdgeWalls(root.transform, dirName, width, depth, dirOpenings ?? new List<(float min, float max)>());
        }

        foreach (var socketHint in socketHints)
            CreateSocketHint(root.transform, socketHint.dirName, socketHint.lateral, width, depth);

        return root;
    }

    static void CreateFloor(Transform parent, float width, float depth)
    {
        var floor = new GameObject("floor");
        floor.transform.SetParent(parent, false);

        var collider = floor.AddComponent<BoxCollider>();
        collider.center = new Vector3(width * 0.5f, 0.05f, depth * 0.5f);
        collider.size = new Vector3(width, 0.1f, depth);
    }

    static void CreateEdgeWalls(Transform parent, string dirName, float width, float depth, List<(float min, float max)> openings)
    {
        float spanMax = dirName == "N" || dirName == "S" ? width : depth;
        float cursor = 0f;
        int segmentIndex = 0;

        foreach ((float min, float max) opening in openings.OrderBy(opening => opening.min))
        {
            if (opening.min > cursor + 0.01f)
                CreateWallSegment(parent, dirName, cursor, opening.min, width, depth, segmentIndex++);

            cursor = Mathf.Max(cursor, opening.max);
        }

        if (cursor < spanMax - 0.01f)
            CreateWallSegment(parent, dirName, cursor, spanMax, width, depth, segmentIndex);
    }

    static void CreateWallSegment(Transform parent, string dirName, float spanStart, float spanEnd, float width, float depth, int segmentIndex)
    {
        if (spanEnd - spanStart <= 0.01f)
            return;

        var wall = new GameObject($"wall_{dirName}_{segmentIndex}");
        wall.transform.SetParent(parent, false);

        var collider = wall.AddComponent<BoxCollider>();
        switch (dirName)
        {
            case "N":
                collider.center = new Vector3((spanStart + spanEnd) * 0.5f, 1f, 0f);
                collider.size = new Vector3(spanEnd - spanStart, 2f, 0.3f);
                break;
            case "S":
                collider.center = new Vector3((spanStart + spanEnd) * 0.5f, 1f, depth);
                collider.size = new Vector3(spanEnd - spanStart, 2f, 0.3f);
                break;
            case "E":
                collider.center = new Vector3(width, 1f, (spanStart + spanEnd) * 0.5f);
                collider.size = new Vector3(0.3f, 2f, spanEnd - spanStart);
                break;
            case "W":
                collider.center = new Vector3(0f, 1f, (spanStart + spanEnd) * 0.5f);
                collider.size = new Vector3(0.3f, 2f, spanEnd - spanStart);
                break;
        }
    }

    static void CreateSocketHint(Transform parent, string dirName, float lateral, float width, float depth)
    {
        var socket = new GameObject($"Socket_{SocketSuffix(dirName)}");
        socket.transform.SetParent(parent, false);

        switch (dirName)
        {
            case "N":
                socket.transform.localPosition = new Vector3(lateral, 0f, 0f);
                break;
            case "S":
                socket.transform.localPosition = new Vector3(lateral, 0f, depth);
                break;
            case "E":
                socket.transform.localPosition = new Vector3(width, 0f, lateral);
                break;
            case "W":
                socket.transform.localPosition = new Vector3(0f, 0f, lateral);
                break;
        }
    }

    static string SocketSuffix(string dirName)
    {
        switch (dirName)
        {
            case "N": return "North";
            case "S": return "South";
            case "E": return "East";
            case "W": return "West";
            default: return dirName;
        }
    }

    static Vector3 GetSocketHint(object layout, string dirName)
    {
        return (Vector3)GetDictionaryValue(Get<object>(layout, "SocketHints"), dirName);
    }

    static Vector3 GetDoorCenter(object layout, string dirName)
    {
        return (Vector3)GetDictionaryValue(Get<object>(layout, "ResolvedDoorCenters"), dirName);
    }

    static object GetEdgeOpening(object layout, string dirName)
    {
        return GetDictionaryValue(Get<object>(layout, "ResolvedEdgeOpenings"), dirName);
    }

    static object GetDictionaryValue(object dictionary, string dirName)
    {
        Assert.That(TryGetDictionaryValue(dictionary, dirName, out object value), Is.True, $"{dirName} in {dictionary.GetType().Name}");
        return value;
    }

    static bool TryGetDictionaryValue(object dictionary, string dirName, out object value)
    {
        MethodInfo tryGetValue = dictionary.GetType().GetMethod("TryGetValue");
        Assert.That(tryGetValue, Is.Not.Null, dictionary.GetType().Name + ".TryGetValue");

        object[] args = { Enum.Parse(DirType, dirName), null };
        bool found = (bool)tryGetValue.Invoke(dictionary, args);
        value = args[1];
        return found;
    }

    static bool ContainsDir(object target, string memberName, string dirName)
    {
        IEnumerable enumerable = Get<IEnumerable>(target, memberName);
        foreach (object value in enumerable)
        {
            if (GetEnumName(value) == dirName)
                return true;
        }

        return false;
    }

    static GameObject LoadPrefab(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        return prefab;
    }

    static T Get<T>(object target, string memberName)
    {
        object value = GetMember(target, memberName);
        return (T)value;
    }

    static object GetMember(object target, string memberName)
    {
        Assert.That(target, Is.Not.Null, memberName);

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo property = target.GetType().GetProperty(memberName, flags);
        if (property != null)
            return property.GetValue(target);

        FieldInfo field = target.GetType().GetField(memberName, flags);
        if (field != null)
            return field.GetValue(target);

        Assert.Fail($"Member '{memberName}' not found on {target.GetType().FullName}");
        return null;
    }

    static void SetField(object target, string fieldName, object value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo field = target.GetType().GetField(fieldName, flags);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    static void SetStaticProperty(Type type, string propertyName, object value)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo property = type.GetProperty(propertyName, flags);
        Assert.That(property, Is.Not.Null, propertyName);
        MethodInfo setter = property.GetSetMethod(true);
        Assert.That(setter, Is.Not.Null, propertyName + " setter");
        setter.Invoke(null, new[] { value });
    }

    static MethodInfo GetStaticMethod(string methodName)
    {
        MethodInfo method = GeneratorType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        return method;
    }

    static MethodInfo GetInstanceMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        return method;
    }

    static object InvokeStatic(string methodName, params object[] args)
    {
        MethodInfo method = GetStaticMethod(methodName);
        return method.Invoke(null, args);
    }

    static bool RectOverlaps(object rectA, object rectB, float tolerance)
    {
        MethodInfo overlaps = rectA.GetType().GetMethod("Overlaps", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(overlaps, Is.Not.Null, "WorldRect.Overlaps");
        return (bool)overlaps.Invoke(rectA, new[] { rectB, (object)tolerance });
    }

    static string GetEnumName(object enumValue) => Enum.GetName(enumValue.GetType(), enumValue);

    static (int a, int b) GetPairKey(int left, int right) =>
        left < right ? (left, right) : (right, left);

    static Vector3 DirToVec(string dirName)
    {
        switch (dirName)
        {
            case "N": return Vector3.back;
            case "S": return Vector3.forward;
            case "E": return Vector3.right;
            case "W": return Vector3.left;
            default: return Vector3.zero;
        }
    }
}
