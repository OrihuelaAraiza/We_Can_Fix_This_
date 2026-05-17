using System.Collections.Generic;
using UnityEngine;

/// Spawns point lights throughout the ship after the layout is generated.
/// Attach to any persistent GameObject in the gameplay scene.
[DefaultExecutionOrder(-90)]
public class ShipAmbientLightInjector : MonoBehaviour
{
    [Header("Room Lights")]
    [SerializeField] private Color  roomLightColor     = new Color(0.55f, 0.70f, 1.00f);
    [SerializeField] private float  roomLightIntensity = 2.2f;
    [SerializeField] private float  roomLightRange     = 11f;
    [SerializeField] private float  roomHeightOffset   = 2.8f;

    [Header("Corridor Lights")]
    [SerializeField] private Color  corridorLightColor     = new Color(0.45f, 0.60f, 0.90f);
    [SerializeField] private float  corridorLightIntensity = 1.4f;
    [SerializeField] private float  corridorLightRange     = 6f;
    [SerializeField] private float  corridorHeightOffset   = 2.2f;

    [Header("Settings")]
    [SerializeField] private bool castShadows = false;

    private readonly List<Light> spawnedLights = new();

    private void OnEnable()
    {
        if (ShipLayoutGenerator.IsReady)
            OnLayoutReady(ShipLayoutGenerator.RoomCenters);
        else
            ShipLayoutGenerator.OnRoomCentersReady += OnLayoutReady;
    }

    private void OnDisable()
    {
        ShipLayoutGenerator.OnRoomCentersReady -= OnLayoutReady;
    }

    private void OnLayoutReady(IReadOnlyDictionary<string, Vector3> roomCenters)
    {
        ShipLayoutGenerator.OnRoomCentersReady -= OnLayoutReady;

        foreach (var kvp in roomCenters)
        {
            bool isCorridor = kvp.Key.StartsWith("Corridor", System.StringComparison.OrdinalIgnoreCase);

            Color color      = isCorridor ? corridorLightColor     : roomLightColor;
            float intensity  = isCorridor ? corridorLightIntensity : roomLightIntensity;
            float range      = isCorridor ? corridorLightRange      : roomLightRange;
            float heightOff  = isCorridor ? corridorHeightOffset    : roomHeightOffset;

            SpawnLight($"AmbientLight_{kvp.Key}", kvp.Value + Vector3.up * heightOff, color, intensity, range);
        }

        Debug.Log($"[ShipAmbientLightInjector] Placed {spawnedLights.Count} lights across {roomCenters.Count} areas");
    }

    private void SpawnLight(string lightName, Vector3 position, Color color, float intensity, float range)
    {
        var go = new GameObject(lightName);
        go.transform.SetParent(transform, worldPositionStays: false);
        go.transform.position = position;

        var light = go.AddComponent<Light>();
        light.type      = LightType.Point;
        light.color     = color;
        light.intensity = intensity;
        light.range     = range;
        light.shadows   = castShadows ? LightShadows.Soft : LightShadows.None;

        spawnedLights.Add(light);
    }
}
