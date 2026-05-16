using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Wcft.Core;

public class LobbyUI : MonoBehaviour
{
    static LobbyUI instance;

    static readonly Color BgColor      = Hex("#090b14");
    static readonly Color PanelAlt     = Hex("#10141a");
    static readonly Color BorderColor  = Hex("#3a4048");
    static readonly Color TextColor    = Hex("#e6dcc0");
    static readonly Color GreenColor   = Hex("#2f8f50");
    static readonly Color RedColor     = Hex("#8f2b2b");

    [Header("Scenes")]
    [SerializeField] int minPlayersToStart = 2;

    [Header("Player Panels — one per player (max 4)")]
    [SerializeField] List<PlayerLobbyPanel> playerPanels = new();

    [Header("Start Button")]
    [SerializeField] GameObject startPrompt;
    [SerializeField] TextMeshProUGUI startPromptText;
    [SerializeField] Button btnStart;
    [SerializeField] Button btnBack;

    [Header("Instructions")]
    [SerializeField] GameObject      joinPromptPanel;
    [SerializeField] TextMeshProUGUI joinPromptText;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }

        instance = this;
    }

    void OnEnable()
    {
        if (instance != null && instance != this)
            return;

        LobbyManager.OnPlayerRoleSelected += OnRoleSelected;
        LobbyManager.OnPlayerReadyChanged += OnReadyChanged;
        LobbyManager.OnAllPlayersReady    += OnAllReady;
    }

    void OnDisable()
    {
        if (instance != this)
            return;

        LobbyManager.OnPlayerRoleSelected -= OnRoleSelected;
        LobbyManager.OnPlayerReadyChanged -= OnReadyChanged;
        LobbyManager.OnAllPlayersReady    -= OnAllReady;
    }

    void Start()
    {
        ApplyRuntimeStyle();

        if (startPrompt != null)
            startPrompt.SetActive(false);
        if (btnStart != null)
            btnStart.gameObject.SetActive(false);

        // Normaliza el layout base para que todos los paneles compartan la misma geometría.
        for (int i = 0; i < playerPanels.Count; i++)
            playerPanels[i].NormalizeLayout();

        // Ocultar paneles sin jugador
        for (int i = 0; i < playerPanels.Count; i++)
            playerPanels[i].SetVisible(false);

        // Instrucciones de controles
        if (joinPromptText != null)
            joinPromptText.text =
                "JOIN  SPACE=P1  ENTER=P2  GAMEPAD=A";

        btnStart?.onClick.AddListener(OnStartClicked);
        btnBack?.onClick.AddListener(OnBackClicked);

        RefreshStartAvailability();
    }

    void OnDestroy()
    {
        btnStart?.onClick.RemoveListener(OnStartClicked);
        btnBack?.onClick.RemoveListener(OnBackClicked);

        if (instance == this)
            instance = null;
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

        Debug.Log($"[LobbyUI] Buttons connected for P{playerIndex}");
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
            new float[] { -160, 160 },
            new float[] { -310, 0, 310 },
            new float[] { -465, -155, 155, 465 }
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
        RefreshStartAvailability();
    }

    void OnReadyChanged(int playerIndex, bool ready)
    {
        if (playerIndex >= playerPanels.Count) return;
        playerPanels[playerIndex].SetReady(ready);
        RefreshStartAvailability();
    }

    void OnAllReady()
    {
        if (joinPromptPanel != null)
            joinPromptPanel.SetActive(false);

        if (btnStart != null)
            btnStart.gameObject.SetActive(false);

        if (startPrompt != null)
        {
            startPrompt.SetActive(true);
            if (startPromptText != null)
                startPromptText.text = "STARTING...";
        }

        // Conserva el estado interno para escenas antiguas que aun tengan BtnStart conectado.
        RefreshStartAvailability();
    }

    void OnReadyChanged_CheckStartButton(int playerIndex, bool ready)
    {
        _ = playerIndex; _ = ready;
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
        SceneLoader.LoadScene(GameConfig.SCENE_MAIN_MENU);
    }

    void RefreshStartAvailability()
    {
        if (btnStart == null)
            return;

        bool canStart = false;
        if (LobbyManager.Instance != null)
        {
            int connected = LobbyManager.Instance.GetConnectedPlayerCount();
            canStart = connected >= minPlayersToStart;

            for (int i = 0; i < connected && canStart; i++)
                canStart = LobbyManager.Instance.IsPlayerReady(i);
        }

        btnStart.interactable = canStart;
    }

    void ApplyRuntimeStyle()
    {
        Canvas canvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            Image background = FindChild<Image>(canvas.transform, "Background");
            if (background != null)
            {
                background.color = BgColor;
                background.raycastTarget = false;
            }

            TMP_Text title = FindChild<TMP_Text>(canvas.transform, "Title");
            if (title != null)
            {
                title.text = "WE CAN FIX THIS!";
                title.fontSize = 46f;
                title.color = TextColor;
                title.alignment = TextAlignmentOptions.Center;
                title.enableWordWrapping = false;
                SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(820f, 48f));
            }

            TMP_Text subtitle = FindChild<TMP_Text>(canvas.transform, "Subtitle");
            if (subtitle != null)
            {
                subtitle.text = "CREW SELECTION  //  COREXIS MAINTENANCE PROTOCOL";
                subtitle.fontSize = 18f;
                subtitle.color = Hex("#8a8e92");
                subtitle.alignment = TextAlignmentOptions.Center;
                subtitle.enableWordWrapping = false;
                SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(820f, 24f));
            }
        }

        StylePrompt(joinPromptPanel, joinPromptText, "JOIN  SPACE=P1  ENTER=P2  GAMEPAD=A");
        StylePrompt(startPrompt, startPromptText, "STARTING...");
        NormalizeStartPrompt();
        StyleButton(btnStart, GreenColor, TextColor);
        StyleButton(btnBack, RedColor, TextColor);

        for (int i = 0; i < playerPanels.Count; i++)
            playerPanels[i].ApplyStyle(i);
    }

    static T FindChild<T>(Transform root, string name) where T : Component
    {
        if (root == null)
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name && child.TryGetComponent(out T component))
                return component;
        }

        return null;
    }

    static void StylePrompt(GameObject panel, TMP_Text text, string value)
    {
        if (panel != null)
        {
            Image image = panel.GetComponent<Image>() ?? panel.AddComponent<Image>();
            image.color = PanelAlt;
            image.raycastTarget = false;
            EnsureOutline(panel, Hex("#252b38"), new Vector2(1f, -1f));
        }

        if (text != null)
        {
            text.text = value;
            text.fontSize = 20f;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
        }
    }

    static void StyleButton(Button button, Color bg, Color text)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>() ?? button.gameObject.AddComponent<Image>();
        image.color = bg;
        image.raycastTarget = true;
        EnsureOutline(button.gameObject, BorderColor, new Vector2(1f, -1f));

        var colors = button.colors;
        colors.normalColor = bg;
        colors.highlightedColor = Color.Lerp(bg, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(bg, Color.black, 0.22f);
        colors.selectedColor = Color.Lerp(bg, Color.white, 0.12f);
        colors.disabledColor = new Color(0.12f, 0.12f, 0.12f, 0.65f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = text;
            label.fontSize = Mathf.Max(label.fontSize, 18f);
            label.alignment = TextAlignmentOptions.Center;
        }
    }

    void NormalizeStartPrompt()
    {
        if (btnStart != null)
            btnStart.gameObject.SetActive(false);

        if (startPrompt == null)
            return;

        RectTransform promptRect = startPrompt.GetComponent<RectTransform>();
        SetRect(promptRect, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(420f, 44f));

        if (startPromptText == null)
            return;

        RectTransform textRect = startPromptText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        startPromptText.alignment = TextAlignmentOptions.Center;
        startPromptText.fontSize = 22f;
        startPromptText.enableWordWrapping = false;
    }

    static void EnsureOutline(GameObject target, Color color, Vector2 distance)
    {
        if (target == null)
            return;

        Outline outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
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

    static readonly Vector2 PanelSize          = new Vector2(268f, 420f);
    static readonly Vector2 PlayerLabelPos     = new Vector2(0f, -18f);
    static readonly Vector2 PlayerLabelSize    = new Vector2(224f, 28f);
    static readonly Vector2 RoleColorBarPos    = new Vector2(0f, -58f);
    static readonly Vector2 RoleColorBarSize   = new Vector2(224f, 5f);
    static readonly Vector2 RoleNamePos        = new Vector2(0f, -74f);
    static readonly Vector2 RoleNameSize       = new Vector2(224f, 42f);
    static readonly Vector2 PerkTextPos        = new Vector2(0f, -132f);
    static readonly Vector2 PerkTextSize       = new Vector2(224f, 74f);
    static readonly Vector2 PenaltyTextPos     = new Vector2(0f, -218f);
    static readonly Vector2 PenaltyTextSize    = new Vector2(224f, 52f);
    static readonly Vector2 PrevRoleButtonPos  = new Vector2(-68f, -292f);
    static readonly Vector2 PrevRoleButtonSize = new Vector2(54f, 40f);
    static readonly Vector2 NextRoleButtonPos  = new Vector2(68f, -292f);
    static readonly Vector2 NextRoleButtonSize = new Vector2(54f, 40f);
    static readonly Vector2 ReadyIndicatorPos  = new Vector2(-78f, -342f);
    static readonly Vector2 ReadyIndicatorSize = new Vector2(16f, 16f);
    static readonly Vector2 ReadyTextPos       = new Vector2(22f, -336f);
    static readonly Vector2 ReadyTextSize      = new Vector2(168f, 24f);
    static readonly Vector2 ReadyButtonPos     = new Vector2(0f, -372f);
    static readonly Vector2 ReadyButtonSize    = new Vector2(176f, 36f);

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

    public void ApplyStyle(int slotIndex)
    {
        if (root != null)
        {
            Image image = root.GetComponent<Image>() ?? root.AddComponent<Image>();
            image.color = Hex("#161a1e");
            image.raycastTarget = false;
            EnsureOutline(root, Hex("#3a4048"), new Vector2(2f, -2f));
        }

        StyleText(playerLabel, 22f, Hex("#e6dcc0"), TextAlignmentOptions.Left, false);
        StyleText(roleNameText, 32f, Hex("#e6dcc0"), TextAlignmentOptions.Left, false);
        StyleText(perkText, 18f, Hex("#7ee06f"), TextAlignmentOptions.Left, true);
        StyleText(penaltyText, 16f, Hex("#ff6565"), TextAlignmentOptions.Left, true);
        StyleText(readyText, 17f, Hex("#e6dcc0"), TextAlignmentOptions.Left, false);

        if (roleColorBar != null)
            roleColorBar.raycastTarget = false;

        StyleButton(prevRoleButton, Hex("#1a1e28"), Hex("#8090a8"));
        StyleButton(nextRoleButton, Hex("#1a1e28"), Hex("#8090a8"));
        StyleButton(readyButton, Hex("#183c20"), Hex("#70b888"));

        if (readyIndicator != null)
        {
            readyIndicator.color = Hex("#4a4a44");
            readyIndicator.raycastTarget = false;
            EnsureOutline(readyIndicator.gameObject, Hex("#242822"), new Vector2(1f, -1f));
        }
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
            playerLabel.text = $"PLAYER {index + 1}";

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
        if (tmp != null)
        {
            tmp.text = text;
            tmp.color = new Color(0.86f, 0.82f, 0.68f);
        }
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
        Debug.Log($"[Lobby] ToggleReady called for playerIndex={playerIndex}");
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
                ? new Color(0.20f, 0.78f, 0.38f)
                : new Color(0.30f, 0.30f, 0.28f);
        if (readyText != null)
            readyText.text = ready ? "READY!" : "PRESS READY";
    }

    public void SetVisible(bool visible)
    {
        if (root != null) root.SetActive(visible);
    }

    static void StyleText(TMP_Text text, float size, Color color, TextAlignmentOptions alignment, bool wrap)
    {
        if (text == null)
            return;

        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = wrap;
        text.overflowMode = wrap ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
    }

    static void StyleButton(Button button, Color bg, Color text)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>() ?? button.gameObject.AddComponent<Image>();
        image.color = bg;
        image.raycastTarget = true;
        EnsureOutline(button.gameObject, Hex("#384048"), new Vector2(1f, -1f));

        var colors = button.colors;
        colors.normalColor = bg;
        colors.highlightedColor = Color.Lerp(bg, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(bg, Color.black, 0.22f);
        colors.selectedColor = Color.Lerp(bg, Color.white, 0.12f);
        colors.disabledColor = new Color(0.12f, 0.12f, 0.12f, 0.65f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = text;
            label.fontSize = Mathf.Max(label.fontSize, 18f);
            label.alignment = TextAlignmentOptions.Center;
        }
    }

    static void EnsureOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
