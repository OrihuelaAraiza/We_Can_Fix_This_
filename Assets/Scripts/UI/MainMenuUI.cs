using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.EventSystems;
using Wcft.Core;

/// <summary>
/// Main menu controller.
/// </summary>
/// <setup>
/// HIERARCHY IN SCENE "MainMenu":
///
/// Canvas  (Screen Space Overlay, Sort Order 0, CanvasScaler Scale With Screen Size 1920x1080 match 0.5)
/// ├── Background          Image, color #0F1215, stretch full
/// ├── ScanLine            Image + CRTScanEffect.cs  (see CRTScanEffect.cs setup)
/// ├── LogoPanel           Image, color #151A1E, border sim via Outline/child Image, ~600x220px centered-top
/// │   ├── TitleText       TextMeshProUGUI  "WE CAN FIX THIS!"  VT323 88px #C07810
/// │   ├── SubtitleText    TextMeshProUGUI  "COREXIS MAINTENANCE CREW — EMERGENCY PROTOCOL"  VT323 18px #384048
/// │   ├── TapeDecor       Image, color #8A8278, height 6px, stretch horizontal, above the title
/// │   ├── ScrewTL         Image sprite screw (circle 7px #1E2228 border #383E44) TL corner of panel
/// │   ├── ScrewBR         Image sprite screw, BR corner of panel
/// │   └── PostItDecor     Image #D8C838 ~60x22px TR corner   +   TextMeshProUGUI "launch v0.1" Caveat 12px #242000
/// ├── ButtonPanel         (~260x160px centered, below LogoPanel)
/// │   ├── BtnPlay         Button + Image #183C20 border #205028  +  TMP "START MISSION"  VT323 15px #70B888
/// │   ├── BtnCredits      Button + Image #1A1E28 border #252B38  +  TMP "CREDITS"        VT323 15px #8090A8
/// │   └── BtnQuit         Button + Image #381818 border #502020  +  TMP "QUIT"           VT323 15px #A85050
/// ├── CreditsPanel        Image #151A1E, initially INACTIVE, ~480x200px centered
/// │   ├── NamesText       TextMeshProUGUI  "JP ORIHUELA · ARANZA ROMO · URIEL VALDEZ"  VT323 22px #C07810
/// │   ├── UnivText        TextMeshProUGUI  "Universidad Panamericana · AI in Video Games · 2026"  VT323 15px #384048
/// │   ├── BandaidDecor    Image #B07840 at TL corner (~40x14px)
/// │   ├── StapleDecor     Image at top edge (~8x4px border #787870)
/// │   └── BtnClose        Button + Image #1A1E28  +  TMP "CLOSE"  VT323 15px #8090A8
/// └── VersionText         TextMeshProUGUI  "BUILD 0.1.0-ALPHA"  VT323 12px #2A2A2A  anchored bottom-right
///
/// Assign SerializeField fields from the Inspector.
/// EventSystem: add a GameObject with EventSystem + StandaloneInputModule.
/// </setup>
public class MainMenuUI : MonoBehaviour
{
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
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (creditsPanel != null) creditsPanel.SetActive(false);

        btnPlay?.onClick.AddListener(OnPlayClicked);
        btnCredits?.onClick.AddListener(OnCreditsClicked);
        btnQuit?.onClick.AddListener(OnQuitClicked);
        btnCreditsClose?.onClick.AddListener(OnCreditsClose);

        if (EventSystem.current != null && btnPlay != null)
            EventSystem.current.SetSelectedGameObject(btnPlay.gameObject);
    }

    void OnDestroy()
    {
        btnPlay?.onClick.RemoveListener(OnPlayClicked);
        btnCredits?.onClick.RemoveListener(OnCreditsClicked);
        btnQuit?.onClick.RemoveListener(OnQuitClicked);
        btnCreditsClose?.onClick.RemoveListener(OnCreditsClose);
    }

    // ── Handlers ─────────────────────────────────────────────────

    public void OnPlayClicked()
    {
        if (SceneExistsInBuild(GameConfig.SCENE_LOBBY))
            SceneLoader.LoadScene(GameConfig.SCENE_LOBBY);
        else
            SceneLoader.LoadScene(GameConfig.SCENE_GAMEPLAY);
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
