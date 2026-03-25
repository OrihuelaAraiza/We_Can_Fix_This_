using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Tools > WCFT > Build Ship Layout
/// Assembles the Corexis ship interior from the Sci-Fi Styled Modular Pack.
/// Destroys any existing ShipInterior GO and rebuilds from scratch.
/// </summary>
public static class ShipLayoutBuilder
{
    // ── Prefab paths ──────────────────────────────────────────────────────────
    const string PACK = "Assets/Art/Environment/Sci-Fi Styled Modular Pack/Prefabs/";

    const string FLOOR_PREFAB    = PACK + "Floors/floor_2.prefab";
    const string WALL_PREFAB     = PACK + "Walls/wall_big.prefab";
    const string DOOR_PREFAB     = PACK + "Doors/door_1.prefab";
    const string CORRIDOR_PREFAB = PACK + "Corridors/Corridor_I.prefab";

    // ── Room descriptor ───────────────────────────────────────────────────────
    struct RoomDef
    {
        public string             name;
        public Vector3            center;
        public ShipRoom.RoomType  roomType;
        public bool               isBridge;

        // Which sides have corridor openings (no wall, just a door)
        public bool corridorN, corridorS, corridorE, corridorW;
    }

    // ── Entry point ───────────────────────────────────────────────────────────
    [MenuItem("Tools/WCFT/Build Ship Layout")]
    public static void Build()
    {
        // Load prefabs
        var floorPrefab    = AssetDatabase.LoadAssetAtPath<GameObject>(FLOOR_PREFAB);
        var wallPrefab     = AssetDatabase.LoadAssetAtPath<GameObject>(WALL_PREFAB);
        var doorPrefab     = AssetDatabase.LoadAssetAtPath<GameObject>(DOOR_PREFAB);
        var corridorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CORRIDOR_PREFAB);

        bool missing = false;
        if (floorPrefab    == null) { Debug.LogError("[ShipLayoutBuilder] Missing: " + FLOOR_PREFAB);    missing = true; }
        if (wallPrefab     == null) { Debug.LogError("[ShipLayoutBuilder] Missing: " + WALL_PREFAB);     missing = true; }
        if (doorPrefab     == null) { Debug.LogError("[ShipLayoutBuilder] Missing: " + DOOR_PREFAB);     missing = true; }
        if (corridorPrefab == null) { Debug.LogError("[ShipLayoutBuilder] Missing: " + CORRIDOR_PREFAB); missing = true; }
        if (missing) return;

        // ── Room definitions ──────────────────────────────────────────────────
        var rooms = new[]
        {
            new RoomDef {
                name     = "Bridge",
                center   = new Vector3(  0, 0,   0),
                roomType = ShipRoom.RoomType.Bridge,
                isBridge = true,
                corridorW = true, corridorE = true, corridorS = true
            },
            new RoomDef {
                name     = "PowerRoom",
                center   = new Vector3(-20, 0,   0),
                roomType = ShipRoom.RoomType.PowerRoom,
                corridorE = true
            },
            new RoomDef {
                name     = "CommsRoom",
                center   = new Vector3( 20, 0,   0),
                roomType = ShipRoom.RoomType.CommsRoom,
                corridorW = true
            },
            new RoomDef {
                name     = "GravityRoom",
                center   = new Vector3(  0, 0, -20),
                roomType = ShipRoom.RoomType.GravityRoom,
                corridorN = true, corridorE = true
            },
            new RoomDef {
                name     = "HullRoom",
                center   = new Vector3( 20, 0, -20),
                roomType = ShipRoom.RoomType.HullRoom,
                corridorW = true
            },
        };

        // ── Clear / create ShipInterior root ──────────────────────────────────
        var existing = GameObject.Find("ShipInterior");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        var root = new GameObject("ShipInterior");
        Undo.RegisterCreatedObjectUndo(root, "Build Ship Layout");

        // ── Collect GOs to mark NavigationStatic ─────────────────────────────
        var navGOs = new List<GameObject>();

        // ── Build rooms ───────────────────────────────────────────────────────
        var roomComponents = new Dictionary<string, ShipRoom>();

        foreach (var def in rooms)
        {
            var roomGO = new GameObject(def.name);
            roomGO.transform.SetParent(root.transform, false);
            roomGO.transform.position = def.center;

            // -- Floors -------------------------------------------------------
            var floorsRoot = new GameObject("Floors");
            floorsRoot.transform.SetParent(roomGO.transform, false);

            Vector3[] floorOffsets =
            {
                new Vector3(-2.5f, 0f, -2.5f),
                new Vector3(-2.5f, 0f,  2.5f),
                new Vector3( 2.5f, 0f, -2.5f),
                new Vector3( 2.5f, 0f,  2.5f),
            };

            foreach (var offset in floorOffsets)
            {
                var go = InstantiatePrefab(floorPrefab, floorsRoot.transform);
                go.transform.localPosition = offset;
                navGOs.Add(go);
            }

            // -- Walls --------------------------------------------------------
            var wallsRoot = new GameObject("Walls");
            wallsRoot.transform.SetParent(roomGO.transform, false);
            BuildRoomWalls(wallPrefab, wallsRoot.transform, def, navGOs);

            // -- Doors --------------------------------------------------------
            var doorsRoot = new GameObject("Doors");
            doorsRoot.transform.SetParent(roomGO.transform, false);
            BuildRoomDoors(doorPrefab, doorsRoot.transform, def);

            // -- SpawnPoints (Bridge only) ------------------------------------
            Transform[] spawnTransforms = null;
            if (def.isBridge)
            {
                var spawnRoot = new GameObject("SpawnPoints");
                spawnRoot.transform.SetParent(roomGO.transform, false);
                spawnRoot.transform.position = def.center;

                var spawnOffsets = new Vector3[]
                {
                    new Vector3( 2f, 0f,  2f),
                    new Vector3(-2f, 0f,  2f),
                    new Vector3( 2f, 0f, -2f),
                    new Vector3(-2f, 0f, -2f),
                };
                string[] spawnNames = { "SpawnPoint_P1", "SpawnPoint_P2", "SpawnPoint_P3", "SpawnPoint_P4" };

                spawnTransforms = new Transform[4];
                for (int i = 0; i < 4; i++)
                {
                    var sp = new GameObject(spawnNames[i]);
                    sp.transform.SetParent(spawnRoot.transform, false);
                    sp.transform.localPosition = spawnOffsets[i];
                    spawnTransforms[i] = sp.transform;
                }
            }

            // -- StationAnchor (non-Bridge rooms) ------------------------------
            Transform stationAnchor = null;
            if (!def.isBridge)
            {
                var anchorGO = new GameObject("StationAnchor");
                anchorGO.transform.SetParent(roomGO.transform, false);
                anchorGO.transform.localPosition = Vector3.zero;
                stationAnchor = anchorGO.transform;
            }

            // -- ShipRoom component -------------------------------------------
            var shipRoom = roomGO.AddComponent<ShipRoom>();
            shipRoom.roomType      = def.roomType;
            shipRoom.stationAnchor = stationAnchor;
            shipRoom.spawnPoints   = spawnTransforms;

            roomComponents[def.name] = shipRoom;
        }

        // ── Build corridors ───────────────────────────────────────────────────
        var corridorRoot = new GameObject("Corridors");
        corridorRoot.transform.SetParent(root.transform, false);

        PlaceCorridor(corridorPrefab, corridorRoot.transform, "Corridor_BridgePower",   new Vector3(-10f, 0f,   0f), 90f,  navGOs);
        PlaceCorridor(corridorPrefab, corridorRoot.transform, "Corridor_BridgeComms",   new Vector3( 10f, 0f,   0f), 90f,  navGOs);
        PlaceCorridor(corridorPrefab, corridorRoot.transform, "Corridor_BridgeGravity", new Vector3(  0f, 0f, -10f),  0f,  navGOs);
        PlaceCorridor(corridorPrefab, corridorRoot.transform, "Corridor_GravityHull",   new Vector3( 10f, 0f, -20f), 90f,  navGOs);

        // ── NavMesh static + bake ─────────────────────────────────────────────
        foreach (var go in navGOs)
        {
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic);
        }

        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

        // ── Wire PlayerManager.spawnPoints ────────────────────────────────────
#pragma warning disable CS0618
        var pm = Object.FindObjectOfType<PlayerManager>();
#pragma warning restore CS0618
        if (pm != null && roomComponents.TryGetValue("Bridge", out var bridge) &&
            bridge.spawnPoints != null && bridge.spawnPoints.Length > 0)
        {
            var pmSO = new SerializedObject(pm);
            var prop = pmSO.FindProperty("spawnPoints");
            prop.arraySize = bridge.spawnPoints.Length;
            for (int i = 0; i < bridge.spawnPoints.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = bridge.spawnPoints[i];
            pmSO.ApplyModifiedProperties();
            Debug.Log("[ShipLayoutBuilder] PlayerManager.spawnPoints wired.");
        }
        else
        {
            Debug.LogWarning("[ShipLayoutBuilder] PlayerManager not found or Bridge has no spawn points.");
        }

        // ── Move existing RepairStations to their room anchors ────────────────
        var typeToRoom = new Dictionary<RepairStation.StationType, string>
        {
            { RepairStation.StationType.Energy,         "PowerRoom"   },
            { RepairStation.StationType.Communications, "CommsRoom"   },
            { RepairStation.StationType.Gravity,        "GravityRoom" },
            { RepairStation.StationType.Hull,           "HullRoom"    },
        };

#pragma warning disable CS0618
        foreach (var rs in Object.FindObjectsOfType<RepairStation>())
#pragma warning restore CS0618
        {
            if (!typeToRoom.TryGetValue(rs.Type, out string roomName)) continue;
            if (!roomComponents.TryGetValue(roomName, out var sr)) continue;
            if (sr.stationAnchor == null) continue;

            Undo.RecordObject(rs.transform, "Move RepairStation");
            rs.transform.position = sr.stationAnchor.position;
            Debug.Log($"[ShipLayoutBuilder] Moved '{rs.name}' ({rs.Type}) → {roomName}/StationAnchor");
        }

        // ── Save scene ────────────────────────────────────────────────────────
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[ShipLayoutBuilder] Done — ship interior built and scene saved.");
    }

    // ── Wall helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Places wall_big segments around all four sides of a 10x10 room.
    /// Sides with corridor connections are left open (no wall placed).
    /// </summary>
    static void BuildRoomWalls(GameObject wallPrefab, Transform parent, RoomDef def, List<GameObject> navGOs)
    {
        Vector3 c = def.center;

        // North wall  (z+5)  — no gap → 2 segments, rotY=0
        if (!def.corridorN)
        {
            PlaceWall(wallPrefab, parent, new Vector3(c.x - 2.5f, 0f, c.z + 5f),   0f, navGOs);
            PlaceWall(wallPrefab, parent, new Vector3(c.x + 2.5f, 0f, c.z + 5f),   0f, navGOs);
        }

        // South wall  (z-5)  — rotY=180
        if (!def.corridorS)
        {
            PlaceWall(wallPrefab, parent, new Vector3(c.x - 2.5f, 0f, c.z - 5f), 180f, navGOs);
            PlaceWall(wallPrefab, parent, new Vector3(c.x + 2.5f, 0f, c.z - 5f), 180f, navGOs);
        }

        // East wall  (x+5)  — rotY=90
        if (!def.corridorE)
        {
            PlaceWall(wallPrefab, parent, new Vector3(c.x + 5f, 0f, c.z + 2.5f),  90f, navGOs);
            PlaceWall(wallPrefab, parent, new Vector3(c.x + 5f, 0f, c.z - 2.5f),  90f, navGOs);
        }

        // West wall  (x-5)  — rotY=270
        if (!def.corridorW)
        {
            PlaceWall(wallPrefab, parent, new Vector3(c.x - 5f, 0f, c.z + 2.5f), 270f, navGOs);
            PlaceWall(wallPrefab, parent, new Vector3(c.x - 5f, 0f, c.z - 2.5f), 270f, navGOs);
        }
    }

    static void PlaceWall(GameObject prefab, Transform parent, Vector3 pos, float rotY, List<GameObject> navGOs)
    {
        var go = InstantiatePrefab(prefab, parent);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        navGOs.Add(go);
    }

    // ── Door helpers ──────────────────────────────────────────────────────────

    static void BuildRoomDoors(GameObject doorPrefab, Transform parent, RoomDef def)
    {
        Vector3 c = def.center;
        if (def.corridorN) PlaceDoor(doorPrefab, parent, new Vector3(c.x,       0f, c.z + 5f),   0f);
        if (def.corridorS) PlaceDoor(doorPrefab, parent, new Vector3(c.x,       0f, c.z - 5f), 180f);
        if (def.corridorE) PlaceDoor(doorPrefab, parent, new Vector3(c.x + 5f,  0f, c.z      ),  90f);
        if (def.corridorW) PlaceDoor(doorPrefab, parent, new Vector3(c.x - 5f,  0f, c.z      ), 270f);
    }

    static void PlaceDoor(GameObject prefab, Transform parent, Vector3 pos, float rotY)
    {
        var go = InstantiatePrefab(prefab, parent);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
    }

    // ── Corridor helper ───────────────────────────────────────────────────────

    static void PlaceCorridor(GameObject prefab, Transform parent, string goName,
                               Vector3 pos, float rotY, List<GameObject> navGOs)
    {
        var go = InstantiatePrefab(prefab, parent);
        go.name = goName;
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        navGOs.Add(go);
    }

    // ── Shared instantiate ────────────────────────────────────────────────────

    static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
    {
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
    }
}
