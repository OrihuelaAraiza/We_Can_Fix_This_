using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyUI : MonoBehaviour
{
    [Header("Player Panels — uno por jugador (máx 4)")]
    [SerializeField] List<PlayerLobbyPanel> playerPanels = new();

    [Header("Start Button")]
    [SerializeField] GameObject startPrompt;
    [SerializeField] TextMeshProUGUI startPromptText;

    [Header("Instrucciones")]
    [SerializeField] GameObject      joinPromptPanel;
    [SerializeField] TextMeshProUGUI joinPromptText;

    void OnEnable()
    {
        LobbyManager.OnPlayerRoleSelected += OnRoleSelected;
        LobbyManager.OnPlayerReadyChanged += OnReadyChanged;
        LobbyManager.OnAllPlayersReady    += OnAllReady;
    }

    void OnDisable()
    {
        LobbyManager.OnPlayerRoleSelected -= OnRoleSelected;
        LobbyManager.OnPlayerReadyChanged -= OnReadyChanged;
        LobbyManager.OnAllPlayersReady    -= OnAllReady;
    }

    void Start()
    {
        if (startPrompt != null)
            startPrompt.SetActive(false);

        // Ocultar paneles sin jugador
        for (int i = 0; i < playerPanels.Count; i++)
            playerPanels[i].SetVisible(false);

        // Instrucciones de controles
        if (joinPromptText != null)
            joinPromptText.text =
                "P1: A/D + ESPACIO  |  P2: ←/→ + ENTER  |  Gamepad: L-Stick + A";
    }

    public void ShowPanel(int playerIndex, List<RoleDefinition> roles)
    {
        if (playerIndex >= playerPanels.Count) return;
        playerPanels[playerIndex].Initialize(playerIndex, roles);
        playerPanels[playerIndex].SetVisible(true);
        RepositionPanels(playerIndex + 1);
        ConnectPanelButtons(playerIndex);

        // Ocultar instrucciones cuando el último slot se ocupa
        if (joinPromptPanel != null && playerIndex >= 3)
            joinPromptPanel.SetActive(false);
    }

    // ── Conexión de botones desde el MonoBehaviour ─────────────
    // PlayerLobbyPanel es [Serializable], no MonoBehaviour.
    // Los listeners se registran aquí para garantizar que el
    // contexto del MonoBehaviour (this) sea el punto de captura.

    void ConnectPanelButtons(int playerIndex)
    {
        if (playerIndex >= playerPanels.Count) return;
        var panel = playerPanels[playerIndex];

        if (panel.prevRoleButton != null)
        {
            panel.prevRoleButton.onClick.RemoveAllListeners();
            int idx = playerIndex;
            panel.prevRoleButton.onClick.AddListener(() => OnPrevRole(idx));
        }

        if (panel.nextRoleButton != null)
        {
            panel.nextRoleButton.onClick.RemoveAllListeners();
            int idx = playerIndex;
            panel.nextRoleButton.onClick.AddListener(() => OnNextRole(idx));
        }

        if (panel.readyButton != null)
        {
            panel.readyButton.onClick.RemoveAllListeners();
            int idx = playerIndex;
            panel.readyButton.onClick.AddListener(() => OnReadyButton(idx));
        }

        Debug.Log($"[LobbyUI] Botones conectados para P{playerIndex}");
    }

    void OnPrevRole(int playerIndex)
    {
        Debug.Log($"[LobbyUI] PrevRole P{playerIndex}");
        var roles   = LobbyManager.Instance?.GetAvailableRoles();
        if (roles == null || roles.Count == 0) return;
        var current = LobbyManager.Instance.GetSelectedRole(playerIndex);
        int idx     = roles.IndexOf(current);
        int prev    = (idx - 1 + roles.Count) % roles.Count;
        LobbyManager.Instance.SelectRole(playerIndex, roles[prev]);
    }

    void OnNextRole(int playerIndex)
    {
        Debug.Log($"[LobbyUI] NextRole P{playerIndex}");
        var roles   = LobbyManager.Instance?.GetAvailableRoles();
        if (roles == null || roles.Count == 0) return;
        var current = LobbyManager.Instance.GetSelectedRole(playerIndex);
        int idx     = roles.IndexOf(current);
        int next    = (idx + 1) % roles.Count;
        LobbyManager.Instance.SelectRole(playerIndex, roles[next]);
    }

    void OnReadyButton(int playerIndex)
    {
        Debug.Log($"[LobbyUI] ReadyButton P{playerIndex}");
        LobbyManager.Instance?.ToggleReady(playerIndex);
    }

    void RepositionPanels(int activePanelCount)
    {
        // Posiciones X según número de jugadores activos
        float[][] positions = new float[][]
        {
            new float[] { 0 },
            new float[] { -220, 220 },
            new float[] { -440, 0, 440 },
            new float[] { -480, -160, 160, 480 }
        };

        float[] xPositions = positions[activePanelCount - 1];

        for (int i = 0; i < activePanelCount; i++)
        {
            if (i >= playerPanels.Count) break;
            var root = playerPanels[i].root;
            if (root == null) continue;

            var rt = root.GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector2 pos = rt.anchoredPosition;
                pos.x = xPositions[i];
                rt.anchoredPosition = pos;
            }
        }
    }

    void OnRoleSelected(int playerIndex, RoleDefinition role)
    {
        if (playerIndex >= playerPanels.Count) return;
        playerPanels[playerIndex].UpdateSelectedRole(role);
    }

    void OnReadyChanged(int playerIndex, bool ready)
    {
        if (playerIndex >= playerPanels.Count) return;
        playerPanels[playerIndex].SetReady(ready);
    }

    void OnAllReady()
    {
        if (startPrompt != null)
        {
            startPrompt.SetActive(true);
            if (startPromptText != null)
                startPromptText.text = "¡INICIANDO...";
        }
    }
}

[System.Serializable]
public class PlayerLobbyPanel
{
    [Header("Root")]
    public GameObject root;

    [Header("UI Elements")]
    public TextMeshProUGUI playerLabel;
    public TextMeshProUGUI roleNameText;
    public TextMeshProUGUI perkText;
    public TextMeshProUGUI penaltyText;
    public Image           roleColorBar;
    public Image           readyIndicator;
    public TextMeshProUGUI readyText;

    [Header("Navigation")]
    public Button prevRoleButton;
    public Button nextRoleButton;
    public Button readyButton;

    int playerIndex;
    int currentRoleIndex;
    List<RoleDefinition> roles;

    public void Initialize(int index, List<RoleDefinition> availableRoles)
    {
        playerIndex      = index;
        roles            = availableRoles;
        currentRoleIndex = 0;

        if (playerLabel != null)
            playerLabel.text = $"JUGADOR {index + 1}";

        // Forzar texto visible en botones (ASCII — evita warnings de Unicode con LiberationSans)
        SetButtonText(prevRoleButton, "<");
        SetButtonText(nextRoleButton, ">");
        SetButtonText(readyButton,    "READY");

        // Remover listeners anteriores
        prevRoleButton?.onClick.RemoveAllListeners();
        nextRoleButton?.onClick.RemoveAllListeners();
        readyButton?.onClick.RemoveAllListeners();

        // Capturar índice en closure para que cada panel tenga su propio valor
        int capturedIndex = index;

        if (prevRoleButton != null)
            prevRoleButton.onClick.AddListener(() => {
                PrevRole();
                Debug.Log($"[Lobby] BtnPrev clicked P{capturedIndex}");
            });

        if (nextRoleButton != null)
            nextRoleButton.onClick.AddListener(() => {
                NextRole();
                Debug.Log($"[Lobby] BtnNext clicked P{capturedIndex}");
            });

        if (readyButton != null)
            readyButton.onClick.AddListener(() => {
                ToggleReady();
                Debug.Log($"[Lobby] BtnReady clicked P{capturedIndex}");
            });

        RefreshDisplay();
    }

    void SetButtonText(Button btn, string text)
    {
        if (btn == null) return;
        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) { tmp.text = text; tmp.color = Color.black; }
    }

    void PrevRole()
    {
        currentRoleIndex = (currentRoleIndex - 1 + roles.Count) % roles.Count;
        LobbyManager.Instance?.SelectRole(playerIndex, roles[currentRoleIndex]);
        RefreshDisplay();
    }

    void NextRole()
    {
        currentRoleIndex = (currentRoleIndex + 1) % roles.Count;
        LobbyManager.Instance?.SelectRole(playerIndex, roles[currentRoleIndex]);
        RefreshDisplay();
    }

    void ToggleReady()
    {
        Debug.Log($"[Lobby] ToggleReady llamado para playerIndex={playerIndex}");
        LobbyManager.Instance?.ToggleReady(playerIndex);
    }

    public void UpdateSelectedRole(RoleDefinition role)
    {
        currentRoleIndex = roles.IndexOf(role);
        RefreshDisplay();
    }

    void RefreshDisplay()
    {
        if (roles == null || roles.Count == 0) return;
        var role = roles[currentRoleIndex];

        if (roleNameText != null)
            roleNameText.text = role.roleName.ToUpper();
        if (perkText != null)
            perkText.text = role.perkDescription;
        if (penaltyText != null)
            penaltyText.text = "! " + role.penaltyDescription;
        if (roleColorBar != null)
            roleColorBar.color = role.roleColor;
    }

    public void SetReady(bool ready)
    {
        if (readyIndicator != null)
            readyIndicator.color = ready
                ? new Color(0.2f, 0.8f, 0.3f)
                : new Color(0.4f, 0.4f, 0.4f);
        if (readyText != null)
            readyText.text = ready ? "✓ LISTO" : "PRESIONA READY";
    }

    public void SetVisible(bool visible)
    {
        if (root != null) root.SetActive(visible);
    }
}
