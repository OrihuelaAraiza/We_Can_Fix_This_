using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime procedural ship layout generator.
/// Runs at execution order -100 (before all other scripts) so geometry
/// exists before PlayerManager or any other system tries to use it.
///
/// Layout model: 3x3 "room grid". Each room slot = 10x10m room.
/// Adjacent slots are separated by 5m corridors.
/// Room (r,c) world center = ((c-1)*15, 0, -(r-1)*15)
/// Bridge is always at grid center (1,1) = world origin (0,0,0).
/// </summary>
[DefaultExecutionOrder(-100)]
public class ShipLayoutGenerator : MonoBehaviour
{
    // ── Static API ────────────────────────────────────────────────────────
    public static int  CurrentSeed { get; private set; }
    public static bool IsReady     { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("Corridor Prefabs")]
    [SerializeField] GameObject corridorPrefab_I;
    [SerializeField] GameObject corridorPrefab_L;   // reserved / future use
    [SerializeField] GameObject corridorPrefab_T;   // reserved / future use
    [SerializeField] GameObject corridorPrefab_X;   // reserved / future use

    [Header("Floor Prefabs (one chosen per room)")]
    [SerializeField] GameObject[] floorVariants;

    [Header("Structure")]
    [SerializeField] GameObject wallPrefab;
    [SerializeField] GameObject doorPrefab;

    [Header("Decorative Props (optional)")]
    [SerializeField] GameObject[] consolePrefabs;   // console, console_screen, computer_station
    [SerializeField] GameObject[] cabinetPrefabs;   // cabinet, cabinet_L
    [SerializeField] GameObject   bigScreenPrefab;

    [Header("Settings")]
    [SerializeField] int seed = 0;   // 0 = random each session

    // ── Private grid state ────────────────────────────────────────────────
    bool[,]              activeRooms;   // [3,3]
    ShipRoom.RoomType[,] roomTypes;     // [3,3]
    bool[,]              hEdge;         // [3,2]  horizontal: edge between col c and c+1
    bool[,]              vEdge;         // [2,3]  vertical:   edge between row r and r+1

    System.Random rng;
    Transform[]   _bridgeSpawnPoints;

    // Station room-type assignment order (matches RepairStation.StationType order)
    static readonly ShipRoom.RoomType[] k_StationRoomTypes =
    {
        ShipRoom.RoomType.PowerRoom,
        ShipRoom.RoomType.CommsRoom,
        ShipRoom.RoomType.GravityRoom,
        ShipRoom.RoomType.HullRoom,
    };

    static readonly RepairStation.StationType[] k_StationTypes =
    {
        RepairStation.StationType.Energy,
        RepairStation.StationType.Communications,
        RepairStation.StationType.Gravity,
        RepairStation.StationType.Hull,
    };

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        IsReady     = false;
        CurrentSeed = (seed == 0) ? UnityEngine.Random.Range(1, int.MaxValue) : seed;
        rng         = new System.Random(CurrentSeed);

        GenerateLayout();
        IsReady = true;
        Debug.Log($"[ShipLayoutGenerator] Ready. Seed: {CurrentSeed}");
    }

    void Start()
    {
        // Wire spawn points after all Awakes have run (PlayerManager.Instance is now set)
        if (_bridgeSpawnPoints != null && PlayerManager.Instance != null)
            PlayerManager.Instance.SetSpawnPoints(_bridgeSpawnPoints);
        else
            Debug.LogWarning("[ShipLayoutGenerator] Could not wire spawn points — PlayerManager not found.");
    }

    // ── Main Generation ───────────────────────────────────────────────────

    public void GenerateLayout()
    {
        // Clear any previous layout (keeps RepairStation GOs which live elsewhere)
        var existing = GameObject.Find("ShipInterior");
        if (existing != null) Destroy(existing);

        // Fresh grid state
        activeRooms = new bool[3, 3];
        roomTypes   = new ShipRoom.RoomType[3, 3];
        hEdge       = new bool[3, 2];
        vEdge       = new bool[2, 3];

        var root = new GameObject("ShipInterior");

        // ── Place Bridge ──────────────────────────────────────────────────
        activeRooms[1, 1] = true;
        roomTypes[1, 1]   = ShipRoom.RoomType.Bridge;

        // ── Pick 4 station positions ──────────────────────────────────────
        var candidates = Shuffle(new List<(int r, int c)>
        {
            (0,0),(0,1),(0,2),(1,0),(1,2),(2,0),(2,1),(2,2)
        });

        var stationPos = new List<(int r, int c)>();
        foreach (var pos in candidates)
        {
            if (stationPos.Count >= 4) break;
            bool tooClose = false;
            foreach (var taken in stationPos)
            {
                if (Mathf.Abs(pos.r - taken.r) + Mathf.Abs(pos.c - taken.c) < 2)
                { tooClose = true; break; }
            }
            if (!tooClose) stationPos.Add(pos);
        }
        // Guaranteed fallback: 4 corners are always mutually non-adjacent
        if (stationPos.Count < 4)
            stationPos = new List<(int r, int c)> { (0,0),(0,2),(2,0),(2,2) };

        for (int i = 0; i < stationPos.Count; i++)
        {
            var (r, c) = stationPos[i];
            activeRooms[r, c] = true;
            roomTypes[r, c]   = k_StationRoomTypes[i];
        }

        // ── BFS paths: each station → bridge ─────────────────────────────
        foreach (var (sr, sc) in stationPos)
            MarkPath(BFSPath(sr, sc, 1, 1));

        // ── Build rooms & corridors ───────────────────────────────────────
        var roomsRoot     = new GameObject("Rooms");
        var corridorsRoot = new GameObject("Corridors");
        roomsRoot.transform.SetParent(root.transform);
        corridorsRoot.transform.SetParent(root.transform);

        var stationAnchors = new Dictionary<ShipRoom.RoomType, Transform>();

        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                if (!activeRooms[r, c]) continue;

                Vector3 center = RoomCenter(r, c);
                bool connN = r > 0 && vEdge[r - 1, c];
                bool connS = r < 2 && vEdge[r, c];
                bool connE = c < 2 && hEdge[r, c];
                bool connW = c > 0 && hEdge[r, c - 1];

                BuildRoom(roomsRoot.transform, r, c, center,
                          connN, connS, connE, connW,
                          stationAnchors, ref _bridgeSpawnPoints);
            }
        }

        // Horizontal corridors
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 2; c++)
                if (hEdge[r, c])
                    BuildCorridor(corridorsRoot.transform, HCorridorPos(r, c), 90f, $"Corridor_H_{r}_{c}");

        // Vertical corridors
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 3; c++)
                if (vEdge[r, c])
                    BuildCorridor(corridorsRoot.transform, VCorridorPos(r, c), 0f, $"Corridor_V_{r}_{c}");

        // ── Reposition existing RepairStations ────────────────────────────
        var rsMap = new Dictionary<RepairStation.StationType, ShipRoom.RoomType>
        {
            { RepairStation.StationType.Energy,         ShipRoom.RoomType.PowerRoom   },
            { RepairStation.StationType.Communications, ShipRoom.RoomType.CommsRoom   },
            { RepairStation.StationType.Gravity,        ShipRoom.RoomType.GravityRoom },
            { RepairStation.StationType.Hull,           ShipRoom.RoomType.HullRoom    },
        };

#pragma warning disable CS0618
        foreach (var rs in FindObjectsOfType<RepairStation>())
#pragma warning restore CS0618
        {
            if (!rsMap.TryGetValue(rs.Type, out var rt)) continue;
            if (!stationAnchors.TryGetValue(rt, out var anchor)) continue;
            rs.transform.position = anchor.position;
            Debug.Log($"[ShipLayoutGenerator] Moved '{rs.name}' → {rt}/StationAnchor");
        }
    }

    // ── Room Builder ──────────────────────────────────────────────────────

    void BuildRoom(Transform parent, int r, int c, Vector3 center,
                   bool connN, bool connS, bool connE, bool connW,
                   Dictionary<ShipRoom.RoomType, Transform> anchors,
                   ref Transform[] bridgeSpawns)
    {
        var rt   = roomTypes[r, c];
        var go   = new GameObject(rt.ToString());
        go.transform.SetParent(parent);
        go.transform.position = center;

        // Floors (pick a random variant per room)
        BuildFloors(go.transform, center);

        // Walls
        if (wallPrefab != null)
            BuildWalls(go.transform, center, connN, connS, connE, connW);

        // Doors (colliders disabled so players pass through)
        if (doorPrefab != null)
            BuildDoors(go.transform, center, connN, connS, connE, connW);

        // ShipRoom component
        var shipRoom   = go.AddComponent<ShipRoom>();
        shipRoom.roomType = rt;

        // Station anchor
        if (rt != ShipRoom.RoomType.Bridge && rt != ShipRoom.RoomType.Corridor)
        {
            var anchor = new GameObject("StationAnchor");
            anchor.transform.SetParent(go.transform);
            anchor.transform.position = center;
            shipRoom.stationAnchor = anchor.transform;
            anchors[rt] = anchor.transform;

            PlaceDecor(go.transform, center);
        }

        // Bridge spawn points
        if (rt == ShipRoom.RoomType.Bridge)
        {
            var spawnRoot = new GameObject("SpawnPoints");
            spawnRoot.transform.SetParent(go.transform);
            spawnRoot.transform.position = center;

            var offsets = new Vector3[]
            {
                new Vector3( 2f, 1f,  2f),
                new Vector3(-2f, 1f,  2f),
                new Vector3( 2f, 1f, -2f),
                new Vector3(-2f, 1f, -2f),
            };
            string[] spawnNames = { "SpawnPoint_P1", "SpawnPoint_P2", "SpawnPoint_P3", "SpawnPoint_P4" };
            bridgeSpawns = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var sp = new GameObject(spawnNames[i]);
                sp.transform.SetParent(spawnRoot.transform);
                sp.transform.position = center + offsets[i];
                bridgeSpawns[i] = sp.transform;
            }
            shipRoom.spawnPoints = bridgeSpawns;
        }
    }

    // ── Floor / Wall / Door ───────────────────────────────────────────────

    void BuildFloors(Transform roomRoot, Vector3 center)
    {
        var fp = PickFloor();
        if (fp == null) return;
        var parent = new GameObject("Floors");
        parent.transform.SetParent(roomRoot);
        var offsets = new Vector3[]
        {
            new Vector3(-2.5f, 0f, -2.5f), new Vector3(-2.5f, 0f, 2.5f),
            new Vector3( 2.5f, 0f, -2.5f), new Vector3( 2.5f, 0f, 2.5f),
        };
        foreach (var o in offsets)
            Instantiate(fp, center + o, Quaternion.identity, parent.transform);
    }

    void BuildWalls(Transform roomRoot, Vector3 center,
                    bool connN, bool connS, bool connE, bool connW)
    {
        var parent = new GameObject("Walls");
        parent.transform.SetParent(roomRoot);
        float cx = center.x, cz = center.z;

        if (!connN)
        {
            Inst(wallPrefab, parent, new Vector3(cx - 2.5f, 0f, cz + 5f),   0f);
            Inst(wallPrefab, parent, new Vector3(cx + 2.5f, 0f, cz + 5f),   0f);
        }
        if (!connS)
        {
            Inst(wallPrefab, parent, new Vector3(cx - 2.5f, 0f, cz - 5f), 180f);
            Inst(wallPrefab, parent, new Vector3(cx + 2.5f, 0f, cz - 5f), 180f);
        }
        if (!connE)
        {
            Inst(wallPrefab, parent, new Vector3(cx + 5f, 0f, cz + 2.5f),  90f);
            Inst(wallPrefab, parent, new Vector3(cx + 5f, 0f, cz - 2.5f),  90f);
        }
        if (!connW)
        {
            Inst(wallPrefab, parent, new Vector3(cx - 5f, 0f, cz + 2.5f), 270f);
            Inst(wallPrefab, parent, new Vector3(cx - 5f, 0f, cz - 2.5f), 270f);
        }
    }

    void BuildDoors(Transform roomRoot, Vector3 center,
                    bool connN, bool connS, bool connE, bool connW)
    {
        var parent = new GameObject("Doors");
        parent.transform.SetParent(roomRoot);
        float cx = center.x, cz = center.z;

        if (connN) SpawnOpenDoor(parent, new Vector3(cx,      0f, cz + 5f),   0f);
        if (connS) SpawnOpenDoor(parent, new Vector3(cx,      0f, cz - 5f), 180f);
        if (connE) SpawnOpenDoor(parent, new Vector3(cx + 5f, 0f, cz      ),  90f);
        if (connW) SpawnOpenDoor(parent, new Vector3(cx - 5f, 0f, cz      ), 270f);
    }

    void SpawnOpenDoor(GameObject parent, Vector3 pos, float rotY)
    {
        var go = Instantiate(doorPrefab, pos, Quaternion.Euler(0f, rotY, 0f), parent.transform);
        // Disable all colliders so players walk through freely (visual door frame only)
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
    }

    void BuildCorridor(Transform parent, Vector3 pos, float rotY, string goName)
    {
        if (corridorPrefab_I == null) return;
        var go = Instantiate(corridorPrefab_I, pos, Quaternion.Euler(0f, rotY, 0f), parent);
        go.name = goName;
    }

    // ── Decorative Props ──────────────────────────────────────────────────

    void PlaceDecor(Transform roomRoot, Vector3 center)
    {
        var parent = new GameObject("Decor");
        parent.transform.SetParent(roomRoot);
        float cx = center.x, cz = center.z;

        // Console on east wall
        if (consolePrefabs != null && consolePrefabs.Length > 0)
        {
            var pfb = consolePrefabs[rng.Next(consolePrefabs.Length)];
            if (pfb != null)
                Instantiate(pfb, new Vector3(cx + 3f, 0f, cz), Quaternion.Euler(0f, 270f, 0f), parent.transform);
        }

        // Cabinet on west wall
        if (cabinetPrefabs != null && cabinetPrefabs.Length > 0)
        {
            var pfb = cabinetPrefabs[rng.Next(cabinetPrefabs.Length)];
            if (pfb != null)
                Instantiate(pfb, new Vector3(cx - 3f, 0f, cz), Quaternion.Euler(0f, 90f, 0f), parent.transform);
        }

        // Big screen on north or south wall (seed-driven)
        if (bigScreenPrefab != null)
        {
            float side = (rng.Next(2) == 0) ? 3f : -3f;
            float rot  = (side > 0f) ? 180f : 0f;
            Instantiate(bigScreenPrefab, new Vector3(cx, 0f, cz + side), Quaternion.Euler(0f, rot, 0f), parent.transform);
        }
    }

    // ── Grid Math ─────────────────────────────────────────────────────────

    static Vector3 RoomCenter(int r, int c)
        => new Vector3((c - 1) * 15f, 0f, -(r - 1) * 15f);

    // Corridor between col c and col c+1 at row r
    static Vector3 HCorridorPos(int r, int c)
        => new Vector3(((c - 1) * 15f + c * 15f) * 0.5f, 0f, -(r - 1) * 15f);

    // Corridor between row r and row r+1 at col c
    static Vector3 VCorridorPos(int r, int c)
        => new Vector3((c - 1) * 15f, 0f, (-(r - 1) * 15f + -r * 15f) * 0.5f);

    // ── BFS Path Finding ──────────────────────────────────────────────────

    static List<(int r, int c)> BFSPath(int startR, int startC, int endR, int endC)
    {
        var queue  = new Queue<(int, int)>();
        var parent = new Dictionary<(int, int), (int, int)>();
        var start  = (startR, startC);
        var end    = (endR,   endC);

        queue.Enqueue(start);
        parent[start] = (-1, -1);

        int[] dr = { -1,  1,  0, 0 };
        int[] dc = {  0,  0, -1, 1 };

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == end) break;
            for (int d = 0; d < 4; d++)
            {
                var next = (cur.Item1 + dr[d], cur.Item2 + dc[d]);
                if (next.Item1 < 0 || next.Item1 >= 3 || next.Item2 < 0 || next.Item2 >= 3) continue;
                if (parent.ContainsKey(next)) continue;
                parent[next] = cur;
                queue.Enqueue(next);
            }
        }

        var path = new List<(int, int)>();
        if (!parent.ContainsKey(end)) return path;

        var step = end;
        while (step != (-1, -1))
        {
            path.Add(step);
            step = parent[step];
        }
        path.Reverse();
        return path;
    }

    void MarkPath(List<(int r, int c)> path)
    {
        for (int i = 1; i < path.Count; i++)
        {
            var (r1, c1) = path[i - 1];
            var (r2, c2) = path[i];

            if (r1 == r2) // horizontal step → horizontal corridor edge
                hEdge[r1, Math.Min(c1, c2)] = true;
            else          // vertical step → vertical corridor edge
                vEdge[Math.Min(r1, r2), c1] = true;

            // Mark intermediate rooms (skip the station start and bridge end)
            if (!activeRooms[r2, c2])
            {
                activeRooms[r2, c2] = true;
                roomTypes[r2, c2]   = ShipRoom.RoomType.Corridor;
            }
        }
    }

    // ── Utilities ─────────────────────────────────────────────────────────

    GameObject PickFloor()
    {
        if (floorVariants == null || floorVariants.Length == 0) return null;
        return floorVariants[rng.Next(floorVariants.Length)];
    }

    static void Inst(GameObject pfb, GameObject parent, Vector3 pos, float rotY)
        => UnityEngine.Object.Instantiate(pfb, pos, Quaternion.Euler(0f, rotY, 0f), parent.transform);

    List<T> Shuffle<T>(List<T> list)
    {
        var result = new List<T>(list);
        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
    }
}
