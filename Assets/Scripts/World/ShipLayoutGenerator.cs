using UnityEngine;

[DefaultExecutionOrder(-100)]
public class ShipLayoutGenerator : MonoBehaviour
{
    [Header("Seed (0 = random each game)")]
    public int seed = 0;
    public static int  CurrentSeed { get; private set; }
    public static bool IsReady     { get; private set; }

    [Header("Room Tile Prefabs — assign in Inspector")]
    public GameObject[] roomTilePrefabs;   // manual room tiles JP will build

    [Header("Connector Prefabs — assign in Inspector")]
    public GameObject[] connectorPrefabs;  // floor-only connector tiles

    [Header("Ship Interior Parent")]
    public Transform shipInteriorParent;

    void Awake()
    {
        // Seed setup for future procedural variation
        int usedSeed = seed == 0 ? Random.Range(1, 99999) : seed;
        CurrentSeed = usedSeed;

        // Layout is built manually in Editor — just mark ready
        IsReady = true;
    }

    // Called externally to regenerate with new seed (future use)
    public void RegenerateWithSeed(int newSeed)
    {
        seed = newSeed;
        CurrentSeed = newSeed;
    }
}
