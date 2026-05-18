using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public class StabilitySmokeTests
{
    readonly List<GameObject> createdObjects = new();
    readonly List<InputDevice> createdDevices = new();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in createdObjects)
        {
            if (go != null)
                UnityEngine.Object.DestroyImmediate(go);
        }

        createdObjects.Clear();

        foreach (InputDevice device in createdDevices)
        {
            if (device != null)
                InputSystem.RemoveDevice(device);
        }

        createdDevices.Clear();
        InvokeStatic(GetGameType("LobbyPlayerSessionData"), "Reset");
    }

    [Test]
    public void ShipHealth_DrainsOncePerBrokenStation()
    {
        Component health = CreateComponent("ShipHealth", GetGameType("ShipHealth"));
        SetField(health, "maxHealth", 100f);
        SetField(health, "currentHealth", 100f);
        SetField(health, "drainPerBrokenStation", 3f);
        SetField(health, "regenPerSecond", 0f);

        Component station = CreateComponent("RepairStation", GetGameType("RepairStation"));
        InvokeInstance(station, "BreakStation");

        InvokeInstance(health, "TickHealth", 1f);

        Assert.That(GetProperty<float>(health, "CurrentHealth"), Is.EqualTo(97f).Within(0.001f));
    }

    [Test]
    public void RepairStation_RaisesStateChangedBrokenAndRepairedEvents()
    {
        Type stationType = GetGameType("RepairStation");
        Component station = CreateComponent("RepairStation", stationType);
        var recorder = new RepairStationEventRecorder(station);

        EventInfo stateChangedEvent = stationType.GetEvent("OnStateChanged", BindingFlags.Public | BindingFlags.Static);
        EventInfo brokenEvent = stationType.GetEvent("OnBroken", BindingFlags.Public | BindingFlags.Instance);
        EventInfo repairedEvent = stationType.GetEvent("OnRepaired", BindingFlags.Public | BindingFlags.Instance);
        Delegate stateChangedHandler = CreateStateChangedHandler(stateChangedEvent.EventHandlerType, recorder);
        Delegate brokenHandler = CreateSingleArgHandler(brokenEvent.EventHandlerType, recorder, nameof(RepairStationEventRecorder.Broken));
        Delegate repairedHandler = CreateSingleArgHandler(repairedEvent.EventHandlerType, recorder, nameof(RepairStationEventRecorder.Repaired));

        stateChangedEvent.AddEventHandler(null, stateChangedHandler);
        brokenEvent.AddEventHandler(station, brokenHandler);
        repairedEvent.AddEventHandler(station, repairedHandler);

        try
        {
            InvokeInstance(station, "BreakStation");
            InvokeInstance(station, "ApplyRemoteRepairBoost", 1f);
        }
        finally
        {
            stateChangedEvent.RemoveEventHandler(null, stateChangedHandler);
            brokenEvent.RemoveEventHandler(station, brokenHandler);
            repairedEvent.RemoveEventHandler(station, repairedHandler);
        }

        Assert.That(recorder.BrokenCount, Is.EqualTo(1));
        Assert.That(recorder.RepairedCount, Is.EqualTo(1));
        Assert.That(recorder.Transitions, Does.Contain(("Functional", "Broken")));
        Assert.That(recorder.Transitions, Does.Contain(("Broken", "Fixed")));
    }

    [Test]
    public void GameConfig_SceneNamesExistInBuildSettings()
    {
        var configuredScenes = new HashSet<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled)
                continue;

            configuredScenes.Add(System.IO.Path.GetFileNameWithoutExtension(scene.path));
        }

        Assert.That(configuredScenes, Does.Contain("00_Bootstrap"));
        Assert.That(configuredScenes, Does.Contain("01_MainMenu scene"));
        Assert.That(configuredScenes, Does.Contain("02_Lobby"));
        Assert.That(configuredScenes, Does.Contain("03_Gameplay"));
    }

    [Test]
    public void PlayerManager_HardcodedFixieAnimationRefsAreBuildSafe()
    {
        string scenePath = "Assets/Scenes/03_Gameplay.unity";
        string visualPath = "Assets/Art/Characters/Fixies/Prefabs/Fixie_P1.prefab";
        string animationSourcePath = "Assets/Art/Models/Astronaut_FinnTheFrog.fbx";

        var sourceImporter = AssetImporter.GetAtPath(animationSourcePath) as ModelImporter;
        Assert.That(sourceImporter, Is.Not.Null, animationSourcePath);
        Assert.That(sourceImporter.importAnimation, Is.True);

        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(visualPath), Is.Not.Null, visualPath);

        AssertUsableClip(animationSourcePath, "CharacterArmature|Idle");
        AssertUsableClip(animationSourcePath, "CharacterArmature|Walk");
        AssertUsableClip(animationSourcePath, "CharacterArmature|Run");
        AssertUsableClip(animationSourcePath, "CharacterArmature|Jump");
        AssertUsableClip(animationSourcePath, "CharacterArmature|Jump_Idle");

        string sceneYaml = System.IO.File.ReadAllText(scenePath);
        Assert.That(sceneYaml, Does.Contain("fixieVisualPrefabs"));
        Assert.That(sceneYaml, Does.Contain("guid: 891e50ae3d48543e58d8c685f03ea703"), "Fixie_P1 must be in fixieVisualPrefabs");
        Assert.That(sceneYaml, Does.Contain("guid: c9d59fcfd6a2141f0880bd5190f93365"), "Fixie_P2 must be in fixieVisualPrefabs");
        Assert.That(sceneYaml, Does.Contain("guid: 9cba2cedafdb645ca8fbf0e0095a4ed3"), "Fixie_P3 must be in fixieVisualPrefabs");
        Assert.That(sceneYaml, Does.Contain("idleClip"));
        Assert.That(sceneYaml, Does.Contain("walkClip"));
        Assert.That(sceneYaml, Does.Contain("runClip"));
        Assert.That(sceneYaml, Does.Contain("jumpClip"));
        Assert.That(sceneYaml, Does.Contain("fallClip"));

        string playerPrefabYaml = System.IO.File.ReadAllText("Assets/Prefabs/Players/Player.prefab");
        Assert.That(playerPrefabYaml, Does.Contain("HardcodedFixieVisual"));
        Assert.That(playerPrefabYaml, Does.Contain("guid: 891e50ae3d48543e58d8c685f03ea703"));
    }

    [Test]
    public void FixieAnimationSets_AreLoadableFromResourcesAndComplete()
    {
        string[] expectedSets = { "Fixie_P1", "Fixie_P2", "Fixie_P3" };
        Type animationSetType = GetGameType("FixieAnimationSet");

        foreach (string setName in expectedSets)
        {
            UnityEngine.Object set = Resources.Load($"FixieAnimations/{setName}", animationSetType);

            Assert.That(set, Is.Not.Null, setName);
            Assert.That(GetProperty<string>(set, "SetId"), Is.EqualTo(setName));
            Assert.That(GetProperty<GameObject>(set, "VisualPrefab"), Is.Not.Null, $"{setName} visual prefab must be referenced by the Resources asset");
            Assert.That(GetProperty<Avatar>(set, "Avatar"), Is.Not.Null, $"{setName} avatar");
            Assert.That(GetProperty<AnimationClip>(set, "IdleClip"), Is.Not.Null, $"{setName} idle");
            Assert.That(GetProperty<AnimationClip>(set, "WalkClip"), Is.Not.Null, $"{setName} walk");
            Assert.That(GetProperty<AnimationClip>(set, "RunClip"), Is.Not.Null, $"{setName} run");
            Assert.That(GetProperty<AnimationClip>(set, "JumpClip"), Is.Not.Null, $"{setName} jump");
            Assert.That(GetProperty<AnimationClip>(set, "FallClip"), Is.Not.Null, $"{setName} fall");
            Assert.That(GetProperty<bool>(set, "IsValid"), Is.True, $"{setName} must be complete for build runtime");
        }
    }

    [Test]
    public void PlayerManager_RebuildPlayerVisual_UsesRuntimeAnimationSet()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Players/Player.prefab");
        Assert.That(playerPrefab, Is.Not.Null);

        GameObject managerObject = new GameObject("PlayerManager_Test");
        createdObjects.Add(managerObject);
        Component manager = managerObject.AddComponent(GetGameType("PlayerManager"));

        GameObject player = UnityEngine.Object.Instantiate(playerPrefab);
        createdObjects.Add(player);

        Component movement = player.GetComponent(GetGameType("PlayerMovement"));
        Assert.That(movement, Is.Not.Null);

        InvokeInstance(manager, "RebuildPlayerVisual", player.transform, movement, 0);

        Transform modelRoot = player.transform.Find("ModelRoot");
        Assert.That(modelRoot, Is.Not.Null);
        Assert.That(modelRoot.childCount, Is.EqualTo(1), "PlayerManager should replace prefab preview visuals with one runtime visual");

        Component runtime = modelRoot.GetComponentInChildren(GetGameType("FixieAnimationRuntime"), true);
        Assert.That(runtime, Is.Not.Null);
        Assert.That(((Behaviour)runtime).enabled, Is.True);

        PlayableGraph graph = GetPrivateField<PlayableGraph>(runtime, "graph");
        Assert.That(graph.IsValid(), Is.True);
        Assert.That(graph.IsPlaying(), Is.True);

        Animator[] activeAnimators = modelRoot.GetComponentsInChildren<Animator>(true)
            .Where(animator => animator != null && animator.enabled)
            .ToArray();
        Assert.That(activeAnimators.Length, Is.EqualTo(1), "Exactly one Animator should remain enabled");
        Animator activeAnimator = activeAnimators[0];
        Assert.That(activeAnimator.runtimeAnimatorController, Is.Null, "PlayableGraph must be the only animation driver");
        Assert.That(activeAnimator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));

        Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Assert.That(renderer.enabled, Is.True, renderer.name);
            Assert.That(renderer.forceRenderingOff, Is.False, renderer.name);

            if (renderer is SkinnedMeshRenderer skin)
            {
                Assert.That(skin.updateWhenOffscreen, Is.True, skin.name);
                Assert.That(skin.quality, Is.EqualTo(SkinQuality.Bone4), skin.name);
            }
        }
    }

    [Test]
    public void PlayerManager_PrepareSpawnedPlayer_PreservesSafeClumsyRagdoll()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Players/Player.prefab");
        Assert.That(playerPrefab, Is.Not.Null);

        GameObject managerObject = new GameObject("PlayerManager_Test");
        createdObjects.Add(managerObject);
        Component manager = managerObject.AddComponent(GetGameType("PlayerManager"));

        GameObject player = UnityEngine.Object.Instantiate(playerPrefab);
        createdObjects.Add(player);

        InvokeInstance(manager, "PrepareSpawnedPlayer", player);

        Component fakeRagdoll = player.GetComponent(GetGameType("FakeRagDoll"));
        Component wobble = player.GetComponent(GetGameType("PlayerVisualWobble"));
        Component movement = player.GetComponent(GetGameType("PlayerMovement"));

        Assert.That(fakeRagdoll, Is.Not.Null);
        Assert.That(wobble, Is.Not.Null);
        Assert.That(movement, Is.Not.Null);
        Assert.That(player.GetComponent(GetGameType("PlayerRagdoll")), Is.Null);
        Assert.That(player.GetComponent(GetGameType("PlayerImpactReaction")), Is.Null);

        Assert.That(GetPrivateField<bool>(movement, "allowImpactReactions"), Is.False);
        Assert.That(GetPrivateField<float>(fakeRagdoll, "hardHitThreshold"), Is.EqualTo(2.8f).Within(0.001f));
        Assert.That(GetPrivateField<float>(fakeRagdoll, "impactCooldown"), Is.EqualTo(0.35f).Within(0.001f));
        Assert.That(GetPrivateField<float>(fakeRagdoll, "maxTiltAmount"), Is.EqualTo(34f).Within(0.001f));
        Assert.That(GetPrivateField<float>(fakeRagdoll, "stunDuration"), Is.EqualTo(0.28f).Within(0.001f));
    }

    [Test]
    public void LobbyPlayerSessionData_RejectsDuplicateKeyboardSchemesAndMaxesAtFourPlayers()
    {
        Type sessionType = GetGameType("LobbyPlayerSessionData");

        Assert.That(TryRegisterKeyboardPlayer(sessionType, "KeyboardP1", out object keyboardP1), Is.True);
        Assert.That(GetProperty<int>(keyboardP1, "PlayerIndex"), Is.EqualTo(0));
        Assert.That(TryRegisterKeyboardPlayer(sessionType, "KeyboardP1", out _), Is.False);

        InputDevice pad1 = AddGamepad("Pad1");
        InputDevice pad2 = AddGamepad("Pad2");
        InputDevice pad3 = AddGamepad("Pad3");

        Assert.That(TryRegisterGamepad(sessionType, pad1, out object gamepad1), Is.True);
        Assert.That(TryRegisterGamepad(sessionType, pad1, out _), Is.False);
        Assert.That(TryRegisterKeyboardPlayer(sessionType, "KeyboardP2", out _), Is.True);
        Assert.That(TryRegisterGamepad(sessionType, pad2, out _), Is.True);
        Assert.That(TryRegisterGamepad(sessionType, pad3, out _), Is.False);

        Assert.That(GetProperty<int>(gamepad1, "PlayerIndex"), Is.EqualTo(1));
        Assert.That(GetStaticProperty<int>(sessionType, "Count"), Is.EqualTo(GetStaticField<int>(sessionType, "MaxPlayers")));
    }

    [Test]
    public void RuntimeNavMesh_PackageAndScriptsArePresent()
    {
        string manifest = System.IO.File.ReadAllText("Packages/manifest.json");
        Assert.That(manifest, Does.Contain("com.unity.ai.navigation"));

        Assert.That(GetGameType("RuntimeShipNavMesh"), Is.Not.Null);
        Assert.That(GetGameType("NavMeshSpawnUtility"), Is.Not.Null);
    }

    [Test]
    public void ClankPrefab_HasNavMeshAgent()
    {
        GameObject clankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NPCs/Clank_NPC/Clank_NPC_Prefab.prefab");
        Assert.That(clankPrefab, Is.Not.Null);

        Assert.That(clankPrefab.GetComponent<NavMeshAgent>(), Is.Not.Null);
        Assert.That(clankPrefab.GetComponent(GetGameType("Clank_NPC")), Is.Not.Null);
    }

    [Test]
    public void BoxSpawner_GuaranteesNavMeshObstacleOnSpawnedBoxes()
    {
        GameObject spawnerGO = new GameObject("BoxSpawner_Test");
        createdObjects.Add(spawnerGO);
        Component spawner = spawnerGO.AddComponent(GetGameType("BoxSpawner"));

        GameObject boxGO = new GameObject("Box_Test");
        createdObjects.Add(boxGO);
        var collider = boxGO.AddComponent<BoxCollider>();
        collider.size = new Vector3(1.25f, 1.5f, 1.25f);
        collider.center = new Vector3(0f, 0.75f, 0f);

        InvokeInstance(spawner, "EnsureObstacle", boxGO);

        NavMeshObstacle obstacle = boxGO.GetComponent<NavMeshObstacle>();
        Assert.That(obstacle, Is.Not.Null);
        Assert.That(obstacle.carving, Is.True);
        Assert.That(obstacle.carveOnlyStationary, Is.True);
        Assert.That(obstacle.shape, Is.EqualTo(NavMeshObstacleShape.Box));
        Assert.That(obstacle.size, Is.EqualTo(collider.size));
        Assert.That(obstacle.center, Is.EqualTo(collider.center));
    }

    [Test]
    public void NPCSpawning_UsesRuntimeNavMeshAndProceduralFallbacks()
    {
        string spawnManagerSource = System.IO.File.ReadAllText("Assets/Scripts/NPC/NPCSpawnManager.cs");
        Assert.That(spawnManagerSource, Does.Contain("RuntimeShipNavMesh.EnsureExists"));
        Assert.That(spawnManagerSource, Does.Contain("RuntimeShipNavMesh.OnReady"));
        Assert.That(spawnManagerSource, Does.Contain("NavMeshSpawnUtility.ResolvePosition"));
        Assert.That(spawnManagerSource, Does.Contain("HasAnyPrefab(blockiePrefabs)"));

        string coreXSource = System.IO.File.ReadAllText("Assets/Scripts/AI/CoreXBrain.cs");
        Assert.That(coreXSource, Does.Contain("ShipLayoutGenerator.RoomCenters"));
        Assert.That(coreXSource, Does.Contain("NavMeshSpawnUtility.TryWarpToNearest"));
    }

    [Test]
    public void EmergencyLighting_TracksStationFailuresAndAmbientInjector()
    {
        string emergencySource = System.IO.File.ReadAllText("Assets/Scripts/World/EmergencyLightController.cs");
        Assert.That(emergencySource, Does.Contain("RepairStation.OnStateChanged"));
        Assert.That(emergencySource, Does.Contain("stationEmergencyActive"));
        Assert.That(emergencySource, Does.Contain("IsStationEmergency"));

        string ambientSource = System.IO.File.ReadAllText("Assets/Scripts/World/ShipAmbientLightInjector.cs");
        Assert.That(ambientSource, Does.Contain("SpawnRoomLights"));
        Assert.That(ambientSource, Does.Contain("SpawnCorridorLights"));
        Assert.That(ambientSource, Does.Contain("RenderSettings.ambientLight"));

        string layoutSource = System.IO.File.ReadAllText("Assets/Scripts/World/ShipLayoutGenerator.cs");
        Assert.That(layoutSource, Does.Contain("EnsureAmbientLightInjector"));
        Assert.That(layoutSource, Does.Contain("AddComponent<ShipAmbientLightInjector>"));
    }

    [Test]
    public void RoamingNPCs_UseNavMeshAgentsAndRecovery()
    {
        string blockieSource = System.IO.File.ReadAllText("Assets/Scripts/NPC/Blockie_NPC/Blockie_NPC.cs");
        Assert.That(blockieSource, Does.Contain("NavMeshAgent"));
        Assert.That(blockieSource, Does.Contain("TryFindReachablePoint"));
        Assert.That(blockieSource, Does.Contain("UpdateStuckTimer"));

        string smoggosSource = System.IO.File.ReadAllText("Assets/Scripts/NPC/Smoggos_NPC/Smoggos_NPC.cs");
        Assert.That(smoggosSource, Does.Contain("NavMeshAgent"));
        Assert.That(smoggosSource, Does.Contain("TryFindReachablePoint"));
        Assert.That(smoggosSource, Does.Contain("UpdateStuckTimer"));

        string clankSource = System.IO.File.ReadAllText("Assets/Scripts/NPC/Clank_NPC/Clank_NPC.cs");
        Assert.That(clankSource, Does.Contain("TrySetReachableDestination"));
        Assert.That(clankSource, Does.Contain("TryGetCompletePathLength"));
        Assert.That(clankSource, Does.Contain("RecoverFromStuck"));
    }

    static void AssertUsableClip(string assetPath, string clipName)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(candidate => candidate.name == clipName);

        Assert.That(clip, Is.Not.Null, clipName);
        Assert.That(clip.empty, Is.False, $"{clipName} must contain animation data");
    }

    Component CreateComponent(string name, Type componentType)
    {
        var go = new GameObject(name);
        createdObjects.Add(go);
        return go.AddComponent(componentType);
    }

    InputDevice AddGamepad(string name)
    {
        InputDevice device = InputSystem.AddDevice<Gamepad>(name);
        createdDevices.Add(device);
        return device;
    }

    static void SetField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    static Type GetGameType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, typeName);
        return type;
    }

    static object InvokeInstance(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(target, args);
    }

    static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        return (T)field.GetValue(target);
    }

    static object InvokeStatic(Type type, string methodName, params object[] args)
    {
        if (type == null)
            return null;

        MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(null, args);
    }

    static T GetProperty<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null, propertyName);
        return (T)property.GetValue(target);
    }

    static T GetStaticProperty<T>(Type type, string propertyName)
    {
        PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null, propertyName);
        return (T)property.GetValue(null);
    }

    static T GetStaticField<T>(Type type, string fieldName)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        return (T)field.GetValue(null);
    }

    static bool TryRegisterKeyboardPlayer(Type sessionType, string controlScheme, out object entry)
    {
        object[] args = { controlScheme, null };
        bool result = (bool)InvokeStatic(sessionType, "TryRegisterKeyboardPlayer", args);
        entry = args[1];
        return result;
    }

    static bool TryRegisterGamepad(Type sessionType, InputDevice device, out object entry)
    {
        object[] args = { device, null };
        bool result = (bool)InvokeStatic(sessionType, "TryRegisterGamepad", args);
        entry = args[1];
        return result;
    }

    static Delegate CreateSingleArgHandler(Type eventHandlerType, RepairStationEventRecorder recorder, string methodName)
    {
        MethodInfo method = typeof(RepairStationEventRecorder).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        ParameterInfo[] parameters = eventHandlerType.GetMethod("Invoke").GetParameters();
        ParameterExpression station = Expression.Parameter(parameters[0].ParameterType, "station");
        MethodCallExpression body = Expression.Call(
            Expression.Constant(recorder),
            method,
            Expression.Convert(station, typeof(object)));
        return Expression.Lambda(eventHandlerType, body, station).Compile();
    }

    static Delegate CreateStateChangedHandler(Type eventHandlerType, RepairStationEventRecorder recorder)
    {
        MethodInfo method = typeof(RepairStationEventRecorder).GetMethod(nameof(RepairStationEventRecorder.StateChanged), BindingFlags.Instance | BindingFlags.Public);
        ParameterInfo[] parameters = eventHandlerType.GetMethod("Invoke").GetParameters();
        ParameterExpression station = Expression.Parameter(parameters[0].ParameterType, "station");
        ParameterExpression previous = Expression.Parameter(parameters[1].ParameterType, "previous");
        ParameterExpression next = Expression.Parameter(parameters[2].ParameterType, "next");
        MethodCallExpression body = Expression.Call(
            Expression.Constant(recorder),
            method,
            Expression.Convert(station, typeof(object)),
            Expression.Convert(previous, typeof(object)),
            Expression.Convert(next, typeof(object)));
        return Expression.Lambda(eventHandlerType, body, station, previous, next).Compile();
    }

    public sealed class RepairStationEventRecorder
    {
        readonly object station;

        public RepairStationEventRecorder(object station)
        {
            this.station = station;
        }

        public int BrokenCount { get; private set; }
        public int RepairedCount { get; private set; }
        public List<(string previous, string next)> Transitions { get; } = new();

        public void Broken(object changedStation)
        {
            if (ReferenceEquals(changedStation, station))
                BrokenCount++;
        }

        public void Repaired(object changedStation)
        {
            if (ReferenceEquals(changedStation, station))
                RepairedCount++;
        }

        public void StateChanged(object changedStation, object previous, object next)
        {
            if (ReferenceEquals(changedStation, station))
                Transitions.Add((previous.ToString(), next.ToString()));
        }
    }
}
