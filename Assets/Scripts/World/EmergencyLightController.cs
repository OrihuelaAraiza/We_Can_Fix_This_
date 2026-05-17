using UnityEngine;

public class EmergencyLightController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Light directionalLight;

    [Header("Normal State")]
    [SerializeField] Color normalColor     = new Color(0.55f, 0.66f, 0.95f);
    [SerializeField] float normalIntensity = 1.05f;

    [Header("Critical State")]
    [SerializeField] Color criticalColor     = new Color(0.8f, 0.1f, 0.05f);
    [SerializeField] float criticalIntensity = 1.2f;
    [SerializeField] float flickerSpeed      = 3f;

    [Header("Station Emergency")]
    [SerializeField] Color stationEmergencyColor = new Color(1f, 0.12f, 0.05f);
    [SerializeField] float stationEmergencyIntensity = 1.35f;

    bool shipCritical;
    bool shipDestroyed;
    bool stationEmergencyActive;
    float flickerTimer;

    void OnEnable()
    {
        ShipHealth.OnShipCritical  += OnCritical;
        ShipHealth.OnShipDestroyed += OnDestroyed;
        ShipHealth.OnShipRecovered += OnRecovered;
        RepairStation.OnRegistered += OnStationRegistered;
        RepairStation.OnUnregistered += OnStationUnregistered;
        RepairStation.OnStateChanged += OnStationStateChanged;
    }

    void OnDisable()
    {
        ShipHealth.OnShipCritical  -= OnCritical;
        ShipHealth.OnShipDestroyed -= OnDestroyed;
        ShipHealth.OnShipRecovered -= OnRecovered;
        RepairStation.OnRegistered -= OnStationRegistered;
        RepairStation.OnUnregistered -= OnStationUnregistered;
        RepairStation.OnStateChanged -= OnStationStateChanged;
    }

    void Start()
    {
        if (directionalLight == null)
#pragma warning disable CS0618
            directionalLight = FindObjectOfType<Light>();
#pragma warning restore CS0618

        RefreshStationEmergency();
        ApplyNormal();
        ApplyCurrentState();
    }

    void Update()
    {
        if (!shipCritical && !stationEmergencyActive) return;
        flickerTimer += Time.deltaTime * flickerSpeed;
        float f = 0.6f + 0.4f * Mathf.Sin(flickerTimer);
        ApplyEmergencyIntensity(f);
    }

    void OnCritical()
    {
        shipCritical = true;
        shipDestroyed = false;
        ApplyCurrentState();
    }

    void OnRecovered()
    {
        shipCritical = false;
        shipDestroyed = false;
        ApplyCurrentState();
        Debug.Log("[EmergencyLight] Normal restored");
    }

    void OnDestroyed()
    {
        shipCritical = false;
        shipDestroyed = true;
        ApplyCurrentState();
    }

    void OnStationRegistered(RepairStation _)
    {
        RefreshStationEmergency();
        ApplyCurrentState();
    }

    void OnStationUnregistered(RepairStation _)
    {
        RefreshStationEmergency();
        ApplyCurrentState();
    }

    void OnStationStateChanged(
        RepairStation station,
        RepairStation.StationState previousState,
        RepairStation.StationState nextState)
    {
        bool wasEmergency = IsStationEmergency(previousState);
        bool isEmergency = IsStationEmergency(nextState);
        if (wasEmergency == isEmergency)
            return;

        RefreshStationEmergency();
        ApplyCurrentState();
    }

    void RefreshStationEmergency()
    {
        stationEmergencyActive = false;
        foreach (RepairStation station in RepairStation.ActiveStations)
        {
            if (station == null)
                continue;

            if (!IsStationEmergency(station.State))
                continue;

            stationEmergencyActive = true;
            return;
        }
    }

    void ApplyCurrentState()
    {
        if (directionalLight == null)
            return;

        if (shipDestroyed)
        {
            directionalLight.color = Color.red;
            directionalLight.intensity = 0.2f;
            return;
        }

        if (shipCritical)
        {
            flickerTimer = 0f;
            directionalLight.color = criticalColor;
            directionalLight.intensity = criticalIntensity;
            return;
        }

        if (stationEmergencyActive)
        {
            flickerTimer = 0f;
            directionalLight.color = stationEmergencyColor;
            directionalLight.intensity = stationEmergencyIntensity;
            return;
        }

        ApplyNormal();
    }

    void ApplyEmergencyIntensity(float flicker)
    {
        if (directionalLight == null || shipDestroyed)
            return;

        directionalLight.intensity = shipCritical
            ? criticalIntensity * flicker
            : stationEmergencyIntensity * flicker;
    }

    void ApplyNormal()
    {
        if (directionalLight == null) return;
        directionalLight.color     = normalColor;
        directionalLight.intensity = normalIntensity;
    }

    static bool IsStationEmergency(RepairStation.StationState state)
    {
        return state == RepairStation.StationState.Broken
            || state == RepairStation.StationState.Repairing;
    }
}
