using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShipHealthUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Slider            healthSlider;
    [SerializeField] TextMeshProUGUI   healthText;
    [SerializeField] TextMeshProUGUI   statusText;
    [SerializeField] Image             fillImage;

    [Header("Colors")]
    [SerializeField] Color colorGood     = new Color(0.2f, 0.8f, 0.3f);
    [SerializeField] Color colorWarning  = new Color(0.9f, 0.6f, 0.1f);
    [SerializeField] Color colorCritical = new Color(0.9f, 0.2f, 0.1f);

    void OnEnable()
    {
        ShipHealth.OnHealthChanged  += UpdateHealth;
        ShipHealth.OnShipCritical   += ShowCritical;
        ShipHealth.OnShipRecovered  += ShowRecovered;
        ShipHealth.OnShipDestroyed  += ShowDestroyed;
    }

    void OnDisable()
    {
        ShipHealth.OnHealthChanged  -= UpdateHealth;
        ShipHealth.OnShipCritical   -= ShowCritical;
        ShipHealth.OnShipRecovered  -= ShowRecovered;
        ShipHealth.OnShipDestroyed  -= ShowDestroyed;
    }

    void Start()
    {
        // Inicializar UI limpia
        SetStatus("", Color.white);
        UpdateHealth(1f);
    }

    void UpdateHealth(float normalized)
    {
        if (healthSlider != null)
            healthSlider.value = normalized;

        if (healthText != null)
            healthText.text = $"{Mathf.RoundToInt(normalized * 100)}%";

        if (fillImage != null)
        {
            if (normalized > 0.6f)       fillImage.color = colorGood;
            else if (normalized > 0.3f)  fillImage.color = colorWarning;
            else                         fillImage.color = colorCritical;
        }

        // Limpiar status si salud está bien y no hay texto de destroyed
        if (normalized > 0.3f && statusText != null
            && statusText.text != "□ DESTROYED")
        {
            SetStatus("", Color.white);
        }
    }

    void ShowCritical()
    {
        SetStatus("□ CRITICAL", colorCritical);
    }

    void ShowRecovered()
    {
        SetStatus("", Color.white);
        Debug.Log("[ShipHealthUI] Status cleared — ship recovered");
    }

    void ShowDestroyed()
    {
        SetStatus("□ DESTROYED", colorCritical);
    }

    void SetStatus(string msg, Color color)
    {
        if (statusText == null) return;
        statusText.text  = msg;
        statusText.color = color;
    }
}
