using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Wcft.Core;

public class LobbyUI : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] int minPlayersToStart = 2;

    [Header("Player Panels — uno por jugador (máx 4)")]
    [SerializeField] List<PlayerLobbyPanel> playerPanels = new();

    [Header("Start Button")]
    [SerializeField] GameObject startPrompt;
    [SerializeField] TextMeshProUGUI startPromptText;
    [SerializeField] Button btnStart;   // botón INICIAR — conectar desde Inspector
    [SerializeField] Button btnBack;    // botón VOLVER  — conectar desde Inspector

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

        // Normaliza el layout base para que todos los paneles compartan la misma geometría.
        for (int i = 0; i < playerPanels.Count; i++)
            playerPanels[i].NormalizeLayout();

        // Ocultar paneles sin jugador
        for (int i = 0; i < playerPanels.Count; i++)
            playerPanels[i].SetVisible(false);

        // Instrucciones de controles
        if (joinPromptText != null)
            joinPromptText.text =
                "UNIRSE: ESPACIO = Teclado 1 | ENTER = Teclado 2 | Gamepad = A";

        btnStart?.onClick.AddListener(OnStartClicked);
        btnBack?.onClick.AddListener(OnBackClicked);

        // Botón INICIAR deshabilitado hasta que haya suficientes jugadores listos
        if (btnStart != null) btnStart.interactable = false;
    }

    public void ShowPanel(int playerIndex, List<RoleDefinition> roles)
    {
        if (playerIndex >= playerPanels.Count) return;
        playerPanels[playerIndex].NormalizeLayout();
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
            new float[] { -320, 320 },
            new float[] { -480, 0, 480 },
            new float[] { -540, -180, 180, 540 }
        };

        float[] xPositions = positions[activePanelCount - 1];

        // Usar el Y del panel 0 como referencia para que todos queden a la misma altura,
        // independientemente de donde estén colocados en la escena.
        float referenceY = 0f;
        var panel0Rt = playerPanels[0].root?.GetComponent<RectTransform>();
        if (panel0Rt != null)
            referenceY = panel0Rt.anchoredPosition.y;

        for (int i = 0; i < activePanelCount; i++)
        {
            if (i >= playerPanels.Count) break;
            var root = playerPanels[i].root;
            if (root == null) continue;

            var rt = root.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = new Vector2(xPositions[i], referenceY);
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

        // Habilitar botón INICIAR cuando todos están listos
        if (btnStart != null) btnStart.interactable = true;
    }

    void OnReadyChanged_CheckStartButton(int playerIndex, bool ready)
    {
        // Contar cuántos jugadores están en estado READY para habilitar el botón INICIAR
        // (requiere acceso a LobbyManager; si no está disponible, depender solo de OnAllReady)
        _ = playerIndex; _ = ready; // usados indirectamente vía LobbyManager
    }

    // ── Botones de escena ─────────────────────────────────────────

    public void OnStartClicked()
    {
        Time.timeScale = 1f;
        LobbyManager.Instance?.StartGameFromUI();
    }

    public void OnBackClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(GameConfig.SCENE_MAIN_MENU);
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

    static readonly Vector2 PanelSize          = new Vector2(320f, 500f);
    static readonly Vector2 PlayerLabelPos     = new Vector2(0f, -20f);
    static readonly Vector2 PlayerLabelSize    = new Vector2(280f, 40f);
    static readonly Vector2 RoleColorBarPos    = new Vector2(0f, -68f);
    static readonly Vector2 RoleColorBarSize   = new Vector2(280f, 6f);
    static readonly Vector2 RoleNamePos        = new Vector2(0f, -90f);
    static readonly Vector2 RoleNameSize       = new Vector2(280f, 50f);
    static readonly Vector2 PerkTextPos        = new Vector2(0f, -155f);
    static readonly Vector2 PerkTextSize       = new Vector2(260f, 70f);
    static readonly Vector2 PenaltyTextPos     = new Vector2(0f, -240f);
    static readonly Vector2 PenaltyTextSize    = new Vector2(260f, 50f);
    static readonly Vector2 PrevRoleButtonPos  = new Vector2(-90f, -310f);
    static readonly Vector2 PrevRoleButtonSize = new Vector2(80f, 50f);
    static readonly Vector2 NextRoleButtonPos  = new Vector2(90f, -310f);
    static readonly Vector2 NextRoleButtonSize = new Vector2(80f, 50f);
    static readonly Vector2 ReadyIndicatorPos  = new Vector2(-70f, -385f);
    static readonly Vector2 ReadyIndicatorSize = new Vector2(20f, 20f);
    static readonly Vector2 ReadyTextPos       = new Vector2(30f, -380f);
    static readonly Vector2 ReadyTextSize      = new Vector2(160f, 30f);
    static readonly Vector2 ReadyButtonPos     = new Vector2(0f, -425f);
    static readonly Vector2 ReadyButtonSize    = new Vector2(200f, 45f);

    public void NormalizeLayout()
    {
        RectTransform rootRect = root != null ? root.GetComponent<RectTransform>() : null;
        if (rootRect != null)
        {
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = PanelSize;
        }

        NormalizeRect(playerLabel, PlayerLabelPos, PlayerLabelSize);
        NormalizeRect(roleColorBar, RoleColorBarPos, RoleColorBarSize);
        NormalizeRect(roleNameText, RoleNamePos, RoleNameSize);
        NormalizeRect(perkText, PerkTextPos, PerkTextSize);
        NormalizeRect(penaltyText, PenaltyTextPos, PenaltyTextSize);
        NormalizeRect(prevRoleButton, PrevRoleButtonPos, PrevRoleButtonSize);
        NormalizeRect(nextRoleButton, NextRoleButtonPos, NextRoleButtonSize);
        NormalizeRect(readyIndicator, ReadyIndicatorPos, ReadyIndicatorSize);
        NormalizeRect(readyText, ReadyTextPos, ReadyTextSize);
        NormalizeRect(readyButton, ReadyButtonPos, ReadyButtonSize);
    }

    void NormalizeRect(Component target, Vector2 anchoredPosition, Vector2 size)
    {
        if (target == null) return;

        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null) return;

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

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
