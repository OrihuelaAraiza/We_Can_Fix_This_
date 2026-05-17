using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-90)]
public class RuntimeShipNavMesh : MonoBehaviour
{
    public static RuntimeShipNavMesh Instance { get; private set; }
    public static bool IsReady => Instance != null && Instance.isReady;
    public static event Action OnReady;

    [SerializeField] private string shipInteriorName = "ShipInterior";
    [SerializeField] private float buildVolumePadding = 4f;
    [SerializeField] private Vector3 minimumBuildVolume = new Vector3(20f, 8f, 20f);

    private NavMeshSurface surface;
    private bool isReady;
    private bool buildAttempted;

    public static RuntimeShipNavMesh EnsureExists()
    {
        if (Instance != null)
            return Instance;

#pragma warning disable CS0618
        Instance = FindObjectOfType<RuntimeShipNavMesh>();
#pragma warning restore CS0618
        if (Instance != null)
            return Instance;

        var go = new GameObject("RuntimeShipNavMesh");
        Instance = go.AddComponent<RuntimeShipNavMesh>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (isReady)
            return;

        if (ShipLayoutGenerator.IsReady)
            Build();
        else
            ShipLayoutGenerator.OnRoomCentersReady += HandleRoomCentersReady;
    }

    private void OnDisable()
    {
        ShipLayoutGenerator.OnRoomCentersReady -= HandleRoomCentersReady;
    }

    private void HandleRoomCentersReady(System.Collections.Generic.IReadOnlyDictionary<string, Vector3> _)
    {
        Build();
    }

    public void Build()
    {
        if (isReady || buildAttempted)
            return;

        buildAttempted = true;
        ShipLayoutGenerator.OnRoomCentersReady -= HandleRoomCentersReady;

        Transform shipInterior = FindShipInterior();
        if (shipInterior == null)
        {
            Debug.LogWarning("[RuntimeShipNavMesh] ShipInterior not found; NPC NavMesh will not be built.");
            return;
        }

        surface = GetComponent<NavMeshSurface>();
        if (surface == null)
            surface = gameObject.AddComponent<NavMeshSurface>();

        surface.collectObjects = CollectObjects.Volume;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = LayerMask.GetMask("Default", "Ground");
        surface.defaultArea = 0;
        surface.center = shipInterior.position;
        surface.size = ComputeBuildSize(shipInterior);
        surface.BuildNavMesh();

        isReady = true;
        OnReady?.Invoke();
        Debug.Log("[RuntimeShipNavMesh] Runtime NavMesh built for procedural ship.");
    }

    private Transform FindShipInterior()
    {
        GameObject shipInterior = GameObject.Find(shipInteriorName);
        if (shipInterior != null)
            return shipInterior.transform;

#pragma warning disable CS0618
        ShipLayoutGenerator generator = FindObjectOfType<ShipLayoutGenerator>();
#pragma warning restore CS0618
        if (generator == null)
            return null;

        Transform candidate = generator.transform.Find(shipInteriorName);
        return candidate != null ? candidate : null;
    }

    private Vector3 ComputeBuildSize(Transform shipInterior)
    {
        Bounds bounds = new Bounds(shipInterior.position, minimumBuildVolume);
        bool initialized = false;

        foreach (Collider collider in shipInterior.GetComponentsInChildren<Collider>(true))
        {
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;

            if (!initialized)
            {
                bounds = collider.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (!initialized)
            return minimumBuildVolume;

        Vector3 size = bounds.size + Vector3.one * buildVolumePadding;
        return new Vector3(
            Mathf.Max(size.x, minimumBuildVolume.x),
            Mathf.Max(size.y, minimumBuildVolume.y),
            Mathf.Max(size.z, minimumBuildVolume.z));
    }
}
