using System;
using UnityEngine;

[CreateAssetMenu(fileName = "FixieAnimationSet", menuName = "WeCanFixThis/Fixie Animation Set")]
public class FixieAnimationSet : ScriptableObject
{
    [SerializeField] private string setId;
    [SerializeField] private string[] aliases = Array.Empty<string>();
    [SerializeField] private Avatar avatar;
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip walkClip;
    [SerializeField] private AnimationClip runClip;
    [SerializeField] private AnimationClip jumpClip;
    [SerializeField] private AnimationClip fallClip;

    public string SetId => setId;
    public Avatar Avatar => avatar;
    public AnimationClip IdleClip => idleClip;
    public AnimationClip WalkClip => walkClip;
    public AnimationClip RunClip => runClip;
    public AnimationClip JumpClip => jumpClip;
    public AnimationClip FallClip => fallClip;
    public AnimationClip AirClip => fallClip != null ? fallClip : jumpClip;
    public bool IsValid => idleClip != null && walkClip != null && runClip != null;

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
        string airName = AirClip != null ? AirClip.name : "NONE";
        return $"idle={GetClipName(idleClip)} walk={GetClipName(walkClip)} run={GetClipName(runClip)} air={airName}";
    }

    private static string GetClipName(AnimationClip clip)
    {
        return clip != null ? clip.name : "NONE";
    }

    public void Configure(string id,
        string[] aliasList,
        Avatar targetAvatar,
        AnimationClip idle,
        AnimationClip walk,
        AnimationClip run,
        AnimationClip jump,
        AnimationClip fall)
    {
        setId = id;
        aliases = aliasList ?? Array.Empty<string>();
        avatar = targetAvatar;
        idleClip = idle;
        walkClip = walk;
        runClip = run;
        jumpClip = jump;
        fallClip = fall;
    }
}
