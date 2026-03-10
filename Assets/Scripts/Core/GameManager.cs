using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Config")]
    [SerializeField] float restartDelay = 3f;

    [Header("Runtime")]
    [SerializeField] bool gameOver = false;
    [SerializeField] bool gameWon  = false;

    public bool IsGameOver => gameOver;
    public bool IsGameWon  => gameWon;

    public static event System.Action    OnGameOver;
    public static event System.Action    OnGameWon;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Roles aplicados principalmente en PlayerManager.OnPlayerJoined().
    // ApplySelectedRoles() actúa como fallback de diagnóstico.

    void Start()
    {
        // Llamar con delay para dar tiempo al PlayerInputManager de spawnear jugadores
        Invoke(nameof(ApplySelectedRoles), 0.5f);
    }

    void ApplySelectedRoles()
    {
        Debug.Log("[GameManager] ApplySelectedRoles called");

        // Buscar por PlayerMovement (componente garantizado en el prefab)
        var movements = FindObjectsOfType<PlayerMovement>();
        Debug.Log($"[GameManager] Found {movements.Length} players con PlayerMovement");

        foreach (var movement in movements)
        {
            // Agregar PlayerRole si el prefab no lo tiene
            if (!movement.TryGetComponent<PlayerRole>(out var pr))
            {
                pr = movement.gameObject.AddComponent<PlayerRole>();
                Debug.Log($"[GameManager] PlayerRole agregado dinámicamente a P{movement.PlayerIndex}");
            }

            int index = movement.PlayerIndex;
            var role  = RoleSelectionData.GetRole(index);

            if (role != null)
            {
                if (pr.Role == null)
                {
                    pr.AssignRole(role);
                    Debug.Log($"[GameManager] ✓ {role.roleName} → P{index}");
                }
                else
                {
                    Debug.Log($"[GameManager] P{index} ya tiene '{pr.Role.roleName}' — skip");
                }
            }
            else
            {
                Debug.LogWarning($"[GameManager] Sin rol guardado para P{index}");
            }
        }
    }

    void OnEnable()
    {
        ShipHealth.OnShipDestroyed += HandleGameOver;
    }

    void OnDisable()
    {
        ShipHealth.OnShipDestroyed -= HandleGameOver;
    }

    void HandleGameOver()
    {
        if (gameOver) return;
        gameOver = true;
        OnGameOver?.Invoke();
        Debug.Log("[GameManager] GAME OVER");
        Invoke(nameof(RestartScene), restartDelay);
    }

    public void HandleGameWon()
    {
        if (gameWon) return;
        gameWon = true;
        OnGameWon?.Invoke();
        Debug.Log("[GameManager] VICTORIA");
    }

    void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
