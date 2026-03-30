using UnityEngine;

public class ShipRoom : MonoBehaviour
{
    public enum RoomType
    {
        Bridge,
        PowerRoom,
        CommsRoom,
        GravityRoom,
        HullRoom,
        SupportRoom,
        Corridor
    }

    public enum RoomArchetype
    {
        Bridge,
        Power,
        Comms,
        Gravity,
        Hull,
        MedBay,
        Cargo,
        CrewQuarters,
        Workshop,
        Security,
        Storage
    }

    [SerializeField] public RoomType roomType;
    [SerializeField] public RoomArchetype archetype;
    [SerializeField] public string displayName;
    [SerializeField] public string zoneLabel;
    [SerializeField] public bool isRepairRoom;
    [SerializeField] public Transform stationAnchor;
    [SerializeField] public Transform[] spawnPoints;

    public void OnStationFailed()
    {
        var light = transform.Find("EmergencyLight");
        if (light != null) light.gameObject.SetActive(true);
    }

    public void OnStationRepaired()
    {
        var light = transform.Find("EmergencyLight");
        if (light != null) light.gameObject.SetActive(false);
    }
}
