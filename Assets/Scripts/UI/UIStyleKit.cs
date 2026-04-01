using UnityEngine;
using TMPro;

/// <summary>
/// ScriptableObject centralizado con todos los tokens del design system "lo-fi minimal".
/// </summary>
/// <setup>
/// SETUP EN UNITY EDITOR:
/// 1. Assets > Create > WCFT > UIStyleKit  → guardar en Assets/Resources/UIStyleKit.asset
/// 2. Asignar las fuentes VT323 y Caveat Bold en los campos terminalFont / handwrittenFont
/// 3. Los colores ya tienen defaults del design system; ajustar si es necesario
/// 4. UIBase.cs carga este asset vía Resources.Load — debe estar en Assets/Resources/
/// </setup>
[CreateAssetMenu(fileName = "UIStyleKit", menuName = "WCFT/UIStyleKit")]
public class UIStyleKit : ScriptableObject
{
    // ── Fuentes ──────────────────────────────────────────────────
    [Header("Fonts")]
    public TMP_FontAsset terminalFont;     // VT323
    public TMP_FontAsset handwrittenFont;  // Caveat Bold 700

    // ── Fondos y paneles ─────────────────────────────────────────
    [Header("Background / Panel Colors")]
    public Color bgRoot       = Hex("#0F1215");  // fondo de pantalla general
    public Color bgPanel      = Hex("#151A1E");  // fondo de cada panel
    public Color borderPanel  = Hex("#242A2F");  // borde de paneles
    public Color borderCorner = Hex("#383F46");  // esquinas decorativas
    public Color bgBar        = Hex("#0A0C0E");  // fondo de barras
    public Color borderBar    = Hex("#1E2428");

    // ── Texto ─────────────────────────────────────────────────────
    [Header("Text Colors")]
    public Color labelColor    = Hex("#384048");  // etiquetas uppercase
    public Color textOrange    = Hex("#C07810");  // valores críticos / salud nave
    public Color textRed       = Hex("#A81C1C");  // Core-X / peligro
    public Color textGreen     = Hex("#287838");  // victoria / sistemas OK
    public Color textYellow    = Hex("#C8A820");  // notas handwritten
    public Color textRedHW     = Hex("#C03030");  // advertencias handwritten
    public Color textBlue      = Hex("#6088B8");  // rol Hacker / info secundaria
    public Color textAmber     = Hex("#B89840");  // rol Gunner / advertencia media
    public Color textGrayMuted = Hex("#2A2A2A");  // slots vacíos / inactive

    // ── Decoraciones ──────────────────────────────────────────────
    [Header("Decoration Colors")]
    public Color tapeBase    = Hex("#8A8278");
    public Color bandaidBase = Hex("#B07840");
    public Color patchDark   = Hex("#323C28");
    public Color postitBg    = Hex("#D8C838");
    public Color postitText  = Hex("#242000");
    public Color screwBg     = Hex("#1E2228");
    public Color screwBorder = Hex("#383E44");

    // ── Botones ───────────────────────────────────────────────────
    [Header("Button Colors")]
    public Color btnGreenBg     = Hex("#183C20");
    public Color btnGreenText   = Hex("#70B888");
    public Color btnGreenBorder = Hex("#205028");
    public Color btnNeutralBg     = Hex("#1A1E28");
    public Color btnNeutralText   = Hex("#8090A8");
    public Color btnNeutralBorder = Hex("#252B38");
    public Color btnRedBg     = Hex("#381818");
    public Color btnRedText   = Hex("#A85050");
    public Color btnRedBorder = Hex("#502020");

    // ── Colores de jugadores (por índice 0-3) ─────────────────────
    [Header("Player Slot Colors (index 0-3)")]
    public Color[] playerBorderEmpty  = { Hex("#1E2820"), Hex("#181C2A"), Hex("#2A2010"), Hex("#201820") };
    public Color[] playerBorderJoined = { Hex("#285828"), Hex("#182038"), Hex("#382808"), Hex("#281828") };
    public Color[] playerBorderReady  = { Hex("#38803A"), Hex("#2030A0"), Hex("#906020"), Hex("#803080") };

    // ── Animación ─────────────────────────────────────────────────
    [Header("Animation")]
    public float animScanSpeed  = 7f;    // segundos para recorrer pantalla
    public float blinkInterval  = 1.1f;  // segundos entre parpadeos

    // ── Utilidad ──────────────────────────────────────────────────
    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Re-parse defaults si se resetean accidentalmente
        bgRoot        = Hex("#0F1215");
        bgPanel       = Hex("#151A1E");
        borderPanel   = Hex("#242A2F");
        borderCorner  = Hex("#383F46");
        bgBar         = Hex("#0A0C0E");
        borderBar     = Hex("#1E2428");
        labelColor    = Hex("#384048");
        textOrange    = Hex("#C07810");
        textRed       = Hex("#A81C1C");
        textGreen     = Hex("#287838");
        textYellow    = Hex("#C8A820");
        textRedHW     = Hex("#C03030");
        textBlue      = Hex("#6088B8");
        textAmber     = Hex("#B89840");
        textGrayMuted = Hex("#2A2A2A");
        tapeBase      = Hex("#8A8278");
        bandaidBase   = Hex("#B07840");
        patchDark     = Hex("#323C28");
        postitBg      = Hex("#D8C838");
        postitText    = Hex("#242000");
        screwBg       = Hex("#1E2228");
        screwBorder   = Hex("#383E44");
    }
#endif
}
