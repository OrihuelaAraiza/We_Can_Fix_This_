using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controlador maestro del HUD de gameplay.
/// Escucha eventos del juego y actualiza todos los sub-elementos.
/// </summary>
public class GameplayHUD : MonoBehaviour
{
    [Header("=== NAVE HEALTH ===")]
    [SerializeField] Slider            shipHealthSlider;
    [SerializeField] Image             shipHealthFill;
    [SerializeField] TextMeshProUGUI   shipHealthPercent;
    [SerializeField] TextMeshProUGUI   shipStatusText;
    [SerializeField] GameObject        criticalFlashPanel; // panel rojo semitransparente

    [Header("=== CORE-X STATUS ===")]
    [SerializeField] TextMeshProUGUI   coreXPhaseText;
    [SerializeField] Image             coreXAggressionFill;
    [SerializeField] TextMeshProUGUI   coreXAggressionLabel;
    [SerializeField] GameObject        bossWarningPanel;

    [Header("=== FALLAS ACTIVAS ===")]
    [SerializeField] GameObject        failureListPanel;
    [SerializeField] TextMeshProUGUI   failureListText;
    [SerializeField] Image             failurePanelBg;

    [Header("=== PAUSA ===")]
    [SerializeField] PauseMenuUI pauseMenu;  // asignar desde el Inspector

    [Header("=== COLORES ===")]
    [SerializeField] Color colorHealthGood     = new Color(0.15f, 0.80f, 0.35f);
    [SerializeField] Color colorHealthWarning  = new Color(0.95f, 0.65f, 0.10f);
    [SerializeField] Color colorHealthCritical = new Color(0.90f, 0.15f, 0.10f);
    [SerializeField] Color colorPhase1         = new Color(0.20f, 0.75f, 0.20f);
    [SerializeField] Color colorPhase2         = new Color(0.95f, 0.55f, 0.10f);
    [SerializeField] Color colorPhase3         = new Color(0.90f, 0.15f, 0.10f);

    // ── Estado interno ──────────────────────────────────────────
    int   activeFailureCount = 0;
    bool  isCritical         = false;

    System.Collections.Generic.List<string> activeFailureNames =
        new System.Collections.Generic.List<string>();

    // Mapa de nombres amigables por tipo de estación
    static readonly System.Collections.Generic.Dictionary<string, string> RoomNames =
        new System.Collections.Generic.Dictionary<string, string>
    {
        { "Energy",         "ENERGIA"         },
        { "Communications", "COMUNICACIONES"  },
        { "Gravity",        "GRAVEDAD"        },
        { "Hull",           "CASCO"           }
    };

    // ─────────────────────────────────────────────────────────────
    void OnEnable()
    {
        // REGLA: todos los eventos son ESTATICOS, acceder por clase
        ShipHealth.OnHealthChanged    += OnHealthChanged;
        ShipHealth.OnShipCritical     += OnShipCritical;
        ShipHealth.OnShipRecovered    += OnShipRecovered;
        ShipHealth.OnShipDestroyed    += OnShipDestroyed;

        FailureSystem.OnStationFailed   += OnStationFailed;
        FailureSystem.OnStationRepaired += OnStationRepaired;

        CoreXBrain.OnPhaseChanged      += OnCoreXPhaseChanged;
        CoreXBrain.OnBossModeActivated += OnBossModeActivated;
    }

    void OnDisable()
    {
        ShipHealth.OnHealthChanged    -= OnHealthChanged;
        ShipHealth.OnShipCritical     -= OnShipCritical;
        ShipHealth.OnShipRecovered    -= OnShipRecovered;
        ShipHealth.OnShipDestroyed    -= OnShipDestroyed;

        FailureSystem.OnStationFailed   -= OnStationFailed;
        FailureSystem.OnStationRepaired -= OnStationRepaired;

        CoreXBrain.OnPhaseChanged      -= OnCoreXPhaseChanged;
        CoreXBrain.OnBossModeActivated -= OnBossModeActivated;
    }

    void Start()
    {
        SetShipStatus("", Color.white);
        SetFailurePanel(false);
        if (bossWarningPanel   != null) bossWarningPanel.SetActive(false);
        if (criticalFlashPanel != null) criticalFlashPanel.SetActive(false);

        OnHealthChanged(1f);
        OnCoreXPhaseChanged(0);
    }

    void Update()
    {
        // Suspender actualizaciones mientras el juego está pausado
        if (pauseMenu != null && pauseMenu.IsPaused) return;

        if (CoreXBrain.Instance == null) return;

        float aggression = CoreXBrain.Instance.AggressionLevel;
        if (coreXAggressionFill != null)
            coreXAggressionFill.fillAmount = aggression;
        if (coreXAggressionLabel != null)
            coreXAggressionLabel.text = $"Agresion: {Mathf.RoundToInt(aggression * 100)}%";
    }

    // ════════════════════════════════════════════════════════════
    // SALUD DE NAVE
    // ════════════════════════════════════════════════════════════

    void OnHealthChanged(float normalized)
    {
        if (shipHealthSlider != null)
            shipHealthSlider.value = normalized;

        if (shipHealthPercent != null)
            shipHealthPercent.text = $"{Mathf.RoundToInt(normalized * 100)}%";

        if (shipHealthFill != null)
        {
            Color target;
            if (normalized > 0.6f)      target = colorHealthGood;
            else if (normalized > 0.3f) target = colorHealthWarning;
            else                        target = colorHealthCritical;
            shipHealthFill.color = target;
        }

        if (normalized > 0.3f && !isCritical)
            SetShipStatus("", Color.white);
    }

    void OnShipCritical()
    {
        isCritical = true;
        SetShipStatus("! CRITICO", colorHealthCritical);
        StartCoroutine(FlashCriticalPanel());
    }

    void OnShipRecovered()
    {
        isCritical = false;
        SetShipStatus("", Color.white);
        if (criticalFlashPanel != null) criticalFlashPanel.SetActive(false);
    }

    void OnShipDestroyed()
    {
        SetShipStatus("DESTRUIDA", colorHealthCritical);
        if (criticalFlashPanel != null) criticalFlashPanel.SetActive(true);
    }

    IEnumerator FlashCriticalPanel()
    {
        if (criticalFlashPanel == null) yield break;
        float duration = 3f;
        float elapsed  = 0f;
        while (elapsed < duration && isCritical)
        {
            criticalFlashPanel.SetActive(!criticalFlashPanel.activeSelf);
            yield return new WaitForSeconds(0.4f);
            elapsed += 0.4f;
        }
        criticalFlashPanel.SetActive(false);
    }

    void SetShipStatus(string msg, Color color)
    {
        if (shipStatusText == null) return;
        shipStatusText.text  = msg;
        shipStatusText.color = color;
    }

    // ════════════════════════════════════════════════════════════
    // FALLAS ACTIVAS
    // ════════════════════════════════════════════════════════════

    void OnStationFailed(RepairStation station)
    {
        string key  = station.Type.ToString();
        string name = RoomNames.ContainsKey(key) ? RoomNames[key] : key;

        if (!activeFailureNames.Contains(name))
            activeFailureNames.Add(name);

        activeFailureCount++;
        UpdateFailurePanel();
    }

    void OnStationRepaired(RepairStation station)
    {
        string key  = station.Type.ToString();
        string name = RoomNames.ContainsKey(key) ? RoomNames[key] : key;
        activeFailureNames.Remove(name);

        activeFailureCount = Mathf.Max(0, activeFailureCount - 1);
        UpdateFailurePanel();
    }

    void UpdateFailurePanel()
    {
        if (activeFailureNames.Count == 0)
        {
            SetFailurePanel(false);
            return;
        }

        SetFailurePanel(true);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("! FALLAS ACTIVAS:");
        foreach (var n in activeFailureNames)
            sb.AppendLine($"  -> {n}");

        if (failureListText != null)
            failureListText.text = sb.ToString();

        if (failurePanelBg != null)
            failurePanelBg.color = activeFailureNames.Count >= 2
                ? new Color(0.6f, 0.05f, 0.05f, 0.88f)
                : new Color(0.6f, 0.35f, 0.05f, 0.88f);
    }

    void SetFailurePanel(bool active)
    {
        if (failureListPanel != null)
            failureListPanel.SetActive(active);
    }

    // ════════════════════════════════════════════════════════════
    // CORE-X STATUS
    // ════════════════════════════════════════════════════════════

    void OnCoreXPhaseChanged(int phase)
    {
        string[] phaseNames = { "Fase 1 - Reactivo", "Fase 2 - Agresivo", "Fase 3 - Critico" };
        if (coreXPhaseText != null)
            coreXPhaseText.text = phase < phaseNames.Length
                ? $"CORE-X  {phaseNames[phase]}"
                : $"CORE-X  Fase {phase + 1}";

        if (coreXAggressionFill != null)
        {
            coreXAggressionFill.color = phase switch
            {
                0 => colorPhase1,
                1 => colorPhase2,
                _ => colorPhase3
            };
        }
    }

    void OnBossModeActivated()
    {
        if (coreXPhaseText != null)
        {
            coreXPhaseText.text  = "! CORE-X  MODO BOSS";
            coreXPhaseText.color = colorHealthCritical;
        }
        if (bossWarningPanel != null)
            bossWarningPanel.SetActive(true);
    }

    // ════════════════════════════════════════════════════════════
    // API PÚBLICA — llamada por sistemas externos
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Actualiza la barra de integridad de la nave con los colores del design system.
    /// normalized: 0-1. Llama también a OnHealthChanged para mantener sincronía.
    /// </summary>
    public void UpdateCorexisIntegrity(float normalized)
    {
        OnHealthChanged(normalized);

        // Override de colores exactos del design system (distintos a los serializados)
        if (shipHealthFill != null)
        {
            Color c;
            if      (normalized > 0.6f) c = new Color(0.722f, 0.431f, 0.031f); // #B86E08
            else if (normalized > 0.3f) c = new Color(0.596f, 0.439f, 0.125f); // #987020
            else                        c = new Color(0.596f, 0.125f, 0.125f); // #982020
            shipHealthFill.color = c;
        }

        if (normalized <= 0.3f && !isCritical)
            OnShipCritical();
        else if (normalized > 0.3f && isCritical)
            OnShipRecovered();
    }

    /// <summary>
    /// Actualiza el panel Core-X con fase y nivel de agresión directamente.
    /// </summary>
    public void SetCoreXPhase(int phase, float aggression)
    {
        OnCoreXPhaseChanged(phase);

        if (coreXAggressionFill != null)
            coreXAggressionFill.fillAmount = Mathf.Clamp01(aggression);
        if (coreXAggressionLabel != null)
            coreXAggressionLabel.text = $"Agresion: {Mathf.RoundToInt(aggression * 100)}%";

        if (coreXPhaseText != null)
            coreXPhaseText.text = $"PHASE {phase + 1}";
    }
}
