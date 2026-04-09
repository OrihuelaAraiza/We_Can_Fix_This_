using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawnea los NPCs basándose en el mapa generado por ShipLayoutGenerator.
///
/// - Blockie / Smoggos → se distribuyen en las habitaciones de reparación del ship.
/// - Clank            → se spawnea en la Storage room.
///                      Se crea automáticamente un BoxSpawner (zona de recolección)
///                      y un DropZone (zona de entrega) dentro de la misma habitación.
///
/// Adjunta este componente al mismo GameObject que ShipLayoutGenerator (o cualquiera
/// que exista en la escena). Asigna los prefabs desde el Inspector.
/// </summary>
public class NPCSpawnManager : MonoBehaviour
{
    [Header("NPCs")]
    [SerializeField] private GameObject[] blockiePrefabs;
    [SerializeField] private GameObject[] smoggosPrefabs;
    [SerializeField] private GameObject   clankPrefab;

    [Header("Clank — Box System")]
    [SerializeField] private GameObject boxPrefab;
    [SerializeField] private float      boxSpawnInterval  = 25f;
    [SerializeField] private int        maxBoxes          = 8;
    [SerializeField] private float      dropZoneRadius    = 1.5f;

    [Header("Spawn Config")]
    [Tooltip("Separación mínima entre NPCs del mismo tipo (en unidades).")]
    [SerializeField] private float minSpacing = 3f;

    // Rooms de reparación en las que se spawnean Blockie/Smoggos
    private static readonly string[] RepairRoomKeys = {
        "Repair_Energy", "Repair_Communications", "Repair_Gravity", "Repair_Hull"
    };

    private const string StorageKey = "Extra_Storage";

    private void OnEnable()
    {
        if (ShipLayoutGenerator.IsReady)
            SpawnAll(ShipLayoutGenerator.RoomCenters);
        else
            ShipLayoutGenerator.OnRoomCentersReady += SpawnAll;
    }

    private void OnDisable()
    {
        ShipLayoutGenerator.OnRoomCentersReady -= SpawnAll;
    }

    private void SpawnAll(IReadOnlyDictionary<string, Vector3> centers)
    {
        ShipLayoutGenerator.OnRoomCentersReady -= SpawnAll;

        SpawnRoamingNPCs(centers);
        SpawnClank(centers);
    }

    // ── Blockie y Smoggos ─────────────────────────────────────────────

    private void SpawnRoamingNPCs(IReadOnlyDictionary<string, Vector3> centers)
    {
        var positions = new List<Vector3>();

        foreach (var key in RepairRoomKeys)
        {
            if (!centers.TryGetValue(key, out Vector3 center)) continue;

            // Un Blockie por sala de reparación (si hay prefab asignado)
            if (blockiePrefabs != null && blockiePrefabs.Length > 0)
            {
                var prefab = blockiePrefabs[Random.Range(0, blockiePrefabs.Length)];
                if (prefab != null)
                {
                    Vector3 pos = FindFreeSpot(center, positions, minSpacing);
                    Instantiate(prefab, pos, RandomYRotation());
                    positions.Add(pos);
                }
            }

            // Un Smoggos por sala de reparación (si hay prefab asignado)
            if (smoggosPrefabs != null && smoggosPrefabs.Length > 0)
            {
                var prefab = smoggosPrefabs[Random.Range(0, smoggosPrefabs.Length)];
                if (prefab != null)
                {
                    Vector3 pos = FindFreeSpot(center, positions, minSpacing);
                    Instantiate(prefab, pos, RandomYRotation());
                    positions.Add(pos);
                }
            }
        }

        Debug.Log($"[NPCSpawnManager] {positions.Count} NPCs deambulantes spawneados.");
    }

    // ── Clank ─────────────────────────────────────────────────────────

    private void SpawnClank(IReadOnlyDictionary<string, Vector3> centers)
    {
        if (clankPrefab == null) return;

        // Si no hay Storage room, usar Bridge como fallback
        if (!centers.TryGetValue(StorageKey, out Vector3 storageCenter))
        {
            if (!centers.TryGetValue("Bridge", out storageCenter))
            {
                Debug.LogWarning("[NPCSpawnManager] No se encontró Storage ni Bridge para Clank.");
                return;
            }
            Debug.LogWarning("[NPCSpawnManager] Extra_Storage no existe en el mapa; Clank usará Bridge.");
        }

        // ── Zona de recolección (BoxSpawner) ──────────────────────────
        // Se ubica en el centro de la Storage room; las cajas aparecerán aquí.
        Transform boxSpawnPoint = CreateAnchor("BoxSpawnPoint", storageCenter + new Vector3(-2f, 0f, 0f));

        GameObject boxSpawnerGO = new GameObject("Clank_BoxSpawner");
        boxSpawnerGO.transform.position = storageCenter;
        var spawner = boxSpawnerGO.AddComponent<BoxSpawner>();
        spawner.boxPrefab             = boxPrefab;
        spawner.spawnPoints           = new Transform[] { boxSpawnPoint };
        spawner.spawnIntervalSeconds  = boxSpawnInterval;
        spawner.maxBoxesAlive         = maxBoxes;
        spawner.spawnOnStart          = true;

        // ── Zona de entrega (DropZone) ────────────────────────────────
        // Se ubica al otro lado de la habitación para que Clank haga el recorrido.
        Vector3 dropPos = storageCenter + new Vector3(2f, 0f, 0f);
        GameObject dropZoneGO = new GameObject("Clank_DropZone");
        dropZoneGO.transform.position = dropPos;

        var dropZone = dropZoneGO.AddComponent<DropZone>();

        // Collider trigger para que DropZone funcione
        var col = dropZoneGO.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size      = new Vector3(dropZoneRadius * 2f, 2f, dropZoneRadius * 2f);

        // ── Clank ─────────────────────────────────────────────────────
        Vector3 clankPos = storageCenter + new Vector3(0f, 0f, -1f);
        var clankGO = Instantiate(clankPrefab, clankPos, Quaternion.identity);
        clankGO.name = "Clank";

        var clank = clankGO.GetComponent<Clank_NPC>();
        if (clank != null)
        {
            clank.dropPoint = dropZoneGO.transform;
            // holdPoint se asigna desde el prefab; si no existe, usar el propio transform
            if (clank.holdPoint == null)
                clank.holdPoint = clankGO.transform;
        }

        Debug.Log($"[NPCSpawnManager] Clank spawneado en Storage ({storageCenter}).");
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private Transform CreateAnchor(string name, Vector3 worldPos)
    {
        var go = new GameObject(name);
        go.transform.position = worldPos;
        return go.transform;
    }

    private Vector3 FindFreeSpot(Vector3 center, List<Vector3> occupied, float minDist)
    {
        // Intentar hasta 8 offsets aleatorios; si ninguno sirve, devolver el centro
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

            if (!tooClose) return candidate;
        }

        return center;
    }

    private static Quaternion RandomYRotation() =>
        Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
}
