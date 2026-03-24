using UnityEngine;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [Header("Sub-Controllers")]
    public ShipHealthUI  shipHealth;
    public CoreXUI       coreX;
    public FailureListUI failureList;
    public PlayerSlotsUI playerSlots;
    public WinLoseUI     winLose;

    [Header("Style")]
    [SerializeField] UIStyleConfig style;

    public UIStyleConfig Style => style;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
}
