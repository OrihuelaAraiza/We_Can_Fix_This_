using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StationStatusUI : MonoBehaviour
{
    [System.Serializable]
    public class StationRow
    {
        public string   stationId;
        public Image    statusDot;
        public TMP_Text nameText;
        public TMP_Text statusLabel;
        public Image    progressTrack;
        public Image    progressFill;
    }

    [SerializeField] StationRow[] rows;

    static readonly Color COL_OK       = HexColor("#33a45d");
    static readonly Color COL_OK_TEXT  = HexColor("#bfd8bc");
    static readonly Color COL_OK_SUB   = HexColor("#8fc29a");
    static readonly Color COL_WARN     = HexColor("#d29a2f");
    static readonly Color COL_WARN_TXT = HexColor("#f0d287");
    static readonly Color COL_WARN_SUB = HexColor("#d9a84f");
    static readonly Color COL_CRIT     = HexColor("#d94242");
    static readonly Color COL_CRIT_TXT = HexColor("#ff9b9b");
    static readonly Color COL_CRIT_SUB = HexColor("#f06b6b");
    static readonly Color COL_FIXED    = HexColor("#51c6c8");
    static readonly Color COL_TRACK    = HexColor("#2a2420");

    Coroutine[] _blinks;
    RepairStation[] _stations;
    float _refreshTimer;

    void OnEnable()
    {
        FailureSystem.OnStationFailed   += HandleFailed;
        FailureSystem.OnStationRepaired += HandleRepaired;
    }

    void OnDisable()
    {
        FailureSystem.OnStationFailed   -= HandleFailed;
        FailureSystem.OnStationRepaired -= HandleRepaired;
    }

    void Start()
    {
        if (rows == null)
            rows = System.Array.Empty<StationRow>();

        _blinks = new Coroutine[rows.Length];
        foreach (var row in rows)
        {
            EnsureRowProgress(row);
            SetRowState(row, StationDisplayState.OK, 0f);
        }

        CacheStations();
        RefreshAllRows();
    }

    void Update()
    {
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer < 0.1f)
            return;

        _refreshTimer = 0f;
        RefreshAllRows();
    }

    void HandleFailed(RepairStation station)
    {
        SetStationRow(station);
    }

    void HandleRepaired(RepairStation station)
    {
        SetStationRow(station);
    }

    // Maps RepairStation.StationType enum to the stationId strings used in the builder
    static string TypeToId(RepairStation.StationType type) => type switch
    {
        RepairStation.StationType.Energy         => "POWER",
        RepairStation.StationType.Communications => "COMMS",
        RepairStation.StationType.Gravity        => "GRAVITY",
        RepairStation.StationType.Hull           => "HULL",
        _                                        => "",
    };

    StationRow FindRow(RepairStation.StationType type)
    {
        string id = TypeToId(type);
        foreach (var r in rows)
            if (r.stationId == id) return r;
        return null;
    }

    enum StationDisplayState { OK, REPAIRING, CRITICAL, FIXED }

    void RefreshAllRows()
    {
        if (_stations == null || _stations.Length == 0)
            CacheStations();

        foreach (var row in rows)
            SetRowState(row, StationDisplayState.OK, 0f);

        if (_stations == null)
            return;

        foreach (var station in _stations)
            SetStationRow(station);
    }

    void SetStationRow(RepairStation station)
    {
        if (station == null)
            return;

        var row = FindRow(station.Type);
        if (row == null)
            return;

        switch (station.State)
        {
            case RepairStation.StationState.Broken:
                SetRowState(row, StationDisplayState.CRITICAL, 0f);
                break;
            case RepairStation.StationState.Repairing:
                SetRowState(row, StationDisplayState.REPAIRING, station.RepairProgress);
                break;
            case RepairStation.StationState.Fixed:
                SetRowState(row, StationDisplayState.FIXED, 1f);
                break;
            default:
                SetRowState(row, StationDisplayState.OK, 0f);
                break;
        }
    }

    void CacheStations()
    {
#pragma warning disable CS0618
        _stations = FindObjectsOfType<RepairStation>();
#pragma warning restore CS0618
    }

    void SetRowState(StationRow row, StationDisplayState state, float progress)
    {
        if (row == null)
            return;

        int idx = System.Array.IndexOf(rows, row);
        if (idx < 0)
            return;

        if (_blinks == null || _blinks.Length != rows.Length)
            _blinks = new Coroutine[rows.Length];

        if (_blinks[idx] != null && state != StationDisplayState.CRITICAL)
        {
            StopCoroutine(_blinks[idx]);
            _blinks[idx] = null;
            if (row.statusDot != null)
            {
                HUDAnimations.StopBlink(row.statusDot);
                var c = row.statusDot.color; c.a = 1f;
                row.statusDot.color = c;
            }
        }

        switch (state)
        {
            case StationDisplayState.OK:
                if (row.statusDot != null) row.statusDot.color = COL_OK;
                if (row.nameText != null) row.nameText.color = COL_OK_TEXT;
                if (row.statusLabel != null)
                {
                    row.statusLabel.text = "OK";
                    row.statusLabel.color = COL_OK_SUB;
                }
                SetProgress(row, 0f, COL_OK, false);
                break;

            case StationDisplayState.REPAIRING:
                if (row.statusDot != null) row.statusDot.color = COL_WARN;
                if (row.nameText != null) row.nameText.color = COL_WARN_TXT;
                if (row.statusLabel != null)
                {
                    row.statusLabel.text = $"{Mathf.RoundToInt(progress * 100f)}%";
                    row.statusLabel.color = COL_WARN_SUB;
                }
                SetProgress(row, progress, COL_WARN, true);
                break;

            case StationDisplayState.CRITICAL:
                if (row.statusDot != null) row.statusDot.color = COL_CRIT;
                if (row.nameText != null) row.nameText.color = COL_CRIT_TXT;
                if (row.statusLabel != null)
                {
                    row.statusLabel.text = "FAIL";
                    row.statusLabel.color = COL_CRIT_SUB;
                }
                SetProgress(row, 0f, COL_CRIT, true);
                if (row.statusDot != null && _blinks[idx] == null)
                    _blinks[idx] = StartCoroutine(HUDAnimations.BlinkCoroutine(row.statusDot));
                break;

            case StationDisplayState.FIXED:
                if (row.statusDot != null) row.statusDot.color = COL_FIXED;
                if (row.nameText != null) row.nameText.color = COL_OK_TEXT;
                if (row.statusLabel != null)
                {
                    row.statusLabel.text = "FIXED";
                    row.statusLabel.color = COL_FIXED;
                }
                SetProgress(row, 1f, COL_FIXED, true);
                break;
        }
    }

    void EnsureRowProgress(StationRow row)
    {
        if (row == null || row.statusLabel == null)
            return;

        Transform parent = row.statusLabel.transform.parent;
        if (parent == null)
            return;

        if (row.progressTrack == null)
        {
            var trackGO = new GameObject("RepairMiniTrack", typeof(RectTransform));
            trackGO.transform.SetParent(parent, false);
            row.progressTrack = trackGO.AddComponent<Image>();
            row.progressTrack.color = COL_TRACK;
            row.progressTrack.raycastTarget = false;

            var rt = trackGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(50f, 3f);
        }

        if (row.progressFill == null)
        {
            var fillGO = new GameObject("RepairMiniFill", typeof(RectTransform));
            fillGO.transform.SetParent(row.progressTrack.transform, false);
            row.progressFill = fillGO.AddComponent<Image>();
            row.progressFill.type = Image.Type.Filled;
            row.progressFill.fillMethod = Image.FillMethod.Horizontal;
            row.progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            row.progressFill.raycastTarget = false;

            var rt = fillGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    void SetProgress(StationRow row, float value01, Color color, bool visible)
    {
        EnsureRowProgress(row);

        if (row.progressTrack != null)
            row.progressTrack.gameObject.SetActive(visible);

        if (row.progressFill != null)
        {
            row.progressFill.gameObject.SetActive(visible);
            row.progressFill.color = color;
            row.progressFill.fillAmount = Mathf.Clamp01(value01);
        }
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
