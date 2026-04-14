using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wcft.Core;

/// <summary>
/// Menu de pausa autocontenido para gameplay.
/// Se puede crear por codigo dentro del Canvas y no depende del Inspector.
/// </summary>
[DisallowMultipleComponent]
public class PauseMenuUI : MonoBehaviour
{
    public static event Action<bool> OnPauseChanged;

    static readonly Vector2 WindowSize = new Vector2(360f, 332f);

    static readonly Color OverlayColor      = new Color32(10, 13, 16, 196);
    static readonly Color WindowColor       = new Color32(15, 18, 21, 245);
    static readonly Color TitleColor        = new Color32(192, 120, 16, 255);
    static readonly Color HintColor         = new Color32(128, 144, 168, 255);
    static readonly Color ContinueColor     = new Color32(26, 30, 40, 255);
    static readonly Color RestartColor      = new Color32(24, 60, 32, 255);
    static readonly Color MainMenuColor     = new Color32(56, 24, 24, 255);
    static readonly Color QuitColor         = new Color32(72, 18, 18, 255);
    static readonly Color ContinueTextColor = new Color32(128, 144, 168, 255);
    static readonly Color RestartTextColor  = new Color32(112, 184, 136, 255);
    static readonly Color MainTextColor     = new Color32(168, 80, 80, 255);
    static readonly Color QuitTextColor     = new Color32(224, 170, 170, 255);

    [Header("Panel")]
    [SerializeField] GameObject panelGO;

    [Header("Buttons")]
    [SerializeField] Button btnContinue;
    [SerializeField] Button btnRestart;
    [SerializeField] Button btnMainMenu;
    [SerializeField] Button btnQuit;

    public bool IsPaused { get; private set; }

    public static PauseMenuUI EnsureOnCanvas(Canvas canvas)
    {
        if (canvas == null)
            return null;

        PauseMenuUI[] existingMenus = canvas.GetComponentsInChildren<PauseMenuUI>(true);
        foreach (PauseMenuUI existingMenu in existingMenus)
        {
            if (existingMenu == null || !existingMenu.gameObject.activeSelf)
                continue;

            existingMenu.BuildRuntimeUIIfNeeded();
            return existingMenu;
        }

        GameObject controllerGO = new GameObject("PauseMenuController", typeof(RectTransform));
        controllerGO.transform.SetParent(canvas.transform, false);
        controllerGO.transform.SetAsLastSibling();

        RectTransform controllerRect = controllerGO.GetComponent<RectTransform>();
        controllerRect.anchorMin = Vector2.zero;
        controllerRect.anchorMax = Vector2.one;
        controllerRect.offsetMin = Vector2.zero;
        controllerRect.offsetMax = Vector2.zero;

        return controllerGO.AddComponent<PauseMenuUI>();
    }

    void Awake()
    {
        BuildRuntimeUIIfNeeded();
        HidePanelImmediate();
    }

    void OnEnable()
    {
        RebindButtons();
    }

    void Start()
    {
        HidePanelImmediate();
    }

    void OnDisable()
    {
        UnbindButtons();
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (IsPaused)
            Resume();
        else
            TryPause();
    }

    public void TryPause()
    {
        if (Time.timeScale == 0f && !IsPaused)
            return;

        SetPaused(true);
    }

    public void Resume()
    {
        SetPaused(false);
    }

    void SetPaused(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (panelGO != null)
            panelGO.SetActive(paused);

        if (paused && btnContinue != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(btnContinue.gameObject);
        }

        OnPauseChanged?.Invoke(paused);
    }

    void ExitPauseState()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        if (panelGO != null)
            panelGO.SetActive(false);

        OnPauseChanged?.Invoke(false);
    }

    void RestartScene()
    {
        ExitPauseState();
        SceneLoader.LoadScene(SceneManager.GetActiveScene().name);
    }

    void GoToMainMenu()
    {
        ExitPauseState();
        SceneLoader.LoadScene(GameConfig.SCENE_MAIN_MENU);
    }

    void QuitGame()
    {
        ExitPauseState();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void HidePanelImmediate()
    {
        IsPaused = false;

        if (panelGO != null)
            panelGO.SetActive(false);
    }

    void RebindButtons()
    {
        UnbindButtons();

        btnContinue?.onClick.AddListener(Resume);
        btnRestart?.onClick.AddListener(RestartScene);
        btnMainMenu?.onClick.AddListener(GoToMainMenu);
        btnQuit?.onClick.AddListener(QuitGame);
    }

    void UnbindButtons()
    {
        btnContinue?.onClick.RemoveListener(Resume);
        btnRestart?.onClick.RemoveListener(RestartScene);
        btnMainMenu?.onClick.RemoveListener(GoToMainMenu);
        btnQuit?.onClick.RemoveListener(QuitGame);
    }

    void BuildRuntimeUIIfNeeded()
    {
        ConfigureControllerRect();

        if (panelGO == null)
            panelGO = transform.Find("PauseOverlay")?.gameObject;

        if (panelGO == null)
        {
            CreateRuntimeUI();
            return;
        }

        if (btnContinue == null)
            btnContinue = FindButtonByName(panelGO.transform, "BtnContinue");
        if (btnRestart == null)
            btnRestart = FindButtonByName(panelGO.transform, "BtnRestart");
        if (btnMainMenu == null)
            btnMainMenu = FindButtonByName(panelGO.transform, "BtnMainMenu");
        if (btnQuit == null)
            btnQuit = FindButtonByName(panelGO.transform, "BtnQuit");

        if (btnContinue == null || btnRestart == null || btnMainMenu == null || btnQuit == null)
            CreateRuntimeUI();
    }

    void ConfigureControllerRect()
    {
        RectTransform controllerRect = transform as RectTransform;
        if (controllerRect == null)
            return;

        controllerRect.anchorMin = Vector2.zero;
        controllerRect.anchorMax = Vector2.one;
        controllerRect.offsetMin = Vector2.zero;
        controllerRect.offsetMax = Vector2.zero;
    }

    void CreateRuntimeUI()
    {
        if (panelGO != null && panelGO.transform.parent == transform)
            Destroy(panelGO);

        RectTransform overlayRect = CreateRect("PauseOverlay", transform);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.SetAsLastSibling();

        Image overlayImage = overlayRect.gameObject.AddComponent<Image>();
        overlayImage.color = OverlayColor;
        overlayImage.sprite = GetBuiltinSprite("UI/Skin/Background.psd");
        overlayImage.type = Image.Type.Sliced;

        panelGO = overlayRect.gameObject;

        RectTransform windowRect = CreateRect("PauseWindow", overlayRect);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = WindowSize;
        windowRect.anchoredPosition = Vector2.zero;

        Image windowImage = windowRect.gameObject.AddComponent<Image>();
        windowImage.color = WindowColor;
        windowImage.sprite = GetBuiltinSprite("UI/Skin/UISprite.psd");
        windowImage.type = Image.Type.Sliced;

        VerticalLayoutGroup windowLayout = windowRect.gameObject.AddComponent<VerticalLayoutGroup>();
        windowLayout.padding = new RectOffset(24, 24, 24, 24);
        windowLayout.spacing = 12f;
        windowLayout.childAlignment = TextAnchor.UpperCenter;
        windowLayout.childControlWidth = true;
        windowLayout.childControlHeight = false;
        windowLayout.childForceExpandWidth = true;
        windowLayout.childForceExpandHeight = false;

        ContentSizeFitter windowSizeFitter = windowRect.gameObject.AddComponent<ContentSizeFitter>();
        windowSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        windowSizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        CreateTextBlock(windowRect, "TitleText", "|| PAUSA", 34f, TitleColor, FontStyles.Bold, 42f);
        CreateTextBlock(windowRect, "HintText", "ESC para continuar", 16f, HintColor, FontStyles.Normal, 26f);

        RectTransform spacer = CreateRect("Spacer", windowRect);
        spacer.gameObject.AddComponent<LayoutElement>().preferredHeight = 8f;

        RectTransform buttonStack = CreateRect("ButtonStack", windowRect);
        VerticalLayoutGroup buttonLayout = buttonStack.gameObject.AddComponent<VerticalLayoutGroup>();
        buttonLayout.spacing = 10f;
        buttonLayout.childAlignment = TextAnchor.UpperCenter;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.childForceExpandHeight = false;

        ContentSizeFitter buttonSizeFitter = buttonStack.gameObject.AddComponent<ContentSizeFitter>();
        buttonSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        buttonSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        btnContinue = CreateButton(buttonStack, "BtnContinue", "CONTINUAR", ContinueColor, ContinueTextColor);
        btnRestart = CreateButton(buttonStack, "BtnRestart", "REINICIAR", RestartColor, RestartTextColor);
        btnMainMenu = CreateButton(buttonStack, "BtnMainMenu", "MENU PRINCIPAL", MainMenuColor, MainTextColor);
        btnQuit = CreateButton(buttonStack, "BtnQuit", "SALIR DEL JUEGO", QuitColor, QuitTextColor);

        panelGO.SetActive(false);
    }

    RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    void CreateTextBlock(Transform parent, string objectName, string content, float fontSize, Color color, FontStyles style, float preferredHeight)
    {
        RectTransform textRect = CreateRect(objectName, parent);

        LayoutElement layoutElement = textRect.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = preferredHeight;

        TextMeshProUGUI tmp = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
    }

    Button CreateButton(Transform parent, string objectName, string label, Color backgroundColor, Color textColor)
    {
        RectTransform buttonRect = CreateRect(objectName, parent);

        LayoutElement layoutElement = buttonRect.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 46f;
        layoutElement.minHeight = 46f;

        Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
        buttonImage.sprite = GetBuiltinSprite("UI/Skin/UISprite.psd");
        buttonImage.type = Image.Type.Sliced;
        buttonImage.color = backgroundColor;

        Button button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = Color.Lerp(backgroundColor, Color.white, 0.12f);
        colors.pressedColor = Color.Lerp(backgroundColor, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        RectTransform labelRect = CreateRect("Label", buttonRect);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI labelTmp = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        labelTmp.text = label;
        labelTmp.fontSize = 18f;
        labelTmp.color = textColor;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.raycastTarget = false;
        labelTmp.enableWordWrapping = false;

        if (TMP_Settings.defaultFontAsset != null)
            labelTmp.font = TMP_Settings.defaultFontAsset;

        return button;
    }

    Button FindButtonByName(Transform root, string objectName)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.name == objectName)
                return button;
        }

        return null;
    }

    Sprite GetBuiltinSprite(string resourcePath)
    {
        Sprite sprite = Resources.GetBuiltinResource<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        return Resources.GetBuiltinResource<Sprite>(System.IO.Path.GetFileName(resourcePath));
    }
}
