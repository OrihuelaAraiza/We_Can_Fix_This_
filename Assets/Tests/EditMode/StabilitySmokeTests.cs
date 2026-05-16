using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

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
