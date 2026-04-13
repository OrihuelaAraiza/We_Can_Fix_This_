using UnityEngine;

namespace Wcft.Core
{
    /// <summary>
    /// Config central del proyecto. Aqui definimos nombres de escenas
    /// y settings globales que deben mantenerse consistentes.
    /// </summary>
    public static class GameConfig
    {
        // Nombres exactos de escenas (deben coincidir con Build Settings)
        public const string SCENE_BOOTSTRAP = "00_Bootstrap";
        public const string SCENE_MAIN_MENU = "01_MainMenu scene";
        public const string SCENE_LOBBY = "02_Lobby";
        public const string SCENE_GAMEPLAY = "03_Gameplay";

        // Settings globales recomendados
        public const int TARGET_FRAMERATE = 60;

        // Para desarrollo: puedes poner true si quieres logs mas verbosos
        public const bool VERBOSE_LOGS = true;
    }
}
