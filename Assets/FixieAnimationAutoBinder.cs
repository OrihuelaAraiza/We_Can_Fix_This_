using System.Collections;
using UnityEngine;

public class FixieAnimationAutoBinder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private FixieAnimationRuntime animationRuntime;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Animator animator;
    [SerializeField] private FixieAnimationSet animationSet;

    [Header("Settings")]
    [SerializeField] private int slotIndex = 0;
    [SerializeField] private float maxWaitTime = 5f;

    private bool bound;
    private string status = "Waiting...";

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        StartCoroutine(BindWhenReady());
    }

    private IEnumerator BindWhenReady()
    {
        bound = false;
        status = "Searching references...";

        float timer = 0f;

        // Espera un frame para que el prefab termine de spawnearse.
        yield return null;

        while (!bound && timer < maxWaitTime)
        {
            ResolveReferences();

            if (movement == null)
            {
                status = "Missing PlayerMovement";
            }
            else if (!movement.IsInitialized)
            {
                status = "Waiting PlayerMovement Initialize...";
            }
            else if (animationRuntime == null)
            {
                status = "Missing FixieAnimationRuntime";
            }
            else if (visualRoot == null)
            {
                status = "Missing VisualRoot";
            }
            else if (animator == null)
            {
                status = "Missing Animator";
            }
            else if (animationSet == null)
            {
                status = "Missing FixieAnimationSet";
            }
            else
            {
                animationRuntime.enabled = true;

                animationRuntime.Bind(
                    movement,
                    visualRoot,
                    animator,
                    animationSet,
                    slotIndex
                );

                bound = true;
                status = "Animation Bound OK";
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        if (!bound)
        {
            Debug.LogError("[FixieAnimationAutoBinder] No se pudo hacer Bind. Status: " + status, this);
        }
    }

    private void ResolveReferences()
    {
        if (movement == null)
            movement = GetComponentInParent<PlayerMovement>();

        if (animationRuntime == null)
            animationRuntime = GetComponentInChildren<FixieAnimationRuntime>(true);

        if (animationRuntime == null)
            animationRuntime = GetComponentInParent<FixieAnimationRuntime>();

        if (visualRoot == null)
        {
            Animator foundAnimator = GetComponentInChildren<Animator>(true);

            if (foundAnimator != null)
                visualRoot = foundAnimator.transform;
            else
                visualRoot = transform;
        }

        if (animator == null && visualRoot != null)
            animator = visualRoot.GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }
}
