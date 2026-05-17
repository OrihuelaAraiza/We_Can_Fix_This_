using System;
using UnityEngine;

[CreateAssetMenu(fileName = "FixieAnimationSet", menuName = "WeCanFixThis/Fixie Animation Set")]
public class FixieAnimationSet : ScriptableObject
{
    [SerializeField] private string setId;
    [SerializeField] private string[] aliases = Array.Empty<string>();
    [SerializeField] private GameObject visualPrefab;
    [SerializeField] private Avatar avatar;
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip walkClip;
    [SerializeField] private AnimationClip runClip;
    [SerializeField] private AnimationClip jumpClip;
    [SerializeField] private AnimationClip fallClip;

    public string SetId => setId;
    public GameObject VisualPrefab => visualPrefab;
    public Avatar Avatar => avatar;
    public AnimationClip IdleClip => idleClip;
    public AnimationClip WalkClip => walkClip;
    public AnimationClip RunClip => runClip;
    public AnimationClip JumpClip => jumpClip;
    public AnimationClip FallClip => fallClip;
    public AnimationClip AirClip => fallClip != null ? fallClip : jumpClip;
    public bool IsValid => idleClip != null && walkClip != null && runClip != null && jumpClip != null && fallClip != null;

    public bool Matches(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        if (string.Equals(setId, candidate, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (string alias in aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias) &&
                string.Equals(alias, candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public string DescribeClips()
    {
        return $"idle={GetClipName(idleClip)} walk={GetClipName(walkClip)} run={GetClipName(runClip)} jump={GetClipName(jumpClip)} fall={GetClipName(fallClip)}";
    }

    private static string GetClipName(AnimationClip clip)
    {
        return clip != null ? clip.name : "NONE";
    }

    public void Configure(string id,
        string[] aliasList,
        GameObject prefab,
        Avatar targetAvatar,
        AnimationClip idle,
        AnimationClip walk,
        AnimationClip run,
        AnimationClip jump,
        AnimationClip fall)
    {
        setId = id;
        aliases = aliasList ?? Array.Empty<string>();
        visualPrefab = prefab;
        avatar = targetAvatar;
        idleClip = idle;
        walkClip = walk;
        runClip = run;
        jumpClip = jump;
        fallClip = fall;
    }
}
