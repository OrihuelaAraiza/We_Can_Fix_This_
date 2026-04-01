using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Efecto de barrido scanline estilo CRT.
/// Mueve una línea horizontal delgada de arriba hacia abajo en bucle continuo.
/// </summary>
/// <setup>
/// SETUP EN UNITY EDITOR:
/// 1. Crear un GameObject hijo del Canvas llamado "ScanLine"
/// 2. Añadir RectTransform + Image + este script
/// 3. Image: color blanco con alpha muy bajo (R:255 G:255 B:255 A:5)
/// 4. RectTransform: Anchor Stretch-Horizontal, Height = 2, Pivot = (0.5, 0.5)
/// 5. El GameObject debe estar al final de la jerarquía para renderizar sobre todo
/// 6. CanvasGroup con Blocks Raycasts = false para no interferir con clicks
/// </setup>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class CRTScanEffect : MonoBehaviour
{
    [SerializeField] float cycleDuration = 7f;  // segundos para recorrer pantalla completa

    RectTransform rt;
    float         screenH;
    float         topY;
    float         bottomY;
    float         elapsed;

    void Start()
    {
        rt = GetComponent<RectTransform>();

        // Asegurar apariencia correcta
        var img = GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.02f);

        // Ficar alto en 2px
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, 2f);

        ResetPosition();
    }

    void Update()
    {
        screenH = Screen.height;
        topY    =  screenH * 1.05f;
        bottomY = -screenH * 0.05f;

        elapsed += Time.unscaledDeltaTime;
        float t = elapsed / cycleDuration;

        if (t >= 1f)
        {
            elapsed = 0f;
            t       = 0f;
            ResetPosition();
        }

        rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(topY, bottomY, t));
    }

    void ResetPosition()
    {
        if (rt != null)
            rt.anchoredPosition = new Vector2(0f, Screen.height * 1.05f);
    }
}
