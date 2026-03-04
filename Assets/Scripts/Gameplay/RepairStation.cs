using System;
using UnityEngine;

public class RepairStation : MonoBehaviour, IInteractable
{
    public enum StationState { Functional, Broken, Repairing, Fixed }
    public enum StationType  { Energy, Communications, Gravity, Hull }

    [Header("Config")]
    [SerializeField] private StationType stationType;
    [SerializeField] private float repairDuration = 5f;
    [SerializeField] private float shipDrainOnBroken = 1f; // extra drain mientras rota

    [Header("Runtime")]
    [SerializeField] private StationState state = StationState.Functional;
    [SerializeField] private float repairProgress; // 0-1

    // Materiales de estado
    [Header("Visuals")]
    [SerializeField] private Color colorFunctional = Color.green;
    [SerializeField] private Color colorBroken     = Color.red;
    [SerializeField] private Color colorRepairing  = Color.yellow;
    [SerializeField] private Color colorFixed      = Color.cyan;

    public event Action<RepairStation> OnBroken;
    public event Action<RepairStation> OnRepaired;

    private Renderer stationRenderer;
    private Material stateMat;
    private PlayerMovement repairingPlayer;

    public StationState State => state;
    public StationType Type => stationType;
    public float RepairProgress => repairProgress;

    private void Awake()
    {
        stationRenderer = GetComponentInChildren<Renderer>();
        if (stationRenderer != null)
            stateMat = new Material(stationRenderer.sharedMaterial);
        ApplyStateVisual();
    }

    private void Update()
    {
        if (state == StationState.Broken)
            ShipHealth.Instance?.ApplyDamage(shipDrainOnBroken * Time.deltaTime);
    }

    // ── IInteractable ──────────────────────────────────────────
    public bool CanInteract(PlayerMovement player)
        => state == StationState.Broken || state == StationState.Repairing;

    public void OnInteractStart(PlayerMovement player)
    {
        if (state != StationState.Broken) return;
        state = StationState.Repairing;
        repairingPlayer = player;
        ApplyStateVisual();
        Debug.Log($"[RepairStation] {stationType} repair started by P{player.PlayerIndex}");
    }

    public void OnInteractHeld(PlayerMovement player, float deltaTime)
    {
        if (state != StationState.Repairing) return;
        repairProgress += deltaTime / repairDuration;

        if (repairProgress >= 1f)
        {
            repairProgress = 1f;
            CompleteRepair();
        }
    }

    public void OnInteractEnd(PlayerMovement player)
    {
        if (state == StationState.Repairing)
        {
            state = StationState.Broken; // cancelar reparación
            repairProgress = 0f;
            repairingPlayer = null;
            ApplyStateVisual();
            Debug.Log($"[RepairStation] {stationType} repair cancelled");
        }
    }

    public string GetInteractLabel()
        => state == StationState.Broken ? $"Reparar {stationType} [E]" : "";

    // ── Public API ─────────────────────────────────────────────
    public void BreakStation()
    {
        if (state == StationState.Broken || state == StationState.Repairing) return;
        state = StationState.Broken;
        repairProgress = 0f;
        ApplyStateVisual();
        OnBroken?.Invoke(this);
        Debug.Log($"[RepairStation] {stationType} BROKEN!");
    }

    private void CompleteRepair()
    {
        state = StationState.Fixed;
        repairingPlayer = null;
        ShipHealth.Instance?.Repair(20f); // reparar da salud a la nave
        ApplyStateVisual();
        OnRepaired?.Invoke(this);
        Debug.Log($"[RepairStation] {stationType} REPAIRED!");

        // Vuelve a Functional después de un delay
        Invoke(nameof(ResetToFunctional), 3f);
    }

    private void ResetToFunctional()
    {
        state = StationState.Functional;
        repairProgress = 0f;
        ApplyStateVisual();
    }

    // ── Visuals ────────────────────────────────────────────────
    private void ApplyStateVisual()
    {
        if (stateMat == null || stationRenderer == null) return;
        Color c = state switch
        {
            StationState.Functional => colorFunctional,
            StationState.Broken     => colorBroken,
            StationState.Repairing  => colorRepairing,
            StationState.Fixed      => colorFixed,
            _                       => Color.white
        };
        stateMat.color = c;
        stationRenderer.material = stateMat;
    }

    private void OnDrawGizmos()
    {
        Color g = state switch
        {
            StationState.Broken    => Color.red,
            StationState.Repairing => Color.yellow,
            StationState.Fixed     => Color.cyan,
            _                      => Color.green
        };
        Gizmos.color = g;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 1.2f);
    }
}
