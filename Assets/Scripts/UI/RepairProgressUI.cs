using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Indicador world-space para cada RepairStation. Si no hay referencias
// asignadas en escena, construye un panel compacto automáticamente.
public class RepairProgressUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private GameObject progressRoot;
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image panelBackground;
    [SerializeField] private Image statusDot;
    [SerializeField] private Image progressFill;
    [SerializeField] private TextMeshProUGUI stationNameLabel;
    [SerializeField] private TextMeshProUGUI statusLabel;
    [SerializeField] private TextMeshProUGUI progressLabel;
    [SerializeField] private TextMeshProUGUI promptLabel;

    [Header("Layout")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.45f, 0f);
    [SerializeField] private float canvasScale = 0.006f;
    [SerializeField] private bool showWhenFunctional = true;

    static readonly Color PanelIdle      = HexColor("#11161bcc");
    static readonly Color PanelActive    = HexColor("#1b1511e6");
    static readonly Color ColorOnline    = HexColor("#33a45d");
    static readonly Color ColorBroken    = HexColor("#d94242");
    static readonly Color ColorRepairing = HexColor("#e7bf4e");
    static readonly Color ColorFixed     = HexColor("#51c6c8");
    static readonly Color TextPrimary    = HexColor("#f1e7c9");
    static readonly Color TextMuted      = HexColor("#b0a78e");

    private RepairStation station;
    private Camera mainCamera;
    private RectTransform panelRect;
    private RepairStation.StationState lastState;
    private float pulse;

    private void Awake()
    {
        station = GetComponentInParent<RepairStation>();
        mainCamera = Camera.main;
        EnsureUI();
        lastState = station != null ? station.State : RepairStation.StationState.Functional;
        RefreshVisuals(true);
    }

    private void Update()
    {
        if (station == null) return;

        RefreshVisuals(false);
    }

    private void LateUpdate()
    {
        if (worldCanvas == null)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera != null)
            worldCanvas.transform.rotation = Quaternion.LookRotation(worldCanvas.transform.position - mainCamera.transform.position);
    }

    void RefreshVisuals(bool force)
    {
        RepairStation.StationState state = station.State;
        float progress = Mathf.Clamp01(station.RepairProgress);
        bool activeState = state != RepairStation.StationState.Functional;
        bool show = showWhenFunctional || activeState;

        if (worldCanvas != null && worldCanvas.gameObject.activeSelf != show)
            worldCanvas.gameObject.SetActive(show);

        if (!show)
            return;

        bool stateChanged = force || state != lastState;
        lastState = state;

        if (progressRoot != null)
            progressRoot.SetActive(activeState);

        if (progressSlider != null)
            progressSlider.value = state == RepairStation.StationState.Fixed ? 1f : progress;

        if (progressFill != null)
            progressFill.color = StateColor(state);

        if (progressLabel != null)
            progressLabel.text = state == RepairStation.StationState.Repairing
                ? $"{Mathf.RoundToInt(progress * 100f)}%"
                : state == RepairStation.StationState.Fixed ? "100%" : "0%";

        if (stationNameLabel != null)
        {
            stationNameLabel.text = StationDisplayName(station.Type);
            stationNameLabel.color = TextPrimary;
        }

        if (statusLabel != null)
        {
            statusLabel.text = StateLabel(state, progress);
            statusLabel.color = StateColor(state);
        }

        if (promptLabel != null)
        {
            promptLabel.text = state switch
            {
                RepairStation.StationState.Broken => "HOLD INTERACT",
                RepairStation.StationState.Repairing => "REPAIRING",
                RepairStation.StationState.Fixed => "STABILIZED",
                _ => "SYSTEM READY"
            };
            promptLabel.color = state == RepairStation.StationState.Functional ? TextMuted : TextPrimary;
        }

        if (statusDot != null)
        {
            statusDot.color = StateColor(state);
            pulse += Time.deltaTime * (state == RepairStation.StationState.Broken ? 7f : 4f);
            float dotScale = state == RepairStation.StationState.Functional ? 1f : 1f + Mathf.Sin(pulse) * 0.16f;
            statusDot.rectTransform.localScale = Vector3.one * dotScale;
        }

        if (panelBackground != null && stateChanged)
            panelBackground.color = activeState ? PanelActive : PanelIdle;

        if (canvasGroup != null)
        {
            float targetAlpha = state == RepairStation.StationState.Functional ? 0.68f : 1f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * 8f);
        }
    }

    void EnsureUI()
    {
        if (progressSlider != null && progressRoot != null)
            return;

        GameObject canvasGO = new GameObject("StationWorldStatusUI", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = localOffset;
        canvasGO.transform.localRotation = Quaternion.identity;
        canvasGO.transform.localScale = Vector3.one * canvasScale;

        worldCanvas = canvasGO.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.sortingOrder = 35;

        canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0.68f;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 12f;

        panelRect = canvasGO.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(240f, 92f);

        panelBackground = CreateImage(canvasGO.transform, "Panel", PanelIdle);
        panelBackground.raycastTarget = false;
        Stretch(panelBackground.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var outline = panelBackground.gameObject.AddComponent<Outline>();
        outline.effectColor = HexColor("#5e5142aa");
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        statusDot = CreateImage(panelBackground.transform, "StatusDot", ColorOnline);
        Pin(statusDot.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -17f), new Vector2(13f, 13f));

        stationNameLabel = CreateLabel(panelBackground.transform, "StationName", 18f, TextPrimary, TextAlignmentOptions.Left);
        Pin(stationNameLabel.rectTransform, new Vector2(0f, 1f), new Vector2(32f, -10f), new Vector2(112f, 22f));

        statusLabel = CreateLabel(panelBackground.transform, "StatusLabel", 15f, ColorOnline, TextAlignmentOptions.Right);
        Pin(statusLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-12f, -11f), new Vector2(96f, 20f));

        progressRoot = new GameObject("RepairProgress", typeof(RectTransform));
        progressRoot.transform.SetParent(panelBackground.transform, false);
        var progressRT = progressRoot.GetComponent<RectTransform>();
        Pin(progressRT, new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(204f, 22f));

        progressSlider = progressRoot.AddComponent<Slider>();
        progressSlider.minValue = 0f;
        progressSlider.maxValue = 1f;
        progressSlider.interactable = false;
        progressSlider.transition = Selectable.Transition.None;

        Image track = CreateImage(progressRoot.transform, "Track", HexColor("#2c2420"));
        Stretch(track.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-44f, -8f));

        progressFill = CreateImage(track.transform, "Fill", ColorRepairing);
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        Stretch(progressFill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        progressSlider.fillRect = progressFill.rectTransform;
        progressSlider.targetGraphic = progressFill;

        progressLabel = CreateLabel(progressRoot.transform, "ProgressLabel", 14f, TextPrimary, TextAlignmentOptions.Right);
        Pin(progressLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(0f, -1f), new Vector2(40f, 18f));

        promptLabel = CreateLabel(panelBackground.transform, "PromptLabel", 12f, TextMuted, TextAlignmentOptions.Center);
        Pin(promptLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(210f, 18f));
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string name, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    static void Pin(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    static Color StateColor(RepairStation.StationState state)
    {
        return state switch
        {
            RepairStation.StationState.Broken => ColorBroken,
            RepairStation.StationState.Repairing => ColorRepairing,
            RepairStation.StationState.Fixed => ColorFixed,
            _ => ColorOnline
        };
    }

    static string StateLabel(RepairStation.StationState state, float progress)
    {
        return state switch
        {
            RepairStation.StationState.Broken => "FAILURE",
            RepairStation.StationState.Repairing => $"FIX {Mathf.RoundToInt(progress * 100f)}%",
            RepairStation.StationState.Fixed => "OK",
            _ => "ONLINE"
        };
    }

    static string StationDisplayName(RepairStation.StationType type)
    {
        return type switch
        {
            RepairStation.StationType.Energy => "POWER",
            RepairStation.StationType.Communications => "COMMS",
            RepairStation.StationType.Gravity => "GRAVITY",
            RepairStation.StationType.Hull => "HULL",
            _ => type.ToString().ToUpperInvariant()
        };
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
