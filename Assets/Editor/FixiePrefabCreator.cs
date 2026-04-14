using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Creates Fixie visual-model prefabs for the player spawn system.
/// Run via: Tools > Create Fixie Prefabs
///
/// These prefabs are VISUAL ONLY — they are instantiated as children of the
/// existing Player prefab at runtime. Physics (Rigidbody, CapsuleCollider)
/// and all game-logic components live on the Player prefab root.
///
/// After running this tool, assign the 3 prefabs to PlayerManager in the Inspector:
///   fixieP1Prefab → Fixie_P1
///   fixieP2Prefab → Fixie_P2
///   fixieP3Prefab → Fixie_P3
///
/// Then assign the Animator field on the Player prefab's PlayerMovement component
/// by wiring it to the Animator component on each spawned Fixie child.
/// (Or leave it null — the SetFloat call is null-guarded.)
/// </summary>
public static class FixiePrefabCreator
{
    const string PrefabDir      = "Assets/Art/Characters/Fixies/Prefabs";
    const string ModelsDir      = "Assets/Art/Models/Temp/Players";
    const string ControllerPath = "Assets/Art/Characters/Fixies/Fixie_AnimatorController.controller";

    [MenuItem("Tools/Create Fixie Prefabs")]
    public static void CreateAll()
    {
        if (!AssetDatabase.IsValidFolder(PrefabDir))
            AssetDatabase.CreateFolder("Assets/Art/Characters/Fixies", "Prefabs");

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller == null)
            Debug.LogWarning("[FixiePrefabCreator] Fixie_AnimatorController not found at " + ControllerPath +
                             ". Animator components will be created without a controller assigned.");

        CreateFixiePrefab("Fixie_P1", "Astronaut_FinnTheFrog",        controller);
        CreateFixiePrefab("Fixie_P2", "Astronaut_FernandoTheFlamingo", controller);
        CreateFixiePrefab("Fixie_P3", "Astronaut_BarbaraTheBee",       controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FixiePrefabCreator] Done. Assign Fixie_P1/P2/P3 to PlayerManager in the Inspector.");
    }

    static void CreateFixiePrefab(string prefabName, string fbxName, RuntimeAnimatorController controller)
    {
        string fbxPath = $"{ModelsDir}/{fbxName}.fbx";
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbxAsset == null)
        {
            Debug.LogError($"[FixiePrefabCreator] FBX not found at {fbxPath}. Skipping {prefabName}.");
            return;
        }

        // Resolve Avatar embedded in the FBX (for Generic/Humanoid rigs)
        Avatar fbxAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(fbxPath);

        // Resolve Player layer (must already exist in Tags & Layers)
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer == -1)
        {
            Debug.LogWarning("[FixiePrefabCreator] Layer 'Player' not found. Add it in Project Settings > Tags & Layers and re-run.");
            playerLayer = 0;
        }

        // Root GameObject
        var root = new GameObject(prefabName);
        root.tag   = "Player";
        root.layer = playerLayer;

        // Animator on root — controller + avatar from FBX
        var anim = root.AddComponent<Animator>();
        if (controller != null)
            anim.runtimeAnimatorController = controller;
        if (fbxAvatar != null)
            anim.avatar = fbxAvatar;
        anim.applyRootMotion = false;

        // FBX model child — SkinnedMeshRenderer + embedded materials untouched
        var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
        modelInstance.name = fbxName;
        modelInstance.transform.SetParent(root.transform, false);
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;

        // HelmetInterior placeholder — future home for CosmeticSelector.cs
        var helmet = new GameObject("HelmetInterior");
        helmet.transform.SetParent(root.transform, false);
        helmet.transform.localPosition = Vector3.zero;

        // Save and clean up
        string prefabPath = $"{PrefabDir}/{prefabName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"[FixiePrefabCreator] Saved {prefabPath}");
    }
}
