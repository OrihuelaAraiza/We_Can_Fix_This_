#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LobbyLayoutFixer
{
    [MenuItem("WeCF/Fix Lobby Layout")]
    public static void FixLayout()
    {
        string[] panelNames = { "PlayerPanel_0", "PlayerPanel_1", "PlayerPanel_2", "PlayerPanel_3" };

        foreach (var panelName in panelNames)
        {
            var panelGO = GameObject.Find(panelName);
            if (panelGO == null)
            {
                Debug.LogWarning($"[LobbyLayoutFixer] '{panelName}' not found in scene — skipping.");
                continue;
            }

            // Panel root: 320×500, anchored center
            SetRect(panelGO,
                anchor: new Vector2(0.5f, 0.5f),
                pivot:  new Vector2(0.5f, 0.5f),
                pos:    new Vector2(0, 0),
                size:   new Vector2(320, 500));

            // Children
            FixChild(panelGO, "PlayerLabel",    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20),   new Vector2(280, 40));
            FixChild(panelGO, "RoleColorBar",   new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -68),   new Vector2(280,  6));
            FixChild(panelGO, "RoleName",       new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -90),   new Vector2(280, 50));
            FixChild(panelGO, "PerkText",       new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -155),  new Vector2(260, 70));
            FixChild(panelGO, "PenaltyText",    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -240),  new Vector2(260, 50));
            FixChild(panelGO, "BtnPrev",        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-90, -310),new Vector2( 80, 50));
            FixChild(panelGO, "BtnNext",        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2( 90, -310),new Vector2( 80, 50));
            FixChild(panelGO, "ReadyIndicator", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-70, -385),new Vector2( 20, 20));
            FixChild(panelGO, "ReadyText",      new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2( 30, -380),new Vector2(160, 30));
            FixChild(panelGO, "BtnReady",       new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -425),  new Vector2(200, 45));

            EditorUtility.SetDirty(panelGO);
            Debug.Log($"[LobbyLayoutFixer] Configured '{panelName}'.");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[LobbyLayoutFixer] Done. Save the scene to persist changes.");
    }

    // ── Helpers ────────────────────────────────────────────────────

    static void SetRect(GameObject go, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;

        EditorUtility.SetDirty(rt);
    }

    static void FixChild(GameObject parent, string childName,
                         Vector2 anchor, Vector2 pivot,
                         Vector2 pos,    Vector2 size)
    {
        var child = parent.transform.Find(childName);
        if (child == null)
        {
            Debug.LogWarning($"[LobbyLayoutFixer] Child '{childName}' not found in '{parent.name}' — skipping.");
            return;
        }

        SetRect(child.gameObject, anchor, pivot, pos, size);
    }
}
#endif
