using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuSceneBuilder
{
    const string ScenePath = "Assets/Scenes/01_MainMenu scene.unity";
    const string BackgroundPath = "Assets/Art/UI/UIMain.jpg";
    const string StartPath = "Assets/Art/UI/Start3D.PNG";
    const string ExitPath = "Assets/Art/UI/Exit3D.PNG";
    const string Title3DPath = "Assets/Art/UI/Titulo3D.PNG";
    const string TitleFlatPath = "Assets/Art/UI/Título.PNG";

    static readonly Vector2 ReferenceResolution = new Vector2(1600f, 900f);

    [MenuItem("WeCF/Build Main Menu Scene")]
    public static void BuildMainMenuScene()
    {
        ConfigureUISprites();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.08f, 0.09f, 0.13f);

        CreateCamera();
        GameObject canvasGO = CreateCanvas();
        RectTransform artRoot = CreateArtRoot(canvasGO.transform);

        CreateBackground(artRoot);
        CreateBuildLabel(artRoot);

        Button startButton = CreateMenuButton(
            artRoot,
            "BtnStart",
            new Vector2(-318f, -253f),
            new Vector2(320f, 122f),
            new Color(0.15f, 0.95f, 1f, 0.26f),
            new Color(0.58f, 1f, 1f, 0.36f));

        Button exitButton = CreateMenuButton(
            artRoot,
            "BtnExit",
            new Vector2(272f, -264f),
            new Vector2(302f, 126f),
            new Color(1f, 0.16f, 0.12f, 0.28f),
            new Color(1f, 0.55f, 0.42f, 0.38f));

        WireNavigation(startButton, exitButton);
        CreateEventSystem(startButton);
        WireMainMenuController(canvasGO, startButton, exitButton);

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
        EnsureMainMenuInBuildSettings();
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = canvasGO;
        Debug.Log("[MainMenuSceneBuilder] Main menu scene rebuilt and wired.");
    }

    static void ConfigureUISprites()
    {
        foreach (string path in new[] { BackgroundPath, StartPath, ExitPath, Title3DPath, TitleFlatPath })
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[MainMenuSceneBuilder] Missing UI texture: {path}");
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = path.EndsWith(".PNG");
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }

    static void CreateCamera()
    {
        var cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        cameraGO.transform.position = new Vector3(0f, 0f, -10f);

        var camera = cameraGO.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        cameraGO.AddComponent<AudioListener>();
    }

    static GameObject CreateCanvas()
    {
        var canvasGO = new GameObject("MainMenuCanvas", typeof(RectTransform));
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        canvasGO.AddComponent<MainMenuUI>();
        return canvasGO;
    }

    static RectTransform CreateArtRoot(Transform canvas)
    {
        var root = new GameObject("MenuArtRoot", typeof(RectTransform));
        root.transform.SetParent(canvas, false);

        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = ReferenceResolution;

        var aspect = root.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspect.aspectRatio = ReferenceResolution.x / ReferenceResolution.y;

        return rt;
    }

    static void CreateBackground(RectTransform artRoot)
    {
        Sprite background = LoadSprite(BackgroundPath);
        var bg = new GameObject("Background", typeof(RectTransform));
        bg.transform.SetParent(artRoot, false);

        var rt = bg.GetComponent<RectTransform>();
        Stretch(rt);

        var image = bg.AddComponent<Image>();
        image.sprite = background;
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = false;

        var dim = new GameObject("EdgeVignette", typeof(RectTransform));
        dim.transform.SetParent(artRoot, false);
        Stretch(dim.GetComponent<RectTransform>());
        var dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.10f);
        dimImage.raycastTarget = false;
    }

    static void CreateBuildLabel(RectTransform artRoot)
    {
        var labelGO = new GameObject("BuildLabel", typeof(RectTransform));
        labelGO.transform.SetParent(artRoot, false);

        var rt = labelGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-24f, 18f);
        rt.sizeDelta = new Vector2(260f, 26f);

        var text = labelGO.AddComponent<TextMeshProUGUI>();
        text.text = "DEMO BUILD";
        text.fontSize = 18f;
        text.alignment = TextAlignmentOptions.Right;
        text.color = new Color(0.68f, 0.72f, 0.82f, 0.55f);
        text.raycastTarget = false;
        text.enableWordWrapping = false;
    }

    static Button CreateMenuButton(
        RectTransform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        Color highlightedColor,
        Color pressedColor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;

        var hitImage = go.AddComponent<Image>();
        hitImage.color = new Color(1f, 1f, 1f, 0.001f);
        hitImage.raycastTarget = true;

        var highlightGO = new GameObject("SelectionGlow", typeof(RectTransform));
        highlightGO.transform.SetParent(go.transform, false);
        var highlightRT = highlightGO.GetComponent<RectTransform>();
        Stretch(highlightRT);

        var highlight = highlightGO.AddComponent<Image>();
        highlight.color = Color.clear;
        highlight.raycastTarget = false;

        var outline = highlightGO.AddComponent<Outline>();
        outline.effectColor = highlightedColor;
        outline.effectDistance = new Vector2(5f, -5f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = highlight;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.clear,
            highlightedColor = highlightedColor,
            pressedColor = pressedColor,
            selectedColor = highlightedColor,
            disabledColor = new Color(0f, 0f, 0f, 0.35f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };

        return button;
    }

    static void WireNavigation(Button startButton, Button exitButton)
    {
        var startNav = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = exitButton };
        var exitNav = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = startButton };
        startButton.navigation = startNav;
        exitButton.navigation = exitNav;
    }

    static void CreateEventSystem(Button firstSelected)
    {
        var eventSystemGO = new GameObject("EventSystem");
        var eventSystem = eventSystemGO.AddComponent<EventSystem>();
        eventSystem.firstSelectedGameObject = firstSelected != null ? firstSelected.gameObject : null;
        eventSystemGO.AddComponent<InputSystemUIInputModule>();
    }

    static void WireMainMenuController(GameObject canvasGO, Button startButton, Button exitButton)
    {
        var menu = canvasGO.GetComponent<MainMenuUI>();
        var so = new SerializedObject(menu);
        SetProp(so, "btnPlay", startButton);
        SetProp(so, "btnQuit", exitButton);
        so.ApplyModifiedProperties();
    }

    static void EnsureMainMenuInBuildSettings()
    {
        var orderedPaths = new[]
        {
            "Assets/Scenes/00_Bootstrap.unity",
            ScenePath,
            "Assets/Scenes/02_Lobby.unity",
            "Assets/Scenes/03_Gameplay.unity"
        };

        var scenes = new List<EditorBuildSettingsScene>();
        foreach (string path in orderedPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogWarning($"[MainMenuSceneBuilder] Sprite not loaded: {path}");
        return sprite;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetProp(SerializedObject so, string propName, Object value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null)
            prop.objectReferenceValue = value;
    }
}
