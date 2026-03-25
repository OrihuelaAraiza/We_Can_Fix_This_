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
    }

    [SerializeField] StationRow[] rows;

    static readonly Color COL_OK       = HexColor("#287040");
    static readonly Color COL_OK_TEXT  = HexColor("#88a888");
    static readonly Color COL_OK_SUB   = HexColor("#405040");
    static readonly Color COL_WARN     = HexColor("#907818");
    static readonly Color COL_WARN_TXT = HexColor("#b89840");
    static readonly Color COL_WARN_SUB = HexColor("#604810");
    static readonly Color COL_CRIT     = HexColor("#a02020");
    static readonly Color COL_CRIT_TXT = HexColor("#c03030");
    static readonly Color COL_CRIT_SUB = HexColor("#802020");

    Coroutine[] _blinks;

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
        _blinks = new Coroutine[rows.Length];
        foreach (var row in rows)
            SetRowState(row, StationState.OK);
    }

    void HandleFailed(RepairStation station)
    {
        var row = FindRow(station.Type);
        if (row != null) SetRowState(row, StationState.CRITICAL);
    }

    void HandleRepaired(RepairStation station)
    {
        var row = FindRow(station.Type);
        if (row != null) SetRowState(row, StationState.OK);
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

    enum StationState { OK, WARNING, CRITICAL }

    void SetRowState(StationRow row, StationState state)
    {
        int idx = System.Array.IndexOf(rows, row);
        if (_blinks[idx] != null)
        {
            StopCoroutine(_blinks[idx]);
            _blinks[idx] = null;
            if (row.statusDot != null)
            {
                var c = row.statusDot.color; c.a = 1f;
                row.statusDot.color = c;
            }
        }

        switch (state)
        {
            case StationState.OK:
                row.statusDot.color   = COL_OK;
                row.nameText.color    = COL_OK_TEXT;
                row.statusLabel.text  = "OK";
                row.statusLabel.color = COL_OK_SUB;
                break;

            case StationState.WARNING:
                row.statusDot.color   = COL_WARN;
                row.nameText.color    = COL_WARN_TXT;
                row.statusLabel.text  = "WARN";
                row.statusLabel.color = COL_WARN_SUB;
                break;

            case StationState.CRITICAL:
                row.statusDot.color   = COL_CRIT;
                row.nameText.color    = COL_CRIT_TXT;
                row.statusLabel.text  = "FAIL";
                row.statusLabel.color = COL_CRIT_SUB;
                _blinks[idx] = StartCoroutine(HUDAnimations.BlinkCoroutine(row.statusDot));
                break;
        }
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
