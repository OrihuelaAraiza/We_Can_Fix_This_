#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tool: WeCF → Setup AudioManager
/// Creates an AudioManager prefab in Assets/Prefabs/Audio/ with all clips
/// from Assets/Audio/ pre-assigned. Then you only need to drag it into
/// AppBootstrap's "Audio Manager Prefab" field.
/// </summary>
public static class AudioManagerSetup
{
    const string PrefabFolder = "Assets/Prefabs/Audio";
    const string PrefabPath   = PrefabFolder + "/AudioManager.prefab";
    const string AudioFolder  = "Assets/Audio";

    [MenuItem("WeCF/Setup AudioManager Prefab")]
    static void CreateAudioManagerPrefab()
    {
        // Ensure output folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Audio");

        // Build the GameObject
        var go = new GameObject("AudioManager");
        AudioManager manager = go.AddComponent<AudioManager>();

        // Load and assign clips via SerializedObject so fields are properly saved
        SerializedObject so = new SerializedObject(manager);

        AssignClip(so, "lobbyMusic",          AudioFolder + "/Lobby.mp3");
        AssignClip(so, "gameplayMusic",       AudioFolder + "/GameplayMusic.ogg");
        AssignClip(so, "gameplayIntenseMusic", AudioFolder + "/GameplayIntense.mp3");
        AssignClip(so, "failStateMusic",      AudioFolder + "/FailState.mp3");
        AssignClip(so, "playerWalkingClip",   AudioFolder + "/PlayerWalking.mp3");
        AssignClip(so, "stationDamagedClip",  AudioFolder + "/StationDamaged.wav");
        AssignClip(so, "stationHealthyClip",  AudioFolder + "/StationHealthy.wav");

        AssignClipArray(so, "shipAmbientClips", new[]
        {
            AudioFolder + "/ShipAmbient.mp3",
            AudioFolder + "/ShipAmbient2.mp3"
        });

        AssignClipArray(so, "stationRepairClips", new[]
        {
            AudioFolder + "/StationRepari.mp3",
            AudioFolder + "/StationRepair2.mp3"
        });

        so.ApplyModifiedPropertiesWithoutUndo();

        // Save as prefab
        bool success;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out success);
        Object.DestroyImmediate(go);

        if (success)
        {
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[AudioManagerSetup] Prefab created at {PrefabPath}. Drag it into AppBootstrap → Audio Manager Prefab.");
        }
        else
        {
            Debug.LogError("[AudioManagerSetup] Failed to save AudioManager prefab.");
        }
    }

    static void AssignClip(SerializedObject so, string fieldName, string assetPath)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManagerSetup] Clip not found: {assetPath}");
            return;
        }
        SerializedProperty prop = so.FindProperty(fieldName);
        if (prop != null)
            prop.objectReferenceValue = clip;
    }

    static void AssignClipArray(SerializedObject so, string fieldName, string[] assetPaths)
    {
        SerializedProperty arrayProp = so.FindProperty(fieldName);
        if (arrayProp == null) return;

        arrayProp.ClearArray();
        int index = 0;
        foreach (string path in assetPaths)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManagerSetup] Clip not found: {path}");
                continue;
            }
            arrayProp.InsertArrayElementAtIndex(index);
            arrayProp.GetArrayElementAtIndex(index).objectReferenceValue = clip;
            index++;
        }
    }
}
#endif
