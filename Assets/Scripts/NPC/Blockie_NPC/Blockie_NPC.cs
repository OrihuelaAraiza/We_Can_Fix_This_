using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
public class BlockieNPC : NPCBehaviourBase
{
    [Header("Speed")]
    public float baseSpeed = 2f;
    public float extraSpeedPerPlayer = 1f;
    public float turnSpeed = 360f;

    [Header("Physics")]
    public float mass = 200f;
    public float drag = 6f;
    public float angularDrag = 10f;

    [Header("Detecci�n")]
    public float forwardCheck = 2.5f;
    public float sideCheck = 2f;
    public float rayHeight = 1f;
    public LayerMask obstacleMask;

    [Header("Player")]
    public string playerTag = "Player";

    [Header("NavMesh Roaming")]
    public float roamRadius = 12f;
    public float minRoamDistance = 3f;
    public float arriveDistance = 0.6f;
    public float destinationCheckInterval = 0.25f;
    public float stuckSeconds = 1.5f;
    public float stuckMoveDistance = 0.12f;
    public float navMeshSampleRadius = 4f;
    public float acceleration = 12f;

    private Rigidbody rb;
    private NavMeshAgent agent;
    private Vector3 homePosition;
    private Vector3 lastProgressPosition;
    private float nextDecisionTime;
    private float stuckTimer;
    private int playersPushing = 0;
    private bool warnedMissingNavMesh;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = gameObject.AddComponent<NavMeshAgent>();

        rb.mass = mass;
        rb.drag = drag;
        rb.angularDrag = angularDrag;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        ConfigureAgent();
        homePosition = transform.position;
        lastProgressPosition = transform.position;
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

        agent.speed = baseSpeed + (playersPushing * extraSpeedPerPlayer);
        playersPushing = 0;

        if (Time.time < nextDecisionTime)
            return;

        nextDecisionTime = Time.time + destinationCheckInterval;

        bool needsNewDestination =
            !agent.hasPath ||
            agent.pathStatus != NavMeshPathStatus.PathComplete ||
            NavMeshSpawnUtility.HasArrived(agent, arriveDistance) ||
            NavMeshSpawnUtility.UpdateStuckTimer(agent, ref lastProgressPosition, ref stuckTimer, stuckMoveDistance, stuckSeconds);

        if (needsNewDestination)
            PickNewDestination();
    }

    void ConfigureAgent()
    {
        if (agent == null)
            return;

        agent.speed = baseSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = turnSpeed;
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
            Debug.LogWarning("[BlockieNPC] Could not place Blockie on the NavMesh.", this);
        }

        return false;
    }

    void PickNewDestination()
    {
        stuckTimer = 0f;
        lastProgressPosition = transform.position;

        if (!NavMeshSpawnUtility.TryFindReachablePoint(agent, homePosition, minRoamDistance, roamRadius, 14, out _))
            agent.ResetPath();
    }

    void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag(playerTag))
            return;

        Vector3 toPlayer = (collision.transform.position - transform.position).normalized;
        float dot = Vector3.Dot(transform.forward, toPlayer);

        if (dot < -0.3f)
        {
            playersPushing++;
        }
    }
}
