using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CoreXUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] TMP_Text phaseLabel;
    [SerializeField] TMP_Text aggressionTitle;
    [SerializeField] Slider   aggressionBar;
    [SerializeField] Image    aggressionFill;
    [SerializeField] TMP_Text reactiveLabel;
    [SerializeField] TMP_Text activeLabel;

    [Header("Style")]
    [SerializeField] UIStyleConfig style;

    static readonly Color ColorLow  = HexColor("#a82020");
    static readonly Color ColorMid  = HexColor("#c04020");
    static readonly Color ColorHigh = HexColor("#e03020");

    Coroutine blinkRoutine;

    void OnEnable()
    {
        CoreXBrain.OnPhaseChanged += OnPhaseChanged;
    }

    void OnDisable()
    {
        CoreXBrain.OnPhaseChanged -= OnPhaseChanged;
    }

    void Start()
    {
        SetPhase(0);
        SetAggression(0f);
    }

    void Update()
    {
        if (CoreXBrain.Instance != null)
            SetAggression(CoreXBrain.Instance.AggressionLevel);
    }

    // ── Public API ──────────────────────────────────────────────

    public void SetPhase(int phase)
    {
        if (phaseLabel != null)
            phaseLabel.text = $"CORE-X \u00b7 PHASE {phase + 1}";
    }

    public void SetAggression(float value01)
    {
        if (aggressionBar != null)
            aggressionBar.value = value01;

        // Color by aggression level
        Color fillColor;
        if (value01 < 0.4f)       fillColor = ColorLow;
        else if (value01 < 0.7f)  fillColor = ColorMid;
        else                      fillColor = ColorHigh;

        if (aggressionFill != null)
            aggressionFill.color = fillColor;

        // Reactive label with percentage
        int pct = Mathf.RoundToInt(value01 * 100);
        string stateStr;
        if (value01 < 0.3f)       stateStr = "REACTIVE";
        else if (value01 < 0.6f)  stateStr = "HUNTING";
        else                      stateStr = "ENRAGED";

        if (reactiveLabel != null)
            reactiveLabel.text = $"{pct}% \u00b7 {stateStr}";

        // Blink active label when aggression is high
        if (value01 >= 0.6f)
        {
            if (activeLabel != null && blinkRoutine == null)
            {
                activeLabel.gameObject.SetActive(true);
                blinkRoutine = StartCoroutine(HUDAnimations.BlinkCoroutine(activeLabel, 0.6f));
            }
        }
        else if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
            if (activeLabel != null)
            {
                HUDAnimations.StopBlink(activeLabel);
                activeLabel.gameObject.SetActive(false);
            }
        }
    }

    public void SetAggressionState(string stateLabel)
    {
        if (reactiveLabel != null)
        {
            int pct = aggressionBar != null ? Mathf.RoundToInt(aggressionBar.value * 100) : 0;
            reactiveLabel.text = $"{pct}% \u00b7 {stateLabel}";
        }
    }

    void OnPhaseChanged(int phase)
    {
        SetPhase(phase);
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
