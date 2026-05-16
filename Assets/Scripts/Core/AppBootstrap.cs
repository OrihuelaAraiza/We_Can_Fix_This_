using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("loadLobbyOnStart")]
        [SerializeField] private bool loadMainMenuOnStart = true;

        [Header("Audio")]
        [SerializeField] private AudioManager audioManagerPrefab;

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
            EnsureAudioManager();
        }

        private void Start()
        {
            if (loadMainMenuOnStart)
            {
                SceneLoader.LoadScene(GameConfig.SCENE_MAIN_MENU);
            }
        }

        private void EnsureAudioManager()
        {
            if (AudioManager.Instance != null)
                return;

            if (audioManagerPrefab != null)
            {
                Instantiate(audioManagerPrefab);
                return;
            }

            // Fallback: create a bare AudioManager so the game doesn't crash without one
            var go = new GameObject("AudioManager", typeof(AudioManager));
            DontDestroyOnLoad(go);
            Debug.LogWarning("[AppBootstrap] AudioManager prefab not assigned — created blank instance. Assign it in the Inspector to load audio clips.");
        }

        private void ApplyGlobalSettings()
        {
            Application.targetFrameRate = GameConfig.TARGET_FRAMERATE;
            EnsureCharacterSkinningQuality();

            // Cursor: por ahora visible (cuando hagamos gameplay lo ajustamos)
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (GameConfig.VERBOSE_LOGS)
                Debug.Log("[AppBootstrap] Global settings applied.");
        }

        private static void EnsureCharacterSkinningQuality()
        {
            if ((int)QualitySettings.skinWeights >= (int)SkinWeights.FourBones)
                return;

            QualitySettings.skinWeights = SkinWeights.FourBones;

            if (GameConfig.VERBOSE_LOGS)
                Debug.Log("[AppBootstrap] Elevated skin weights to FourBones to keep character rigs stable in builds.");
        }
    }
}
