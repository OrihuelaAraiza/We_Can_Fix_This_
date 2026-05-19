using System;
using UnityEngine;

namespace Wcft.Core
{
    public sealed class LevelDefinition
    {
        public LevelDefinition(
            int index,
            string name,
            float durationSeconds,
            int extraRoomBudget,
            int extraRoomChainDepth,
            float failureRateMultiplier,
            int maxSimultaneousFailures,
            float npcMultiplier,
            float clankBoxIntervalSeconds)
        {
            Index = index;
            Name = name;
            DurationSeconds = durationSeconds;
            ExtraRoomBudget = extraRoomBudget;
            ExtraRoomChainDepth = extraRoomChainDepth;
            FailureRateMultiplier = failureRateMultiplier;
            MaxSimultaneousFailures = maxSimultaneousFailures;
            NpcMultiplier = npcMultiplier;
            ClankBoxIntervalSeconds = clankBoxIntervalSeconds;
        }

        public int Index { get; }
        public string Name { get; }
        public float DurationSeconds { get; }
        public int ExtraRoomBudget { get; }
        public int ExtraRoomChainDepth { get; }
        public float FailureRateMultiplier { get; }
        public int MaxSimultaneousFailures { get; }
        public float NpcMultiplier { get; }
        public float ClankBoxIntervalSeconds { get; }
    }

    public static class LevelProgression
    {
        static readonly LevelDefinition[] Definitions =
        {
            new LevelDefinition(1, "Level 1", 150f, 3, 1, 1.00f, 3, 1.0f, 25f),
            new LevelDefinition(2, "Level 2", 150f, 5, 2, 1.35f, 4, 1.5f, 18f),
            new LevelDefinition(3, "Level 3", 150f, 7, 3, 1.75f, 5, 2.0f, 12f),
        };

        static int currentDefinitionIndex;

        public static LevelDefinition Current => Definitions[currentDefinitionIndex];
        public static bool IsFinalLevel => currentDefinitionIndex >= Definitions.Length - 1;
        public static int LevelCount => Definitions.Length;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntimeState()
        {
            Reset();
        }

        /// <summary>
        /// Returns true when progression advanced to another level. Returns false when the demo is complete.
        /// </summary>
        public static bool AdvanceOrComplete()
        {
            if (IsFinalLevel)
                return false;

            currentDefinitionIndex = Mathf.Min(currentDefinitionIndex + 1, Definitions.Length - 1);
            return true;
        }

        public static void Reset()
        {
            currentDefinitionIndex = 0;
        }

        public static void SetCurrentLevelForTesting(int levelIndex)
        {
            if (levelIndex < 1 || levelIndex > Definitions.Length)
                throw new ArgumentOutOfRangeException(nameof(levelIndex), levelIndex, "Level index is outside the demo definition range.");

            currentDefinitionIndex = levelIndex - 1;
        }
    }
}
