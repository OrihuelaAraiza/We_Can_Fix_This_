using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wcft.Core
{
    /// <summary>
    /// Encapsula el cambio de escenas. Asi no hay SceneManager.LoadScene
    /// regado por todos lados. Esto es practica profesional.
    /// </summary>
    public static class SceneLoader
    {
        public static void LoadScene(string sceneName)
        {
            if (GameConfig.VERBOSE_LOGS)
                Debug.Log($"[SceneLoader] Loading scene: {sceneName}");

            SceneManager.LoadScene(sceneName);
        }

        public static IEnumerator LoadSceneAsync(string sceneName)
        {
            if (GameConfig.VERBOSE_LOGS)
                Debug.Log($"[SceneLoader] Loading scene async: {sceneName}");

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = true;

            while (!op.isDone)
            {
                // Aqui mas adelante podemos reportar progreso a UI
                yield return null;
            }
        }
    }
}