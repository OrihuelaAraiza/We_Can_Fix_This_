using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Core-X: Adaptive AI director for We Can Fix This!
/// Analyzes player patterns and scales difficulty by phase.
/// Compatible with FailureSystem (keeps SelectStationToBreak / RegisterRepair).
/// </summary>
public class CoreXBrain : MonoBehaviour
{
    public static CoreXBrain Instance { get; private set; }

    // ── Eventos estáticos ──────────────────────────────────────
    public static event System.Action<RepairStation> OnTargetSelected;
    public static event System.Action<int>           OnPhaseChanged;
    public static event System.Action                OnBossModeActivated;
    public static event System.Action                OnCoreXDefeated;

    // ── Config inspector ───────────────────────────────────────
    [Header("Phases")]
    [SerializeField] private CoreXPhase[] phases;

    [Header("Player Analysis")]
    [SerializeField] private float analysisInterval  = 10f;
    [SerializeField] private float patternMemoryTime = 30f;

    [Header("Boss Mode")]
    [SerializeField] private float bossHealthThreshold = 0.2f;
    [SerializeField] private float bossActivationDelay = 3f;

    [Header("Legacy compat - Debug")]
    [SerializeField] private float aggressionLevel;
    [SerializeField] private string lastDecision;

    // ── Estado interno ─────────────────────────────────────────
    private int   currentPhaseIndex = 0;
    private bool  bossActivated     = false;
    private bool  coreXDefeated     = false;
    private float phaseTimer        = 0f;
    private float analysisTimer     = 0f;

    private RepairStation[] allStations;
    private Dictionary<RepairStation, int> repairFrequency = new();
    private Dictionary<RepairStation, int> breakFrequency  = new();

    // Repair stats for scaling
    private float repairsPerMinute    = 0f;
    private int   totalRepairs        = 0;
    private int   consecutiveRepairs  = 0;
    private float gameStartTime;

    // NPCs spawned by CoreX
    private readonly List<GameObject> spawnedNPCs = new();

    // ── Properties públicas ───────────────────────────────────
    public float AggressionLevel => aggressionLevel;
    public int   CurrentPhase    => currentPhaseIndex;
    public bool  BossActivated   => bossActivated;

    // ── Lifecycle ─────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        ShipHealth.OnHealthChanged  += HandleHealthChanged;
        ShipHealth.OnShipDestroyed  += HandleShipDestroyed;
        FailureSystem.OnStationRepaired += HandleStationRepaired;
        FailureSystem.OnStationFailed   += HandleStationFailed;
    }

    private void OnDisable()
    {
        ShipHealth.OnHealthChanged  -= HandleHealthChanged;
        ShipHealth.OnShipDestroyed  -= HandleShipDestroyed;
        FailureSystem.OnStationRepaired -= HandleStationRepaired;
        FailureSystem.OnStationFailed   -= HandleStationFailed;
    }

    private void Start()
    {
        RuntimeShipNavMesh.EnsureExists();

        allStations    = GetRegisteredStations();
        gameStartTime  = Time.time;

        if (phases != null && phases.Length > 0)
            EnterPhase(0);

        StartCoroutine(BrainTick());
        Debug.Log($"[CoreX] Started with {allStations.Length} stations and {phases?.Length ?? 0} phases");
    }

    static RepairStation[] GetRegisteredStations()
    {
        var stations = new List<RepairStation>();
        foreach (RepairStation station in RepairStation.ActiveStations)
        {
            if (station != null)
                stations.Add(station);
        }

        if (stations.Count > 0)
            return stations.ToArray();

#pragma warning disable CS0618
        return FindObjectsOfType<RepairStation>();
#pragma warning restore CS0618
    }

    // ── Main tick (simplified Behavior Tree) ──────────────────
    private IEnumerator BrainTick()
    {
        while (!coreXDefeated)
        {
            float interval = CurrentPhaseData?.tickInterval ?? 8f;
            yield return new WaitForSeconds(interval);

            if (coreXDefeated) yield break;

            phaseTimer   += interval;
            analysisTimer += interval;

            // Update aggression
            aggressionLevel = Mathf.Min(1f, aggressionLevel + interval * 0.003f);

            // Analyze patterns periodically
            if (analysisTimer >= analysisInterval)
            {
                analysisTimer = 0f;
                AnalyzePlayerPatterns();
            }

            // Advance to next phase if time has elapsed
            float phaseDuration = CurrentPhaseData?.phaseDuration ?? 120f;
            if (phaseTimer >= phaseDuration && currentPhaseIndex < (phases?.Length ?? 1) - 1)
            {
                phaseTimer = 0f;
                EnterPhase(currentPhaseIndex + 1);
            }

            // Additional strategic sabotage (on top of FailureSystem)
            float sabotageInterval = CurrentPhaseData?.sabotageInterval ?? 15f;
            if (phaseTimer % sabotageInterval < interval)
                TriggerStrategicSabotage();
        }
    }

    // ── Phases ────────────────────────────────────────────────
    private void EnterPhase(int index)
    {
        if (phases == null || index >= phases.Length) return;

        currentPhaseIndex = index;
        var phase = phases[index];

        if (FailureSystem.Instance != null)
            FailureSystem.Instance.SetFailureRate(phase.failureRate);

        // Replace NPCs from the previous phase with those from the new phase
        DespawnAllNPCs();
        SpawnPhaseNPCs(phase);

        OnPhaseChanged?.Invoke(index);
        Debug.Log($"[CoreX] Phase {index} ({phase.phaseName}) activated | failures/min={phase.failureRate}");
    }

    // ── NPC Spawning ──────────────────────────────────────────
    private void SpawnPhaseNPCs(CoreXPhase phase)
    {
        if (!phase.canDeployNPCs) return;
        if (phase.npcPrefabs == null || phase.npcPrefabs.Length == 0)
        {
            Debug.LogWarning("[CoreX] canDeployNPCs=true but npcPrefabs is empty.");
            return;
        }

        RuntimeShipNavMesh.EnsureExists();

        Vector3[] spawnPositions = GatherSpawnPositions(phase);
        if (spawnPositions == null || spawnPositions.Length == 0)
        {
            Debug.LogWarning("[CoreX] No spawn points available for NPCs.");
            return;
        }

        for (int i = 0; i < spawnPositions.Length; i++)
        {
            var prefab = phase.npcPrefabs[i % phase.npcPrefabs.Length];
            if (prefab == null) continue;

            Vector3 spawnPosition = NavMeshSpawnUtility.ResolvePosition(spawnPositions[i], 5f);
            var npc = Instantiate(prefab, spawnPosition, Quaternion.identity);
            NavMeshAgent agent = npc.GetComponent<NavMeshAgent>();
            if (agent != null)
                NavMeshSpawnUtility.TryWarpToNearest(agent, spawnPosition, 5f);

            spawnedNPCs.Add(npc);
        }

        Debug.Log($"[CoreX] {spawnedNPCs.Count} NPCs spawned for phase {currentPhaseIndex}.");
    }

    private void DespawnAllNPCs()
    {
        foreach (var npc in spawnedNPCs)
            if (npc != null) Destroy(npc);

        spawnedNPCs.Clear();
    }

    private Vector3[] GatherSpawnPositions(CoreXPhase phase)
    {
        if (phase.npcSpawnPoints != null && phase.npcSpawnPoints.Length > 0)
        {
            var configured = new List<Vector3>();
            foreach (Transform spawnPoint in phase.npcSpawnPoints)
            {
                if (spawnPoint != null)
                    configured.Add(spawnPoint.position);
            }

            if (configured.Count > 0)
                return configured.ToArray();
        }

        return GatherShipSpawnPositions();
    }

    /// <summary>
    /// Recolecta spawn points como fallback:
    /// primero de ShipRoom, luego de ShipLayoutGenerator y por último de ShipAssembler.
    /// </summary>
    private Vector3[] GatherShipSpawnPositions()
    {
        var points = new List<Vector3>();

        // Intentar desde ShipRoom
        foreach (var room in FindObjectsOfType<ShipRoom>())
        {
            if (room.spawnPoints == null) continue;
            foreach (var sp in room.spawnPoints)
                if (sp != null) points.Add(sp.position);
        }

        if (points.Count == 0 && ShipLayoutGenerator.RoomCenters != null)
        {
            foreach (Vector3 center in ShipLayoutGenerator.RoomCenters.Values)
                points.Add(center);
        }

        // Si ShipRoom no tiene puntos, usar los generados por ShipAssembler
        if (points.Count == 0 && ShipAssembler.NPCSpawnPoints != null)
        {
            foreach (var sp in ShipAssembler.NPCSpawnPoints)
                if (sp != null) points.Add(sp.position);
        }

        return points.ToArray();
    }

    private CoreXPhase CurrentPhaseData =>
        (phases != null && currentPhaseIndex < phases.Length) ? phases[currentPhaseIndex] : null;

    // ── Pattern analysis ──────────────────────────────────────
    private void AnalyzePlayerPatterns()
    {
        var players = FindObjectsOfType<PlayerMovement>();
        if (players.Length == 0) return;

        // Actualizar reparaciones/minuto
        float elapsed    = Time.time - gameStartTime;
        repairsPerMinute = elapsed > 0 ? (totalRepairs / elapsed) * 60f : 0f;

        // Si los jugadores reparan muy rápido, subir agresión
        float threshold = CurrentPhaseData?.repairRateThreshold ?? 3f;
        if (repairsPerMinute > threshold)
        {
            aggressionLevel = Mathf.Min(1f, aggressionLevel + 0.1f);
            Debug.Log($"[CoreX] Efficient players ({repairsPerMinute:F1}/min) — raising aggression");
        }

        // Si hubo muchas reparaciones seguidas, contraatacar
        int trigger = CurrentPhaseData?.consecutiveRepairsTrigger ?? 3;
        if (consecutiveRepairs >= trigger)
        {
            consecutiveRepairs = 0;
            TriggerStrategicSabotage();
            Debug.Log("[CoreX] Pattern detected: counterattack activated");
        }
    }

    // ── Strategic sabotage ────────────────────────────────────
    private void TriggerStrategicSabotage()
    {
        if (FailureSystem.Instance == null) return;

        var candidates = System.Array.FindAll(allStations, s =>
            s.State == RepairStation.StationState.Functional ||
            s.State == RepairStation.StationState.Fixed);

        if (candidates.Length == 0) return;

        var target = SelectByStrategy(new List<RepairStation>(candidates));
        if (target == null) return;

        FailureSystem.Instance.ForceFailStation(target);
        OnTargetSelected?.Invoke(target);
        Debug.Log($"[CoreX] Strategic sabotage → {target.Type}");
    }

    // ── Boss mode ─────────────────────────────────────────────
    private void HandleHealthChanged(float healthPercent)
    {
        if (bossActivated || coreXDefeated) return;
        if (healthPercent <= bossHealthThreshold)
            StartCoroutine(ActivateBossMode());
    }

    private IEnumerator ActivateBossMode()
    {
        bossActivated = true;
        Debug.Log("[CoreX] BOSS MODE — activating in " + bossActivationDelay + "s");
        yield return new WaitForSeconds(bossActivationDelay);

        OnBossModeActivated?.Invoke();

        // En modo jefe: romper todo simultáneamente
        StartCoroutine(BossTick());
    }

    private IEnumerator BossTick()
    {
        while (bossActivated && !coreXDefeated)
        {
            yield return new WaitForSeconds(5f);
            if (FailureSystem.Instance == null) yield break;

            foreach (var station in allStations)
            {
                if (station.State == RepairStation.StationState.Functional ||
                    station.State == RepairStation.StationState.Fixed)
                    FailureSystem.Instance.ForceFailStation(station);
            }
            Debug.Log("[CoreX] Boss tick — breaking all stations");
        }
    }

    // ── Deactivation (victory) ────────────────────────────────
    public void Deactivate()
    {
        if (coreXDefeated) return;
        coreXDefeated = true;
        bossActivated = false;
        StopAllCoroutines();
        DespawnAllNPCs();
        OnCoreXDefeated?.Invoke();
        GameManager.Instance?.TriggerVictory();
        Debug.Log("[CoreX] Deactivated — players won!");
    }

    // ── Event handlers ────────────────────────────────────────
    private void HandleShipDestroyed()
    {
        coreXDefeated = true; // detener ticks
        StopAllCoroutines();
    }

    private void HandleStationRepaired(RepairStation station)
    {
        RegisterRepair(station);
        consecutiveRepairs++;
    }

    private void HandleStationFailed(RepairStation station)
    {
        consecutiveRepairs = 0;
        if (!breakFrequency.ContainsKey(station)) breakFrequency[station] = 0;
        breakFrequency[station]++;
    }

    // ── Public API (FailureSystem compatibility) ───────────────

    /// <summary>Called by FailureSystem to choose which station to break.</summary>
    public RepairStation SelectStationToBreak(List<RepairStation> stations)
    {
        var candidates = stations.FindAll(s =>
            s.State == RepairStation.StationState.Functional ||
            s.State == RepairStation.StationState.Fixed);

        if (candidates.Count == 0) return null;

        var chosen = SelectByStrategy(candidates);
        if (chosen == null) return null;

        if (!breakFrequency.ContainsKey(chosen)) breakFrequency[chosen] = 0;
        breakFrequency[chosen]++;

        Debug.Log($"[CoreX] SelectStationToBreak → {chosen.Type} | Aggression: {aggressionLevel:F2}");
        OnTargetSelected?.Invoke(chosen);
        return chosen;
    }

    /// <summary>Called by FailureSystem when a station is repaired.</summary>
    public void RegisterRepair(RepairStation station)
    {
        if (!repairFrequency.ContainsKey(station)) repairFrequency[station] = 0;
        repairFrequency[station]++;
        totalRepairs++;
    }

    // ── Selection strategies ───────────────────────────────────
    private RepairStation SelectByStrategy(List<RepairStation> candidates)
    {
        if (aggressionLevel < 0.3f)
        {
            lastDecision = "Farthest from players";
            return SelectFarthestFromPlayers(candidates);
        }
        else if (aggressionLevel < 0.6f)
        {
            lastDecision = "Recently repaired";
            return SelectMostRecentlyRepaired(candidates);
        }
        else
        {
            lastDecision = "Priority target";
            return SelectByPriority(candidates);
        }
    }

    private RepairStation SelectFarthestFromPlayers(List<RepairStation> candidates)
    {
        var players = FindObjectsOfType<PlayerMovement>();
        if (players.Length == 0)
            return candidates[Random.Range(0, candidates.Count)];

        RepairStation farthest = null;
        float maxDist = -1f;

        foreach (var station in candidates)
        {
            float minDist = float.MaxValue;
            foreach (var p in players)
            {
                float d = Vector3.Distance(station.transform.position, p.transform.position);
                if (d < minDist) minDist = d;
            }
            if (minDist > maxDist) { maxDist = minDist; farthest = station; }
        }
        return farthest ?? candidates[0];
    }

    private RepairStation SelectMostRecentlyRepaired(List<RepairStation> candidates)
    {
        RepairStation best = null;
        int maxCount = -1;

        foreach (var s in candidates)
        {
            int count = repairFrequency.ContainsKey(s) ? repairFrequency[s] : 0;
            if (count > maxCount) { maxCount = count; best = s; }
        }

        return (best != null && maxCount > 0) ? best : SelectFarthestFromPlayers(candidates);
    }

    private RepairStation SelectByPriority(List<RepairStation> candidates)
    {
        var priority = new[]
        {
            RepairStation.StationType.Energy,
            RepairStation.StationType.Hull,
            RepairStation.StationType.Gravity,
            RepairStation.StationType.Communications
        };

        foreach (var type in priority)
        {
            var match = candidates.Find(s => s.Type == type);
            if (match != null) return match;
        }

        return candidates[0];
    }
}
