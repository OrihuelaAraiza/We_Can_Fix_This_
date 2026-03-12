using UnityEngine;

[System.Serializable]
public class CoreXPhase
{
    [Header("Identidad")]
    public string phaseName = "Fase 1";

    [Header("Temporización")]
    public float tickInterval     = 8f;   // segundos entre ticks del Brain
    public float phaseDuration    = 120f; // segundos antes de escalar
    public float sabotageInterval = 15f;  // segundos entre sabotajes

    [Header("Fallas")]
    public float failureRate          = 0.5f; // fallas por minuto
    public int   maxSimultaneousFails = 2;

    [Header("NPCs")]
    public bool          canDeployNPCs  = false;
    public GameObject[]  npcPrefabs;
    public Transform[]   npcSpawnPoints;

    [Header("Escalado por habilidad del equipo")]
    public float repairRateThreshold       = 3f; // reparaciones/min para escalar
    public int   consecutiveRepairsTrigger = 3;  // reparaciones seguidas para responder
}
