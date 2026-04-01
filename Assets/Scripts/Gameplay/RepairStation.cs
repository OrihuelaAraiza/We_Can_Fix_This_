using System;
using System.Collections.Generic;
using System.Linq;
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
    [SerializeField] private string currentLocationLabel = "UNKNOWN";

    // Materiales de estado
    [Header("Visuals")]
    [SerializeField] private Color colorFunctional = Color.green;
    [SerializeField] private Color colorBroken     = Color.red;
    [SerializeField] private Color colorRepairing  = Color.yellow;
    [SerializeField] private Color colorFixed      = Color.cyan;

    public event Action<RepairStation> OnBroken;
    public event Action<RepairStation> OnRepaired;

    [NonSerialized] private Renderer[] runtimeVisualRenderers;
    private Renderer[] stationRenderers;
    private readonly Dictionary<Renderer, Material[]> runtimeMaterials = new Dictionary<Renderer, Material[]>();
    private PlayerMovement repairingPlayer;

    public StationState State => state;
    public StationType Type => stationType;
    public float RepairProgress => repairProgress;
    public string CurrentLocationLabel => string.IsNullOrWhiteSpace(currentLocationLabel) ? "UNKNOWN" : currentLocationLabel;

    private void Awake()
    {
        RefreshVisualBindings();
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

    /// <summary>Rompe la estación y dispara el evento estático de FailureSystem (usado por CoreX).</summary>
    public void TriggerFailure()
    {
        if (state == StationState.Broken || state == StationState.Repairing) return;
        state         = StationState.Broken;
        repairProgress = 0f;
        ApplyStateVisual();
        FailureSystem.NotifyStationFailed(this);
        OnBroken?.Invoke(this);
        Debug.Log($"[RepairStation] {stationType} FALLO (CoreX)");
    }

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
        Color c = state switch
        {
            StationState.Functional => colorFunctional,
            StationState.Broken     => colorBroken,
            StationState.Repairing  => colorRepairing,
            StationState.Fixed      => colorFixed,
            _                       => Color.white
        };

        EnsureRuntimeMaterials();
        foreach (var pair in runtimeMaterials)
        {
            if (pair.Key == null)
                continue;

            foreach (Material material in pair.Value)
            {
                if (material == null || !material.HasProperty("_Color"))
                    continue;

                material.color = c;
            }

            pair.Key.materials = pair.Value;
        }
    }

    // Para habilidad Hacker — avanza progreso de reparación
    public void ApplyRemoteRepairBoost(float progressAmount)
    {
        if (state != StationState.Broken) return;
        repairProgress += progressAmount;
        repairProgress  = Mathf.Clamp01(repairProgress);
        Debug.Log($"[RepairStation] Remote boost applied: {repairProgress:P0}");
        if (repairProgress >= 1f)
            CompleteRepair();
    }

    // Para habilidad Mecánico — resetea degradación acumulada
    public void ResetDegradation()
    {
        // Si la estación tiene un timer de degradación, resetearlo
        // Por ahora simplemente repara si está rota
        if (state == StationState.Broken)
        {
            state          = StationState.Functional;
            repairProgress = 0f;
            ApplyStateVisual();
            OnRepaired?.Invoke(this);
            Debug.Log($"[RepairStation] Degradation reset: {stationType}");
        }
    }

    public void SetGeneratedLocationLabel(string locationLabel)
    {
        currentLocationLabel = string.IsNullOrWhiteSpace(locationLabel) ? "UNKNOWN" : locationLabel;
    }

    public void ConfigureRuntimeBinding(
        Transform parent,
        Vector3 worldPosition,
        Quaternion worldRotation,
        Bounds localInteractionBounds,
        Renderer[] visualRenderers,
        string locationLabel)
    {
        if (parent != null)
            transform.SetParent(parent, true);

        transform.SetPositionAndRotation(worldPosition, worldRotation);
        gameObject.layer = LayerMask.NameToLayer("Interactable") >= 0 ? LayerMask.NameToLayer("Interactable") : 7;

        var collider = GetComponent<BoxCollider>();
        if (collider == null)
            collider = gameObject.AddComponent<BoxCollider>();

        collider.isTrigger = false;
        collider.center = localInteractionBounds.center;
        collider.size = new Vector3(
            Mathf.Max(localInteractionBounds.size.x, 0.25f),
            Mathf.Max(localInteractionBounds.size.y, 0.75f),
            Mathf.Max(localInteractionBounds.size.z, 0.25f));

        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null)
                renderer.enabled = false;
        }

        runtimeVisualRenderers = visualRenderers?
            .Where(renderer => renderer != null)
            .Distinct()
            .ToArray();

        SetGeneratedLocationLabel(locationLabel);
        RefreshVisualBindings();
        ApplyStateVisual();
    }

    void RefreshVisualBindings()
    {
        stationRenderers = runtimeVisualRenderers != null && runtimeVisualRenderers.Length > 0
            ? runtimeVisualRenderers
            : GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null && renderer.enabled)
                .ToArray();

        runtimeMaterials.Clear();
    }

    void EnsureRuntimeMaterials()
    {
        if (stationRenderers == null || stationRenderers.Length == 0)
            RefreshVisualBindings();

        if (stationRenderers == null)
            return;

        foreach (Renderer renderer in stationRenderers)
        {
            if (renderer == null || runtimeMaterials.ContainsKey(renderer))
                continue;

            Material[] materials = renderer.sharedMaterials
                .Where(material => material != null)
                .Select(material => new Material(material))
                .ToArray();

            if (materials.Length == 0)
                continue;

            runtimeMaterials[renderer] = materials;
        }
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
