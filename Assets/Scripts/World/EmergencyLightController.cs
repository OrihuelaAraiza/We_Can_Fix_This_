using UnityEngine;

public class EmergencyLightController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Light directionalLight;

    [Header("Normal State")]
    [SerializeField] Color normalColor     = new Color(0.4f, 0.5f, 0.8f);
    [SerializeField] float normalIntensity = 0.8f;

    [Header("Critical State")]
    [SerializeField] Color criticalColor     = new Color(0.8f, 0.1f, 0.05f);
    [SerializeField] float criticalIntensity = 1.2f;
    [SerializeField] float flickerSpeed      = 3f;

    bool  isCritical;
    float flickerTimer;

    void OnEnable()
    {
        ShipHealth.OnShipCritical  += OnCritical;
        ShipHealth.OnShipDestroyed += OnDestroyed;
        ShipHealth.OnShipRecovered += OnRecovered;
    }

    void OnDisable()
    {
        ShipHealth.OnShipCritical  -= OnCritical;
        ShipHealth.OnShipDestroyed -= OnDestroyed;
        ShipHealth.OnShipRecovered -= OnRecovered;
    }

    void Start()
    {
        if (directionalLight == null)
            directionalLight = FindObjectOfType<Light>();
        ApplyNormal();
    }

    void Update()
    {
        if (!isCritical) return;
        flickerTimer += Time.deltaTime * flickerSpeed;
        float f = 0.6f + 0.4f * Mathf.Sin(flickerTimer);
        if (directionalLight != null)
            directionalLight.intensity = criticalIntensity * f;
    }

    void OnCritical()
    {
        isCritical = true;
        if (directionalLight != null)
            directionalLight.color = criticalColor;
    }

    void OnRecovered()
    {
        isCritical = false;
        ApplyNormal();
        Debug.Log("[EmergencyLight] Normal restored");
    }

    void OnDestroyed()
    {
        isCritical = false;
        if (directionalLight != null)
        {
            directionalLight.color     = Color.red;
            directionalLight.intensity = 0.2f;
        }
    }

    void ApplyNormal()
    {
        if (directionalLight == null) return;
        directionalLight.color     = normalColor;
        directionalLight.intensity = normalIntensity;
    }
}
