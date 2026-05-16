using UnityEngine;

[System.Serializable]
public class CoreXPhase
{
    [Header("Identity")]
    public string phaseName = "Phase 1";

    [Header("Timing")]
    public float tickInterval     = 8f;
    public float phaseDuration    = 120f;
    public float sabotageInterval = 15f;

    [Header("Failures")]
    public float failureRate          = 0.5f;
    public int   maxSimultaneousFails = 2;

    [Header("NPCs")]
    public bool          canDeployNPCs  = false;
    public GameObject[]  npcPrefabs;
    public Transform[]   npcSpawnPoints;

    [Header("Team skill scaling")]
    public float repairRateThreshold       = 3f;
    public int   consecutiveRepairsTrigger = 3;
}
