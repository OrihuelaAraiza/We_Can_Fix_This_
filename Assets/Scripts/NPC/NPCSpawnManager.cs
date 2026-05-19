using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wcft.Core;

/// <summary>
/// Spawnea NPCs basándose en el mapa generado por ShipLayoutGenerator.
///
/// - Blockie / Smoggos se distribuyen en habitaciones de reparación.
/// - Clank se spawnea en Storage o Bridge.
/// - Se crean BoxSpawners y DropZones en módulos diferentes de la nave.
/// - Ningún BoxSpawner se genera en el mismo módulo que una DropZone.
/// - Los Blockies tienen límite total para que no se generen demasiados.
/// </summary>
public class NPCSpawnManager : MonoBehaviour
{
    [Header("NPCs")]
    [SerializeField] private GameObject[] blockiePrefabs;
    [SerializeField] private GameObject[] smoggosPrefabs;
    [SerializeField] private GameObject clankPrefab;

    [Header("Clank — Box System")]
    [SerializeField] private GameObject boxPrefab;
    [SerializeField] private float boxSpawnInterval = 25f;
    [SerializeField] private int maxBoxes = 8;
    [SerializeField] private float dropZoneRadius = 1.5f;

    [Header("BoxSpawner / DropZone Amount")]
    [SerializeField] private int boxSpawnerCount = 2;
    [SerializeField] private int dropZoneCount = 2;

    [Header("Blockie Limit")]
    [SerializeField] private int maxBlockiesTotal = 2;

    [Header("Spawn Config")]
    [Tooltip("Minimum spacing between NPCs of the same type.")]
    [SerializeField] private float minSpacing = 3f;

    [Tooltip("Distancia mínima entre un BoxSpawner y una DropZone.")]
    [SerializeField] private float minDistanceBetweenBoxAndDrop = 8f;

    private static readonly string[] RepairRoomKeys =
    {
        "Repair_Energy",
        "Repair_Communications",
        "Repair_Gravity",
        "Repair_Hull"
    };

    private const string StorageKey = "Extra_Storage";

    private IReadOnlyDictionary<string, Vector3> pendingCenters;
    private bool spawned;

    private void OnEnable()
    {
        RuntimeShipNavMesh.EnsureExists();

        if (ShipLayoutGenerator.IsReady)
            SpawnAll(ShipLayoutGenerator.RoomCenters);
        else
            ShipLayoutGenerator.OnRoomCentersReady += SpawnAll;
    }

    private void OnDisable()
    {
        ShipLayoutGenerator.OnRoomCentersReady -= SpawnAll;
        RuntimeShipNavMesh.OnReady -= SpawnPending;
    }

    private void SpawnAll(IReadOnlyDictionary<string, Vector3> centers)
    {
        ShipLayoutGenerator.OnRoomCentersReady -= SpawnAll;

        if (spawned)
            return;

        pendingCenters = centers;

        if (!RuntimeShipNavMesh.IsReady && RequiresNavMesh())
        {
            RuntimeShipNavMesh.OnReady -= SpawnPending;
            RuntimeShipNavMesh.OnReady += SpawnPending;
            return;
        }

        SpawnPending();
    }

    private void SpawnPending()
    {
        RuntimeShipNavMesh.OnReady -= SpawnPending;

        if (spawned || pendingCenters == null)
            return;

        spawned = true;

        var centers = pendingCenters;
        pendingCenters = null;

        SpawnRoamingNPCs(centers);
        SpawnClankAndDistributedBoxSystem(centers);
    }

    // ── Blockie y Smoggos ─────────────────────────────────────────────

    private void SpawnRoamingNPCs(IReadOnlyDictionary<string, Vector3> centers)
    {
        var positions = new List<Vector3>();

        int spawnMultiplier = Mathf.Max(1, Mathf.RoundToInt(LevelProgression.Current.NpcMultiplier));

        int blockiesSpawned = 0;
        int smoggosSpawned = 0;

        foreach (var key in RepairRoomKeys)
        {
            if (!centers.TryGetValue(key, out Vector3 center))
                continue;

            // ── BLOCKIES CON LÍMITE TOTAL ─────────────────────────
            for (int i = 0; i < spawnMultiplier && blockiePrefabs != null && blockiePrefabs.Length > 0; i++)
            {
                if (blockiesSpawned >= maxBlockiesTotal)
                    break;

                GameObject prefab = blockiePrefabs[Random.Range(0, blockiePrefabs.Length)];

                if (prefab != null)
                {
                    Vector3 pos = FindFreeSpot(center, positions, minSpacing);
                    pos = NavMeshSpawnUtility.ResolvePosition(pos);

                    GameObject blockie = Instantiate(prefab, pos, RandomYRotation());
                    blockie.name = $"Blockie_{blockiesSpawned + 1}";

                    positions.Add(pos);
                    blockiesSpawned++;
                }
            }

            // ── SMOGGOS SE QUEDAN COMO ESTABAN ─────────────────────
            for (int i = 0; i < spawnMultiplier && smoggosPrefabs != null && smoggosPrefabs.Length > 0; i++)
            {
                GameObject prefab = smoggosPrefabs[Random.Range(0, smoggosPrefabs.Length)];

                if (prefab != null)
                {
                    Vector3 pos = FindFreeSpot(center, positions, minSpacing);
                    pos = NavMeshSpawnUtility.ResolvePosition(pos);

                    GameObject smoggo = Instantiate(prefab, pos, RandomYRotation());
                    smoggo.name = $"Smoggo_{smoggosSpawned + 1}";

                    positions.Add(pos);
                    smoggosSpawned++;
                }
            }
        }

        Debug.Log($"[NPCSpawnManager] {blockiesSpawned} Blockies spawned. {smoggosSpawned} Smoggos spawned.");
    }

    // ── Clank + BoxSpawners + DropZones distribuidos ─────────────────

    private void SpawnClankAndDistributedBoxSystem(IReadOnlyDictionary<string, Vector3> centers)
    {
        if (centers == null || centers.Count == 0)
        {
            Debug.LogWarning("[NPCSpawnManager] No room centers found.");
            return;
        }

        Vector3 clankCenter = GetClankSpawnCenter(centers);

        List<RoomPoint> roomPoints = GetRoomPoints(centers);

        if (roomPoints.Count < 4)
        {
            Debug.LogWarning("[NPCSpawnManager] Not enough rooms to distribute BoxSpawners and DropZones. Using available rooms anyway.");
        }

        List<RoomPoint> boxRooms = PickSpreadRooms(roomPoints, boxSpawnerCount, null);
        List<RoomPoint> dropRooms = PickSpreadRoomsFarFromBoxRooms(roomPoints, dropZoneCount, boxRooms);

        List<Transform> dropZones = CreateDropZones(dropRooms);
        List<Transform> boxSpawnPoints = CreateBoxSpawners(boxRooms);

        SpawnClank(clankCenter, dropZones);

        Debug.Log($"[NPCSpawnManager] Created {boxSpawnPoints.Count} BoxSpawners and {dropZones.Count} DropZones in different modules.");
    }

    private Vector3 GetClankSpawnCenter(IReadOnlyDictionary<string, Vector3> centers)
    {
        if (centers.TryGetValue(StorageKey, out Vector3 storageCenter))
            return storageCenter;

        if (centers.TryGetValue("Bridge", out Vector3 bridgeCenter))
        {
            Debug.LogWarning("[NPCSpawnManager] Extra_Storage not found; Clank will use Bridge.");
            return bridgeCenter;
        }

        foreach (var pair in centers)
            return pair.Value;

        return Vector3.zero;
    }

    private List<RoomPoint> GetRoomPoints(IReadOnlyDictionary<string, Vector3> centers)
    {
        List<RoomPoint> rooms = new List<RoomPoint>();

        foreach (var pair in centers)
        {
            rooms.Add(new RoomPoint(pair.Key, pair.Value));
        }

        return rooms;
    }

    private List<RoomPoint> PickSpreadRooms(List<RoomPoint> rooms, int amount, List<RoomPoint> forbiddenRooms)
    {
        List<RoomPoint> selected = new List<RoomPoint>();

        int targetAmount = Mathf.Max(1, amount);

        foreach (RoomPoint room in rooms)
        {
            if (IsForbiddenRoom(room, forbiddenRooms))
                continue;

            selected.Add(room);
            break;
        }

        while (selected.Count < targetAmount)
        {
            RoomPoint bestRoom = null;
            float bestDistance = -1f;

            foreach (RoomPoint candidate in rooms)
            {
                if (ContainsRoom(selected, candidate))
                    continue;

                if (IsForbiddenRoom(candidate, forbiddenRooms))
                    continue;

                float nearestDistance = GetNearestDistance(candidate.position, selected);

                if (nearestDistance > bestDistance)
                {
                    bestDistance = nearestDistance;
                    bestRoom = candidate;
                }
            }

            if (bestRoom == null)
                break;

            selected.Add(bestRoom);
        }

        return selected;
    }

    private List<RoomPoint> PickSpreadRoomsFarFromBoxRooms(List<RoomPoint> rooms, int amount, List<RoomPoint> boxRooms)
    {
        List<RoomPoint> selected = new List<RoomPoint>();

        int targetAmount = Mathf.Max(1, amount);

        while (selected.Count < targetAmount)
        {
            RoomPoint bestRoom = null;
            float bestScore = -1f;

            foreach (RoomPoint candidate in rooms)
            {
                if (ContainsRoom(selected, candidate))
                    continue;

                if (ContainsRoom(boxRooms, candidate))
                    continue;

                float distanceToNearestBox = GetNearestDistance(candidate.position, boxRooms);

                if (distanceToNearestBox < minDistanceBetweenBoxAndDrop)
                    continue;

                float distanceToSelectedDrops = selected.Count > 0
                    ? GetNearestDistance(candidate.position, selected)
                    : distanceToNearestBox;

                float score = distanceToNearestBox + distanceToSelectedDrops;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestRoom = candidate;
                }
            }

            if (bestRoom == null)
            {
                foreach (RoomPoint candidate in rooms)
                {
                    if (ContainsRoom(selected, candidate))
                        continue;

                    if (ContainsRoom(boxRooms, candidate))
                        continue;

                    float distanceToNearestBox = GetNearestDistance(candidate.position, boxRooms);

                    if (distanceToNearestBox > bestScore)
                    {
                        bestScore = distanceToNearestBox;
                        bestRoom = candidate;
                    }
                }
            }

            if (bestRoom == null)
                break;

            selected.Add(bestRoom);
        }

        return selected;
    }

    private List<Transform> CreateBoxSpawners(List<RoomPoint> boxRooms)
    {
        List<Transform> boxSpawnPoints = new List<Transform>();

        for (int i = 0; i < boxRooms.Count; i++)
        {
            Vector3 boxPos = ResolveClankPoint(boxRooms[i].position);

            Transform boxSpawnPoint = CreateAnchor($"BoxSpawnPoint_{i + 1}_{boxRooms[i].key}", boxPos);
            boxSpawnPoints.Add(boxSpawnPoint);

            GameObject boxSpawnerGO = new GameObject($"Clank_BoxSpawner_{i + 1}_{boxRooms[i].key}");
            boxSpawnerGO.transform.position = boxPos;

            BoxSpawner spawner = boxSpawnerGO.AddComponent<BoxSpawner>();
            spawner.boxPrefab = boxPrefab;
            spawner.spawnPoints = new Transform[] { boxSpawnPoint };
            spawner.spawnIntervalSeconds = LevelProgression.Current.ClankBoxIntervalSeconds > 0f
                ? LevelProgression.Current.ClankBoxIntervalSeconds
                : boxSpawnInterval;
            spawner.maxBoxesAlive = maxBoxes;
            spawner.spawnOnStart = true;
        }

        return boxSpawnPoints;
    }

    private List<Transform> CreateDropZones(List<RoomPoint> dropRooms)
    {
        List<Transform> dropZones = new List<Transform>();

        for (int i = 0; i < dropRooms.Count; i++)
        {
            Vector3 dropPos = ResolveClankPoint(dropRooms[i].position);

            GameObject dropZoneGO = new GameObject($"Clank_DropZone_{i + 1}_{dropRooms[i].key}");
            dropZoneGO.transform.position = dropPos;

            dropZoneGO.AddComponent<DropZone>();

            BoxCollider col = dropZoneGO.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(dropZoneRadius * 2f, 2f, dropZoneRadius * 2f);

            dropZones.Add(dropZoneGO.transform);
        }

        return dropZones;
    }

    private void SpawnClank(Vector3 center, List<Transform> dropZones)
    {
        if (clankPrefab == null)
            return;

        Vector3 clankPos = ResolveClankPoint(center);

        GameObject clankGO = Instantiate(clankPrefab, clankPos, Quaternion.identity);
        clankGO.name = "Clank";

        Clank_NPC clank = clankGO.GetComponent<Clank_NPC>();

        if (clank != null)
        {
            if (dropZones != null && dropZones.Count > 0)
                clank.dropPoint = dropZones[0];

            if (clank.holdPoint == null)
                clank.holdPoint = clankGO.transform;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private Transform CreateAnchor(string name, Vector3 worldPos)
    {
        GameObject go = new GameObject(name);
        go.transform.position = worldPos;
        return go.transform;
    }

    private Vector3 FindFreeSpot(Vector3 center, List<Vector3> occupied, float minDist)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 rand = Random.insideUnitCircle * minDist;
            Vector3 candidate = center + new Vector3(rand.x, 0f, rand.y);

            bool tooClose = false;

            foreach (var p in occupied)
            {
                if (Vector3.Distance(candidate, p) < minDist)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
                return candidate;
        }

        return center;
    }

    private float GetNearestDistance(Vector3 position, List<RoomPoint> points)
    {
        if (points == null || points.Count == 0)
            return 9999f;

        float nearest = float.MaxValue;

        foreach (RoomPoint point in points)
        {
            float distance = Vector3.Distance(position, point.position);

            if (distance < nearest)
                nearest = distance;
        }

        return nearest;
    }

    private bool ContainsRoom(List<RoomPoint> rooms, RoomPoint target)
    {
        if (rooms == null)
            return false;

        foreach (RoomPoint room in rooms)
        {
            if (room.key == target.key)
                return true;
        }

        return false;
    }

    private bool IsForbiddenRoom(RoomPoint room, List<RoomPoint> forbiddenRooms)
    {
        if (forbiddenRooms == null)
            return false;

        foreach (RoomPoint forbidden in forbiddenRooms)
        {
            if (forbidden.key == room.key)
                return true;
        }

        return false;
    }

    private static Quaternion RandomYRotation()
    {
        return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    private bool RequiresNavMesh()
    {
        if (HasAnyPrefab(blockiePrefabs) || HasAnyPrefab(smoggosPrefabs))
            return true;

        if (clankPrefab != null && clankPrefab.GetComponent<UnityEngine.AI.NavMeshAgent>() != null)
            return true;

        return PrefabsRequireNavMesh(blockiePrefabs) || PrefabsRequireNavMesh(smoggosPrefabs);
    }

    private static bool HasAnyPrefab(GameObject[] prefabs)
    {
        if (prefabs == null)
            return false;

        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null)
                return true;
        }

        return false;
    }

    private static bool PrefabsRequireNavMesh(GameObject[] prefabs)
    {
        if (prefabs == null)
            return false;

        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null && prefab.GetComponent<UnityEngine.AI.NavMeshAgent>() != null)
                return true;
        }

        return false;
    }

    private static Vector3 ResolveClankPoint(Vector3 requestedPosition)
    {
        if (NavMeshSpawnUtility.TrySamplePosition(requestedPosition, out Vector3 navMeshPosition, 6f))
            return navMeshPosition;

        Debug.LogWarning($"[NPCSpawnManager] Could not project {requestedPosition} to NavMesh; using requested position.");
        return requestedPosition;
    }

    private class RoomPoint
    {
        public string key;
        public Vector3 position;

        public RoomPoint(string key, Vector3 position)
        {
            this.key = key;
            this.position = position;
        }
    }
}