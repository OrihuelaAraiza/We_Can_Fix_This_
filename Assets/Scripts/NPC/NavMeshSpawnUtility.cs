using UnityEngine;
using UnityEngine.AI;

public static class NavMeshSpawnUtility
{
    public const float DefaultSampleRadius = 3f;

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
}
