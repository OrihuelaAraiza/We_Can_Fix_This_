using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Creates Fixie visual-model prefabs for the player spawn system.
/// Run via: Tools > Create Fixie Prefabs
///
/// These prefabs are VISUAL ONLY — they are instantiated as children of the
/// existing Player prefab at runtime. Physics (Rigidbody, CapsuleCollider)
/// and all game-logic components live on the Player prefab root.
/// </summary>
public static class FixiePrefabCreator
{
    const string PrefabDir  = "Assets/Art/Characters/Fixies/Prefabs";
    const string ModelsDir  = "Assets/Art/Models/Temp/Players";

    [MenuItem("Tools/Create Fixie Prefabs")]
    public static void CreateAll()
    {
        if (!AssetDatabase.IsValidFolder(PrefabDir))
            AssetDatabase.CreateFolder("Assets/Art/Characters/Fixies", "Prefabs");

        CreateFixiePrefab("Fixie_P1", "Astronaut_FinnTheFrog");
        CreateFixiePrefab("Fixie_P2", "Astronaut_FernandoTheFlamingo");
        CreateFixiePrefab("Fixie_P3", "Astronaut_BarbaraTheBee");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FixiePrefabCreator] Done. Assign Fixie_P1, Fixie_P2, Fixie_P3 to PlayerManager.fixieP1Prefab / fixieP2Prefab / fixieP3Prefab in the Inspector.");
    }

    static void CreateFixiePrefab(string prefabName, string fbxName)
    {
        string fbxPath = $"{ModelsDir}/{fbxName}.fbx";
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbxAsset == null)
        {
            Debug.LogError($"[FixiePrefabCreator] FBX not found at {fbxPath}. Skipping {prefabName}.");
            return;
        }

        // Resolve Player layer index (must already exist in Tags & Layers)
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer == -1)
        {
            Debug.LogWarning("[FixiePrefabCreator] Layer 'Player' not found in Tags & Layers. " +
                             "Add it manually (Project Settings > Tags & Layers) and re-run this tool.");
            playerLayer = 0;
        }

        // Root GameObject
        var root = new GameObject(prefabName);
        root.tag   = "Player";
        root.layer = playerLayer;

        // FBX model child — SkinnedMeshRenderer + embedded materials stay untouched
        var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
        modelInstance.name = fbxName;
        modelInstance.transform.SetParent(root.transform, false);
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;

        // HelmetInterior placeholder for future CosmeticSelector.cs
        var helmet = new GameObject("HelmetInterior");
        helmet.transform.SetParent(root.transform, false);
        helmet.transform.localPosition = Vector3.zero;

        // Save prefab asset and destroy the temporary scene object
        string prefabPath = $"{PrefabDir}/{prefabName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"[FixiePrefabCreator] Saved {prefabPath}");
    }
}
