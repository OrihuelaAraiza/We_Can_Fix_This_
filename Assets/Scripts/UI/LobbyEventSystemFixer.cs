using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Agrega este componente al mismo GameObject que LobbyManager (o cualquier
/// objeto que esté en la escena del lobby).
///
/// Problema raíz: Unity New Input System 1.x reemplaza StandaloneInputModule
/// con InputSystemUIInputModule. Si la escena todavía tiene el módulo viejo,
/// los clics de mouse no llegan a los botones Unity UI — el teclado sí funciona
/// porque PlayerLobbyInputHandler lo maneja directamente sin pasar por el UI.
/// </summary>
public class LobbyEventSystemFixer : MonoBehaviour
{
    void Awake()
    {
        var es = FindObjectOfType<EventSystem>();
        if (es == null)
        {
            // No hay EventSystem en escena — crearlo
            var go = new GameObject("EventSystem");
            es = go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            Debug.Log("[LobbyEventSystemFixer] EventSystem creado con InputSystemUIInputModule");
            return;
        }

        // Si ya tiene InputSystemUIInputModule, no hacer nada
        if (es.GetComponent<InputSystemUIInputModule>() != null) return;

        // Remover StandaloneInputModule si existe y reemplazar
        var standalone = es.GetComponent<StandaloneInputModule>();
        if (standalone != null)
        {
            Destroy(standalone);
            Debug.Log("[LobbyEventSystemFixer] StandaloneInputModule removido");
        }

        es.gameObject.AddComponent<InputSystemUIInputModule>();
        Debug.Log("[LobbyEventSystemFixer] InputSystemUIInputModule agregado — mouse habilitado");
    }
}
