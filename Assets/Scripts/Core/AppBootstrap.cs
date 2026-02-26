using UnityEngine;

namespace Wcft.Core
{
    /// <summary>
    /// Entry point del juego. Vive en 00_Bootstrap.
    /// Debe existir exactamente una vez y persistir entre escenas.
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        private static AppBootstrap _instance;

        [Header("Boot Flow")]
        [SerializeField] private bool loadLobbyOnStart = true;

        private void Awake()
        {
            // Singleton simple para evitar duplicados
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            ApplyGlobalSettings();
        }

        private void Start()
        {
            if (loadLobbyOnStart)
            {
                SceneLoader.LoadScene(GameConfig.SCENE_LOBBY);
            }
        }

        private void ApplyGlobalSettings()
        {
            Application.targetFrameRate = GameConfig.TARGET_FRAMERATE;

            // Cursor: por ahora visible (cuando hagamos gameplay lo ajustamos)
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (GameConfig.VERBOSE_LOGS)
                Debug.Log("[AppBootstrap] Global settings applied.");
        }
    }
}