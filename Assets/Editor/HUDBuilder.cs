using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > Build HUD
/// Creates the full GameplayCanvas hierarchy, attaches HUD scripts,
/// and wires HUDManager references in one click.
/// Safe to run multiple times — existing GameObjects are reused.
/// </summary>
public static class HUDBuilder
{
    // Panel background: #161a1e
    static readonly Color PanelBg      = Hex("#161a1e");
    // Transparent container (no visual background)
    static readonly Color Transparent  = new Color(0, 0, 0, 0);

    [MenuItem("Tools/Build HUD")]
    static void BuildHUD()
    {
        var canvasGO = GameObject.Find("GameplayCanvas");
        if (canvasGO == null)
        {
            EditorUtility.DisplayDialog("HUDBuilder",
                "GameplayCanvas not found in the active scene.\n" +
                "Open the 02_Gameplay scene and try again.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build HUD");

        // ── 1. Create top-level panel GameObjects ─────────────────────
        var shipHealthPanel  = Panel(canvasGO.transform, "ShipHealthPanel",  PanelBg);
        var coreXPanel       = Panel(canvasGO.transform, "CoreXPanel",       PanelBg);
        var failureListPanel = Panel(canvasGO.transform, "FailureListPanel", PanelBg);
        // WinLosePanel is a full-screen invisible container;
        // WinLoseContent inside is the actual visible panel.
        var winLosePanel     = Container(canvasGO.transform, "WinLosePanel");
        var playerSlotsGO    = Container(canvasGO.transform, "PlayerSlots");

        // ── 2. Anchors + sizes ────────────────────────────────────────
        // Top-left  (anchor TL, pivot TL)
        SetRect(shipHealthPanel,
            anchorMin: new Vector2(0, 1), anchorMax: new Vector2(0, 1),
            pivot:     new Vector2(0, 1),
            size:      new Vector2(200, 80),
            pos:       new Vector2(12, -12));

        // Top-center (anchor TC, pivot TC)
        SetRect(coreXPanel,
            anchorMin: new Vector2(0.5f, 1), anchorMax: new Vector2(0.5f, 1),
            pivot:     new Vector2(0.5f, 1),
            size:      new Vector2(196, 78),
            pos:       new Vector2(0, -12));

        // Top-right  (anchor TR, pivot TR)
        SetRect(failureListPanel,
            anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1),
            pivot:     new Vector2(1, 1),
            size:      new Vector2(148, 90),
            pos:       new Vector2(-12, -12));

        // WinLosePanel: full-screen stretch, transparent — always stays active
        SetRect(winLosePanel,
            anchorMin: Vector2.zero, anchorMax: Vector2.one,
            pivot:     new Vector2(0.5f, 0.5f),
            size:      Vector2.zero,
            pos:       Vector2.zero);

        // PlayerSlots: bottom-center
        SetRect(playerSlotsGO,
            anchorMin: new Vector2(0.5f, 0), anchorMax: new Vector2(0.5f, 0),
            pivot:     new Vector2(0.5f, 0),
            size:      new Vector2(400, 70),
            pos:       new Vector2(0, 12));

        // ── 3. WinLose visible content child ──────────────────────────
        // WinLoseUI.panel points here — Show/Hide toggle this, not the parent
        var winLoseContent = Panel(winLosePanel.transform, "WinLoseContent", PanelBg);
        SetRect(winLoseContent,
            anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
            pivot:     new Vector2(0.5f, 0.5f),
            size:      new Vector2(196, 120),
            pos:       Vector2.zero);
        winLoseContent.SetActive(false); // hidden by default

        // ── 4. FailureList item container ─────────────────────────────
        var failureItemsParent = Container(failureListPanel.transform, "FailureList");
        SetRect(failureItemsParent,
            anchorMin: new Vector2(0, 0), anchorMax: new Vector2(1, 1),
            pivot:     new Vector2(0.5f, 0.5f),
            size:      new Vector2(-12, -28),
            pos:       new Vector2(0, -12));

        var vlg = EnsureComponent<VerticalLayoutGroup>(failureItemsParent);
        vlg.spacing             = 4f;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment      = TextAnchor.UpperLeft;
        var csf = EnsureComponent<ContentSizeFitter>(failureItemsParent);
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── 5. PlayerSlots layout + 4 slot children ───────────────────
        var hlg = EnsureComponent<HorizontalLayoutGroup>(playerSlotsGO);
        hlg.spacing             = 6f;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth  = false;
        hlg.childAlignment      = TextAnchor.MiddleCenter;

        var slotComponents = new PlayerSlot[4];
        for (int i = 0; i < 4; i++)
        {
            string slotName = $"PlayerSlot_P{i + 1}";
            var slotGO = Panel(playerSlotsGO.transform, slotName, PanelBg);
            SetRect(slotGO,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot:     new Vector2(0.5f, 0.5f),
                size:      new Vector2(66, 68),
                pos:       Vector2.zero);
            slotComponents[i] = EnsureComponent<PlayerSlot>(slotGO);
        }

        // ── 6. Attach HUD scripts ─────────────────────────────────────
        var shipHealthUI  = EnsureComponent<ShipHealthUI>(shipHealthPanel);
        var coreXUI       = EnsureComponent<CoreXUI>(coreXPanel);
        var failureListUI = EnsureComponent<FailureListUI>(failureListPanel);
        var winLoseUI     = EnsureComponent<WinLoseUI>(winLosePanel);
        var playerSlotsUI = EnsureComponent<PlayerSlotsUI>(playerSlotsGO);

        // ── 7. Wire WinLoseUI.panel → WinLoseContent ──────────────────
        {
            var so = new SerializedObject(winLoseUI);
            SetObjProp(so, "panel", winLoseContent);
            so.ApplyModifiedProperties();
        }

        // ── 8. Wire FailureListUI.listParent ──────────────────────────
        {
            var so = new SerializedObject(failureListUI);
            SetObjProp(so, "listParent", failureItemsParent.transform);
            so.ApplyModifiedProperties();
        }

        // ── 9. Wire PlayerSlotsUI.slots[0..3] ─────────────────────────
        {
            var so = new SerializedObject(playerSlotsUI);
            var slotsProp = so.FindProperty("slots");
            if (slotsProp != null)
            {
                slotsProp.arraySize = 4;
                for (int i = 0; i < 4; i++)
                    slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotComponents[i];
            }
            so.ApplyModifiedProperties();
        }

        // ── 10. Wire HUDManager ───────────────────────────────────────
        var hudManager = EnsureComponent<HUDManager>(canvasGO);
        {
            var so = new SerializedObject(hudManager);
            SetObjProp(so, "shipHealth",  shipHealthUI);
            SetObjProp(so, "coreX",       coreXUI);
            SetObjProp(so, "failureList", failureListUI);
            SetObjProp(so, "playerSlots", playerSlotsUI);
            SetObjProp(so, "winLose",     winLoseUI);
            so.ApplyModifiedProperties();
        }

        // ── 11. Mark scene dirty ──────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            "[HUDBuilder] ✓ HUD built and wired.\n" +
            "Remaining manual steps:\n" +
            "  1. Right-click Assets/ScriptableObjects → Create → WCFTThis → UIStyleConfig\n" +
            "     Assign VT323 and Caveat TMP FontAssets, then drag it into each HUD script's Style field.\n" +
            "  2. Build PlayerSlot.prefab: arrange children on PlayerSlot_P1, drag to Assets/Prefabs/UI.\n" +
            "  3. Build FailureItem.prefab: StatusDot + TextGroup, drag to Assets/Prefabs/UI.\n" +
            "     Then wire FailureListUI.failureItemPrefab.\n" +
            "  4. Wire [SerializeField] slots inside each panel (Sliders, TMP_Text, Images).\n" +
            "  5. Ctrl+S to save the scene."
        );

        EditorUtility.DisplayDialog("HUDBuilder",
            "✓ HUD hierarchy built!\n\n" +
            "Check the Console for the list of remaining manual wiring steps.", "OK");
    }

    // ── Factory helpers ───────────────────────────────────────────────

    /// Creates or returns a child GO with an Image component (panel style).
    static GameObject Panel(Transform parent, string name, Color bg)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color         = bg;
        img.raycastTarget = false;

        return go;
    }

    /// Creates or returns a child GO with a transparent Image (layout container).
    static GameObject Container(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color         = Transparent;
        img.raycastTarget = false;

        return go;
    }

    static void SetRect(GameObject go,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 size, Vector2 pos)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.sizeDelta        = size;
        rt.anchoredPosition = pos;
    }

    static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = Undo.AddComponent<T>(go);
        return c;
    }

    static void SetObjProp(SerializedObject so, string propName, Object value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null)
            prop.objectReferenceValue = value;
        else
            Debug.LogWarning($"[HUDBuilder] Property '{propName}' not found on " +
                             $"{so.targetObject.GetType().Name}");
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
