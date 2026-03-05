using UnityEngine;

// Controla las luces de emergencia globales de la nave
public class EmergencyLightController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Light directionalLight;

    [Header("Normal State")]
    [SerializeField] private Color normalColor     = new Color(0.4f, 0.5f, 0.8f);
    [SerializeField] private float normalIntensity = 0.8f;

    [Header("Critical State")]
    [SerializeField] private Color criticalColor     = new Color(0.8f, 0.1f, 0.05f);
    [SerializeField] private float criticalIntensity = 1.2f;
    [SerializeField] private float flickerSpeed      = 3f;

    private bool isCritical;
    private float flickerTimer;

    private void Start()
    {
        if (directionalLight == null)
            directionalLight = FindObjectOfType<Light>();

        if (ShipHealth.Instance != null)
        {
            ShipHealth.Instance.OnShipCritical  += OnCritical;
            ShipHealth.Instance.OnShipDestroyed += OnDestroyed;
            ShipHealth.OnShipRecovered += OnRecovered;
        }
        ApplyNormal();
    }

    private void OnDestroy()
    {
        if (ShipHealth.Instance != null)
        {
            ShipHealth.Instance.OnShipCritical  -= OnCritical;
            ShipHealth.Instance.OnShipDestroyed -= OnDestroyed;
            ShipHealth.OnShipRecovered -= OnRecovered;
        }
    }

    private void Update()
    {
        if (!isCritical) return;
        flickerTimer += Time.deltaTime * flickerSpeed;
        float flicker = 0.6f + 0.4f * Mathf.Sin(flickerTimer);
        if (directionalLight != null)
            directionalLight.intensity = criticalIntensity * flicker;
    }

    private void OnCritical()
    {
        isCritical = true;
        if (directionalLight != null)
            directionalLight.color = criticalColor;
        Debug.Log("[EmergencyLight] CRITICAL mode activated");
    }

    private void OnRecovered()
    {
        isCritical = false;
        ApplyNormal();
        Debug.Log("[EmergencyLight] Normal mode restored");
    }

    private void OnDestroyed()
    {
        if (directionalLight != null)
        {
            directionalLight.color     = Color.red;
            directionalLight.intensity = 0.2f;
        }
    }

    private void ApplyNormal()
    {
        if (directionalLight == null) return;
        directionalLight.color     = normalColor;
        directionalLight.intensity = normalIntensity;
    }
}
