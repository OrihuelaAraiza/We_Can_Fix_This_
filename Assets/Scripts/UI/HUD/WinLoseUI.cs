using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Wcft.Core;

public class WinLoseUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text   missionLabel;
    [SerializeField] TMP_Text   resultTitle;
    [SerializeField] TMP_Text   subtitleText;
    [SerializeField] Button     restartButton;
    [SerializeField] Button     lobbyButton;
    [SerializeField] Image      panelBorderImage;

    [Header("Style")]
    [SerializeField] UIStyleConfig style;

    [Header("Button Colors")]
    [SerializeField] Color victoryRestartBg = HexColor("#1e5030");
    [SerializeField] Color victoryLobbyBg   = HexColor("#1e3020");
    [SerializeField] Color defeatLobbyBg    = HexColor("#3a1a1a");

    void OnEnable()
    {
        GameManager.OnGameOver += OnGameOver;
        GameManager.OnGameWon  += OnGameWon;
    }

    void OnDisable()
    {
        GameManager.OnGameOver -= OnGameOver;
        GameManager.OnGameWon  -= OnGameWon;
    }

    void Start()
    {
        Hide();

        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestart);
        if (lobbyButton != null)
            lobbyButton.onClick.AddListener(OnLobby);
    }

    void OnGameOver()
    {
        ShowDefeat("Corexis destroyed");
    }

    void OnGameWon()
    {
        ShowVictory("Core-X neutralized");
    }

    // ── Public API ──────────────────────────────────────────────

    public void ShowVictory(string subtitle)
    {
        if (panel != null)
            panel.SetActive(true);

        if (missionLabel != null)
            missionLabel.text = "MISSION COMPLETE";

        if (resultTitle != null)
        {
            resultTitle.text  = "VICTORY!";
            resultTitle.color = style != null ? style.victoryText : HexColor("#2a8040");
        }

        if (subtitleText != null)
            subtitleText.text = subtitle;

        if (panelBorderImage != null)
            panelBorderImage.color = style != null ? style.victoryBorder : HexColor("#182a18");

        ApplyButtonColor(restartButton, victoryRestartBg);
        ApplyButtonColor(lobbyButton, victoryLobbyBg);
    }

    public void ShowDefeat(string subtitle)
    {
        if (panel != null)
            panel.SetActive(true);

        if (missionLabel != null)
            missionLabel.text = "MISSION FAILED";

        if (resultTitle != null)
        {
            resultTitle.text  = "DEFEAT";
            resultTitle.color = style != null ? style.defeatText : HexColor("#a02828");
        }

        if (subtitleText != null)
            subtitleText.text = subtitle;

        if (panelBorderImage != null)
            panelBorderImage.color = style != null ? style.defeatBorder : HexColor("#2a1818");

        ApplyButtonColor(restartButton, victoryRestartBg);
        ApplyButtonColor(lobbyButton, defeatLobbyBg);
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    // ── Button handlers ─────────────────────────────────────────

    void OnRestart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnLobby()
    {
        SceneManager.LoadScene(GameConfig.SCENE_LOBBY);
    }

    void ApplyButtonColor(Button btn, Color bg)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = bg;
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
