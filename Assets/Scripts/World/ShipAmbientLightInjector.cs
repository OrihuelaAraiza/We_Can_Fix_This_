using System.Collections.Generic;
using UnityEngine;

/// Spawns point lights throughout the ship after the layout is generated.
/// Attach to any persistent GameObject in the gameplay scene.
[DefaultExecutionOrder(-90)]
public class ShipAmbientLightInjector : MonoBehaviour
{
    [Header("Room Lights")]
    [SerializeField] private Color  roomLightColor     = new Color(0.72f, 0.84f, 1.00f);
    [SerializeField] private float  roomLightIntensity = 3.2f;
    [SerializeField] private float  roomLightRange     = 14f;
    [SerializeField] private float  roomHeightOffset   = 3.1f;
    [SerializeField] private float  roomFillOffset     = 4.5f;
    [SerializeField] private float  roomFillIntensity  = 1.35f;
    [SerializeField] private float  roomFillRange      = 7f;

    [Header("Corridor Lights")]
    [SerializeField] private Color  corridorLightColor     = new Color(0.62f, 0.76f, 1.00f);
    [SerializeField] private float  corridorLightIntensity = 2.1f;
    [SerializeField] private float  corridorLightRange     = 8f;
    [SerializeField] private float  corridorHeightOffset   = 2.45f;
    [SerializeField] private float  corridorSecondaryOffset = 2.2f;

    [Header("Settings")]
    [SerializeField] private bool castShadows = false;
    [SerializeField] private bool adjustAmbientLighting = true;
    [SerializeField] private Color ambientColor = new Color(0.18f, 0.22f, 0.30f);
    [SerializeField] private float ambientIntensity = 1.25f;

    private readonly List<Light> spawnedLights = new();
    private bool spawned;

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

        if (spawned)
            return;

        spawned = true;
        ApplyAmbientSettings();

        foreach (var kvp in roomCenters)
        {
            bool isCorridor = kvp.Key.StartsWith("Corridor", System.StringComparison.OrdinalIgnoreCase);

            if (isCorridor)
                SpawnCorridorLights(kvp.Key, kvp.Value);
            else
                SpawnRoomLights(kvp.Key, kvp.Value);
        }

        Debug.Log($"[ShipAmbientLightInjector] Placed {spawnedLights.Count} lights across {roomCenters.Count} areas");
    }

    private void SpawnRoomLights(string areaName, Vector3 center)
    {
        SpawnLight($"AmbientLight_{areaName}_Center", center + Vector3.up * roomHeightOffset, roomLightColor, roomLightIntensity, roomLightRange);

        Vector3[] offsets =
        {
            new Vector3(roomFillOffset, 0f, roomFillOffset),
            new Vector3(-roomFillOffset, 0f, roomFillOffset),
            new Vector3(roomFillOffset, 0f, -roomFillOffset),
            new Vector3(-roomFillOffset, 0f, -roomFillOffset),
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            SpawnLight(
                $"AmbientLight_{areaName}_Fill_{i + 1}",
                center + offsets[i] + Vector3.up * (roomHeightOffset - 0.45f),
                roomLightColor,
                roomFillIntensity,
                roomFillRange);
        }
    }

    private void SpawnCorridorLights(string areaName, Vector3 center)
    {
        SpawnLight($"AmbientLight_{areaName}_Center", center + Vector3.up * corridorHeightOffset, corridorLightColor, corridorLightIntensity, corridorLightRange);
        SpawnLight($"AmbientLight_{areaName}_A", center + Vector3.right * corridorSecondaryOffset + Vector3.up * corridorHeightOffset, corridorLightColor, corridorLightIntensity * 0.65f, corridorLightRange * 0.75f);
        SpawnLight($"AmbientLight_{areaName}_B", center - Vector3.right * corridorSecondaryOffset + Vector3.up * corridorHeightOffset, corridorLightColor, corridorLightIntensity * 0.65f, corridorLightRange * 0.75f);
    }

    private void ApplyAmbientSettings()
    {
        if (!adjustAmbientLighting)
            return;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.ambientIntensity = ambientIntensity;
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
