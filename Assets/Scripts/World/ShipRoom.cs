using UnityEngine;

public class ShipRoom : MonoBehaviour
{
    public enum RoomType { Bridge, PowerRoom, CommsRoom, GravityRoom, HullRoom, Corridor }

    [SerializeField] public RoomType roomType;
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
