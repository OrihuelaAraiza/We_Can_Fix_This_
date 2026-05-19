using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Clank_NPC : NPCBehaviourBase
{
    [Header("Agent")]
    public NavMeshAgent agent;
    public float arriveDistance = 0.8f;
    public float destinationRefreshInterval = 0.35f;
    public float destinationRefreshDistance = 0.6f;
    public float targetSearchInterval = 0.5f;
    public float stuckSeconds = 1.6f;
    public float stuckMoveDistance = 0.12f;
    public float navMeshSampleRadius = 4f;

    [Header("References")]
    public Transform holdPoint;
    public Transform dropPoint;

    [Header("Hold Offset")]
    public Vector3 holdLocalOffset = new Vector3(0f, 0.2f, 0.4f);

    [Header("Player")]
    public GameObject playerObject;

    [Header("After collision")]
    public float waitAfterDropSeconds = 2f;
    public float backOffDistance = 0.7f;

    [Header("Chaos")]
    public float throwForce = 6f;
    public float throwUpForce = 2.5f;
    public float chaosCooldown = 1f;

    private float chaosTimer = 0f;
    private bool waiting = false;
    private bool warnedMissingNavMesh = false;
    private float nextDestinationRefreshTime;
    private float nextTargetSearchTime;
    private Vector3 lastDestination;
    private Vector3 lastProgressPosition;
    private float stuckTimer;

    private BoxItem targetBox;
    private BoxItem carriedBox;

    // NUEVO:
    // Diccionario compartido por todos los Clanks.
    // Sirve para que una caja solo pueda ser objetivo de un Clank a la vez.
    private static readonly Dictionary<BoxItem, Clank_NPC> reservedBoxes = new Dictionary<BoxItem, Clank_NPC>();

    private enum State
    {
        SearchingBox,
        GoingToBox,
        GoingToDropZone
    }

    private State state = State.SearchingBox;

    void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        lastDestination = transform.position;
        lastProgressPosition = transform.position;
    }

    private void OnDestroy()
    {
        ReleaseTargetBox();
    }

    protected override void OnDisabled()
    {
        ReleaseTargetBox();

        if (agent != null)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }
    }

    protected override void OnEnabled()
    {
        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = false;
    }

    void Update()
    {
        if (IsDisabled) return;
        if (waiting) return;
        if (!EnsureAgentOnNavMesh()) return;

        if (chaosTimer > 0)
            chaosTimer -= Time.deltaTime;

        if (NavMeshSpawnUtility.UpdateStuckTimer(agent, ref lastProgressPosition, ref stuckTimer, stuckMoveDistance, stuckSeconds))
            RecoverFromStuck();

        switch (state)
        {
            case State.SearchingBox:
                SearchBox();
                break;

            case State.GoingToBox:
                GoToBox();
                break;

            case State.GoingToDropZone:
                GoToDrop();
                break;
        }
    }

    void SearchBox()
    {
        if (Time.time < nextTargetSearchTime)
            return;

        nextTargetSearchTime = Time.time + targetSearchInterval;

        ReleaseTargetBox();

        targetBox = FindClosestBox();

        if (targetBox != null)
        {
            ReserveBox(targetBox);

            if (TryMoveTo(GetBoxPosition(targetBox), true))
            {
                state = State.GoingToBox;
            }
            else
            {
                ReleaseTargetBox();
                targetBox = null;
            }
        }
    }

    void GoToBox()
    {
        if (targetBox == null)
        {
            ReleaseTargetBox();
            state = State.SearchingBox;
            return;
        }

        // NUEVO:
        // Si por alguna razón la caja ya no está disponible,
        // este Clank la suelta como objetivo.
        if (targetBox.delivered || targetBox.pickedUp || targetBox.blocked || targetBox.IsOnCooldown())
        {
            ReleaseTargetBox();
            targetBox = null;
            state = State.SearchingBox;
            return;
        }

        // NUEVO:
        // Si otro Clank la reservó, ya no vamos por ella.
        if (IsReservedByOtherClank(targetBox))
        {
            targetBox = null;
            state = State.SearchingBox;
            return;
        }

        if (!TryMoveTo(GetBoxPosition(targetBox), false))
        {
            ReleaseTargetBox();
            targetBox = null;
            state = State.SearchingBox;
            return;
        }

        if (!HasArrived()) return;

        PickUpBox(targetBox);

        if (dropPoint == null)
        {
            state = State.SearchingBox;
            return;
        }

        if (TryMoveTo(dropPoint.position, true))
            state = State.GoingToDropZone;
        else
            state = State.SearchingBox;
    }

    void GoToDrop()
    {
        if (dropPoint == null)
        {
            state = State.SearchingBox;
            return;
        }

        if (!TryMoveTo(dropPoint.position, false))
        {
            state = State.SearchingBox;
            return;
        }

        if (!HasArrived()) return;

        DropBox();
        state = State.SearchingBox;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (waiting) return;
        if (chaosTimer > 0) return;
        if (carriedBox == null) return;
        if (playerObject == null) return;

        bool hitPlayer =
            collision.collider.gameObject == playerObject ||
            collision.collider.transform.IsChildOf(playerObject.transform);

        if (!hitPlayer) return;

        DropAndThrow(collision);
        chaosTimer = chaosCooldown;

        StartCoroutine(WaitThenSearch());
    }

    IEnumerator WaitThenSearch()
    {
        waiting = true;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();

            if (backOffDistance > 0)
                agent.Move(-transform.forward * backOffDistance);
        }

        yield return new WaitForSeconds(waitAfterDropSeconds);

        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = false;

        waiting = false;
        state = State.SearchingBox;
    }

    bool HasArrived()
    {
        return NavMeshSpawnUtility.HasArrived(agent, arriveDistance);
    }

    BoxItem FindClosestBox()
    {
        BoxItem[] boxes = FindObjectsOfType<BoxItem>();

        float bestDistance = Mathf.Infinity;
        BoxItem bestBox = null;

        foreach (BoxItem box in boxes)
        {
            if (box == null) continue;

            if (box.delivered) continue;
            if (box.pickedUp) continue;
            if (box.blocked) continue;
            if (box.IsOnCooldown()) continue;

            // NUEVO:
            // Si otro Clank ya la eligió, este Clank no la toma.
            if (IsReservedByOtherClank(box)) continue;

            if (!TryGetBoxDestination(box, out Vector3 destination))
                continue;

            if (!NavMeshSpawnUtility.TryGetCompletePathLength(agent, destination, out float distance, navMeshSampleRadius))
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestBox = box;
            }
        }

        return bestBox;
    }

    Vector3 GetBoxPosition(BoxItem box)
    {
        if (box.pickUpSnap != null)
            return box.pickUpSnap.position;

        return box.transform.position;
    }

    bool TryGetBoxDestination(BoxItem box, out Vector3 destination)
    {
        destination = default;

        if (box == null)
            return false;

        return NavMeshSpawnUtility.TrySamplePosition(GetBoxPosition(box), out destination, navMeshSampleRadius);
    }

    void PickUpBox(BoxItem box)
    {
        if (box == null)
            return;

        // NUEVO:
        // Seguridad extra: si la caja ya fue tomada por otro,
        // este Clank no la recoge.
        if (box.pickedUp || box.delivered || box.blocked)
        {
            ReleaseTargetBox();
            targetBox = null;
            state = State.SearchingBox;
            return;
        }

        if (IsReservedByOtherClank(box))
        {
            targetBox = null;
            state = State.SearchingBox;
            return;
        }

        if (holdPoint == null)
            holdPoint = transform;

        carriedBox = box;

        // NUEVO:
        // Ya no necesita estar reservada porque ahora oficialmente está cargada.
        ReleaseSpecificBox(box);

        targetBox = null;

        carriedBox.pickedUp = true;

        Transform t = carriedBox.transform;
        t.SetParent(holdPoint);
        t.localPosition = holdLocalOffset;
        t.localRotation = Quaternion.identity;

        Rigidbody rb = carriedBox.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        NavMeshObstacle obs = carriedBox.GetComponent<NavMeshObstacle>();
        if (obs != null)
            obs.enabled = false;
    }

    void DropBox()
    {
        if (carriedBox == null) return;

        Transform t = carriedBox.transform;

        t.SetParent(null);
        t.position = NavMeshSpawnUtility.ResolvePosition(dropPoint.position + dropPoint.forward, navMeshSampleRadius);

        carriedBox.delivered = true;
        carriedBox.pickedUp = false;

        Rigidbody rb = carriedBox.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.detectCollisions = true;
            rb.isKinematic = false;
        }

        NavMeshObstacle obs = carriedBox.GetComponent<NavMeshObstacle>();
        if (obs != null)
            obs.enabled = true;

        carriedBox = null;
    }

    void DropAndThrow(Collision collision)
    {
        if (carriedBox == null) return;

        Transform t = carriedBox.transform;

        t.SetParent(null);
        t.position = holdPoint.position + transform.forward * 0.6f;

        Rigidbody rb = carriedBox.GetComponent<Rigidbody>();
        if (rb == null)
            rb = carriedBox.gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.detectCollisions = true;

        Vector3 pushDir = transform.forward;

        if (collision.contacts.Length > 0)
            pushDir = -collision.contacts[0].normal;

        Vector3 force = (pushDir.normalized * throwForce) + (Vector3.up * throwUpForce);

        rb.AddForce(force, ForceMode.Impulse);

        carriedBox.pickedUp = false;
        carriedBox.blocked = true;

        NavMeshObstacle obs = carriedBox.GetComponent<NavMeshObstacle>();
        if (obs != null)
            obs.enabled = true;

        carriedBox = null;
        state = State.SearchingBox;
    }

    bool EnsureAgentOnNavMesh()
    {
        if (agent == null)
        {
            if (!warnedMissingNavMesh)
            {
                warnedMissingNavMesh = true;
                Debug.LogWarning("[Clank_NPC] Missing NavMeshAgent; Clank navigation is disabled.", this);
            }

            return false;
        }

        if (!agent.enabled)
            return false;

        if (agent.isOnNavMesh)
            return true;

        if (NavMeshSpawnUtility.TryWarpToNearest(agent, transform.position, navMeshSampleRadius + 2f))
            return true;

        if (!warnedMissingNavMesh)
        {
            warnedMissingNavMesh = true;
            Debug.LogWarning("[Clank_NPC] Could not place Clank on a NavMesh. Ensure RuntimeShipNavMesh builds before Clank spawns.", this);
        }

        return false;
    }

    bool TryMoveTo(Vector3 destination, bool forceRefresh)
    {
        if (!forceRefresh && agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete && Time.time < nextDestinationRefreshTime)
            return true;

        if (!forceRefresh && agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete && Vector3.Distance(destination, lastDestination) < destinationRefreshDistance)
            return true;

        if (NavMeshSpawnUtility.TrySetReachableDestination(agent, destination, out Vector3 resolvedDestination, navMeshSampleRadius))
        {
            lastDestination = resolvedDestination;
            nextDestinationRefreshTime = Time.time + destinationRefreshInterval;
            stuckTimer = 0f;
            lastProgressPosition = transform.position;
            return true;
        }

        if (!warnedMissingNavMesh)
        {
            warnedMissingNavMesh = true;
            Debug.LogWarning($"[Clank_NPC] Could not set NavMesh destination near {destination}.", this);
        }

        return false;
    }

    void RecoverFromStuck()
    {
        stuckTimer = 0f;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();

        if (carriedBox != null && dropPoint != null)
        {
            TryMoveTo(dropPoint.position, true);
            state = State.GoingToDropZone;
            return;
        }

        ReleaseTargetBox();
        targetBox = null;
        state = State.SearchingBox;
        nextTargetSearchTime = 0f;
    }

    // ─────────────────────────────────────────────
    // NUEVO: SISTEMA DE RESERVACIÓN DE CAJAS
    // ─────────────────────────────────────────────

    void ReserveBox(BoxItem box)
    {
        if (box == null)
            return;

        if (!reservedBoxes.ContainsKey(box))
            reservedBoxes.Add(box, this);
        else
            reservedBoxes[box] = this;
    }

    void ReleaseTargetBox()
    {
        if (targetBox == null)
            return;

        ReleaseSpecificBox(targetBox);
        targetBox = null;
    }

    void ReleaseSpecificBox(BoxItem box)
    {
        if (box == null)
            return;

        if (reservedBoxes.TryGetValue(box, out Clank_NPC owner))
        {
            if (owner == this)
                reservedBoxes.Remove(box);
        }
    }

    bool IsReservedByOtherClank(BoxItem box)
    {
        if (box == null)
            return false;

        if (!reservedBoxes.TryGetValue(box, out Clank_NPC owner))
            return false;

        if (owner == null)
        {
            reservedBoxes.Remove(box);
            return false;
        }

        return owner != this;
    }
}