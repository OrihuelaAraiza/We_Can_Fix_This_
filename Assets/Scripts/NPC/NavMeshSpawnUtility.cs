using UnityEngine;
using UnityEngine.AI;

public static class NavMeshSpawnUtility
{
    public const float DefaultSampleRadius = 3f;
    private static readonly NavMeshPath SharedPath = new NavMeshPath();

    public static bool TrySamplePosition(Vector3 requestedPosition, out Vector3 navMeshPosition, float radius = DefaultSampleRadius)
    {
        if (NavMesh.SamplePosition(requestedPosition, out NavMeshHit hit, radius, NavMesh.AllAreas))
        {
            navMeshPosition = hit.position;
            return true;
        }

        navMeshPosition = requestedPosition;
        return false;
    }

    public static Vector3 ResolvePosition(Vector3 requestedPosition, float radius = DefaultSampleRadius)
    {
        return TrySamplePosition(requestedPosition, out Vector3 navMeshPosition, radius)
            ? navMeshPosition
            : requestedPosition;
    }

    public static bool TryWarpToNearest(NavMeshAgent agent, Vector3 requestedPosition, float radius = DefaultSampleRadius)
    {
        if (agent == null)
            return false;

        if (!agent.enabled)
            return false;

        if (!TrySamplePosition(requestedPosition, out Vector3 navMeshPosition, radius))
            return false;

        return agent.Warp(navMeshPosition);
    }

    public static bool TrySetDestination(NavMeshAgent agent, Vector3 requestedDestination, float sampleRadius = DefaultSampleRadius)
    {
        if (agent == null)
            return false;

        if (!agent.enabled)
            return false;

        if (!agent.isOnNavMesh && !TryWarpToNearest(agent, agent.transform.position, sampleRadius))
            return false;

        if (!TrySamplePosition(requestedDestination, out Vector3 navMeshDestination, sampleRadius))
            return false;

        return agent.SetDestination(navMeshDestination);
    }

    public static bool TrySetReachableDestination(
        NavMeshAgent agent,
        Vector3 requestedDestination,
        out Vector3 navMeshDestination,
        float sampleRadius = DefaultSampleRadius)
    {
        navMeshDestination = requestedDestination;

        if (agent == null || !agent.enabled)
            return false;

        if (!agent.isOnNavMesh && !TryWarpToNearest(agent, agent.transform.position, sampleRadius))
            return false;

        if (!TrySamplePosition(requestedDestination, out navMeshDestination, sampleRadius))
            return false;

        if (!agent.CalculatePath(navMeshDestination, SharedPath))
            return false;

        if (SharedPath.status != NavMeshPathStatus.PathComplete)
            return false;

        return agent.SetPath(SharedPath);
    }

    public static bool TryFindReachablePoint(
        NavMeshAgent agent,
        Vector3 center,
        float minRadius,
        float maxRadius,
        int attempts,
        out Vector3 point)
    {
        point = center;

        if (agent == null || !agent.enabled)
            return false;

        if (!agent.isOnNavMesh && !TryWarpToNearest(agent, agent.transform.position, DefaultSampleRadius))
            return false;

        minRadius = Mathf.Max(0f, minRadius);
        maxRadius = Mathf.Max(minRadius, maxRadius);
        attempts = Mathf.Max(1, attempts);

        for (int i = 0; i < attempts; i++)
        {
            Vector2 random = Random.insideUnitCircle.normalized * Random.Range(minRadius, maxRadius);
            Vector3 candidate = center + new Vector3(random.x, 0f, random.y);

            if (TrySetReachableDestination(agent, candidate, out point, DefaultSampleRadius))
                return true;
        }

        return TrySetReachableDestination(agent, center, out point, maxRadius + DefaultSampleRadius);
    }

    public static bool HasArrived(NavMeshAgent agent, float arriveDistance)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return false;

        if (agent.pathPending)
            return false;

        float threshold = Mathf.Max(arriveDistance, agent.stoppingDistance);
        return agent.remainingDistance <= threshold;
    }

    public static bool UpdateStuckTimer(
        NavMeshAgent agent,
        ref Vector3 lastPosition,
        ref float stuckTimer,
        float minMoveDistance,
        float stuckSeconds)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || !agent.hasPath || agent.pathPending)
        {
            stuckTimer = 0f;
            if (agent != null)
                lastPosition = agent.transform.position;
            return false;
        }

        if (agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, agent.radius))
        {
            stuckTimer = 0f;
            lastPosition = agent.transform.position;
            return false;
        }

        float moved = Vector3.Distance(agent.transform.position, lastPosition);
        if (moved >= minMoveDistance)
        {
            stuckTimer = 0f;
            lastPosition = agent.transform.position;
            return false;
        }

        stuckTimer += Time.deltaTime;
        return stuckTimer >= stuckSeconds;
    }

    public static float GetPathLength(NavMeshPath path)
    {
        if (path == null || path.corners == null || path.corners.Length < 2)
            return Mathf.Infinity;

        float length = 0f;
        Vector3 previous = path.corners[0];
        for (int i = 1; i < path.corners.Length; i++)
        {
            Vector3 next = path.corners[i];
            length += Vector3.Distance(previous, next);
            previous = next;
        }

        return length;
    }

    public static bool TryGetCompletePathLength(
        NavMeshAgent agent,
        Vector3 requestedDestination,
        out float pathLength,
        float sampleRadius = DefaultSampleRadius)
    {
        pathLength = Mathf.Infinity;

        if (agent == null || !agent.enabled)
            return false;

        if (!agent.isOnNavMesh && !TryWarpToNearest(agent, agent.transform.position, sampleRadius))
            return false;

        if (!TrySamplePosition(requestedDestination, out Vector3 navMeshDestination, sampleRadius))
            return false;

        if (!agent.CalculatePath(navMeshDestination, SharedPath))
            return false;

        if (SharedPath.status != NavMeshPathStatus.PathComplete)
            return false;

        pathLength = GetPathLength(SharedPath);
        return true;
    }
}
