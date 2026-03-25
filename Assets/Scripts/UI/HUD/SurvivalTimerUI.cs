using UnityEngine;
using TMPro;

public class SurvivalTimerUI : MonoBehaviour
{
    [SerializeField] public TMP_Text timerValue;
    [SerializeField] public float    survivalDuration = 600f;

    static readonly Color COL_NORMAL = HexColor("#c8a020");
    static readonly Color COL_WARN   = HexColor("#c07010");
    static readonly Color COL_CRIT   = HexColor("#a02020");

    float _timeRemaining;
    bool  _running;

    void Start()
    {
        _timeRemaining = survivalDuration;
        _running       = true;
        UpdateDisplay();
    }

    void Update()
    {
        if (!_running) return;

        _timeRemaining -= Time.deltaTime;
        if (_timeRemaining <= 0f)
        {
            _timeRemaining = 0f;
            _running       = false;
            UpdateDisplay();
            GameManager.Instance?.OnTimerExpired();
        }
        else
        {
            UpdateDisplay();
        }
    }

    void UpdateDisplay()
    {
        int minutes = Mathf.FloorToInt(_timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(_timeRemaining % 60f);
        if (timerValue != null)
        {
            timerValue.text  = string.Format("{0:00}:{1:00}", minutes, seconds);
            timerValue.color = _timeRemaining <= 60f  ? COL_CRIT  :
                               _timeRemaining <= 180f ? COL_WARN  : COL_NORMAL;
        }
    }

    public void StopTimer() => _running = false;

    public string GetFormattedTime()
    {
        int m = Mathf.FloorToInt(_timeRemaining / 60f);
        int s = Mathf.FloorToInt(_timeRemaining % 60f);
        return string.Format("{0:00}:{1:00}", m, s);
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
