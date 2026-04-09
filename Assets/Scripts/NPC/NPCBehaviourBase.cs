using System.Collections;
using UnityEngine;

/// <summary>
/// Clase base para todos los NPCs del juego.
/// Permite que CoreXBrain los registre/desregistre y que el Saboteur los deshabilite.
/// </summary>
public abstract class NPCBehaviourBase : MonoBehaviour
{
    private bool isDisabled = false;
    private Coroutine disableCoroutine;

    public bool IsDisabled => isDisabled;

    /// <summary>Deshabilita el comportamiento del NPC por <paramref name="duration"/> segundos.</summary>
    public void Disable(float duration)
    {
        if (disableCoroutine != null)
            StopCoroutine(disableCoroutine);

        disableCoroutine = StartCoroutine(DisableRoutine(duration));
    }

    private IEnumerator DisableRoutine(float duration)
    {
        isDisabled = true;
        OnDisabled();

        yield return new WaitForSeconds(duration);

        isDisabled = false;
        OnEnabled();
        disableCoroutine = null;
    }

    /// <summary>Llamado cuando el NPC es deshabilitado (ej. por ElectricBomb).</summary>
    protected virtual void OnDisabled() { }

    /// <summary>Llamado cuando el NPC recupera su comportamiento.</summary>
    protected virtual void OnEnabled() { }
}
