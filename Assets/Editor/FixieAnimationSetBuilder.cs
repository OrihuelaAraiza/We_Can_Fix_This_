using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FixieAnimationSetBuilder
{
    private const string ResourcesFolder = "Assets/Resources";
    private const string OutputFolder = "Assets/Resources/FixieAnimations";

    private static readonly Binding[] Bindings =
    {
        new(
            "Fixie_P1",
            new[] { "Fixie_P1", "Astronaut_FinnTheFrog" },
            "Assets/Art/Models/Astronaut_FinnTheFrog.fbx",
            "Assets/Art/Models/Temp/Players/Astronaut_FinnTheFrog.fbx"),
        new(
            "Fixie_P2",
            new[] { "Fixie_P2", "Astronaut_FernandoTheFlamingo" },
            "Assets/Art/Models/Astronaut_FernandoTheFlamingo.fbx",
            "Assets/Art/Models/Temp/Players/Astronaut_FernandoTheFlamingo.fbx"),
        new(
            "Fixie_P3",
            new[] { "Fixie_P3", "Astronaut_BarbaraTheBee" },
            "Assets/Art/Models/Astronaut_BarbaraTheBee.fbx",
            "Assets/Art/Models/Temp/Players/Astronaut_BarbaraTheBee.fbx"),
    };

    [InitializeOnLoadMethod]
    private static void AutoBuildOnLoad()
    {
        EditorApplication.delayCall += EnsureAnimationSets;
    }

    [MenuItem("Tools/Fixies/Rebuild Runtime Animation Sets")]
    public static void EnsureAnimationSets()
    {
        EnsureFolder(ResourcesFolder);
        EnsureFolder(OutputFolder);

        foreach (Binding binding in Bindings)
            CreateOrUpdateSet(binding);

        AssetDatabase.SaveAssets();
    }

    private static void CreateOrUpdateSet(Binding binding)
    {
        string clipSourcePath = ResolveClipSourcePath(binding);
        if (string.IsNullOrWhiteSpace(clipSourcePath))
            return;

        if (!TryResolveClips(clipSourcePath, out AnimationClip idle, out AnimationClip walk, out AnimationClip run, out AnimationClip jump, out AnimationClip fall))
            return;

        Avatar avatar = ResolveAvatar(binding.AvatarSourcePath);
        string assetPath = $"{OutputFolder}/{binding.AssetName}.asset";
        FixieAnimationSet set = AssetDatabase.LoadAssetAtPath<FixieAnimationSet>(assetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<FixieAnimationSet>();
            AssetDatabase.CreateAsset(set, assetPath);
        }

        set.Configure(binding.AssetName, binding.Aliases, avatar, idle, walk, run, jump, fall);
        EditorUtility.SetDirty(set);
    }

    private static string ResolveClipSourcePath(Binding binding)
    {
        string[] candidates =
        {
            binding.PreferredClipSourcePath,
            binding.AvatarSourcePath,
        };

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            if (!AssetExists(candidate))
                continue;

            if (!TryResolveClips(candidate, out _, out _, out _, out _, out _))
                continue;

            ModelImporter importer = AssetImporter.GetAtPath(candidate) as ModelImporter;
            if (importer != null && importer.animationType == ModelImporterAnimationType.Human)
                return candidate;
        }

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !AssetExists(candidate))
                continue;

            if (TryResolveClips(candidate, out _, out _, out _, out _, out _))
                return candidate;
        }

        return null;
    }

    private static Avatar ResolveAvatar(string avatarPath)
    {
        if (!AssetExists(avatarPath))
            return null;

        return AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
    }

    private static bool TryResolveClips(string assetPath,
        out AnimationClip idle,
        out AnimationClip walk,
        out AnimationClip run,
        out AnimationClip jump,
        out AnimationClip fall)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        AnimationClip[] clips = assets.OfType<AnimationClip>().ToArray();

        idle = clips.FirstOrDefault(clip => clip != null && clip.name.EndsWith("|Idle"));
        walk = clips.FirstOrDefault(clip => clip != null && clip.name.EndsWith("|Walk"));
        run = clips.FirstOrDefault(clip => clip != null && clip.name.EndsWith("|Run"));
        jump = clips.FirstOrDefault(clip => clip != null && clip.name.EndsWith("|Jump"));
        fall = clips.FirstOrDefault(clip => clip != null && clip.name.EndsWith("|Jump_Idle"));

        return idle != null && walk != null && run != null;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        string currentPath = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = $"{currentPath}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(nextPath))
                AssetDatabase.CreateFolder(currentPath, parts[i]);

            currentPath = nextPath;
        }
    }

    private static bool AssetExists(string assetPath)
    {
        return AssetDatabase.LoadMainAssetAtPath(assetPath) != null;
    }

    private readonly struct Binding
    {
        public Binding(string assetName, string[] aliases, string preferredClipSourcePath, string avatarSourcePath)
        {
            AssetName = assetName;
            Aliases = aliases;
            PreferredClipSourcePath = preferredClipSourcePath;
            AvatarSourcePath = avatarSourcePath;
        }

        public string AssetName { get; }
        public string[] Aliases { get; }
        public string PreferredClipSourcePath { get; }
        public string AvatarSourcePath { get; }
    }
}
