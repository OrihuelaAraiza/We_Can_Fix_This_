using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Smoggos_NPC : NPCBehaviourBase
{
    [Header("Movement")]
    public float speed = 3f;
    public float detectionDistance = 2f;
    public float radius = 10f;

    [Header("Roaming")]
    public float minDestinationDistance = 2.5f;
    public float arriveDistance = 0.7f;
    public float destinationCheckInterval = 0.25f;
    public float stuckSeconds = 1.4f;
    public float stuckMoveDistance = 0.1f;
    public float navMeshSampleRadius = 4f;
    public float acceleration = 10f;

    [Header("Personality")]
    public float zigzagAmount = 2f;
    public float zigzagSpeed = 2f;

    private NavMeshAgent agent;
    private Vector3 startPoint;
    private Vector3 lastProgressPosition;
    private float nextDecisionTime;
    private float stuckTimer;
    private bool warnedMissingNavMesh;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = gameObject.AddComponent<NavMeshAgent>();

        startPoint = transform.position;
        lastProgressPosition = transform.position;
        ConfigureAgent();
        PickNewDestination();
    }

    protected override void OnDisabled()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    protected override void OnEnabled()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = false;
    }

    void Update()
    {
        if (IsDisabled)
            return;

        if (!EnsureAgentOnNavMesh())
            return;

        agent.speed = speed;

        if (Time.time < nextDecisionTime)
            return;

        nextDecisionTime = Time.time + destinationCheckInterval;

        bool pathNeedsRefresh =
            !agent.hasPath ||
            agent.pathStatus != NavMeshPathStatus.PathComplete ||
            NavMeshSpawnUtility.HasArrived(agent, arriveDistance) ||
            NavMeshSpawnUtility.UpdateStuckTimer(agent, ref lastProgressPosition, ref stuckTimer, stuckMoveDistance, stuckSeconds);

        if (pathNeedsRefresh || HasObstacleAhead())
            PickNewDestination();
    }

    void ConfigureAgent()
    {
        if (agent == null)
            return;

        agent.speed = speed;
        agent.acceleration = acceleration;
        agent.angularSpeed = 420f;
        agent.stoppingDistance = arriveDistance;
        agent.autoBraking = false;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    }

    bool EnsureAgentOnNavMesh()
    {
        if (agent == null || !agent.enabled)
            return false;

        if (agent.isOnNavMesh)
            return true;

        if (NavMeshSpawnUtility.TryWarpToNearest(agent, transform.position, navMeshSampleRadius))
            return true;

        if (!warnedMissingNavMesh)
        {
            warnedMissingNavMesh = true;
            Debug.LogWarning("[Smoggos_NPC] Could not place Smoggos on the NavMesh.", this);
        }

        return false;
    }

    bool HasObstacleAhead()
    {
        if (detectionDistance <= 0f)
            return false;

        Vector3 origin = transform.position + Vector3.up * agent.height * 0.5f;
        return Physics.Raycast(origin, transform.forward, detectionDistance);
    }

    void PickNewDestination()
    {
        stuckTimer = 0f;
        lastProgressPosition = transform.position;

        Vector3 center = Vector3.Distance(transform.position, startPoint) > radius
            ? startPoint
            : transform.position;

        Vector3 side = transform.right * Mathf.Sin(Time.time * zigzagSpeed) * zigzagAmount;
        Vector3 biasedCenter = center + transform.forward * minDestinationDistance + side;

        if (NavMeshSpawnUtility.TryFindReachablePoint(agent, biasedCenter, minDestinationDistance, radius, 14, out _))
            return;

        if (NavMeshSpawnUtility.TryFindReachablePoint(agent, startPoint, minDestinationDistance, radius, 14, out _))
            return;

        agent.ResetPath();
    }
}
