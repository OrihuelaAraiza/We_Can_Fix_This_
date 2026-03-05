using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Muestra flechas en pantalla apuntando a estaciones rotas
public class AlertSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera gameCamera;
    [SerializeField] private RectTransform canvasRect;

    [Header("Alert Prefab")]
    [SerializeField] private GameObject alertPrefab; // prefab con Image + TMP

    [Header("Config")]
    [SerializeField] private float edgePadding = 60f;
    [SerializeField] private Color alertColor  = new Color(1f, 0.3f, 0.1f);

    private Dictionary<RepairStation, GameObject> activeAlerts = new();

    private void OnEnable()
    {
        FailureSystem.OnStationFailed   += ShowAlert;
        FailureSystem.OnStationRepaired += HideAlert;
    }

    private void OnDisable()
    {
        FailureSystem.OnStationFailed   -= ShowAlert;
        FailureSystem.OnStationRepaired -= HideAlert;
    }

    private void Start()
    {
        if (gameCamera == null)
            gameCamera = Camera.main;
        if (canvasRect == null)
            canvasRect = GetComponentInParent<Canvas>().GetComponent<RectTransform>();
    }

    private void LateUpdate()
    {
        foreach (var kvp in activeAlerts)
            UpdateAlertPosition(kvp.Key, kvp.Value);
    }

    private void ShowAlert(RepairStation station)
    {
        if (activeAlerts.ContainsKey(station)) return;

        GameObject alert = alertPrefab != null
            ? Instantiate(alertPrefab, transform)
            : CreateDefaultAlert();

        activeAlerts[station] = alert;

        // Etiqueta del tipo
        var label = alert.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = $"⚠ {station.Type}";
    }

    private void HideAlert(RepairStation station)
    {
        if (!activeAlerts.TryGetValue(station, out var alert)) return;
        Destroy(alert);
        activeAlerts.Remove(station);
    }

    private void UpdateAlertPosition(RepairStation station, GameObject alertObj)
    {
        if (station == null || alertObj == null) return;

        Vector3 screenPos = gameCamera.WorldToScreenPoint(station.transform.position);
        bool isVisible = screenPos.z > 0
            && screenPos.x > 0 && screenPos.x < Screen.width
            && screenPos.y > 0 && screenPos.y < Screen.height;

        alertObj.SetActive(!isVisible); // solo mostrar cuando fuera de pantalla

        if (!isVisible)
        {
            // Calcular dirección y posicionar en borde de pantalla
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = new Vector2(screenPos.x, screenPos.y) - screenCenter;

            if (screenPos.z < 0) dir = -dir;

            dir.Normalize();
            float angle = Mathf.Atan2(dir.y, dir.x);

            float hw = Screen.width  * 0.5f - edgePadding;
            float hh = Screen.height * 0.5f - edgePadding;

            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            float t   = Mathf.Min(
                cos != 0 ? hw / Mathf.Abs(cos) : float.MaxValue,
                sin != 0 ? hh / Mathf.Abs(sin) : float.MaxValue);

            Vector2 edgePos = screenCenter + dir * t;

            var rt = alertObj.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = edgePos - screenCenter;

            // Rotar flecha hacia la estación
            float degrees = angle * Mathf.Rad2Deg;
            alertObj.transform.rotation = Quaternion.Euler(0, 0, degrees);
        }
    }

    private GameObject CreateDefaultAlert()
    {
        // Alert básico sin prefab
        var go = new GameObject("Alert");
        go.transform.SetParent(transform);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(80, 30);

        var img = go.AddComponent<Image>();
        img.color = alertColor;

        var textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform);

        var trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize  = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        return go;
    }
}
