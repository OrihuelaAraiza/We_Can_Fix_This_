using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Wcft.Core;

/// <summary>
/// Menú de pausa para la escena Gameplay.
/// Se activa con Escape. Gestiona Time.timeScale y coordina con WinLoseScreen.
/// </summary>
/// <setup>
/// JERARQUÍA DENTRO DEL CANVAS DE GAMEPLAY (Sort Order 10):
///
/// PausePanel   (inicialmente INACTIVO)  Image semi-trans alpha ~0.92, color #0F1215, ~280x200px centrado
///              + este script PauseMenuUI.cs
/// ├── TapeDecor       Image #8A8278 height 5px en borde superior, stretch horizontal
/// ├── ScrewTL         Image círculo 7px color #1E2228 borde #383E44  esquina TL
/// ├── ScrewTR         Image círculo igual  esquina TR
/// ├── ScrewBL         Image círculo igual  esquina BL
/// ├── ScrewBR         Image círculo igual  esquina BR
/// ├── PostItDecor     Image #D8C838 ~52x20px rotación -3°  esquina superior derecha
/// │   └── PostItText  TextMeshProUGUI "pausa!" Caveat Bold 13px #242000
/// ├── TitleText       TextMeshProUGUI "|| PAUSA"  VT323 32px #C07810
/// ├── Separator       Image 1px alto color #242A2F  stretch horizontal
/// ├── BtnContinue     Button Image #1A1E28 borde #252B38  + TMP "CONTINUAR"  VT323 15px #8090A8
/// ├── BtnRestart      Button Image #183C20 borde #205028  + TMP "REINICIAR"  VT323 15px #70B888
/// └── BtnMainMenu     Button Image #381818 borde #502020  + TMP "MENU PRINCIPAL"  VT323 15px #A85050
///
/// INSTRUCCIONES DE MONTAJE:
/// 1. Crear Canvas separado o usar el Canvas existente del HUD con Sort Order 10
/// 2. Añadir PausePanel como hijo del Canvas (inicialmente desactivado)
/// 3. Añadir un Image de fondo oscuro semi-transparente (blocker) al PausePanel para bloquear clicks al gameplay
/// 4. Asignar este script al PausePanel y conectar los campos desde el Inspector
/// 5. En GameplayHUD, asignar el PauseMenuUI en el campo pauseMenu
/// </setup>
public class PauseMenuUI : MonoBehaviour
{
    // ── Evento estático — suscribir por nombre de clase, nunca por .Instance ──
    public static event Action<bool> OnPauseChanged;

    [Header("Panel")]
    [SerializeField] GameObject panelGO;  // el PausePanel GameObject

    [Header("Buttons")]
    [SerializeField] Button btnContinue;
    [SerializeField] Button btnRestart;
    [SerializeField] Button btnMainMenu;

    public bool IsPaused { get; private set; }

    // ─────────────────────────────────────────────────────────────
    void Start()
    {
        if (panelGO != null) panelGO.SetActive(false);

        btnContinue?.onClick.AddListener(Resume);
        btnRestart?.onClick.AddListener(RestartScene);
        btnMainMenu?.onClick.AddListener(GoToMainMenu);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsPaused)
                Resume();
            else
                TryPause();
        }
    }

    // ── API pública ───────────────────────────────────────────────

    public void TryPause()
    {
        // No pausar si otro sistema (WinLoseScreen) ya tomó timeScale = 0
        // y nosotros no somos los responsables
        if (Time.timeScale == 0f && !IsPaused) return;

        SetPaused(true);
    }

    public void Resume()
    {
        SetPaused(false);
    }

    // ── Internos ──────────────────────────────────────────────────

    void SetPaused(bool paused)
    {
        IsPaused       = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (panelGO != null)
            panelGO.SetActive(paused);

        OnPauseChanged?.Invoke(paused);
    }

    void RestartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(GameConfig.SCENE_MAIN_MENU);
    }
}
