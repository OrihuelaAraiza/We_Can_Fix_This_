#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Herramienta: WeCF → Fix Raycast Targets in Lobby
///
/// Las líneas rojas diagonales en el Scene View indican que un Image o
/// Panel decorativo tiene Raycast Target = true y bloquea todos los clicks
/// a los botones que están debajo en el orden Z.
///
/// Esta herramienta desactiva Raycast Target en todos los Image y
/// TextMeshProUGUI que NO son botones ni hijos de botones.
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
            // Mantener raycast en el propio Button y su imagen de fondo
            bool isButton      = img.TryGetComponent<Button>(out _);
            bool insideButton  = img.GetComponentInParent<Button>() != null;

            if (!isButton && !insideButton && img.raycastTarget)
            {
                Undo.RecordObject(img, "Disable Raycast Target");
                img.raycastTarget = false;
                EditorUtility.SetDirty(img);
                fixedCount++;
                Debug.Log($"[RaycastFix] Image desactivada: {FullPath(img.gameObject)}");
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
                Debug.Log($"[RaycastFix] TMP desactivado: {FullPath(txt.gameObject)}");
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[RaycastFix] Listo. {fixedCount} elementos corregidos. Guardá la escena (Ctrl+S).");
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
