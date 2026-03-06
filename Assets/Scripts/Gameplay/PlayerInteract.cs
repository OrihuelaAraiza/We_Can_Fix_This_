using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerInteract : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private float interactRadius = 2f;
    [SerializeField] private LayerMask interactLayer;

    [Header("Runtime")]
    [SerializeField] private bool isInteracting;
    [SerializeField] private string currentLabel;

    private PlayerMovement movement;
    private IInteractable currentTarget;
    private bool holdingInteract;
    private float repairMultiplier = 1f;

    private void Awake() => movement = GetComponent<PlayerMovement>();

    private void Update()
    {
        if (!movement.IsInitialized) return;
        DetectInteractable();
        HandleHold();
    }

    private void DetectInteractable()
    {
        // Si ya estamos interactuando, no buscar nuevo target
        if (isInteracting) return;

        Collider[] hits = Physics.OverlapSphere(
            transform.position, interactRadius, interactLayer);

        IInteractable best = null;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var interactable = hit.GetComponent<IInteractable>();
            if (interactable == null) continue;
            if (!interactable.CanInteract(movement)) continue;

            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < bestDist) { bestDist = dist; best = interactable; }
        }

        currentTarget = best;
        currentLabel = best?.GetInteractLabel() ?? "";
    }

    private void HandleHold()
    {
        if (currentTarget == null) return;

        if (holdingInteract && !isInteracting)
        {
            isInteracting = true;
            currentTarget.OnInteractStart(movement);
        }
        else if (holdingInteract && isInteracting)
        {
            currentTarget.OnInteractHeld(movement, Time.deltaTime * repairMultiplier);
        }
        else if (!holdingInteract && isInteracting)
        {
            isInteracting = false;
            currentTarget.OnInteractEnd(movement);
        }
    }

    // Llamado desde PlayerInputHandler
    public void SetInteractHeld(bool held)
    {
        if (!held && isInteracting && currentTarget != null)
        {
            currentTarget.OnInteractEnd(movement);
            isInteracting = false;
        }
        holdingInteract = held;
    }

    public void SetRepairMultiplier(float multiplier)
    {
        repairMultiplier = multiplier;
    }

    public IInteractable CurrentTarget => currentTarget;
    public bool IsInteracting => isInteracting;
    public string CurrentLabel => currentLabel;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
