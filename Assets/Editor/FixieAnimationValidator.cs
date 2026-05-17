// AUTO-GENERATED — safe to delete after validation completes.
// Validates the FixieAnimationRuntime system without entering Play mode.
// Run via Tools > Validate Fixie Animations (or auto-triggers on domain reload / FBX reimport).

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

// Triggers validation automatically whenever the FinnTheFrog FBX is reimported.
public class FixieAnimationPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        const string fbxPath = "Assets/Art/Models/Astronaut_FinnTheFrog.fbx";
        foreach (string asset in importedAssets)
        {
            if (asset == fbxPath)
            {
                EditorApplication.delayCall += FixieAnimationValidator.RunValidation;
                break;
            }
        }
    }
}

[InitializeOnLoad]
public static class FixieAnimationValidator
{
    private const string ResultsFile = "/tmp/fixie-validation.txt";
    private const string SentinelKey = "FixieAnimationValidator_HasRun_v4";

    static FixieAnimationValidator()
    {
        if (SessionState.GetBool(SentinelKey, false))
            return;

        SessionState.SetBool(SentinelKey, true);
        EditorApplication.delayCall += RunValidation;
    }

    [MenuItem("Tools/Validate Fixie Animations")]
    public static void RunValidation()
    {
        var log = new System.Text.StringBuilder();
        log.AppendLine("=== Fixie Animation Runtime Validation ===");
        log.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        log.AppendLine($"Unity: {Application.unityVersion}");
        log.AppendLine();

        int passed = 0, failed = 0;

        string fbxPath = "Assets/Art/Models/Astronaut_FinnTheFrog.fbx";

        // Force-refresh so stale AssetDatabase cache doesn't hide clips
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var allFbxAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        int clipCount = allFbxAssets != null ? allFbxAssets.OfType<AnimationClip>().Count() : 0;
        log.AppendLine($"  FBX total sub-assets: {(allFbxAssets != null ? allFbxAssets.Length : -1)}, AnimationClips found: {clipCount}");
        log.AppendLine();

        AnimationClip idle = LoadClip(fbxPath, "CharacterArmature|Idle");
        AnimationClip walk = LoadClip(fbxPath, "CharacterArmature|Walk");
        AnimationClip run  = LoadClip(fbxPath, "CharacterArmature|Run");
        AnimationClip jump = LoadClip(fbxPath, "CharacterArmature|Jump");
        AnimationClip fall = LoadClip(fbxPath, "CharacterArmature|Jump_Idle");

        Check(ref passed, ref failed, log, idle != null, "PASS — Idle clip found", "FAIL — Idle clip MISSING");
        Check(ref passed, ref failed, log, walk != null, "PASS — Walk clip found", "FAIL — Walk clip MISSING");
        Check(ref passed, ref failed, log, run  != null, "PASS — Run clip found",  "FAIL — Run clip MISSING");
        Check(ref passed, ref failed, log, jump != null, "PASS — Jump clip found", "FAIL — Jump clip MISSING");
        Check(ref passed, ref failed, log, fall != null, "PASS — Fall clip found", "FAIL — Fall/Jump_Idle clip MISSING");

        if (idle == null || walk == null || run == null || jump == null || fall == null)
        {
            log.AppendLine();
            log.AppendLine("ABORT — cannot continue without all 5 clips.");
            WriteFinal(log, passed, failed);
            return;
        }

        log.AppendLine();

        var slots = new[]
        {
            ("Assets/Art/Characters/Fixies/Prefabs/Fixie_P1.prefab", "P1 FinnTheFrog"),
            ("Assets/Art/Characters/Fixies/Prefabs/Fixie_P2.prefab", "P2 FernandoTheFlamingo"),
            ("Assets/Art/Characters/Fixies/Prefabs/Fixie_P3.prefab", "P3 BarbaraTheBee"),
        };

        foreach ((string path, string label) in slots)
        {
            log.AppendLine($"--- {label} ---");
            ValidateSlot(label, path, idle, walk, run, jump, fall, ref passed, ref failed, log);
            log.AppendLine();
        }

        WriteFinal(log, passed, failed);
    }

    static void ValidateSlot(
        string label, string prefabPath,
        AnimationClip idle, AnimationClip walk, AnimationClip run,
        AnimationClip jump, AnimationClip fall,
        ref int passed, ref int failed, System.Text.StringBuilder log)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (!Check(ref passed, ref failed, log, prefab != null,
            $"PASS — prefab found", $"FAIL — prefab MISSING at {prefabPath}"))
            return;

        GameObject instance = null;
        PlayableGraph graph = default;

        try
        {
            instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = $"Validator_{label}";

            int animatorsBefore = instance.GetComponentsInChildren<Animator>(true).Length;
            log.AppendLine($"  Animators before Bind: {animatorsBefore}");

            Animator modelAnimator = instance.GetComponentInChildren<Animator>(true);

            if (modelAnimator != null)
            {
                // Generic animations don't require a humanoid avatar — note it but don't fail.
                string avatarNote = modelAnimator.avatar != null
                    ? (modelAnimator.avatar.isHuman ? "humanoid" : "generic/no-mapping")
                    : "none";
                log.AppendLine($"  INFO — Animator found, avatar={avatarNote} (Generic clips work without humanoid mapping)");
            }
            else
            {
                log.AppendLine("  INFO — No Animator pre-Bind (EnsureAnimator will add one)");
            }

            FixieAnimationRuntime runtime = instance.AddComponent<FixieAnimationRuntime>();
            runtime.Bind(null, instance.transform, modelAnimator, idle, walk, run, jump, fall, 0);

            // enabled==true ↔ Bind() succeeded (clips valid, Animator found)
            if (!Check(ref passed, ref failed, log, runtime.enabled,
                "PASS — Bind() succeeded (runtime.enabled=true)",
                "FAIL — Bind() FAILED (runtime.enabled=false) — check [FixieAnimation] warnings in Console"))
                return;

            // PlayableGraph via reflection (avoids UnityEngine.Animations module reference issue)
            graph = GetField<PlayableGraph>(runtime, "graph");

            Check(ref passed, ref failed, log, graph.IsValid(),
                "PASS — PlayableGraph.IsValid() == true",
                "FAIL — PlayableGraph invalid — animation cannot play");

            if (!graph.IsValid())
                return;

            Check(ref passed, ref failed, log, graph.IsPlaying(),
                "PASS — PlayableGraph.IsPlaying() == true",
                "FAIL — PlayableGraph NOT playing — character frozen");

            // Check mixer via reflection without referencing AnimationMixerPlayable type
            object mixerObj = GetFieldObj(runtime, "mixer");
            if (mixerObj != null)
            {
                Type mixerType = mixerObj.GetType();

                // GetInputCount()
                MethodInfo getInputCount = mixerType.GetMethod("GetInputCount",
                    BindingFlags.Public | BindingFlags.Instance);
                if (getInputCount != null)
                {
                    int inputCount = (int)getInputCount.Invoke(mixerObj, null);
                    Check(ref passed, ref failed, log, inputCount == 4,
                        $"PASS — Mixer has 4 inputs (Idle/Walk/Run/Air)",
                        $"FAIL — Mixer has {inputCount} inputs, expected 4");
                }

                // GetInputWeight(0) → idle weight should be 1.0
                MethodInfo getWeight = mixerType.GetMethod("GetInputWeight",
                    BindingFlags.Public | BindingFlags.Instance);
                if (getWeight != null)
                {
                    float idleWeight = (float)getWeight.Invoke(mixerObj, new object[] { 0 });
                    float walkWeight = (float)getWeight.Invoke(mixerObj, new object[] { 1 });
                    float runWeight  = (float)getWeight.Invoke(mixerObj, new object[] { 2 });
                    float airWeight  = (float)getWeight.Invoke(mixerObj, new object[] { 3 });

                    log.AppendLine($"  Mixer weights: idle={idleWeight:F3} walk={walkWeight:F3} run={runWeight:F3} air={airWeight:F3}");

                    Check(ref passed, ref failed, log, Mathf.Abs(idleWeight - 1f) < 0.001f,
                        $"PASS — Idle weight={idleWeight:F3} (spawns in idle, not T-pose)",
                        $"FAIL — Idle weight={idleWeight:F3}, expected 1.0 (T-pose on first frame)");
                }

                // GetInput(i) → each input must be connected
                MethodInfo getInput = mixerType.GetMethod("GetInput",
                    BindingFlags.Public | BindingFlags.Instance);
                if (getInput != null)
                {
                    string[] clipNames = { "Idle", "Walk", "Run", "Air" };
                    for (int i = 0; i < 4; i++)
                    {
                        object inputPlayable = getInput.Invoke(mixerObj, new object[] { i });
                        MethodInfo isValid = inputPlayable.GetType().GetMethod("IsValid",
                            BindingFlags.Public | BindingFlags.Instance);
                        bool connected = isValid != null && (bool)isValid.Invoke(inputPlayable, null);
                        Check(ref passed, ref failed, log, connected,
                            $"PASS — input[{i}] ({clipNames[i]}) connected",
                            $"FAIL — input[{i}] ({clipNames[i]}) NOT connected — that state can never play");
                    }
                }
            }

            // Manually tick the graph (simulates one 30fps game frame)
            graph.Evaluate(0.033f);

            // Animator state after Bind
            Animator chosen = GetField<Animator>(runtime, "animator");
            Check(ref passed, ref failed, log, chosen != null,
                "PASS — EnsureAnimator selected an Animator",
                "FAIL — No Animator selected — PlayableGraph has no output target");

            if (chosen != null)
            {
                Check(ref passed, ref failed, log, chosen.runtimeAnimatorController == null,
                    "PASS — Active Animator has no RuntimeAnimatorController",
                    "FAIL — Active Animator STILL has controller — fights PlayableGraph for bones");
                Check(ref passed, ref failed, log, chosen.avatar != null,
                    $"PASS — Animator avatar='{chosen.avatar?.name}'",
                    "FAIL — Animator has no avatar");
                if (chosen.avatar != null)
                    Check(ref passed, ref failed, log, chosen.avatar.isHuman,
                        "PASS — Avatar is humanoid",
                        "FAIL — Avatar NOT humanoid — clips won't retarget");
            }

            // Only ONE animator should be enabled after Bind (dual-animator fix)
            Animator[] allA = instance.GetComponentsInChildren<Animator>(true);
            int activeCount = allA.Count(a => a != null && a.enabled);
            Check(ref passed, ref failed, log, activeCount == 1,
                $"PASS — Exactly 1 Animator enabled (was {animatorsBefore} before Bind)",
                $"FAIL — {activeCount} Animators enabled — dual-animator conflict not resolved");

            // SkinnedMeshRenderers must be enabled (character visible)
            SkinnedMeshRenderer[] skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Check(ref passed, ref failed, log, skins.Length > 0,
                $"PASS — {skins.Length} SkinnedMeshRenderer(s) present",
                "FAIL — NO SkinnedMeshRenderer — character has no mesh");
            foreach (SkinnedMeshRenderer smr in skins)
            {
                Check(ref passed, ref failed, log, smr.enabled,
                    $"PASS — SMR '{smr.name}' enabled (visible)",
                    $"FAIL — SMR '{smr.name}' DISABLED (invisible)");
            }
        }
        catch (Exception ex)
        {
            log.AppendLine($"  EXCEPTION: {ex.Message}");
            failed++;
        }
        finally
        {
            if (graph.IsValid())
                graph.Destroy();
            if (instance != null)
                UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    static bool Check(ref int passed, ref int failed, System.Text.StringBuilder log,
        bool condition, string passMsg, string failMsg)
    {
        if (condition) { log.AppendLine($"  {passMsg}"); passed++; return true; }
        else           { log.AppendLine($"  {failMsg}"); failed++; return false; }
    }

    static void WriteFinal(System.Text.StringBuilder log, int passed, int failed)
    {
        log.AppendLine($"=== RESULT: {passed} passed / {failed} failed ===");
        string text = log.ToString();
        File.WriteAllText(ResultsFile, text);

        if (failed == 0)
            Debug.Log($"[FixieAnimationValidator] ALL {passed} CHECKS PASSED\n\n{text}");
        else
            Debug.LogError($"[FixieAnimationValidator] {failed} FAILED\n\n{text}");
    }

    static AnimationClip LoadClip(string assetPath, string clipName) =>
        AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c.name == clipName);

    static T GetField<T>(object target, string name)
    {
        FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (f == null) throw new Exception($"Field '{name}' not found on {target.GetType().Name}");
        return (T)f.GetValue(target);
    }

    static object GetFieldObj(object target, string name)
    {
        FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        return f?.GetValue(target);
    }
}
