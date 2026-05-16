#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tool: WeCF → Fix Raycast Targets in Lobby
///
/// Red diagonal lines in the Scene View indicate that an Image or
/// decorative Panel has Raycast Target = true and blocks all clicks
/// on buttons beneath it in Z order.
///
/// This tool disables Raycast Target on all Images and
/// TextMeshProUGUI elements that are NOT buttons or children of buttons.
/// </summary>
public static class FixRaycastTargets
{
    [MenuItem("WeCF/Fix Raycast Targets in Lobby")]
    static void FixAll()
    {
        int fixedCount = 0;

        // ── Images ──────────────────────────────────────────────
        var allImages = Object.FindObjectsOfType<Image>(true);
        foreach (var img in allImages)
        {
            bool isButton      = img.TryGetComponent<Button>(out _);
            bool insideButton  = img.GetComponentInParent<Button>() != null;

            if (!isButton && !insideButton && img.raycastTarget)
            {
                Undo.RecordObject(img, "Disable Raycast Target");
                img.raycastTarget = false;
                EditorUtility.SetDirty(img);
                fixedCount++;
                Debug.Log($"[RaycastFix] Image disabled: {FullPath(img.gameObject)}");
            }
        }

        // ── TextMeshProUGUI ──────────────────────────────────────
        var allTexts = Object.FindObjectsOfType<TMPro.TextMeshProUGUI>(true);
        foreach (var txt in allTexts)
        {
            bool insideButton = txt.GetComponentInParent<Button>() != null;
            if (!insideButton && txt.raycastTarget)
            {
                Undo.RecordObject(txt, "Disable Raycast Target");
                txt.raycastTarget = false;
                EditorUtility.SetDirty(txt);
                fixedCount++;
                Debug.Log($"[RaycastFix] TMP disabled: {FullPath(txt.gameObject)}");
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[RaycastFix] Done. {fixedCount} elements fixed. Save the scene (Ctrl+S).");
    }

    static string FullPath(GameObject go)
    {
        string path = go.name;
        var t = go.transform.parent;
        while (t != null) { path = t.name + "/" + path; t = t.parent; }
        return path;
    }
}
#endif
