using UnityEngine;
using TMPro;
using System;

public class SurvivalTimerUI : MonoBehaviour
{
    [SerializeField] public TMP_Text timerValue;
    [SerializeField] public float    survivalDuration = 600f;
    [SerializeField] float criticalTimeThreshold = 60f;

    public static event Action OnTimeCritical;

    static readonly Color COL_NORMAL = HexColor("#e7bf4e");
    static readonly Color COL_WARN   = HexColor("#ff9b39");
    static readonly Color COL_CRIT   = HexColor("#ff6257");

    float _timeRemaining;
    bool  _running;
    bool  _criticalRaised;

    void Start()
    {
        _timeRemaining = survivalDuration;
        _running       = true;
        _criticalRaised = false;
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
            if (!_criticalRaised && _timeRemaining <= criticalTimeThreshold)
            {
                _criticalRaised = true;
                OnTimeCritical?.Invoke();
            }

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
