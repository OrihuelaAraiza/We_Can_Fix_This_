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

    [Header("Indicator Light")]
    [SerializeField] private Light stationLight;
    [SerializeField] private float lightRangeBroken    = 5f;
    [SerializeField] private float lightIntensityBroken = 2.5f;
    [SerializeField] private float lightFlickerSpeed   = 6f;

    [Header("UI")]
    [SerializeField] private bool autoCreateWorldStatusUI = true;

    private static readonly List<RepairStation> activeStations = new();

    public static IReadOnlyList<RepairStation> ActiveStations => activeStations;
    public static event Action<RepairStation> OnRegistered;
    public static event Action<RepairStation> OnUnregistered;
    public static event Action<RepairStation, StationState, StationState> OnStateChanged;

    public event Action<RepairStation> OnBroken;
    public event Action<RepairStation> OnRepaired;

    [NonSerialized] private Renderer[] runtimeVisualRenderers;
    private Renderer[] stationRenderers;
    private readonly Dictionary<Renderer, Material[]> runtimeMaterials = new Dictionary<Renderer, Material[]>();
    private PlayerMovement repairingPlayer;
    private float lightFlickerTimer;

    public StationState State => state;
    public StationType Type => stationType;
    public float RepairProgress => repairProgress;
    public string CurrentLocationLabel => string.IsNullOrWhiteSpace(currentLocationLabel) ? "UNKNOWN" : currentLocationLabel;

    private void Awake()
    {
        EnsureIndicatorLight();
        RefreshVisualBindings();
        ApplyStateVisual();
        EnsureWorldStatusUI();
    }

    private void Update()
    {
        if (stationLight == null || !stationLight.enabled) return;
        if (state != StationState.Broken && state != StationState.Repairing) return;

        lightFlickerTimer += Time.deltaTime * lightFlickerSpeed;
        stationLight.intensity = lightIntensityBroken * (0.6f + 0.4f * Mathf.Sin(lightFlickerTimer));
    }

    private void EnsureIndicatorLight()
    {
        if (stationLight != null) return;

        var go = new GameObject("IndicatorLight");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.up * 1.8f;

        stationLight = go.AddComponent<Light>();
        stationLight.type      = LightType.Point;
        stationLight.range     = lightRangeBroken;
        stationLight.color     = colorBroken;
        stationLight.intensity = lightIntensityBroken;
        stationLight.shadows   = LightShadows.None;
        stationLight.enabled   = false;
    }

    private void OnEnable()
    {
        if (!activeStations.Contains(this))
        {
            activeStations.Add(this);
            OnRegistered?.Invoke(this);
        }
    }

    private void OnDisable()
    {
        if (activeStations.Remove(this))
            OnUnregistered?.Invoke(this);
    }

    // ── IInteractable ──────────────────────────────────────────
    public bool CanInteract(PlayerMovement player)
        => state == StationState.Broken || state == StationState.Repairing;

    public void OnInteractStart(PlayerMovement player)
    {
        if (state != StationState.Broken) return;
        repairingPlayer = player;
        SetState(StationState.Repairing);
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
            repairProgress = 0f;
            repairingPlayer = null;
            SetState(StationState.Broken); // cancelar reparación
            ApplyStateVisual();
            Debug.Log($"[RepairStation] {stationType} repair cancelled");
        }
    }

    public string GetInteractLabel()
        => state == StationState.Broken ? $"Repair {stationType} [E]" : "";

    // ── Public API ─────────────────────────────────────────────

    /// <summary>Rompe la estación y dispara el evento estático de FailureSystem (usado por CoreX).</summary>
    public void TriggerFailure()
    {
        if (state == StationState.Broken || state == StationState.Repairing) return;
        repairProgress = 0f;
        SetState(StationState.Broken);
        ApplyStateVisual();
        Debug.Log($"[RepairStation] {stationType} FALLO (CoreX)");
    }

    public void BreakStation()
    {
        if (state == StationState.Broken || state == StationState.Repairing) return;
        repairProgress = 0f;
        SetState(StationState.Broken);
        ApplyStateVisual();
        Debug.Log($"[RepairStation] {stationType} BROKEN!");
    }

    private void CompleteRepair()
    {
        repairingPlayer = null;
        ShipHealth.Instance?.Repair(20f); // reparar da salud a la nave
        SetState(StationState.Fixed);
        ApplyStateVisual();
        Debug.Log($"[RepairStation] {stationType} REPAIRED!");

        // Vuelve a Functional después de un delay
        Invoke(nameof(ResetToFunctional), 3f);
    }

    private void ResetToFunctional()
    {
        repairProgress = 0f;
        SetState(StationState.Functional);
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
                if (material == null) continue;

                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", c);
                else if (material.HasProperty("_Color"))
                    material.SetColor("_Color", c);
            }

            pair.Key.materials = pair.Value;
        }

        ApplyIndicatorLight();
    }

    private void ApplyIndicatorLight()
    {
        if (stationLight == null) return;

        switch (state)
        {
            case StationState.Broken:
                lightFlickerTimer     = 0f;
                stationLight.enabled  = true;
                stationLight.color    = colorBroken;
                stationLight.range    = lightRangeBroken;
                stationLight.intensity = lightIntensityBroken;
                break;
            case StationState.Repairing:
                lightFlickerTimer     = 0f;
                stationLight.enabled  = true;
                stationLight.color    = colorRepairing;
                stationLight.range    = lightRangeBroken * 0.7f;
                stationLight.intensity = lightIntensityBroken * 0.6f;
                break;
            case StationState.Fixed:
            case StationState.Functional:
            default:
                stationLight.enabled = false;
                break;
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

    // For Mechanic ability — resets accumulated degradation
    public void ResetDegradation()
    {
        // Si la estación tiene un timer de degradación, resetearlo
        // Por ahora simplemente repara si está rota
        if (state == StationState.Broken)
        {
            repairProgress = 0f;
            SetState(StationState.Functional);
            ApplyStateVisual();
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

        collider.isTrigger = true;
        collider.center = localInteractionBounds.center;
        collider.size = new Vector3(
            Mathf.Max(localInteractionBounds.size.x, 0.6f),
            Mathf.Max(localInteractionBounds.size.y, 1.1f),
            Mathf.Max(localInteractionBounds.size.z, 0.6f));

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

    void SetState(StationState nextState)
    {
        if (state == nextState)
            return;

        StationState previousState = state;
        state = nextState;

        OnStateChanged?.Invoke(this, previousState, nextState);

        if (nextState == StationState.Broken && previousState != StationState.Repairing)
        {
            OnBroken?.Invoke(this);
            return;
        }

        bool recoveredFromFailure = previousState == StationState.Broken
            || previousState == StationState.Repairing;
        if (nextState == StationState.Fixed || (nextState == StationState.Functional && recoveredFromFailure))
            OnRepaired?.Invoke(this);
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

    void EnsureWorldStatusUI()
    {
        if (!autoCreateWorldStatusUI)
            return;

        if (GetComponentInChildren<RepairProgressUI>(true) != null)
            return;

        gameObject.AddComponent<RepairProgressUI>();
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
