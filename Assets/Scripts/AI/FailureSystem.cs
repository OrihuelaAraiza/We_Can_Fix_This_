using System.Collections.Generic;
using UnityEngine;

public class FailureSystem : MonoBehaviour
{
    public static FailureSystem Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private float initialFailureInterval = 30f;
    [SerializeField] private float minimumFailureInterval = 12f;
    [SerializeField] private float intervalDecreaseRate   = 0.97f; // multiplier per failure
    [SerializeField] private int   maxSimultaneousBroken  = 2;

    [Header("Runtime")]
    [SerializeField] private float currentInterval;
    [SerializeField] private float timer;
    [SerializeField] private int   totalFailures;
    [SerializeField] private bool  active = true;

    private List<RepairStation> allStations = new();
    private CoreXBrain coreX;

    public int TotalFailures => totalFailures;
    public float CurrentInterval => currentInterval;

    public static event System.Action<RepairStation> OnStationFailed;
    public static event System.Action<RepairStation> OnStationRepaired;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        currentInterval = initialFailureInterval;
    }

    private void Start()
    {
        // Registrar todas las estaciones
        allStations.AddRange(FindObjectsOfType<RepairStation>());
        coreX = GetComponent<CoreXBrain>();

        // Suscribirse a eventos de reparación
        foreach (var s in allStations)
            s.OnRepaired += HandleStationRepaired;

        Debug.Log($"[FailureSystem] Registered {allStations.Count} stations");
    }

    private void Update()
    {
        if (!active) return;
        timer += Time.deltaTime;
        if (timer >= currentInterval)
        {
            timer = 0f;
            TriggerFailure();
        }
    }

    private void TriggerFailure()
    {
        // Contar cuántas están rotas actualmente
        int brokenCount = 0;
        foreach (var s in allStations)
            if (s.State == RepairStation.StationState.Broken ||
                s.State == RepairStation.StationState.Repairing)
                brokenCount++;

        if (brokenCount >= maxSimultaneousBroken)
        {
            Debug.Log("[FailureSystem] Max broken reached, skipping");
            return;
        }

        // Pedir a CoreX que elija qué romper
        RepairStation target = coreX != null
            ? coreX.SelectStationToBreak(allStations)
            : SelectRandom();

        if (target == null) return;

        target.BreakStation();
        totalFailures++;
        OnStationFailed?.Invoke(target);

        // Escalar dificultad
        currentInterval = Mathf.Max(
            minimumFailureInterval,
            currentInterval * intervalDecreaseRate);

        Debug.Log($"[FailureSystem] Failure #{totalFailures} | Next in {currentInterval:F1}s");
    }

    private void HandleStationRepaired(RepairStation station)
    {
        OnStationRepaired?.Invoke(station);
    }

    private RepairStation SelectRandom()
    {
        var candidates = allStations.FindAll(s =>
            s.State == RepairStation.StationState.Functional ||
            s.State == RepairStation.StationState.Fixed);
        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    public void SetActive(bool value) => active = value;
    public List<RepairStation> GetAllStations() => allStations;
}
