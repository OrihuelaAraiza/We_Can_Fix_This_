using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class LevelProgressionTests
{
    static Type LevelProgressionType => Type.GetType("Wcft.Core.LevelProgression, Assembly-CSharp");
    static Type FailureSystemType => Type.GetType("FailureSystem, Assembly-CSharp");

    [SetUp]
    public void SetUp()
    {
        Assert.That(LevelProgressionType, Is.Not.Null, "LevelProgression type");
        Assert.That(FailureSystemType, Is.Not.Null, "FailureSystem type");
    }

    [TearDown]
    public void TearDown()
    {
        ResetLevelProgression();
    }

    [Test]
    public void LevelProgression_AdvancesThroughThreeDemoLevelsThenCompletes()
    {
        ResetLevelProgression();

        Assert.That(GetCurrentLevelMember<int>("Index"), Is.EqualTo(1));
        Assert.That(GetCurrentLevelMember<float>("DurationSeconds"), Is.EqualTo(150f));

        Assert.That(AdvanceOrComplete(), Is.True);
        Assert.That(GetCurrentLevelMember<int>("Index"), Is.EqualTo(2));

        Assert.That(AdvanceOrComplete(), Is.True);
        Assert.That(GetCurrentLevelMember<int>("Index"), Is.EqualTo(3));
        Assert.That(GetStaticProperty<bool>("IsFinalLevel"), Is.True);

        Assert.That(AdvanceOrComplete(), Is.False);
        Assert.That(GetCurrentLevelMember<int>("Index"), Is.EqualTo(3));

        ResetLevelProgression();
        Assert.That(GetCurrentLevelMember<int>("Index"), Is.EqualTo(1));
    }

    [Test]
    public void FailureSystem_AppliesCurrentLevelMaxSimultaneousFailures()
    {
        InvokeStatic("SetCurrentLevelForTesting", 3);

        UnityEngine.Object existing = UnityEngine.Object.FindObjectOfType(FailureSystemType);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(((Component)existing).gameObject);

        var go = new GameObject("FailureSystem_Test");
        try
        {
            Component failureSystem = go.AddComponent(FailureSystemType);
            Assert.That(GetMember<int>(failureSystem, "MaxSimultaneousBroken"), Is.EqualTo(GetCurrentLevelMember<int>("MaxSimultaneousFailures")));

            InvokeInstance(failureSystem, "SetMaxSimultaneousBroken", 2);
            Assert.That(GetMember<int>(failureSystem, "MaxSimultaneousBroken"), Is.EqualTo(2));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static void ResetLevelProgression()
    {
        InvokeStatic("Reset");
    }

    static bool AdvanceOrComplete()
    {
        return (bool)InvokeStatic("AdvanceOrComplete");
    }

    static T GetCurrentLevelMember<T>(string memberName)
    {
        return GetMember<T>(GetStaticProperty<object>("Current"), memberName);
    }

    static T GetStaticProperty<T>(string propertyName)
    {
        PropertyInfo property = LevelProgressionType.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null, propertyName);
        return (T)property.GetValue(null);
    }

    static object InvokeStatic(string methodName, params object[] args)
    {
        MethodInfo method = LevelProgressionType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(null, args);
    }

    static object InvokeInstance(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(target, args);
    }

    static T GetMember<T>(object target, string memberName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo property = target.GetType().GetProperty(memberName, flags);
        if (property != null)
            return (T)property.GetValue(target);

        FieldInfo field = target.GetType().GetField(memberName, flags);
        if (field != null)
            return (T)field.GetValue(target);

        Assert.Fail($"Member '{memberName}' not found on {target.GetType().FullName}");
        return default;
    }
}
