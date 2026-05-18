using System.Collections;
using UnityEngine;
using Wcft.Core;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Config")]
    [SerializeField] float restartDelay = 3f;
    [SerializeField] float levelTransitionDelay = 2.25f;

    [Header("Runtime")]
    [SerializeField] bool gameOver = false;
    [SerializeField] bool gameWon  = false;

    public bool IsGameOver => gameOver;
    public bool IsGameWon  => gameWon;

    public static event System.Action    OnGameOver;
    public static event System.Action    OnGameWon;
    public static event System.Action<LevelDefinition, LevelDefinition> OnLevelTransitionStarted;

    bool levelTransitioning;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Roles are applied primarily in PlayerManager.OnPlayerJoined().
    // ApplySelectedRoles() acts as a diagnostic fallback.

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);
        ApplySelectedRoles();
    }

    void ApplySelectedRoles()
    {
        Debug.Log("[GameManager] ApplySelectedRoles called");

        // Buscar por PlayerMovement (componente garantizado en el prefab)
        var movements = FindObjectsOfType<PlayerMovement>();
        Debug.Log($"[GameManager] Found {movements.Length} players with PlayerMovement");

        foreach (var movement in movements)
        {
            // Add PlayerRole if the prefab does not have it
            if (!movement.TryGetComponent<PlayerRole>(out var pr))
            {
                pr = movement.gameObject.AddComponent<PlayerRole>();
                Debug.Log($"[GameManager] PlayerRole dynamically added to P{movement.PlayerIndex}");
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
                Debug.LogWarning($"[GameManager] No saved role for P{index}");
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
        LevelProgression.Reset();
        OnGameOver?.Invoke();
        Debug.Log("[GameManager] GAME OVER");
        Invoke(nameof(RestartScene), restartDelay);
    }

    public void HandleGameWon()
    {
        if (gameWon) return;
        gameWon = true;
        OnGameWon?.Invoke();
        Debug.Log("[GameManager] VICTORY");
    }

    /// <summary>Called by CoreXBrain when Core-X is defeated.</summary>
    public void TriggerVictory()
    {
        Debug.Log("[GameManager] Core-X defeated — VICTORY!");
        HandleGameWon();
    }

    /// <summary>Called by SurvivalTimerUI when the timer runs out.</summary>
    public void OnTimerExpired()
    {
        if (gameOver || gameWon || levelTransitioning) return;

        LevelDefinition currentLevel = LevelProgression.Current;
        Debug.Log($"[GameManager] Timer expired on {currentLevel.Name} ({currentLevel.Index}/{LevelProgression.LevelCount})");

        StopGameplaySystems();

        if (LevelProgression.AdvanceOrComplete())
        {
            LevelDefinition nextLevel = LevelProgression.Current;
            Debug.Log($"[GameManager] Timer expired — advancing to {nextLevel.Name}");
            StartCoroutine(AdvanceToNextLevel(currentLevel, nextLevel));
            return;
        }

        Debug.Log("[GameManager] Final demo timer expired — VICTORY!");
        HandleGameWon();
    }

    IEnumerator AdvanceToNextLevel(LevelDefinition completedLevel, LevelDefinition nextLevel)
    {
        levelTransitioning = true;
        Time.timeScale = 1f;
        OnLevelTransitionStarted?.Invoke(completedLevel, nextLevel);

        if (levelTransitionDelay > 0f)
            yield return new WaitForSecondsRealtime(levelTransitionDelay);

        SceneLoader.ReloadActiveScene();
    }

    void StopGameplaySystems()
    {
        FailureSystem.Instance?.SetActive(false);
        CoreXBrain.Instance?.StopDirector();
    }

    void RestartScene()
    {
        SceneLoader.ReloadActiveScene();
    }
}
