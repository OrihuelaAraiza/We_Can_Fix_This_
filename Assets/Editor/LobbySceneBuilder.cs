using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LobbySceneBuilder
{
    const string ScenePath = "Assets/Scenes/02_Lobby.unity";
    const string FontPath = "Assets/Fonts/VT323 SDF.asset";

    static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);
    static readonly Color Bg = Hex("#090b14");
    static readonly Color Panel = Hex("#161a1e");
    static readonly Color PanelAlt = Hex("#10141a");
    static readonly Color Border = Hex("#3a4048");
    static readonly Color Text = Hex("#e6dcc0");
    static readonly Color Muted = Hex("#8a8e92");
    static readonly Color Green = Hex("#2f8f50");
    static readonly Color Red = Hex("#8f2b2b");
    static readonly Color Amber = Hex("#c07010");

    [MenuItem("WeCF/Build Lobby Scene")]
    public static void BuildLobbyScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsureCamera();
        EnsureEventSystem();

        GameObject canvasGO = EnsureCanvas();
        ClearCanvas(canvasGO.transform);

        var background = ImageGO(canvasGO.transform, "Background", Bg);
        Stretch(background.rectTransform);
        background.raycastTarget = false;

        CreateTopBar(canvasGO.transform);
        CreateFooter(canvasGO.transform);

        RectTransform cardsRoot = Container(canvasGO.transform, "PlayerCards");
        Pin(cardsRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(1120f, 430f));

        var panels = new PlayerLobbyPanel[4];
        for (int i = 0; i < panels.Length; i++)
            panels[i] = CreatePlayerPanel(cardsRoot, i);

        GameObject joinPrompt = CreateJoinPrompt(canvasGO.transform, out TextMeshProUGUI joinText);
        GameObject startPrompt = CreateStartPrompt(canvasGO.transform, out TextMeshProUGUI startText, out Button startButton);
        Button backButton = CreateBackButton(canvasGO.transform);

        LobbyUI lobbyUI = EnsureSingleLobbyUI(canvasGO);
        WireLobbyUI(lobbyUI, panels, joinPrompt, joinText, startPrompt, startText, startButton, backButton);
        EnsureLobbySceneSystems();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[LobbySceneBuilder] Lobby scene rebuilt and wired.");
    }

    static void EnsureCamera()
    {
        Camera camera = Object.FindObjectOfType<Camera>();
        GameObject cameraGO = camera != null ? camera.gameObject : new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        cameraGO.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);

        camera = cameraGO.GetComponent<Camera>() ?? cameraGO.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Bg;
        camera.orthographic = true;
        camera.orthographicSize = 5f;

        if (cameraGO.GetComponent<AudioListener>() == null)
            cameraGO.AddComponent<AudioListener>();
    }

    static void EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindObjectOfType<EventSystem>();
        GameObject eventSystemGO = eventSystem != null ? eventSystem.gameObject : new GameObject("EventSystem");

        if (eventSystemGO.GetComponent<EventSystem>() == null)
            eventSystemGO.AddComponent<EventSystem>();

        foreach (var standalone in eventSystemGO.GetComponents<StandaloneInputModule>())
            Object.DestroyImmediate(standalone);

        if (eventSystemGO.GetComponent<InputSystemUIInputModule>() == null)
            eventSystemGO.AddComponent<InputSystemUIInputModule>();
    }

    static GameObject EnsureCanvas()
    {
        GameObject canvasGO = GameObject.Find("LobbyCanvas");
        if (canvasGO == null)
            canvasGO = new GameObject("LobbyCanvas", typeof(RectTransform));

        canvasGO.layer = LayerMask.NameToLayer("UI");

        var canvas = canvasGO.GetComponent<Canvas>() ?? canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>() ?? canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasGO.GetComponent<GraphicRaycaster>() == null)
            canvasGO.AddComponent<GraphicRaycaster>();

        return canvasGO;
    }

    static void ClearCanvas(Transform canvas)
    {
        for (int i = canvas.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(canvas.GetChild(i).gameObject);
    }

    static void CreateTopBar(Transform canvas)
    {
        var top = ImageGO(canvas, "TopBar", PanelAlt);
        Pin(top.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(980f, 104f));
        AddOutline(top.gameObject, Border, new Vector2(1.5f, -1.5f));

        TMP_Text title = Label(top.transform, "Title", "WE CAN FIX THIS!", 50f, Text, TextAlignmentOptions.Center);
        Pin(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(820f, 48f));

        TMP_Text subtitle = Label(top.transform, "Subtitle", "CREW SELECTION  //  COREXIS MAINTENANCE PROTOCOL", 18f, Muted, TextAlignmentOptions.Center);
        Pin(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(820f, 24f));
    }

    static void CreateFooter(Transform canvas)
    {
        var footer = ImageGO(canvas, "FooterPanel", PanelAlt);
        Pin(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(780f, 46f));
        AddOutline(footer.gameObject, Hex("#252b38"), new Vector2(1f, -1f));
    }

    static PlayerLobbyPanel CreatePlayerPanel(Transform parent, int index)
    {
        var rootImage = ImageGO(parent, $"PlayerPanel_{index}", Panel);
        var root = rootImage.gameObject;
        Pin(rootImage.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(268f, 420f));
        root.SetActive(false);
        AddOutline(root, Border, new Vector2(2f, -2f));

        var accent = ImageGO(root.transform, "RoleColorBar", Amber);
        var playerLabel = Label(root.transform, "PlayerLabel", $"PLAYER {index + 1}", 22f, Text, TextAlignmentOptions.Left);
        var roleName = Label(root.transform, "RoleName", "FIXIE", 32f, Text, TextAlignmentOptions.Left);
        var perk = Label(root.transform, "PerkText", "Select a role to see perks.", 18f, Hex("#7ee06f"), TextAlignmentOptions.Left);
        var penalty = Label(root.transform, "PenaltyText", "! No active penalty", 16f, Hex("#ff6565"), TextAlignmentOptions.Left);

        Image readyIndicator = ImageGO(root.transform, "ReadyIndicator", Hex("#4a4a44"));
        TextMeshProUGUI readyText = Label(root.transform, "ReadyText", "PRESS READY", 17f, Text, TextAlignmentOptions.Left);

        Button prev = ButtonGO(root.transform, "BtnPrev", "<", Hex("#1a1e28"), Hex("#8090a8"), new Vector2(54f, 40f));
        Button next = ButtonGO(root.transform, "BtnNext", ">", Hex("#1a1e28"), Hex("#8090a8"), new Vector2(54f, 40f));
        Button ready = ButtonGO(root.transform, "BtnReady", "READY", Hex("#183c20"), Hex("#70b888"), new Vector2(176f, 36f));

        var panel = new PlayerLobbyPanel
        {
            root = root,
            playerLabel = playerLabel,
            roleNameText = roleName,
            perkText = perk,
            penaltyText = penalty,
            roleColorBar = accent,
            readyIndicator = readyIndicator,
            readyText = readyText,
            prevRoleButton = prev,
            nextRoleButton = next,
            readyButton = ready
        };
        panel.NormalizeLayout();
        return panel;
    }

    static GameObject CreateJoinPrompt(Transform canvas, out TextMeshProUGUI joinText)
    {
        var prompt = ImageGO(canvas, "JoinPrompt", PanelAlt);
        Pin(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(780f, 46f));
        AddOutline(prompt.gameObject, Hex("#252b38"), new Vector2(1f, -1f));

        joinText = Label(prompt.transform, "JoinPromptText", "JOIN  SPACE=P1  ENTER=P2  GAMEPAD=A", 20f, Text, TextAlignmentOptions.Center);
        Stretch(joinText.rectTransform);
        return prompt.gameObject;
    }

    static GameObject CreateStartPrompt(Transform canvas, out TextMeshProUGUI startText, out Button startButton)
    {
        var prompt = ImageGO(canvas, "StartPrompt", Hex("#142018"));
        Pin(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(420f, 44f));
        AddOutline(prompt.gameObject, Green, new Vector2(1.5f, -1.5f));
        prompt.gameObject.SetActive(false);

        startText = Label(prompt.transform, "StartPromptText", "INICIANDO...", 22f, Hex("#9fe6a5"), TextAlignmentOptions.Center);
        Stretch(startText.rectTransform);

        startButton = null;
        return prompt.gameObject;
    }

    static Button CreateBackButton(Transform canvas)
    {
        Button back = ButtonGO(canvas, "BtnBack", "MENU", Hex("#2a1a1a"), Hex("#c88888"), new Vector2(104f, 34f));
        Pin(back.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(104f, 34f));
        return back;
    }

    static LobbyUI EnsureSingleLobbyUI(GameObject canvasGO)
    {
        foreach (LobbyUI ui in Object.FindObjectsOfType<LobbyUI>())
        {
            if (ui.gameObject != canvasGO)
                Object.DestroyImmediate(ui.gameObject);
        }

        return canvasGO.GetComponent<LobbyUI>() ?? canvasGO.AddComponent<LobbyUI>();
    }

    static void WireLobbyUI(
        LobbyUI ui,
        PlayerLobbyPanel[] panels,
        GameObject joinPrompt,
        TextMeshProUGUI joinText,
        GameObject startPrompt,
        TextMeshProUGUI startText,
        Button startButton,
        Button backButton)
    {
        var so = new SerializedObject(ui);
        SetProp(so, "minPlayersToStart", 1);
        var panelProp = so.FindProperty("playerPanels");
        panelProp.arraySize = panels.Length;

        for (int i = 0; i < panels.Length; i++)
        {
            SerializedProperty elem = panelProp.GetArrayElementAtIndex(i);
            SetRelative(elem, "root", panels[i].root);
            SetRelative(elem, "playerLabel", panels[i].playerLabel);
            SetRelative(elem, "roleNameText", panels[i].roleNameText);
            SetRelative(elem, "perkText", panels[i].perkText);
            SetRelative(elem, "penaltyText", panels[i].penaltyText);
            SetRelative(elem, "roleColorBar", panels[i].roleColorBar);
            SetRelative(elem, "readyIndicator", panels[i].readyIndicator);
            SetRelative(elem, "readyText", panels[i].readyText);
            SetRelative(elem, "prevRoleButton", panels[i].prevRoleButton);
            SetRelative(elem, "nextRoleButton", panels[i].nextRoleButton);
            SetRelative(elem, "readyButton", panels[i].readyButton);
        }

        SetProp(so, "joinPromptPanel", joinPrompt);
        SetProp(so, "joinPromptText", joinText);
        SetProp(so, "startPrompt", startPrompt);
        SetProp(so, "startPromptText", startText);
        SetProp(so, "btnStart", startButton);
        SetProp(so, "btnBack", backButton);
        so.ApplyModifiedProperties();
    }

    static void EnsureLobbySceneSystems()
    {
        if (Object.FindObjectOfType<LobbyManager>() == null)
            Debug.LogWarning("[LobbySceneBuilder] LobbyManager missing. Existing scene systems were expected.");

        if (Object.FindObjectOfType<LobbyPlayerJoiner>() == null)
        {
            var joinerGO = new GameObject("LobbyPlayerJoiner");
            joinerGO.AddComponent<LobbyPlayerJoiner>();
        }

        if (Object.FindObjectOfType<LobbyEventSystemFixer>() == null)
            Object.FindObjectOfType<LobbyManager>()?.gameObject.AddComponent<LobbyEventSystemFixer>();
    }

    static Image ImageGO(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static RectTransform Container(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        return label;
    }

    static Button ButtonGO(Transform parent, string name, string text, Color bg, Color textColor, Vector2 size)
    {
        var image = ImageGO(parent, name, bg);
        image.raycastTarget = true;
        Pin(image.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, size);
        AddOutline(image.gameObject, Hex("#384048"), new Vector2(1f, -1f));

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = new ColorBlock
        {
            normalColor = bg,
            highlightedColor = bg * 1.35f,
            pressedColor = bg * 0.75f,
            selectedColor = bg * 1.2f,
            disabledColor = new Color(0.12f, 0.12f, 0.12f, 0.65f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };

        TMP_Text label = Label(image.transform, "Label", text, 19f, textColor, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }

    static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        var outline = target.GetComponent<Outline>() ?? target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    static void Pin(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void SetProp(SerializedObject so, string name, Object value)
    {
        SerializedProperty prop = so.FindProperty(name);
        if (prop != null)
            prop.objectReferenceValue = value;
    }

    static void SetProp(SerializedObject so, string name, int value)
    {
        SerializedProperty prop = so.FindProperty(name);
        if (prop != null)
            prop.intValue = value;
    }

    static void SetRelative(SerializedProperty parent, string name, Object value)
    {
        SerializedProperty prop = parent.FindPropertyRelative(name);
        if (prop != null)
            prop.objectReferenceValue = value;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
