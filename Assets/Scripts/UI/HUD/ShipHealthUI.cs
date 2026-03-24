using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShipHealthUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Slider   integrityBar;
    [SerializeField] TMP_Text integrityPct;
    [SerializeField] Slider   powerBar;
    [SerializeField] Slider   hullBar;
    [SerializeField] Image    integrityFill;

    [Header("Style")]
    [SerializeField] UIStyleConfig style;

    // Color thresholds for integrity bar
    static readonly Color ColorOrange = HexColor("#c07010");
    static readonly Color ColorAmber  = HexColor("#907818");
    static readonly Color ColorRed    = HexColor("#a02020");

    Coroutine blinkRoutine;
    RepairStation[] cachedStations;

    void OnEnable()
    {
        ShipHealth.OnHealthChanged         += OnHealthChanged;
        FailureSystem.OnStationFailed      += OnStationChanged;
        FailureSystem.OnStationRepaired    += OnStationChanged;
    }

    void OnDisable()
    {
        ShipHealth.OnHealthChanged         -= OnHealthChanged;
        FailureSystem.OnStationFailed      -= OnStationChanged;
        FailureSystem.OnStationRepaired    -= OnStationChanged;
    }

    void Start()
    {
        cachedStations = FindObjectsOfType<RepairStation>();
        SetIntegrity(1f);
        SetPower(1f);
        SetHull(1f);
    }

    void OnHealthChanged(float normalized)
    {
        SetIntegrity(normalized);
    }

    void OnStationChanged(RepairStation _)
    {
        UpdateSubBars();
    }

    // ── Public API ──────────────────────────────────────────────

    public void SetIntegrity(float value01)
    {
        if (integrityBar != null)
            integrityBar.value = value01;

        if (integrityPct != null)
            integrityPct.text = $"{Mathf.RoundToInt(value01 * 100)}%";

        // Color thresholds
        Color fillColor;
        if (value01 > 0.6f)       fillColor = ColorOrange;
        else if (value01 > 0.3f)  fillColor = ColorAmber;
        else                      fillColor = ColorRed;

        if (integrityFill != null)
            integrityFill.color = fillColor;

        // Blink when critical
        if (value01 <= 0.3f && blinkRoutine == null && integrityFill != null)
            blinkRoutine = StartCoroutine(HUDAnimations.BlinkCoroutine(integrityFill, 0.6f));
        else if (value01 > 0.3f && blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
            HUDAnimations.StopBlink(integrityFill);
        }
    }

    public void SetPower(float value01)
    {
        if (powerBar != null)
            powerBar.value = value01;
    }

    public void SetHull(float value01)
    {
        if (hullBar != null)
            hullBar.value = value01;
    }

    // ── Sub-bar logic ───────────────────────────────────────────

    void UpdateSubBars()
    {
        if (cachedStations == null || cachedStations.Length == 0)
            cachedStations = FindObjectsOfType<RepairStation>();

        float powerHealth = 1f;
        float hullHealth  = 1f;

        foreach (var s in cachedStations)
        {
            if (s == null) continue;
            bool broken = s.State == RepairStation.StationState.Broken
                       || s.State == RepairStation.StationState.Repairing;

            if (s.Type == RepairStation.StationType.Energy && broken)
                powerHealth = 0f;
            if (s.Type == RepairStation.StationType.Hull && broken)
                hullHealth = 0f;
        }

        SetPower(powerHealth);
        SetHull(hullHealth);
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
