using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD de estado de Core-X: fase actual y modo jefe.
/// Agregar a un Canvas con Image (fill), TextMeshProUGUI y panel de advertencia.
/// </summary>
public class CoreXHealthBar : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] private Image           phaseFill;
    [SerializeField] private TextMeshProUGUI phaseText;
    [SerializeField] private GameObject      bossWarningPanel;

    private void OnEnable()
    {
        CoreXBrain.OnPhaseChanged      += HandlePhaseChanged;
        CoreXBrain.OnBossModeActivated += HandleBossActivated;
        CoreXBrain.OnCoreXDefeated     += HandleDefeated;
    }

    private void OnDisable()
    {
        CoreXBrain.OnPhaseChanged      -= HandlePhaseChanged;
        CoreXBrain.OnBossModeActivated -= HandleBossActivated;
        CoreXBrain.OnCoreXDefeated     -= HandleDefeated;
    }

    private void Start()
    {
        if (bossWarningPanel != null) bossWarningPanel.SetActive(false);
        HandlePhaseChanged(0);
    }

    private void HandlePhaseChanged(int phase)
    {
        if (phaseText != null)
            phaseText.text = $"CORE-X — FASE {phase + 1}";

        if (phaseFill != null)
        {
            phaseFill.color = phase switch
            {
                0 => new Color(0.2f, 0.8f, 0.2f),  // verde
                1 => new Color(1f,   0.6f, 0f),     // naranja
                2 => new Color(0.9f, 0.1f, 0.1f),  // rojo
                _ => Color.red
            };
            // Fill avanza con cada fase (visual de "vida del jefe")
            phaseFill.fillAmount = 1f - (phase / 3f);
        }
    }

    private void HandleBossActivated()
    {
        if (bossWarningPanel != null) bossWarningPanel.SetActive(true);
        if (phaseText != null)      phaseText.text = "! CORE-X — MODO CRITICO !";
        if (phaseFill != null)
        {
            phaseFill.color      = Color.red;
            phaseFill.fillAmount = 0.15f;
        }
    }

    private void HandleDefeated()
    {
        if (bossWarningPanel != null) bossWarningPanel.SetActive(false);
        if (phaseText != null)      phaseText.text = "CORE-X ELIMINADO";
        if (phaseFill != null)
        {
            phaseFill.color      = Color.gray;
            phaseFill.fillAmount = 0f;
        }
    }
}
