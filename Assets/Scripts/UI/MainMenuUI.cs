using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controlador del menú principal.
/// </summary>
/// <setup>
/// JERARQUÍA EN LA ESCENA "MainMenu":
///
/// Canvas  (Screen Space Overlay, Sort Order 0, CanvasScaler Scale With Screen Size 1920x1080 match 0.5)
/// ├── Background          Image, color #0F1215, stretch full
/// ├── ScanLine            Image + CRTScanEffect.cs  (ver CRTScanEffect.cs setup)
/// ├── LogoPanel           Image, color #151A1E, border sim via Outline/child Image, ~600x220px centrado-top
/// │   ├── TitleText       TextMeshProUGUI  "WE CAN FIX THIS!"  VT323 88px #C07810
/// │   ├── SubtitleText    TextMeshProUGUI  "COREXIS MAINTENANCE CREW — EMERGENCY PROTOCOL"  VT323 18px #384048
/// │   ├── TapeDecor       Image, color #8A8278, height 6px, stretch horizontal, sobre el título
/// │   ├── ScrewTL         Image sprite screw (círculo 7px #1E2228 borde #383E44) posición TL del panel
/// │   ├── ScrewBR         Image sprite screw, posición BR del panel
/// │   └── PostItDecor     Image #D8C838 ~60x22px esquina TR   +   TextMeshProUGUI "launch v0.1" Caveat 12px #242000
/// ├── ButtonPanel         (~260x160px centrado, bajo LogoPanel)
/// │   ├── BtnPlay         Button + Image #183C20 borde #205028  +  TMP "INICIAR MISION"  VT323 15px #70B888
/// │   ├── BtnCredits      Button + Image #1A1E28 borde #252B38  +  TMP "CREDITOS"         VT323 15px #8090A8
/// │   └── BtnQuit         Button + Image #381818 borde #502020  +  TMP "SALIR"             VT323 15px #A85050
/// ├── CreditsPanel        Image #151A1E, inicialmente INACTIVO, ~480x200px centrado
/// │   ├── NamesText       TextMeshProUGUI  "JP ORIHUELA · ARANZA ROMO · URIEL VALDEZ"  VT323 22px #C07810
/// │   ├── UnivText        TextMeshProUGUI  "Universidad Panamericana · AI en Videojuegos · 2026"  VT323 15px #384048
/// │   ├── BandaidDecor    Image #B07840 en esquina TL (~40x14px)
/// │   ├── StapleDecor     Image en borde superior (~8x4px borde #787870)
/// │   └── BtnClose        Button + Image #1A1E28  +  TMP "CERRAR"  VT323 15px #8090A8
/// └── VersionText         TextMeshProUGUI  "BUILD 0.1.0-ALPHA"  VT323 12px #2A2A2A  anclado bottom-right
///
/// Asignar los campos SerializeField desde el Inspector.
/// EventSystem: añadir un GameObject con EventSystem + StandaloneInputModule.
/// </setup>
public class MainMenuUI : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] string lobbyScene    = "Lobby";
    [SerializeField] string gameplayScene = "Gameplay";

    [Header("Panels")]
    [SerializeField] GameObject creditsPanel;

    [Header("Buttons")]
    [SerializeField] UnityEngine.UI.Button btnPlay;
    [SerializeField] UnityEngine.UI.Button btnCredits;
    [SerializeField] UnityEngine.UI.Button btnQuit;
    [SerializeField] UnityEngine.UI.Button btnCreditsClose;

    // ─────────────────────────────────────────────────────────────
    void Start()
    {
        if (creditsPanel != null) creditsPanel.SetActive(false);

        btnPlay?.onClick.AddListener(OnPlayClicked);
        btnCredits?.onClick.AddListener(OnCreditsClicked);
        btnQuit?.onClick.AddListener(OnQuitClicked);
        btnCreditsClose?.onClick.AddListener(OnCreditsClose);
    }

    // ── Handlers ─────────────────────────────────────────────────

    public void OnPlayClicked()
    {
        // Intentar cargar lobbyScene; si no está en BuildSettings, ir directo a gameplayScene
        if (SceneExistsInBuild(lobbyScene))
            SceneManager.LoadScene(lobbyScene);
        else
            SceneManager.LoadScene(gameplayScene);
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnCreditsClicked()
    {
        if (creditsPanel != null) creditsPanel.SetActive(true);
    }

    public void OnCreditsClose()
    {
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }

    // ── Utilidad ──────────────────────────────────────────────────

    static bool SceneExistsInBuild(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                return true;
        }
        return false;
    }
}
