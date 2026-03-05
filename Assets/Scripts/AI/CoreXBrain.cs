using System.Collections.Generic;
using UnityEngine;

// Core-X: IA central adaptativa del GDD
// Fase 1 - Reactiva: decide qué romper según estado de la nave y jugadores
public class CoreXBrain : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private float playerDetectionRadius = 20f;
    [SerializeField] private float aggressionLevel = 0f; // 0-1, sube con el tiempo

    [Header("Runtime - Debug")]
    [SerializeField] private string lastDecision;
    [SerializeField] private float  aggressionDisplay;

    // Historial para detectar patrones (base para fase deliberativa)
    private Dictionary<RepairStation, int> repairFrequency = new();
    private Dictionary<RepairStation, int> breakFrequency  = new();

    private void Update()
    {
        // Aggression sube lentamente con el tiempo
        aggressionLevel = Mathf.Min(1f, aggressionLevel + Time.deltaTime * 0.005f);
        aggressionDisplay = aggressionLevel;
    }

    // Llamado por FailureSystem para elegir qué estación romper
    public RepairStation SelectStationToBreak(List<RepairStation> allStations)
    {
        var candidates = allStations.FindAll(s =>
            s.State == RepairStation.StationState.Functional ||
            s.State == RepairStation.StationState.Fixed);

        if (candidates.Count == 0) return null;

        // Estrategia según nivel de agresión
        RepairStation chosen;

        if (aggressionLevel < 0.3f)
        {
            // Fase reactiva: romper la más lejana a los jugadores (más difícil de llegar)
            chosen = SelectFarthestFromPlayers(candidates);
            lastDecision = "Farthest from players";
        }
        else if (aggressionLevel < 0.6f)
        {
            // Fase táctica: romper la más reparada recientemente
            chosen = SelectMostRecentlyRepaired(candidates);
            lastDecision = "Recently repaired";
        }
        else
        {
            // Fase agresiva: romper la más crítica según tipo
            chosen = SelectByPriority(candidates);
            lastDecision = "Priority target";
        }

        // Registrar en historial
        if (!breakFrequency.ContainsKey(chosen)) breakFrequency[chosen] = 0;
        breakFrequency[chosen]++;

        Debug.Log($"[CoreX] Decision: {lastDecision} → {chosen.Type} | Aggression: {aggressionLevel:F2}");
        return chosen;
    }

    // Registrar reparación para aprender patrones
    public void RegisterRepair(RepairStation station)
    {
        if (!repairFrequency.ContainsKey(station)) repairFrequency[station] = 0;
        repairFrequency[station]++;
    }

    // ── Estrategias de selección ───────────────────────────────

    private RepairStation SelectFarthestFromPlayers(List<RepairStation> candidates)
    {
        var players = FindObjectsOfType<PlayerMovement>();
        if (players.Length == 0)
            return candidates[Random.Range(0, candidates.Count)];

        RepairStation farthest = null;
        float maxDist = -1f;

        foreach (var station in candidates)
        {
            float minPlayerDist = float.MaxValue;
            foreach (var p in players)
            {
                float d = Vector3.Distance(station.transform.position,
                                           p.transform.position);
                if (d < minPlayerDist) minPlayerDist = d;
            }
            if (minPlayerDist > maxDist)
            {
                maxDist = minPlayerDist;
                farthest = station;
            }
        }
        return farthest ?? candidates[0];
    }

    private RepairStation SelectMostRecentlyRepaired(List<RepairStation> candidates)
    {
        RepairStation mostRepaired = null;
        int maxCount = -1;

        foreach (var s in candidates)
        {
            int count = repairFrequency.ContainsKey(s) ? repairFrequency[s] : 0;
            if (count > maxCount) { maxCount = count; mostRepaired = s; }
        }

        // Si ninguna ha sido reparada, caer en farthest
        return (mostRepaired != null && maxCount > 0)
            ? mostRepaired
            : SelectFarthestFromPlayers(candidates);
    }

    private RepairStation SelectByPriority(List<RepairStation> candidates)
    {
        // Prioridad según GDD: Energía > Casco > Gravedad > Comunicaciones
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

    public float AggressionLevel => aggressionLevel;
}
