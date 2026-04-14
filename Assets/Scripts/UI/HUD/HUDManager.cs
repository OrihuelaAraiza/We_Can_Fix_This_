using UnityEngine;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    static readonly Vector2 HudReferenceResolution = new Vector2(1280f, 720f);
    static readonly Vector3 HudPanelScale          = Vector3.one * 1.1f;
    static readonly string[] ScaledPanelNames =
    {
        "ShipHealthPanel",
        "TimerPanel",
        "CoreXPanel",
        "StationStatusPanel",
        "PlayerSlots",
    };

    public static HUDManager Instance { get; private set; }

    [Header("Sub-Controllers")]
    public ShipHealthUI    shipHealth;
    public CoreXUI         coreX;
    public FailureListUI   failureList;
    public StationStatusUI stationStatus;
    public SurvivalTimerUI timer;
    public PlayerSlotsUI   playerSlots;
    public WinLoseUI       winLose;

    [Header("Style")]
    [SerializeField] UIStyleConfig style;

    public UIStyleConfig Style => style;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        ApplyResponsiveCanvasScale();
        ApplyAccessibilityScale();
    }

    void ApplyResponsiveCanvasScale()
    {
        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) return;

        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = HudReferenceResolution;
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;
    }

    void ApplyAccessibilityScale()
    {
        foreach (var panelName in ScaledPanelNames)
        {
            var panel = transform.Find(panelName);
            if (panel != null)
                panel.localScale = HudPanelScale;
        }
    }
}
