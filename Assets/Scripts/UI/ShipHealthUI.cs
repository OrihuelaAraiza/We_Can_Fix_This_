using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShipHealthUI : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image  fillImage;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI statusText;

    private static readonly Color ColorSafe     = new Color(0.2f, 0.8f, 0.2f);
    private static readonly Color ColorWarning  = new Color(1f,   0.6f, 0f);
    private static readonly Color ColorCritical = new Color(0.9f, 0.1f, 0.1f);

    private void Start()
    {
        if (ShipHealth.Instance == null) return;
        ShipHealth.Instance.OnHealthChanged += UpdateUI;
        ShipHealth.Instance.OnShipCritical  += OnCritical;
        ShipHealth.Instance.OnShipDestroyed += OnDestroyed;
        UpdateUI(1f);
    }

    private void OnDestroy()
    {
        if (ShipHealth.Instance == null) return;
        ShipHealth.Instance.OnHealthChanged -= UpdateUI;
        ShipHealth.Instance.OnShipCritical  -= OnCritical;
        ShipHealth.Instance.OnShipDestroyed -= OnDestroyed;
    }

    private void UpdateUI(float percent)
    {
        if (healthSlider) healthSlider.value = percent;
        if (healthText)   healthText.text    = $"{Mathf.RoundToInt(percent * 100)}%";
        if (fillImage)
        {
            fillImage.color = percent > 0.6f ? ColorSafe :
                              percent > 0.3f ? ColorWarning : ColorCritical;
        }
    }

    private void OnCritical()
    {
        if (statusText) statusText.text = "⚠ CRITICAL";
    }

    private void OnDestroyed()
    {
        if (statusText) statusText.text = "✗ DESTROYED";
    }
}
