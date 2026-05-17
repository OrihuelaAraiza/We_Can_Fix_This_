using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class Clank_NPC : NPCBehaviourBase
{
    [Header("Agent")]
    public NavMeshAgent agent;
    public float arriveDistance = 0.8f;

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

    private BoxItem targetBox;
    private BoxItem carriedBox;

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
    }

    protected override void OnDisabled()
    {
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
        targetBox = FindClosestBox();

        if (targetBox != null)
        {
            if (TrySetDestination(GetBoxPosition(targetBox)))
                state = State.GoingToBox;
            else
                targetBox = null;
        }
    }

    void GoToBox()
    {
        if (targetBox == null)
        {
            state = State.SearchingBox;
            return;
        }

        if (!TrySetDestination(GetBoxPosition(targetBox)))
        {
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

        if (TrySetDestination(dropPoint.position))
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

        if (!TrySetDestination(dropPoint.position))
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
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        if (agent.pathPending) return false;
        return agent.remainingDistance <= arriveDistance;
    }

    BoxItem FindClosestBox()
    {
        BoxItem[] boxes = FindObjectsOfType<BoxItem>();

        float bestDistance = Mathf.Infinity;
        BoxItem bestBox = null;

        foreach (BoxItem box in boxes)
        {
            if (box.delivered) continue;
            if (box.pickedUp) continue;
            if (box.blocked) continue;
            if (box.IsOnCooldown()) continue;

            if (!TryGetBoxDestination(box, out Vector3 destination))
                continue;

            float distance = Vector3.Distance(transform.position, destination);

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

        return NavMeshSpawnUtility.TrySamplePosition(GetBoxPosition(box), out destination, 3f);
    }

    void PickUpBox(BoxItem box)
    {
        if (holdPoint == null)
            holdPoint = transform;

        carriedBox = box;
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
        t.position = NavMeshSpawnUtility.ResolvePosition(dropPoint.position + dropPoint.forward, 2f);

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

        if (NavMeshSpawnUtility.TryWarpToNearest(agent, transform.position, 6f))
            return true;

        if (!warnedMissingNavMesh)
        {
            warnedMissingNavMesh = true;
            Debug.LogWarning("[Clank_NPC] Could not place Clank on a NavMesh. Ensure RuntimeShipNavMesh builds before Clank spawns.", this);
        }

        return false;
    }

    bool TrySetDestination(Vector3 destination)
    {
        if (NavMeshSpawnUtility.TrySetDestination(agent, destination, 4f))
            return true;

        if (!warnedMissingNavMesh)
        {
            warnedMissingNavMesh = true;
            Debug.LogWarning($"[Clank_NPC] Could not set NavMesh destination near {destination}.", this);
        }

        return false;
    }
}
