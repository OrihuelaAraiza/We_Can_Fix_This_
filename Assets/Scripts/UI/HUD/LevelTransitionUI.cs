using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wcft.Core;

public sealed class LevelTransitionUI : MonoBehaviour
{
    static LevelTransitionUI instance;

    CanvasGroup canvasGroup;
    TextMeshProUGUI titleText;
    TextMeshProUGUI subtitleText;
    TextMeshProUGUI detailText;
    Coroutine hideRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureExists()
    {
        if (instance != null)
            return;

        var go = new GameObject("LevelTransitionUI");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<LevelTransitionUI>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BuildUI();
        HideImmediate();
    }

    void OnEnable()
    {
        GameManager.OnLevelTransitionStarted += ShowTransition;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDisable()
    {
        GameManager.OnLevelTransitionStarted -= ShowTransition;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void ShowTransition(LevelDefinition completedLevel, LevelDefinition nextLevel)
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        titleText.text = $"LEVEL {completedLevel.Index} COMPLETE";
        subtitleText.text = $"PREPARING LEVEL {nextLevel.Index}";
        detailText.text = $"{nextLevel.Name.ToUpperInvariant()}  |  BIGGER MAP  |  HIGHER PRESSURE";

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;
    }

    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (canvasGroup.alpha <= 0f)
            return;

        hideRoutine = StartCoroutine(HideAfterDelay(1.1f));
    }

    IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        float duration = 0.35f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }

        HideImmediate();
        hideRoutine = null;
    }

    void HideImmediate()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        RectTransform root = gameObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image overlay = CreateImage(root, "Overlay", new Color32(8, 10, 12, 226));
        Stretch(overlay.rectTransform);

        Image topLine = CreateImage(root, "TopLine", new Color32(231, 191, 78, 255));
        SetHorizontalLine(topLine.rectTransform, 0.68f);

        Image bottomLine = CreateImage(root, "BottomLine", new Color32(255, 98, 87, 255));
        SetHorizontalLine(bottomLine.rectTransform, 0.32f);

        titleText = CreateLabel(root, "Title", 54f, new Color32(231, 191, 78, 255), TextAlignmentOptions.Center);
        SetAnchored(titleText.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(980f, 86f), Vector2.zero);

        subtitleText = CreateLabel(root, "Subtitle", 28f, new Color32(230, 235, 242, 255), TextAlignmentOptions.Center);
        SetAnchored(subtitleText.rectTransform, new Vector2(0.5f, 0.45f), new Vector2(980f, 48f), Vector2.zero);

        detailText = CreateLabel(root, "Detail", 18f, new Color32(128, 144, 168, 255), TextAlignmentOptions.Center);
        SetAnchored(detailText.rectTransform, new Vector2(0.5f, 0.38f), new Vector2(980f, 36f), Vector2.zero);
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string name, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static void SetHorizontalLine(RectTransform rect, float anchorY)
    {
        rect.anchorMin = new Vector2(0.12f, anchorY);
        rect.anchorMax = new Vector2(0.88f, anchorY);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 2f);
    }

    static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }
}
