using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StationAlertUI : MonoBehaviour
{
    [Header("Alert Panel")]
    [SerializeField] GameObject alertPanel;
    [SerializeField] TextMeshProUGUI alertText;
    [SerializeField] Image           alertBackground;

    [Header("Colors")]
    [SerializeField] Color colorCritical = new Color(0.9f, 0.2f, 0.1f, 0.9f);
    [SerializeField] Color colorWarning  = new Color(0.9f, 0.6f, 0.1f, 0.9f);
    [SerializeField] Color colorOk       = new Color(0.1f, 0.7f, 0.3f, 0.9f);

    readonly Dictionary<string, string> roomNames = new()
    {
        { "Energy",         "⚡ ENERGY ROOM"       },
        { "Communications", "📡 COMMUNICATIONS"    },
        { "Gravity",        "🌀 GRAVITY CONTROL"   },
        { "Hull",           "🛡 HULL INTEGRITY"    }
    };

    // Lista de alertas activas
    List<string> activeAlerts = new();

    void OnEnable()
    {
        FailureSystem.OnStationFailed   += OnFailed;
        FailureSystem.OnStationRepaired += OnRepaired;
    }

    void OnDisable()
    {
        FailureSystem.OnStationFailed   -= OnFailed;
        FailureSystem.OnStationRepaired -= OnRepaired;
    }

    void Start()
    {
        if (alertPanel != null)
            alertPanel.SetActive(false);
    }

    void OnFailed(RepairStation station)
    {
        string key = station.Type.ToString();
        if (!activeAlerts.Contains(key))
            activeAlerts.Add(key);
        UpdatePanel();
    }

    void OnRepaired(RepairStation station)
    {
        activeAlerts.Remove(station.Type.ToString());
        UpdatePanel();
    }

    void UpdatePanel()
    {
        if (alertPanel == null) return;

        if (activeAlerts.Count == 0)
        {
            alertPanel.SetActive(false);
            return;
        }

        alertPanel.SetActive(true);

        // Build text with all active alerts
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("⚠ FAILURES DETECTED:");
        foreach (var key in activeAlerts)
        {
            string name = roomNames.ContainsKey(key) ? roomNames[key] : key;
            sb.AppendLine($"  → {name}");
        }

        if (alertText != null)
            alertText.text = sb.ToString();

        // Color based on failure count
        if (alertBackground != null)
            alertBackground.color = activeAlerts.Count >= 2
                ? colorCritical
                : colorWarning;
    }
}
