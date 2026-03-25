using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Tools > WCFT > Build HUD Layout
/// Destroys all UI children on GameplayCanvas and reconstructs the full
/// lo-fi terminal HUD. Run from the 02_Gameplay scene. Ctrl+Z undoes.
/// </summary>
public static class HUDLayoutBuilder
{
    // ── Design tokens ─────────────────────────────────────────────────────
    static readonly Color BG          = Hex("#161a1e");
    static readonly Color Border      = Hex("#2a3040");
    static readonly Color BarBG       = Hex("#0e1218");
    static readonly Color TextPrimary = Hex("#c8d8c0");
    static readonly Color TextDim     = Hex("#607060");
    static readonly Color TextLabel   = Hex("#485848");
    static readonly Color Screw       = Hex("#1e2830");
    static readonly Color Transparent = new Color(0, 0, 0, 0);

    // Player accent colors [0..3] = P1..P4
    static readonly Color[] PlayerBorder     = { Hex("#182618"), Hex("#182030"), Hex("#282008"), Hex("#2a2a2a") };
    static readonly Color[] PlayerLabel      = { Hex("#284820"), Hex("#20304a"), Hex("#483808"), Hex("#333333") };
    static readonly Color[] PlayerName       = { Hex("#8aaa88"), Hex("#88a0c8"), Hex("#c8b870"), Hex("#333333") };
    static readonly Color[] PlayerIconBG     = { Hex("#101410"), Hex("#101218"), Hex("#141008"), new Color(0,0,0,0) };
    static readonly Color[] PlayerIconStroke = { Hex("#203018"), Hex("#182840"), Hex("#403010"), Hex("#2a2a2a") };

    // Bar fill colors
    static readonly Color BarIntegrity = Hex("#2a7040");
    static readonly Color BarPower     = Hex("#204878");
    static readonly Color BarHull      = Hex("#483018");
    static readonly Color BarAggress   = Hex("#7a2820");

    // ── Font handles (reset each run) ─────────────────────────────────────
    static TMP_FontAsset _vt323;
    static TMP_FontAsset _caveat;

    static TMP_FontAsset VT323  => _vt323  ??=
        AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/VT323 SDF.asset") ??
        AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

    static TMP_FontAsset Caveat => _caveat ??=
        AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Caveat-Bold SDF.asset");

    // ── Entry point ───────────────────────────────────────────────────────
    [MenuItem("Tools/WCFT/Build HUD Layout")]
    static void BuildHUDLayout()
    {
        _vt323  = null;   // force fresh load each run
        _caveat = null;

        var canvasGO = GameObject.Find("GameplayCanvas");
        if (canvasGO == null)
        {
            EditorUtility.DisplayDialog("HUDLayoutBuilder",
                "GameplayCanvas not found.\nOpen 02_Gameplay and try again.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("HUDLayoutBuilder",
            "Destroy all children of GameplayCanvas and rebuild HUD?\n\nCtrl+Z undoes.",
            "Yes, rebuild", "Cancel"))
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build HUD Layout");

        var canvasTr = canvasGO.transform;
        for (int i = canvasTr.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(canvasTr.GetChild(i).gameObject);

        BuildShipHealthPanel(canvasTr);
        BuildTimerPanel(canvasTr);
        BuildCoreXPanel(canvasTr);
        BuildStationStatusPanel(canvasTr);
        BuildPlayerSlotsPanel(canvasTr);
        BuildWinLosePanel(canvasTr);

        WireHUDManager(canvasGO);
        WireAlertSystem(canvasGO);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("[HUDLayoutBuilder] ✓ HUD built and scene saved.\n" +
                  "Manual steps remaining:\n" +
                  "  · Create UIStyleConfig asset → assign VT323 + Caveat fonts\n" +
                  "  · Build FailureItem.prefab and wire into FailureListUI.failureItemPrefab");

        EditorUtility.DisplayDialog("HUDLayoutBuilder", "✓ HUD built and scene saved!", "OK");
    }

    // ═════════════════════════════════════════════════════════════════════
    // SHIP HEALTH PANEL  top-left · 200 × 82
    // All children: anchorMin=(0,1) anchorMax=(0,1) pivot=(0,1)
    // ═════════════════════════════════════════════════════════════════════
    static void BuildShipHealthPanel(Transform canvas)
    {
        var panel = MakePanel(canvas, "ShipHealthPanel", BG, Border,
            anchorMin: new Vector2(0, 1), anchorMax: new Vector2(0, 1),
            pivot:     new Vector2(0, 1),
            size: new Vector2(200, 82), pos: new Vector2(12, -12));

        AddScrews(panel.transform, 200, 82);

        // INTG label
        var intLbl = Label(panel.transform, "LabelIntegrity", "INTG",
            11, TextDim, TextAnchor.MiddleLeft);
        Pin(intLbl.transform, 8, -8, 120, 16);

        // Integrity bar — full-width
        var intBar = MakeBar(panel.transform, "IntegrityBar", BarBG, BarIntegrity);
        Pin(intBar.transform, 8, -26, 150, 11);

        // Pct label
        var intPct = Label(panel.transform, "IntegrityPct", "100%",
            10, TextPrimary, TextAnchor.MiddleRight);
        Pin(intPct.transform, 162, -24, 34, 18);

        // PWR label + bar (left half)
        var pwrLbl = Label(panel.transform, "LabelPower", "PWR",
            11, TextDim, TextAnchor.MiddleLeft);
        Pin(pwrLbl.transform, 8, -42, 85, 13);

        var pwrBar = MakeBar(panel.transform, "PowerBar", BarBG, BarPower);
        Pin(pwrBar.transform, 8, -56, 85, 7);

        // HULL label + bar (right half)
        var hullLbl = Label(panel.transform, "LabelHull", "HULL",
            11, TextDim, TextAnchor.MiddleLeft);
        Pin(hullLbl.transform, 105, -42, 85, 13);

        var hullBar = MakeBar(panel.transform, "HullBar", BarBG, BarHull);
        Pin(hullBar.transform, 105, -56, 85, 7);

        // Status text (alert messages)
        var statusLbl = Label(panel.transform, "StatusText", "",
            10, Hex("#a03030"), TextAnchor.MiddleLeft);
        Pin(statusLbl.transform, 8, -68, 184, 14);

        var ui = EnsureComp<ShipHealthUI>(panel);
        var so = new SerializedObject(ui);
        SetSlider(so, "integrityBar", intBar);
        SetProp(so,   "integrityPct", intPct);
        SetSlider(so, "powerBar",     pwrBar);
        SetSlider(so, "hullBar",      hullBar);
        SetProp(so,   "statusText",   statusLbl);
        so.ApplyModifiedProperties();
    }

    // ═════════════════════════════════════════════════════════════════════
    // CORE-X PANEL  top-center · 196 × 78
    // ═════════════════════════════════════════════════════════════════════
    static void BuildCoreXPanel(Transform canvas)
    {
        var panel = MakePanel(canvas, "CoreXPanel", BG, Border,
            anchorMin: new Vector2(0.5f, 1), anchorMax: new Vector2(0.5f, 1),
            pivot:     new Vector2(0.5f, 1),
            size: new Vector2(196, 78), pos: new Vector2(0, -12));

        AddScrews(panel.transform, 196, 78);

        // Phase label (top)
        var phaseLbl = Label(panel.transform, "PhaseLabel", "PHASE I",
            12, TextPrimary, TextAnchor.MiddleLeft);
        Pin(phaseLbl.transform, 10, -8, 176, 16);

        // Aggression title (decorative, not wired)
        var aggTitle = Label(panel.transform, "AggressionTitle", "AGGRESSION",
            11, TextDim, TextAnchor.MiddleLeft);
        Pin(aggTitle.transform, 10, -26, 176, 22);

        // Aggression bar
        var aggBar = MakeBar(panel.transform, "AggressionBar", BarBG, BarAggress);
        Pin(aggBar.transform, 10, -50, 176, 11);

        // Status labels
        var reactiveLbl = Label(panel.transform, "ReactiveLabel", "● REACTIVE",
            10, TextDim, TextAnchor.MiddleLeft);
        Pin(reactiveLbl.transform, 10, -64, 96, 14);

        var activeLbl = Label(panel.transform, "ActiveLabel", "● ACTIVE",
            10, Hex("#a03030"), TextAnchor.MiddleLeft);
        Pin(activeLbl.transform, 116, -64, 70, 14);

        var ui = EnsureComp<CoreXUI>(panel);
        var so = new SerializedObject(ui);
        SetSlider(so, "aggressionBar",  aggBar);
        SetProp(so,   "phaseLabel",     phaseLbl);
        SetProp(so,   "reactiveLabel",  reactiveLbl);
        SetProp(so,   "activeLabel",    activeLbl);
        so.ApplyModifiedProperties();
    }

    // ═════════════════════════════════════════════════════════════════════
    // TIMER PANEL  top-left below ShipHealthPanel · 120 × 44
    // ═════════════════════════════════════════════════════════════════════
    static void BuildTimerPanel(Transform canvas)
    {
        var panel = MakePanel(canvas, "TimerPanel", BG, Border,
            anchorMin: new Vector2(0, 1), anchorMax: new Vector2(0, 1),
            pivot:     new Vector2(0, 1),
            size: new Vector2(120, 44), pos: new Vector2(12, -102));

        // "TIME LEFT" label
        var lbl = LabelRaw(panel.transform, "TimerLabel", "TIME LEFT",
            12, Hex("#3a4048"), TextAlignmentOptions.Left);
        Pin(lbl.transform, 8, -7, 104, 14);

        // Countdown value
        var val = LabelRaw(panel.transform, "TimerValue", "10:00",
            24, Hex("#c8a020"), TextAlignmentOptions.Left);
        Pin(val.transform, 8, -22, 104, 20);

        var ui = EnsureComp<SurvivalTimerUI>(panel);
        var so = new SerializedObject(ui);
        SetProp(so, "timerValue", val);
        so.FindProperty("survivalDuration").floatValue = 600f;
        so.ApplyModifiedProperties();
    }

    // ═════════════════════════════════════════════════════════════════════
    // STATION STATUS PANEL  top-right · 156 × 112
    // 4 rows: POWER · COMMS · GRAVITY · HULL
    // ═════════════════════════════════════════════════════════════════════
    static void BuildStationStatusPanel(Transform canvas)
    {
        var panel = MakePanel(canvas, "StationStatusPanel", BG, Border,
            anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1),
            pivot:     new Vector2(1, 1),
            size: new Vector2(156, 112), pos: new Vector2(-12, -12));

        AddScrews(panel.transform, 156, 112);

        // Header
        var header = LabelRaw(panel.transform, "HeaderLabel", "SHIP SYSTEMS",
            12, Hex("#3a4048"), TextAlignmentOptions.Left);
        Pin(header.transform, 8, -8, 140, 16);

        // Station data
        string[] stationIds = { "POWER", "COMMS", "GRAVITY", "HULL" };

        // Collect refs for wiring StationStatusUI
        var dotImages    = new Image[4];
        var nameTexts    = new TMP_Text[4];
        var statusLabels = new TMP_Text[4];

        for (int i = 0; i < 4; i++)
        {
            // Row parent — pin to top-left of panel
            var row = new GameObject($"StationRow_{i}");
            Undo.RegisterCreatedObjectUndo(row, "Create StationRow");
            row.transform.SetParent(panel.transform, false);
            var rowRT = row.AddComponent<RectTransform>();
            rowRT.anchorMin        = new Vector2(0, 1);
            rowRT.anchorMax        = new Vector2(0, 1);
            rowRT.pivot            = new Vector2(0, 1);
            rowRT.anchoredPosition = new Vector2(8, -28 - i * 20f);
            rowRT.sizeDelta        = new Vector2(140, 17);

            // A) Status dot — hard 7×7
            var dot = MakeImage(row.transform, "StatusDot", Hex("#287040"));
            var dotRT = dot.GetComponent<RectTransform>();
            dotRT.anchorMin        = new Vector2(0, 0.5f);
            dotRT.anchorMax        = new Vector2(0, 0.5f);
            dotRT.pivot            = new Vector2(0, 0.5f);
            dotRT.anchoredPosition = new Vector2(0, 0);
            dotRT.sizeDelta        = new Vector2(7, 7);
            dotImages[i] = dot.GetComponent<Image>();

            // B) Station name
            var nameTMP = LabelRaw(row.transform, "StationName", stationIds[i],
                14, Hex("#88a888"), TextAlignmentOptions.Left);
            var nameRT = nameTMP.GetComponent<RectTransform>();
            nameRT.anchorMin        = new Vector2(0, 0.5f);
            nameRT.anchorMax        = new Vector2(0, 0.5f);
            nameRT.pivot            = new Vector2(0, 0.5f);
            nameRT.anchoredPosition = new Vector2(12, 2);
            nameRT.sizeDelta        = new Vector2(80, 14);
            nameTMP.overflowMode       = TextOverflowModes.Overflow;
            nameTMP.enableWordWrapping = false;
            nameTexts[i] = nameTMP;

            // C) Status label (right-aligned, anchored to right edge of row)
            var statusTMP = LabelRaw(row.transform, "StatusLabel", "OK",
                13, Hex("#405040"), TextAlignmentOptions.Right);
            var statusRT = statusTMP.GetComponent<RectTransform>();
            statusRT.anchorMin        = new Vector2(1, 0.5f);
            statusRT.anchorMax        = new Vector2(1, 0.5f);
            statusRT.pivot            = new Vector2(1, 0.5f);
            statusRT.anchoredPosition = new Vector2(0, 2);
            statusRT.sizeDelta        = new Vector2(50, 14);
            statusTMP.overflowMode       = TextOverflowModes.Overflow;
            statusTMP.enableWordWrapping = false;
            statusLabels[i] = statusTMP;
        }

        // Wire StationStatusUI via SerializedObject
        var ui = EnsureComp<StationStatusUI>(panel);
        var so = new SerializedObject(ui);
        var rowsProp = so.FindProperty("rows");
        if (rowsProp != null)
        {
            rowsProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var elem = rowsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("stationId").stringValue  = stationIds[i];
                elem.FindPropertyRelative("statusDot").objectReferenceValue   = dotImages[i];
                elem.FindPropertyRelative("nameText").objectReferenceValue    = nameTexts[i];
                elem.FindPropertyRelative("statusLabel").objectReferenceValue = statusLabels[i];
            }
        }
        so.ApplyModifiedProperties();
    }

    // ═════════════════════════════════════════════════════════════════════
    // PLAYER SLOTS  bottom-center · 280 × 72
    // ═════════════════════════════════════════════════════════════════════
    static void BuildPlayerSlotsPanel(Transform canvas)
    {
        var container = MakeContainer(canvas, "PlayerSlots",
            anchorMin: new Vector2(0.5f, 0), anchorMax: new Vector2(0.5f, 0),
            pivot:     new Vector2(0.5f, 0),
            size: new Vector2(280, 72), pos: new Vector2(0, 12));

        var hlg = EnsureComp<HorizontalLayoutGroup>(container);
        hlg.spacing                = 6f;
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.padding                = new RectOffset(4, 4, 2, 2);

        string[] roleNames = { "MECHANIC", "HACKER", "GUNNER", "EMPTY" };
        var slotComponents = new PlayerSlot[4];

        for (int i = 0; i < 4; i++)
        {
            var slot = MakePanel(container.transform, $"PlayerSlot_P{i + 1}", BG, PlayerBorder[i],
                anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                pivot:     new Vector2(0.5f, 0.5f),
                size: new Vector2(64, 68), pos: Vector2.zero);

            var le = EnsureComp<LayoutElement>(slot);
            le.preferredWidth  = 64;
            le.preferredHeight = 68;

            // P# label — pinned to top-center
            var pLbl = Label(slot.transform, "PlayerLabel", $"P{i + 1}",
                11, PlayerLabel[i], TextAnchor.MiddleCenter);
            PinCenter(pLbl.transform, 0, -5, 54, 14);

            // Role icon BG
            var iconBG = MakeImage(slot.transform, "RoleIconBG", PlayerIconBG[i]);
            PinCenter(iconBG.transform, 0, -22, 22, 22);
            var iconOutline = EnsureComp<Outline>(iconBG);
            iconOutline.effectColor    = PlayerIconStroke[i];
            iconOutline.effectDistance = new Vector2(1.5f, 1.5f);

            // Role icon placeholder (hidden)
            var roleIcon = MakeImage(slot.transform, "RoleIcon", Color.white);
            PinCenter(roleIcon.transform, 0, -25, 18, 18);
            roleIcon.GetComponent<Image>().enabled = false;

            // Role name
            var roleLbl = Label(slot.transform, "RoleNameText", roleNames[i],
                13, PlayerName[i], TextAnchor.MiddleCenter);
            PinCenter(roleLbl.transform, 0, -52, 60, 16);

            // P4 ghost opacity
            if (i == 3)
            {
                var cg = EnsureComp<CanvasGroup>(slot);
                cg.alpha = 0.20f;
            }

            slotComponents[i] = EnsureComp<PlayerSlot>(slot);
            var soS = new SerializedObject(slotComponents[i]);
            SetProp(soS, "playerLabel",  pLbl);
            SetProp(soS, "roleIconBG",   iconBG.GetComponent<Image>());
            SetProp(soS, "roleIcon",     roleIcon.GetComponent<Image>());
            SetProp(soS, "roleNameText", roleLbl);
            soS.ApplyModifiedProperties();
        }

        var playerSlotsUI = EnsureComp<PlayerSlotsUI>(container);
        var soP = new SerializedObject(playerSlotsUI);
        var slotsProp = soP.FindProperty("slots");
        if (slotsProp != null)
        {
            slotsProp.arraySize = 4;
            for (int i = 0; i < 4; i++)
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotComponents[i];
        }
        soP.ApplyModifiedProperties();
    }

    // ═════════════════════════════════════════════════════════════════════
    // WIN/LOSE PANEL  center · 240 × 160 · starts inactive
    // All children anchored to center (0.5, 0.5) with anchoredPosition offset
    // ═════════════════════════════════════════════════════════════════════
    static void BuildWinLosePanel(Transform canvas)
    {
        // Full-screen transparent parent keeps WinLoseUI.OnEnable firing
        var winLosePanel = MakeContainer(canvas, "WinLosePanel",
            anchorMin: Vector2.zero, anchorMax: Vector2.one,
            pivot: new Vector2(0.5f, 0.5f),
            size: Vector2.zero, pos: Vector2.zero);

        var content = MakePanel(winLosePanel.transform, "WinLoseContent", BG, Border,
            anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
            pivot:     new Vector2(0.5f, 0.5f),
            size: new Vector2(240, 160), pos: Vector2.zero);

        AddScrews(content.transform, 240, 160);

        // Helper: pin child to center of content panel
        static void C(Transform t, float x, float y, float w, float h)
        {
            var rt = t.GetComponent<RectTransform>() ?? t.gameObject.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta        = new Vector2(w, h);
        }

        // Mission label
        var missionLbl = LabelRaw(content.transform, "MissionLabel", "MISSION COMPLETE",
            12, Hex("#204828"), TextAlignmentOptions.Center);
        C(missionLbl.transform, 0, 58, 220, 18);

        // Result title
        var resultTitle = LabelRaw(content.transform, "ResultTitle", "VICTORY!",
            42, Hex("#2a8040"), TextAlignmentOptions.Center);
        C(resultTitle.transform, 0, 28, 220, 46);

        // Subtitle
        var subtitleLbl = LabelRaw(content.transform, "SubtitleText", "Corexis stabilized",
            13, Hex("#203828"), TextAlignmentOptions.Center);
        C(subtitleLbl.transform, 0, -8, 220, 18);

        // Buttons row container
        var btnRow = new GameObject("ButtonsRow");
        Undo.RegisterCreatedObjectUndo(btnRow, "Create ButtonsRow");
        btnRow.transform.SetParent(content.transform, false);
        btnRow.AddComponent<RectTransform>();
        C(btnRow.transform, 0, -50, 220, 36);

        var rowHLG = EnsureComp<HorizontalLayoutGroup>(btnRow);
        rowHLG.spacing                = 12f;
        rowHLG.childAlignment         = TextAnchor.MiddleCenter;
        rowHLG.childForceExpandWidth  = false;
        rowHLG.childForceExpandHeight = false;
        rowHLG.childControlWidth      = false;
        rowHLG.childControlHeight     = false;

        // Restart button
        var restartBtn = MakeStyledButton(btnRow.transform, "RestartButton", "RESTART",
            Hex("#1e5030"), Hex("#2a7040"), Hex("#80c898"));
        EnsureComp<LayoutElement>(restartBtn).preferredWidth  = 100;
        EnsureComp<LayoutElement>(restartBtn).preferredHeight = 34;

        // Lobby button
        var lobbyBtn = MakeStyledButton(btnRow.transform, "LobbyButton", "MAIN MENU",
            Hex("#2a1a1a"), Hex("#3a1818"), Hex("#c88888"));
        EnsureComp<LayoutElement>(lobbyBtn).preferredWidth  = 100;
        EnsureComp<LayoutElement>(lobbyBtn).preferredHeight = 34;

        content.SetActive(false);

        var ui = EnsureComp<WinLoseUI>(winLosePanel);
        var so = new SerializedObject(ui);
        SetProp(so, "panel",         content);
        SetProp(so, "missionLabel",  missionLbl);
        SetProp(so, "resultTitle",   resultTitle);
        SetProp(so, "subtitleText",  subtitleLbl);
        SetProp(so, "restartButton", restartBtn.GetComponent<Button>());
        SetProp(so, "lobbyButton",   lobbyBtn.GetComponent<Button>());
        so.ApplyModifiedProperties();
    }

    // ═════════════════════════════════════════════════════════════════════
    // Wire HUDManager
    // ═════════════════════════════════════════════════════════════════════
    static void WireHUDManager(GameObject canvasGO)
    {
        var hudManager = EnsureComp<HUDManager>(canvasGO);
        var so = new SerializedObject(hudManager);
        SetProp(so, "shipHealth",    canvasGO.transform.Find("ShipHealthPanel")?.GetComponent<ShipHealthUI>());
        SetProp(so, "coreX",         canvasGO.transform.Find("CoreXPanel")?.GetComponent<CoreXUI>());
        SetProp(so, "stationStatus", canvasGO.transform.Find("StationStatusPanel")?.GetComponent<StationStatusUI>());
        SetProp(so, "timer",         canvasGO.transform.Find("TimerPanel")?.GetComponent<SurvivalTimerUI>());
        SetProp(so, "playerSlots",   canvasGO.transform.Find("PlayerSlots")?.GetComponent<PlayerSlotsUI>());
        SetProp(so, "winLose",       canvasGO.transform.Find("WinLosePanel")?.GetComponent<WinLoseUI>());
        so.ApplyModifiedProperties();
    }

    // ═════════════════════════════════════════════════════════════════════
    // Wire AlertSystem
    // ═════════════════════════════════════════════════════════════════════
    static void WireAlertSystem(GameObject canvasGO)
    {
        var alertSystem = Object.FindObjectOfType<AlertSystem>();
        if (alertSystem == null)
        {
            Debug.LogWarning("[HUDLayoutBuilder] AlertSystem not found — skipping.");
            return;
        }

        var prefab = EnsureDirectionalAlertPrefab();
        var so = new SerializedObject(alertSystem);
        SetProp(so, "canvasRect", canvasGO.GetComponent<RectTransform>());
        if (prefab != null)
            SetProp(so, "alertPrefab", prefab);
        so.ApplyModifiedProperties();
    }

    static GameObject EnsureDirectionalAlertPrefab()
    {
        const string path = "Assets/Prefabs/UI/DirectionalAlert.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

        var root = new GameObject("DirectionalAlert");
        var rt = root.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(24, 24);
        var img = root.AddComponent<Image>();
        img.color = new Color(1f, 0.35f, 0.1f, 1f);
        img.raycastTarget = false;

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(root.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.5f, 0); trt.anchorMax = new Vector2(0.5f, 0);
        trt.pivot = new Vector2(0.5f, 1); trt.sizeDelta = new Vector2(60, 16);
        trt.anchoredPosition = new Vector2(0, -2);
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "⚠"; tmp.fontSize = 14; tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false; tmp.fontStyle = FontStyles.Normal;
        if (VT323 != null) tmp.font = VT323;

        var asset = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.Refresh();
        return asset;
    }

    // ═════════════════════════════════════════════════════════════════════
    // Factory helpers
    // ═════════════════════════════════════════════════════════════════════

    static GameObject MakePanel(Transform parent, string name, Color bg, Color outlineColor,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 pos)
    {
        var ex = parent.Find(name);
        if (ex != null) return ex.gameObject;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = bg; img.raycastTarget = false;

        var ol = go.AddComponent<Outline>();
        ol.effectColor    = outlineColor;
        ol.effectDistance = new Vector2(1, -1);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = pivot; rt.sizeDelta = size; rt.anchoredPosition = pos;
        return go;
    }

    static GameObject MakeContainer(Transform parent, string name,
        Vector2 anchorMin = default, Vector2 anchorMax = default,
        Vector2 pivot = default, Vector2 size = default, Vector2 pos = default)
    {
        var ex = parent.Find(name);
        if (ex != null) return ex.gameObject;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = Transparent; img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = pivot; rt.sizeDelta = size; rt.anchoredPosition = pos;
        return go;
    }

    /// Non-interactable horizontal Slider — fill only, no handle
    static Slider MakeBar(Transform parent, string name, Color bgColor, Color fillColor)
    {
        var ex = parent.Find(name);
        if (ex != null) return ex.GetComponent<Slider>();

        var barGO = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(barGO, "Create " + name);
        barGO.transform.SetParent(parent, false);

        var bgImg = barGO.AddComponent<Image>();
        bgImg.color = bgColor; bgImg.raycastTarget = false;

        // Fill Area — stretch to fill bar bounds
        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(barGO.transform, false);
        var faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
        faRT.sizeDelta = Vector2.zero; faRT.anchoredPosition = Vector2.zero;

        // Fill image
        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = fillColor; fillImg.raycastTarget = false;
        var fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
        fillRT.sizeDelta = Vector2.zero; fillRT.anchoredPosition = Vector2.zero;

        var slider = barGO.AddComponent<Slider>();
        slider.direction    = Slider.Direction.LeftToRight;
        slider.fillRect     = fillRT;
        slider.handleRect   = null;
        slider.interactable = false;
        slider.transition   = Selectable.Transition.None;
        slider.value        = 1f;

        // Remove "Handle Slide Area" if Unity added one automatically
        var handleArea = barGO.transform.Find("Handle Slide Area");
        if (handleArea != null) Object.DestroyImmediate(handleArea.gameObject);

        return slider;
    }

    /// TMP label using VT323 font (TextAnchor overload)
    static TMP_Text Label(Transform parent, string name, string text,
        int size, Color color, TextAnchor anchor)
        => MakeLabelFont(parent, name, text, VT323, size, color, anchor);

    /// TMP label using VT323 font with direct TMP alignment (no conversion needed)
    static TMP_Text LabelRaw(Transform parent, string name, string text,
        int size, Color color, TextAlignmentOptions alignment)
    {
        var ex = parent.Find(name);
        if (ex != null) return ex.GetComponent<TMP_Text>();

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = size;
        tmp.color         = color;
        tmp.fontStyle     = FontStyles.Normal;
        tmp.alignment     = alignment;
        tmp.raycastTarget = false;
        if (VT323 != null) tmp.font = VT323;
        return tmp;
    }

    /// Button with Image + Outline + TMP label, sized by LayoutElement
    static GameObject MakeStyledButton(Transform parent, string name, string labelText,
        Color bgColor, Color outlineColor, Color textColor)
    {
        var ex = parent.Find(name);
        if (ex != null) return ex.gameObject;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var ol = go.AddComponent<Outline>();
        ol.effectColor    = outlineColor;
        ol.effectDistance = new Vector2(1.5f, 1.5f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = labelText;
        tmp.fontSize  = 18;
        tmp.color     = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Normal;
        tmp.raycastTarget = false;
        if (VT323 != null) tmp.font = VT323;

        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
        return go;
    }

    /// TMP label with explicit font
    static TMP_Text MakeLabelFont(Transform parent, string name, string text,
        TMP_FontAsset font, int size, Color color, TextAnchor anchor)
    {
        var ex = parent.Find(name);
        if (ex != null) return ex.GetComponent<TMP_Text>();

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = size;
        tmp.color         = color;
        tmp.fontStyle     = FontStyles.Normal;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;

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

    static GameObject MakeImage(Transform parent, string name, Color color)
    {
        var ex = parent.Find(name);
        if (ex != null) return ex.gameObject;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = color; img.raycastTarget = false;
        return go;
    }

    static GameObject MakeButton(Transform parent, string name, string labelText,
        Color bgColor, Color textColor, TMP_FontAsset font, int fontSize)
    {
        var ex = parent.Find(name);
        if (ex != null) return ex.gameObject;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text = labelText; tmp.fontSize = fontSize; tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center; tmp.fontStyle = FontStyles.Normal;
        if (font != null) tmp.font = font;
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero; txtRT.anchoredPosition = Vector2.zero;
        return go;
    }

    static void AddScrews(Transform parent, float w, float h)
    {
        float inset = 5f;
        Vector2[] anchors   = { new Vector2(0,1), new Vector2(1,1), new Vector2(0,0), new Vector2(1,0) };
        Vector2[] positions = { new Vector2(inset,-inset), new Vector2(-inset,-inset),
                                new Vector2(inset, inset), new Vector2(-inset, inset) };

        for (int i = 0; i < 4; i++)
        {
            var screw = MakeImage(parent, $"Screw_{i}", Screw);
            var rt = screw.GetComponent<RectTransform>();
            rt.anchorMin = anchors[i]; rt.anchorMax = anchors[i];
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(5, 5);
            rt.anchoredPosition = positions[i];
        }
    }

    // ── RectTransform positional helpers ──────────────────────────────────

    /// Pin to top-left corner; x/y already include sign (y is negative = down)
    static void Pin(Transform t, float x, float y, float w, float h)
    {
        var rt = t.GetComponent<RectTransform>() ?? t.gameObject.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0, 1);
        rt.anchorMax        = new Vector2(0, 1);
        rt.pivot            = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(w, h);
    }

    /// Pin to top-center; x is offset from center
    static void PinCenter(Transform t, float x, float y, float w, float h)
    {
        var rt = t.GetComponent<RectTransform>() ?? t.gameObject.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1);
        rt.anchorMax        = new Vector2(0.5f, 1);
        rt.pivot            = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(w, h);
    }

    /// Fixed-size pivot-based placement (for non-TMP objects)
    static void SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 size, Vector2 pos)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.pivot = pivot; rt.sizeDelta = size; rt.anchoredPosition = pos;
    }

    // ── SerializedObject helpers ──────────────────────────────────────────

    static T EnsureComp<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = Undo.AddComponent<T>(go);
        return c;
    }

    static void SetProp(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[HUDLayoutBuilder] '{prop}' not found on {so.targetObject.GetType().Name}");
    }

    static void SetSlider(SerializedObject so, string prop, Slider value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[HUDLayoutBuilder] '{prop}' not found on {so.targetObject.GetType().Name}");
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
