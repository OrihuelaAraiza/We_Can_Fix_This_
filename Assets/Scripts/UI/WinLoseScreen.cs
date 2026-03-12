using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class WinLoseScreen : MonoBehaviour
{
    [Header("Panel principal")]
    [SerializeField] GameObject      screenPanel;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI subtitleText;
    [SerializeField] Image           backgroundOverlay;
    [SerializeField] Button          restartButton;
    [SerializeField] Button          lobbyButton;

    [Header("Colores")]
    [SerializeField] Color colorWin  = new Color(0.05f, 0.40f, 0.10f, 0.92f);
    [SerializeField] Color colorLose = new Color(0.40f, 0.05f, 0.05f, 0.92f);

    void OnEnable()
    {
        GameManager.OnGameOver += ShowLose;
        GameManager.OnGameWon  += ShowWin;
    }

    void OnDisable()
    {
        GameManager.OnGameOver -= ShowLose;
        GameManager.OnGameWon  -= ShowWin;
    }

    void Start()
    {
        if (screenPanel != null) screenPanel.SetActive(false);

        restartButton?.onClick.AddListener(RestartGame);
        lobbyButton?.onClick.AddListener(GoToLobby);
    }

    void ShowWin()
    {
        Show("WE FIXED IT!", "Core-X desactivado. La nave sobrevivio.", colorWin);
    }

    void ShowLose()
    {
        Show("GAME OVER", "La nave fue destruida. El espacio es cruel.", colorLose);
    }

    void Show(string title, string subtitle, Color bgColor)
    {
        if (screenPanel       != null) screenPanel.SetActive(true);
        if (titleText         != null) titleText.text    = title;
        if (subtitleText      != null) subtitleText.text = subtitle;
        if (backgroundOverlay != null) backgroundOverlay.color = bgColor;

        Time.timeScale = 0f; // Pausar el juego
    }

    void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void GoToLobby()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("01_Lobby");
    }
}
