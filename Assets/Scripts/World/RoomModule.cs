using UnityEngine;

public class RoomModule : MonoBehaviour
{
    public enum RoomType { MainHall, EnergyRoom, CommunicationsRoom,
                           GravityRoom, HullRoom, Corridor, GunnerRoom }

    [Header("Config")]
    [SerializeField] private RoomType roomType;
    [SerializeField] private Color    ambientColor = new Color(0.1f, 0.15f, 0.3f);

    [Header("References")]
    [SerializeField] private Light[]         roomLights;
    [SerializeField] private RepairStation   assignedStation;

    [Header("Emergency FX")]
    [SerializeField] private float normalIntensity    = 1f;
    [SerializeField] private float emergencyIntensity = 2f;
    [SerializeField] private Color normalLightColor     = new Color(0.4f, 0.6f, 1f);
    [SerializeField] private Color emergencyLightColor  = new Color(1f, 0.1f, 0.05f);
    [SerializeField] private float flickerSpeed = 8f;

    private bool isEmergency;
    private float flickerTimer;

    private void Start()
    {
        if (assignedStation != null)
        {
            assignedStation.OnBroken   += _ => SetEmergency(true);
            assignedStation.OnRepaired += _ => SetEmergency(false);
        }
        ApplyNormalLighting();
    }

    private void Update()
    {
        if (!isEmergency) return;
        flickerTimer += Time.deltaTime * flickerSpeed;
        float flicker = 0.7f + 0.3f * Mathf.Sin(flickerTimer);
        foreach (var l in roomLights)
            if (l != null) l.intensity = emergencyIntensity * flicker;
    }

    public void SetEmergency(bool emergency)
    {
        isEmergency = emergency;
        if (!emergency) ApplyNormalLighting();
        else foreach (var l in roomLights)
            if (l != null) l.color = emergencyLightColor;
    }

    private void ApplyNormalLighting()
    {
        foreach (var l in roomLights)
        {
            if (l == null) continue;
            l.color     = normalLightColor;
            l.intensity = normalIntensity;
        }
    }

    public RoomType Type           => roomType;
    public RepairStation Station   => assignedStation;
}
