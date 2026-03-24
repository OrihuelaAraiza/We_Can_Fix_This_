using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Tools > WCFT > Build HUD Layout
/// Destroys all UI children on GameplayCanvas, then reconstructs the full
/// lo-fi terminal HUD with all design tokens, scripts, and serialized-field wiring.
/// Run from the 02_Gameplay scene. Ctrl+Z undoes everything.
/// </summary>
public static class HUDLayoutBuilder
{
    // ── Design tokens ────────────────────────────────────────────────────
    static readonly Color BG          = Hex("#161a1e");
    static readonly Color Border      = Hex("#2a3040");
    static readonly Color BarBG       = Hex("#0e1218");
    static readonly Color BarFill     = Hex("#2a7040");
    static readonly Color TextPrimary = Hex("#c8d8c0");
    static readonly Color TextDim     = Hex("#607060");
    static readonly Color TextLabel   = Hex("#485848");
    static readonly Color Screw       = Hex("#1e2830");

    // Player accent colors [0..3] = P1..P4
    static readonly Color[] PlayerBorder       = { Hex("#3a6080"), Hex("#803a50"), Hex("#708030"), Hex("#5a4080") };
    static readonly Color[] PlayerLabel        = { Hex("#60a0d0"), Hex("#d06080"), Hex("#a8c040"), Hex("#9060d0") };
    static readonly Color[] PlayerName         = { Hex("#4880a8"), Hex("#a84868"), Hex("#88a030"), Hex("#7848a8") };
    static readonly Color[] PlayerIconStroke   = { Hex("#284858"), Hex("#582838"), Hex("#485820"), Hex("#382858") };

    // Bar fill colors by semantic role
    static readonly Color BarIntegrity = Hex("#2a7040");
    static readonly Color BarPower     = Hex("#204878");
    static readonly Color BarHull      = Hex("#483018");
    static readonly Color BarAggress   = Hex("#7a2820");

    static readonly Color Transparent = new Color(0, 0, 0, 0);

    // ── Font handles (loaded once) ────────────────────────────────────────
    static TMP_FontAsset _vt323;
    static TMP_FontAsset _caveat;

    static TMP_FontAsset VT323  => _vt323  ??= AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/VT323 SDF.asset");
    static TMP_FontAsset Caveat => _caveat ??= AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Caveat-Bold SDF.asset");

    // ── Entry point ───────────────────────────────────────────────────────
    [MenuItem("Tools/WCFT/Build HUD Layout")]
    static void BuildHUDLayout()
    {
        var canvasGO = GameObject.Find("GameplayCanvas");
        if (canvasGO == null)
        {
            EditorUtility.DisplayDialog("HUDLayoutBuilder",
                "GameplayCanvas not found.\nOpen the 02_Gameplay scene and try again.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("HUDLayoutBuilder",
            "This will DESTROY all children of GameplayCanvas and rebuild the HUD from scratch.\n\nContinue?",
            "Yes, rebuild", "Cancel"))
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build HUD Layout");

        // Clear existing children
        var canvasTr = canvasGO.transform;
        for (int i = canvasTr.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(canvasTr.GetChild(i).gameObject);

        // ── Build panels ──────────────────────────────────────────────────
        BuildShipHealthPanel(canvasTr);
        BuildCoreXPanel(canvasTr);
        BuildFailureListPanel(canvasTr);
        BuildPlayerSlotsPanel(canvasTr);
        BuildWinLosePanel(canvasTr);

        // ── Wire HUDManager ───────────────────────────────────────────────
        WireHUDManager(canvasGO);

        // ── Save & finish ──────────────────────────────────────────────────
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[HUDLayoutBuilder] ✓ HUD layout built, wired, and scene saved.\n" +
                  "Remaining manual steps:\n" +
                  "  1. Create UIStyleConfig asset (Assets/ScriptableObjects → Create → WCFTThis → UIStyleConfig)\n" +
                  "     Assign VT323 SDF and Caveat-Bold SDF fonts, then drag into each HUD script's Style field.\n" +
                  "  2. Build FailureItem.prefab: StatusDot Image + FailureName TMP + FailureSub TMP\n" +
                  "     Attach FailureItemUI, wire fields, save to Assets/Prefabs/UI/.\n" +
                  "     Drag prefab into FailureListUI.failureItemPrefab.\n" +
                  "  3. Build PlayerSlot.prefab from PlayerSlot_P1: add child Images/TMP, attach PlayerSlot,\n" +
                  "     wire fields, save to Assets/Prefabs/UI/.");

        EditorUtility.DisplayDialog("HUDLayoutBuilder",
            "✓ HUD layout built and scene saved!\n\nCheck Console for remaining manual steps.", "OK");
    }

    // ═════════════════════════════════════════════════════════════════════
    // SHIP HEALTH PANEL  (top-left, 200×82)
    // ═════════════════════════════════════════════════════════════════════
    static void BuildShipHealthPanel(Transform canvas)
    {
        var panel = MakePanel(canvas, "ShipHealthPanel", BG, Border,
            anchorMin: new Vector2(0, 1), anchorMax: new Vector2(0, 1),
            pivot: new Vector2(0, 1),
            size: new Vector2(200, 82), pos: new Vector2(12, -12));

        AddScrews(panel.transform, 200, 82);

        // ── Title ─────────────────────────────────────────────────────────
        var titleLbl = MakeLabel(panel.transform, "TitleLabel", "SHIP STATUS",
            VT323, 11, TextLabel, TextAnchor.UpperLeft);
        SetRect(titleLbl, AnchorFull(), new Vector2(8, -5), new Vector2(-8, -18));

        // ── Integrity row ──────────────────────────────────────────────────
        var intRow = MakeContainer(panel.transform, "IntegrityRow");
        SetRect(intRow, AnchorFull(), new Vector2(8, -22), new Vector2(-8, -35));

        var intLbl = MakeLabel(intRow.transform, "Label", "INTG",
            VT323, 11, TextDim, TextAnchor.MiddleLeft);
        SetRect(intLbl, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(-8, -5));

        var intBar = MakeBar(intRow.transform, "IntegrityBar", BarBG, BarIntegrity);
        SetRect(intBar, new Vector2(0, 0), new Vector2(1, 1), new Vector2(30, 2), new Vector2(-30, -2));

        var intPct = MakeLabel(intRow.transform, "IntegrityPct", "100%",
            VT323, 10, TextPrimary, TextAnchor.MiddleRight);
        SetRect(intPct, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-38, 0), new Vector2(0, 0));

        // ── Power row ────────────────────────────────────────────────────
        var pwrRow = MakeContainer(panel.transform, "PowerRow");
        SetRect(pwrRow, AnchorFull(), new Vector2(8, -37), new Vector2(-8, -50));

        var pwrLbl = MakeLabel(pwrRow.transform, "Label", "PWR ",
            VT323, 11, TextDim, TextAnchor.MiddleLeft);
        SetRect(pwrLbl, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(-8, -5));

        var pwrBar = MakeBar(pwrRow.transform, "PowerBar", BarBG, BarPower);
        SetRect(pwrBar, new Vector2(0, 0), new Vector2(1, 1), new Vector2(30, 2), new Vector2(-30, -2));

        // ── Hull row ──────────────────────────────────────────────────────
        var hullRow = MakeContainer(panel.transform, "HullRow");
        SetRect(hullRow, AnchorFull(), new Vector2(8, -52), new Vector2(-8, -65));

        var hullLbl = MakeLabel(hullRow.transform, "Label", "HULL",
            VT323, 11, TextDim, TextAnchor.MiddleLeft);
        SetRect(hullLbl, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(-8, -5));

        var hullBar = MakeBar(hullRow.transform, "HullBar", BarBG, BarHull);
        SetRect(hullBar, new Vector2(0, 0), new Vector2(1, 1), new Vector2(30, 2), new Vector2(-30, -2));

        // ── Status text ───────────────────────────────────────────────────
        var statusLbl = MakeLabel(panel.transform, "StatusText", "",
            VT323, 10, Hex("#a03030"), TextAnchor.LowerLeft);
        SetRect(statusLbl, AnchorFull(), new Vector2(8, 4), new Vector2(-8, 14));

        // ── Attach script ─────────────────────────────────────────────────
        var shipHealthUI = EnsureComp<ShipHealthUI>(panel);
        {
            var so = new SerializedObject(shipHealthUI);
            SetSlider(so, "integrityBar", intBar);
            SetObjProp(so, "integrityPct", intPct);
            SetSlider(so, "powerBar", pwrBar);
            SetSlider(so, "hullBar", hullBar);
            SetObjProp(so, "statusText", statusLbl);
            so.ApplyModifiedProperties();
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // CORE-X PANEL  (top-center, 196×78)
    // ═════════════════════════════════════════════════════════════════════
    static void BuildCoreXPanel(Transform canvas)
    {
        var panel = MakePanel(canvas, "CoreXPanel", BG, Border,
            anchorMin: new Vector2(0.5f, 1), anchorMax: new Vector2(0.5f, 1),
            pivot: new Vector2(0.5f, 1),
            size: new Vector2(196, 78), pos: new Vector2(0, -12));

        AddScrews(panel.transform, 196, 78);

        // Title
        var titleLbl = MakeLabel(panel.transform, "TitleLabel", "CORE-X",
            VT323, 13, TextPrimary, TextAnchor.UpperCenter);
        SetRect(titleLbl, AnchorFull(), new Vector2(8, -5), new Vector2(-8, -20));

        // Aggression bar + label
        var aggRow = MakeContainer(panel.transform, "AggressionRow");
        SetRect(aggRow, AnchorFull(), new Vector2(8, -24), new Vector2(-8, -37));

        var aggLbl = MakeLabel(aggRow.transform, "Label", "AGG ",
            VT323, 11, TextDim, TextAnchor.MiddleLeft);
        SetRect(aggLbl, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(-8, -5));

        var aggBar = MakeBar(aggRow.transform, "AggressionBar", BarBG, BarAggress);
        SetRect(aggBar, new Vector2(0, 0), new Vector2(1, 1), new Vector2(30, 2), new Vector2(-4, -2));

        // Phase label
        var phaseLbl = MakeLabel(panel.transform, "PhaseLabel", "PHASE I",
            VT323, 11, TextDim, TextAnchor.MiddleCenter);
        SetRect(phaseLbl, AnchorFull(), new Vector2(8, -40), new Vector2(-8, -52));

        // Reactive / Active labels row
        var statusRow = MakeContainer(panel.transform, "StatusRow");
        SetRect(statusRow, AnchorFull(), new Vector2(8, -54), new Vector2(-8, -66));

        var reactiveLbl = MakeLabel(statusRow.transform, "ReactiveLabel", "● REACTIVE",
            VT323, 10, TextDim, TextAnchor.MiddleLeft);
        SetRect(reactiveLbl, new Vector2(0, 0), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);

        var activeLbl = MakeLabel(statusRow.transform, "ActiveLabel", "● ACTIVE",
            VT323, 10, Hex("#a03030"), TextAnchor.MiddleRight);
        SetRect(activeLbl, new Vector2(0.5f, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);

        // Attach script
        var coreXUI = EnsureComp<CoreXUI>(panel);
        {
            var so = new SerializedObject(coreXUI);
            SetSlider(so, "aggressionBar", aggBar);
            SetObjProp(so, "phaseLabel", phaseLbl);
            SetObjProp(so, "reactiveLabel", reactiveLbl);
            SetObjProp(so, "activeLabel", activeLbl);
            so.ApplyModifiedProperties();
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // FAILURE LIST PANEL  (top-right, 150×100)
    // ═════════════════════════════════════════════════════════════════════
    static void BuildFailureListPanel(Transform canvas)
    {
        var panel = MakePanel(canvas, "FailureListPanel", BG, Border,
            anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1),
            pivot: new Vector2(1, 1),
            size: new Vector2(150, 100), pos: new Vector2(-12, -12));

        AddScrews(panel.transform, 150, 100);

        // Title
        var titleLbl = MakeLabel(panel.transform, "TitleLabel", "FAILURES",
            VT323, 11, TextLabel, TextAnchor.UpperLeft);
        SetRect(titleLbl, AnchorFull(), new Vector2(8, -5), new Vector2(-8, -18));

        // Scroll / VLG container
        var listContainer = MakeContainer(panel.transform, "FailureList");
        SetRect(listContainer, AnchorFull(), new Vector2(6, -20), new Vector2(-6, -6));

        var vlg = EnsureComp<VerticalLayoutGroup>(listContainer);
        vlg.spacing              = 3f;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment       = TextAnchor.UpperLeft;
        var csf = EnsureComp<ContentSizeFitter>(listContainer);
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Attach script
        var failureListUI = EnsureComp<FailureListUI>(panel);
        {
            var so = new SerializedObject(failureListUI);
            SetObjProp(so, "listParent", listContainer.transform);
            // failureItemPrefab must be wired manually after prefab creation
            so.ApplyModifiedProperties();
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // PLAYER SLOTS  (bottom-center, 272×70)
    // ═════════════════════════════════════════════════════════════════════
    static void BuildPlayerSlotsPanel(Transform canvas)
    {
        var container = MakeContainer(canvas, "PlayerSlots",
            anchorMin: new Vector2(0.5f, 0), anchorMax: new Vector2(0.5f, 0),
            pivot: new Vector2(0.5f, 0),
            size: new Vector2(272, 70), pos: new Vector2(0, 12));

        var hlg = EnsureComp<HorizontalLayoutGroup>(container);
        hlg.spacing              = 6f;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth  = false;
        hlg.childAlignment       = TextAnchor.MiddleCenter;
        hlg.padding              = new RectOffset(4, 4, 0, 0);

        var slotComponents = new PlayerSlot[4];
        for (int i = 0; i < 4; i++)
        {
            string slotName = $"PlayerSlot_P{i + 1}";
            var slot = MakePanel(container.transform, slotName, BG, PlayerBorder[i],
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f),
                size: new Vector2(62, 68), pos: Vector2.zero);

            var le = EnsureComp<LayoutElement>(slot);
            le.preferredWidth  = 62;
            le.preferredHeight = 68;

            // P# label top-left
            var pLbl = MakeLabel(slot.transform, "PlayerLabel", $"P{i + 1}",
                VT323, 10, PlayerLabel[i], TextAnchor.UpperLeft);
            SetRect(pLbl, AnchorFull(), new Vector2(4, -3), new Vector2(-4, -14));

            // Icon BG (center square)
            var iconBG = MakeImage(slot.transform, "RoleIconBG", PlayerIconStroke[i]);
            SetRect(iconBG,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f),
                size: new Vector2(28, 28), pos: new Vector2(0, 4));

            // Role icon (inside BG)
            var roleIcon = MakeImage(slot.transform, "RoleIcon", Color.white);
            SetRect(roleIcon,
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f),
                size: new Vector2(22, 22), pos: new Vector2(0, 4));
            roleIcon.GetComponent<Image>().enabled = false;

            // Role name label (bottom)
            var roleLbl = MakeLabel(slot.transform, "RoleNameText", "EMPTY",
                VT323, 9, PlayerName[i], TextAnchor.LowerCenter);
            SetRect(roleLbl, AnchorFull(), new Vector2(2, 3), new Vector2(-2, 14));

            // Panel border highlight (thin outline at bottom)
            var borderImg = MakeImage(slot.transform, "PanelBorder", PlayerBorder[i]);
            SetRect(borderImg,
                anchorMin: new Vector2(0, 0), anchorMax: new Vector2(1, 0),
                pivot: new Vector2(0.5f, 0),
                size: new Vector2(0, 2), pos: Vector2.zero);

            slotComponents[i] = EnsureComp<PlayerSlot>(slot);
            {
                var so = new SerializedObject(slotComponents[i]);
                SetObjProp(so, "playerLabel",   pLbl);
                SetObjProp(so, "roleIconBG",    iconBG.GetComponent<Image>());
                SetObjProp(so, "roleIcon",      roleIcon.GetComponent<Image>());
                SetObjProp(so, "roleNameText",  roleLbl);
                SetObjProp(so, "panelBorder",   borderImg.GetComponent<Image>());
                so.ApplyModifiedProperties();
            }
        }

        // Attach PlayerSlotsUI
        var playerSlotsUI = EnsureComp<PlayerSlotsUI>(container);
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
    }

    // ═════════════════════════════════════════════════════════════════════
    // WIN/LOSE PANEL  (center, 220×140, starts inactive)
    // ═════════════════════════════════════════════════════════════════════
    static void BuildWinLosePanel(Transform canvas)
    {
        // Full-screen transparent parent (always active — needed for OnEnable events)
        var winLosePanel = MakeContainer(canvas, "WinLosePanel",
            anchorMin: Vector2.zero, anchorMax: Vector2.one,
            pivot: new Vector2(0.5f, 0.5f),
            size: Vector2.zero, pos: Vector2.zero);

        // Visible content child
        var content = MakePanel(winLosePanel.transform, "WinLoseContent", BG, Border,
            anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
            pivot: new Vector2(0.5f, 0.5f),
            size: new Vector2(220, 140), pos: Vector2.zero);

        AddScrews(content.transform, 220, 140);

        // Mission label (top)
        var missionLbl = MakeLabel(content.transform, "MissionLabel", "MISSION COMPLETE",
            VT323, 12, TextLabel, TextAnchor.UpperCenter);
        SetRect(missionLbl, AnchorFull(), new Vector2(8, -8), new Vector2(-8, -22));

        // Result title (large)
        var resultTitle = MakeLabel(content.transform, "ResultTitle", "VICTORY!",
            Caveat, 22, Hex("#2a8040"), TextAnchor.MiddleCenter);
        SetRect(resultTitle, AnchorFull(), new Vector2(8, -24), new Vector2(-8, -62));

        // Subtitle
        var subtitleLbl = MakeLabel(content.transform, "SubtitleText", "Core-X neutralized",
            VT323, 11, TextDim, TextAnchor.MiddleCenter);
        SetRect(subtitleLbl, AnchorFull(), new Vector2(8, -64), new Vector2(-8, -82));

        // Divider image
        var divider = MakeImage(content.transform, "Divider", Border);
        SetRect(divider,
            anchorMin: new Vector2(0.1f, 0), anchorMax: new Vector2(0.9f, 0),
            pivot: new Vector2(0.5f, 0),
            size: new Vector2(0, 1), pos: new Vector2(0, 55));

        // Button row
        var btnRow = MakeContainer(content.transform, "ButtonRow");
        SetRect(btnRow, AnchorFull(), new Vector2(12, 10), new Vector2(-12, 44));

        var hlg = EnsureComp<HorizontalLayoutGroup>(btnRow);
        hlg.spacing              = 8f;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;

        var restartBtn = MakeButton(btnRow.transform, "RestartButton", "RESTART",
            Hex("#1e5030"), TextPrimary, VT323, 12);
        var lobbyBtn = MakeButton(btnRow.transform, "LobbyButton", "LOBBY",
            Hex("#1e3020"), TextDim, VT323, 12);

        // Panel border image (recolored on victory/defeat)
        var panelBorder = MakeImage(content.transform, "PanelBorder", Border);
        SetRect(panelBorder,
            anchorMin: Vector2.zero, anchorMax: Vector2.one,
            pivot: new Vector2(0.5f, 0.5f),
            size: Vector2.zero, pos: Vector2.zero);
        var img = panelBorder.GetComponent<Image>();
        img.type = Image.Type.Sliced;

        // Deactivate content by default
        content.SetActive(false);

        // Attach WinLoseUI to the parent (not content)
        var winLoseUI = EnsureComp<WinLoseUI>(winLosePanel);
        {
            var so = new SerializedObject(winLoseUI);
            SetObjProp(so, "panel",           content);
            SetObjProp(so, "missionLabel",    missionLbl);
            SetObjProp(so, "resultTitle",     resultTitle);
            SetObjProp(so, "subtitleText",    subtitleLbl);
            SetObjProp(so, "restartButton",   restartBtn.GetComponent<Button>());
            SetObjProp(so, "lobbyButton",     lobbyBtn.GetComponent<Button>());
            SetObjProp(so, "panelBorderImage", panelBorder.GetComponent<Image>());
            so.ApplyModifiedProperties();
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // Wire HUDManager on GameplayCanvas
    // ═════════════════════════════════════════════════════════════════════
    static void WireHUDManager(GameObject canvasGO)
    {
        var hudManager = EnsureComp<HUDManager>(canvasGO);
        var so = new SerializedObject(hudManager);

        SetObjProp(so, "shipHealth",  canvasGO.transform.Find("ShipHealthPanel")?.GetComponent<ShipHealthUI>());
        SetObjProp(so, "coreX",       canvasGO.transform.Find("CoreXPanel")?.GetComponent<CoreXUI>());
        SetObjProp(so, "failureList", canvasGO.transform.Find("FailureListPanel")?.GetComponent<FailureListUI>());
        SetObjProp(so, "playerSlots", canvasGO.transform.Find("PlayerSlots")?.GetComponent<PlayerSlotsUI>());
        SetObjProp(so, "winLose",     canvasGO.transform.Find("WinLosePanel")?.GetComponent<WinLoseUI>());

        so.ApplyModifiedProperties();
    }

    // ═════════════════════════════════════════════════════════════════════
    // Factory helpers
    // ═════════════════════════════════════════════════════════════════════

    /// Panel with Image + Outline effect
    static GameObject MakePanel(Transform parent, string name, Color bg, Color outlineColor,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 pos)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color         = bg;
        img.raycastTarget = false;

        var outline = go.AddComponent<Outline>();
        outline.effectColor    = outlineColor;
        outline.effectDistance = new Vector2(1, -1);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.sizeDelta        = size;
        rt.anchoredPosition = pos;

        return go;
    }

    /// Transparent container (layout only)
    static GameObject MakeContainer(Transform parent, string name,
        Vector2 anchorMin = default, Vector2 anchorMax = default,
        Vector2 pivot = default, Vector2 size = default, Vector2 pos = default)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color         = Transparent;
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.sizeDelta        = size;
        rt.anchoredPosition = pos;

        return go;
    }

    // Overload for child containers using offset-based SetRect after creation
    static GameObject MakeContainer(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color         = Transparent;
        img.raycastTarget = false;

        go.AddComponent<RectTransform>();
        return go;
    }

    /// Non-interactable Slider (no handle sprite, fill only)
    static Slider MakeBar(Transform parent, string name, Color bgColor, Color fillColor)
    {
        // Background
        var barBG = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(barBG, "Create " + name);
        barBG.transform.SetParent(parent, false);

        var bgImg = barBG.AddComponent<Image>();
        bgImg.color         = bgColor;
        bgImg.raycastTarget = false;

        // Fill area
        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(barBG.transform, false);
        var fillAreaRT = fillArea.AddComponent<RectTransform>();
        fillAreaRT.anchorMin  = Vector2.zero;
        fillAreaRT.anchorMax  = Vector2.one;
        fillAreaRT.sizeDelta  = new Vector2(-2, -2);
        fillAreaRT.anchoredPosition = Vector2.zero;

        // Fill
        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color         = fillColor;
        fillImg.raycastTarget = false;
        var fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin  = Vector2.zero;
        fillRT.anchorMax  = new Vector2(1, 1);
        fillRT.sizeDelta  = Vector2.zero;
        fillRT.anchoredPosition = Vector2.zero;

        // Slider component
        var slider = barBG.AddComponent<Slider>();
        slider.fillRect      = fillRT;
        slider.interactable  = false;
        slider.transition    = Selectable.Transition.None;
        slider.value         = 1f;

        return slider;
    }

    /// TMP_Text label
    static TMP_Text MakeLabel(Transform parent, string name, string text,
        TMP_FontAsset font, int size, Color color, TextAnchor anchor)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.GetComponent<TMP_Text>();

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;

        // Map TextAnchor → TMP alignment
        tmp.alignment = anchor switch
        {
            TextAnchor.UpperLeft    => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter  => TextAlignmentOptions.Top,
            TextAnchor.UpperRight   => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft   => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight  => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft    => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter  => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight   => TextAlignmentOptions.BottomRight,
            _                       => TextAlignmentOptions.Left,
        };

        return tmp;
    }

    /// Plain Image
    static GameObject MakeImage(Transform parent, string name, Color color)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;

        return go;
    }

    /// Button with background image + TMP label child
    static GameObject MakeButton(Transform parent, string name, string label,
        Color bgColor, Color textColor, TMP_FontAsset font, int fontSize)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        // Label child
        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = fontSize;
        tmp.color     = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        if (font != null) tmp.font = font;

        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin  = Vector2.zero;
        txtRT.anchorMax  = Vector2.one;
        txtRT.sizeDelta  = Vector2.zero;
        txtRT.anchoredPosition = Vector2.zero;

        return go;
    }

    /// 4 corner screw decorations
    static void AddScrews(Transform parent, float w, float h)
    {
        float inset = 5f;
        Vector2[] corners = {
            new Vector2( inset,    -inset),
            new Vector2( w - inset, -inset),
            new Vector2( inset,    -(h - inset)),
            new Vector2( w - inset, -(h - inset)),
        };

        Vector2[] anchors = {
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, 0), new Vector2(1, 0),
        };

        Vector2[] pivots = {
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
        };

        for (int i = 0; i < 4; i++)
        {
            var screw = MakeImage(parent, $"Screw_{i}", Screw);
            var rt = screw.GetComponent<RectTransform>();
            rt.anchorMin  = anchors[i];
            rt.anchorMax  = anchors[i];
            rt.pivot      = pivots[i];
            rt.sizeDelta  = new Vector2(5, 5);

            // Position relative to corner
            float xSign = (i % 2 == 0) ? 1f : -1f;
            float ySign = (i < 2) ? -1f : 1f;
            rt.anchoredPosition = new Vector2(xSign * inset, ySign * inset);
        }
    }

    // ── RectTransform helpers ─────────────────────────────────────────────

    static Vector4 AnchorFull() => Vector4.zero; // sentinel — use SetRect(AnchorFull overload)

    // Offset-based (children inside a panel using offsets from edges)
    static void SetRect(TMP_Text go, Vector4 _, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.offsetMin  = offsetMin;
        rt.offsetMax  = offsetMax;
    }

    static void SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 size, Vector2 pos)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.sizeDelta        = size;
        rt.anchoredPosition = pos;
    }

    // Stretch rect using offsetMin/Max relative to parent
    static void SetRect(GameObject go, Vector4 _, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.offsetMin  = offsetMin;
        rt.offsetMax  = offsetMax;
    }

    static void SetRect(TMP_Text go, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin  = anchorMin;
        rt.anchorMax  = anchorMax;
        rt.offsetMin  = offsetMin;
        rt.offsetMax  = offsetMax;
    }

    static void SetRect(Slider s, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = s.GetComponent<RectTransform>();
        rt.anchorMin  = anchorMin;
        rt.anchorMax  = anchorMax;
        rt.offsetMin  = offsetMin;
        rt.offsetMax  = offsetMax;
    }

    // ── SerializedObject helpers ──────────────────────────────────────────

    static T EnsureComp<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = Undo.AddComponent<T>(go);
        return c;
    }

    static void SetObjProp(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p != null)
            p.objectReferenceValue = value;
        else
            Debug.LogWarning($"[HUDLayoutBuilder] '{prop}' not found on {so.targetObject.GetType().Name}");
    }

    static void SetSlider(SerializedObject so, string prop, Slider value)
    {
        var p = so.FindProperty(prop);
        if (p != null)
            p.objectReferenceValue = value;
        else
            Debug.LogWarning($"[HUDLayoutBuilder] '{prop}' not found on {so.targetObject.GetType().Name}");
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
