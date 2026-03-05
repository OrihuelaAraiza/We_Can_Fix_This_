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
