using System.Collections.Generic;
using UnityEngine;

public class FailureListUI : MonoBehaviour
{
    [SerializeField] GameObject failureItemPrefab;
    [SerializeField] Transform  listParent;
    [SerializeField] int        maxVisible = 4;

    [Header("Style")]
    [SerializeField] UIStyleConfig style;

    readonly List<GameObject> activeItems = new();
    readonly List<FailureData> currentFailures = new();

    void OnEnable()
    {
        FailureSystem.OnStationFailed   += OnStationFailed;
        FailureSystem.OnStationRepaired += OnStationRepaired;
    }

    void OnDisable()
    {
        FailureSystem.OnStationFailed   -= OnStationFailed;
        FailureSystem.OnStationRepaired -= OnStationRepaired;
    }

    void OnStationFailed(RepairStation station)
    {
        string sysName = StationTypeName(station.Type);

        // Avoid duplicates
        if (currentFailures.Exists(f => f.systemName == sysName)) return;

        currentFailures.Add(new FailureData
        {
            systemName = sysName,
            location   = StationLocation(station),
            severity   = FailureSeverity.CRITICAL
        });
        RefreshFailures(currentFailures);
    }

    void OnStationRepaired(RepairStation station)
    {
        string sysName = StationTypeName(station.Type);
        currentFailures.RemoveAll(f => f.systemName == sysName);
        RefreshFailures(currentFailures);
    }

    public void RefreshFailures(List<FailureData> failures)
    {
        // Clear existing items
        foreach (var item in activeItems)
            if (item != null) Destroy(item);
        activeItems.Clear();

        if (failureItemPrefab == null || listParent == null) return;

        int count = Mathf.Min(failures.Count, maxVisible);
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(failureItemPrefab, listParent);
            var ui = go.GetComponent<FailureItemUI>();
            if (ui != null)
                ui.Setup(failures[i], style);
            activeItems.Add(go);
        }
    }

    static string StationTypeName(RepairStation.StationType type)
    {
        return type switch
        {
            RepairStation.StationType.Energy         => "POWER CORE",
            RepairStation.StationType.Hull           => "HULL BREACH",
            RepairStation.StationType.Gravity        => "GRAVITY",
            RepairStation.StationType.Communications => "COMMS",
            _ => type.ToString().ToUpper()
        };
    }

    static string StationLocation(RepairStation station)
    {
        if (station != null && !string.IsNullOrWhiteSpace(station.CurrentLocationLabel))
            return station.CurrentLocationLabel;

        return station != null ? LegacyStationLocation(station.Type) : "UNKNOWN";
    }

    static string LegacyStationLocation(RepairStation.StationType type)
    {
        return type switch
        {
            RepairStation.StationType.Energy         => "WEST WING",
            RepairStation.StationType.Hull           => "SOUTH SECTOR",
            RepairStation.StationType.Gravity        => "NORTH SECTOR",
            RepairStation.StationType.Communications => "EAST WING",
            _ => "UNKNOWN"
        };
    }
}

[System.Serializable]
public class FailureData
{
    public string          systemName;
    public string          location;
    public FailureSeverity severity;
}

public enum FailureSeverity { OK, WARNING, CRITICAL }
