using UnityEngine;
using UnityEngine.UI;

// Attach este script al Canvas world-space de cada RepairStation
public class RepairProgressUI : MonoBehaviour
{
    [SerializeField] private Slider progressSlider;
    [SerializeField] private GameObject progressRoot;

    private RepairStation station;

    private void Awake()
    {
        station = GetComponentInParent<RepairStation>();
        if (progressRoot) progressRoot.SetActive(false);
    }

    private void Update()
    {
        if (station == null) return;

        bool showBar = station.State == RepairStation.StationState.Repairing;
        if (progressRoot) progressRoot.SetActive(showBar);

        if (showBar && progressSlider)
            progressSlider.value = station.RepairProgress;
    }
}
