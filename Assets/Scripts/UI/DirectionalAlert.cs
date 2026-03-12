using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Muestra flechas en el borde de la pantalla apuntando hacia
/// estaciones rotas que están fuera del campo visual.
/// Similar al sistema de alertas de Among Us / Overcooked.
/// </summary>
public class DirectionalAlert : MonoBehaviour
{
    [Header("Prefab de flecha")]
    [SerializeField] GameObject arrowPrefab; // Image con texto opcional

    [Header("Config")]
    [SerializeField] Camera gameCamera;
    [SerializeField] float  edgeMargin = 70f;

    // Diccionario: estación -> flecha UI activa
    Dictionary<RepairStation, RectTransform> activeArrows = new();

    Canvas        parentCanvas;
    RectTransform canvasRect;

    // ─────────────────────────────────────────────────────────────
    void OnEnable()
    {
        FailureSystem.OnStationFailed   += ShowArrow;
        FailureSystem.OnStationRepaired += HideArrow;
    }

    void OnDisable()
    {
        FailureSystem.OnStationFailed   -= ShowArrow;
        FailureSystem.OnStationRepaired -= HideArrow;
    }

    void Start()
    {
        if (gameCamera == null) gameCamera = Camera.main;
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
            canvasRect = parentCanvas.GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        foreach (var kvp in activeArrows)
        {
            if (kvp.Key == null || kvp.Value == null) continue;
            UpdateArrow(kvp.Key, kvp.Value);
        }
    }

    // ─────────────────────────────────────────────────────────────

    void ShowArrow(RepairStation station)
    {
        if (activeArrows.ContainsKey(station)) return;

        GameObject arrow = arrowPrefab != null
            ? Instantiate(arrowPrefab, transform)
            : CreateDefaultArrow(station);

        activeArrows[station] = arrow.GetComponent<RectTransform>();
    }

    void HideArrow(RepairStation station)
    {
        if (!activeArrows.TryGetValue(station, out var arrow)) return;
        if (arrow != null) Destroy(arrow.gameObject);
        activeArrows.Remove(station);
    }

    void UpdateArrow(RepairStation station, RectTransform arrowRT)
    {
        if (gameCamera == null || canvasRect == null) return;

        Vector3 screenPos = gameCamera.WorldToScreenPoint(station.transform.position);
        bool inScreen = screenPos.z > 0
            && screenPos.x > 0 && screenPos.x < Screen.width
            && screenPos.y > 0 && screenPos.y < Screen.height;

        // Solo mostrar cuando la estación está fuera de pantalla
        arrowRT.gameObject.SetActive(!inScreen);
        if (inScreen) return;

        // Dirección desde el centro hacia la estación
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 dir    = new Vector2(screenPos.x, screenPos.y) - center;
        if (screenPos.z < 0) dir = -dir;
        dir.Normalize();

        // Posición en el borde de pantalla
        float hw  = Screen.width  * 0.5f - edgeMargin;
        float hh  = Screen.height * 0.5f - edgeMargin;
        float cos = Mathf.Cos(Mathf.Atan2(dir.y, dir.x));
        float sin = Mathf.Sin(Mathf.Atan2(dir.y, dir.x));
        float t   = Mathf.Min(
            cos != 0f ? hw / Mathf.Abs(cos) : float.MaxValue,
            sin != 0f ? hh / Mathf.Abs(sin) : float.MaxValue);

        Vector2 edgePos = center + dir * t;

        // Convertir a coordenadas del canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, edgePos, null, out Vector2 localPos);
        arrowRT.anchoredPosition = localPos;

        // Rotar la flecha para que apunte en la dirección correcta
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        arrowRT.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    GameObject CreateDefaultArrow(RepairStation station)
    {
        var go = new GameObject($"Arrow_{station.Type}");
        go.transform.SetParent(transform);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(40f, 50f);

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 0.3f, 0.1f, 0.9f);

        // Label del tipo de sala debajo de la flecha
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform);

        var lrt = labelGO.AddComponent<RectTransform>();
        lrt.anchoredPosition = new Vector2(0f, -30f);
        lrt.sizeDelta        = new Vector2(80f, 20f);

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = station.Type.ToString().Substring(0, 3).ToUpper();
        tmp.fontSize  = 12f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        return go;
    }
}
