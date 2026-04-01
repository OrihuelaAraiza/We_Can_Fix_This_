using System.Collections.Generic;
using UnityEngine.InputSystem;

public static class LobbyPlayerSessionData
{
    public const int MaxPlayers = 4;

    static readonly List<LobbyPlayerSessionEntry> entries = new();

    public static IReadOnlyList<LobbyPlayerSessionEntry> Entries => entries;
    public static bool HasEntries => entries.Count > 0;
    public static int Count => entries.Count;

    public static void Reset()
    {
        entries.Clear();
    }

    public static bool HasControlScheme(string controlScheme)
    {
        if (string.IsNullOrWhiteSpace(controlScheme))
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].ControlScheme == controlScheme)
                return true;
        }

        return false;
    }

    public static bool HasGamepadDevice(int deviceId)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            LobbyPlayerSessionEntry entry = entries[i];
            if (!entry.IsKeyboard && entry.DeviceId == deviceId)
                return true;
        }

        return false;
    }

    public static bool TryRegisterKeyboardPlayer(string controlScheme, out LobbyPlayerSessionEntry entry)
    {
        entry = null;

        if (entries.Count >= MaxPlayers || HasControlScheme(controlScheme))
            return false;

        entry = new LobbyPlayerSessionEntry(
            GetNextPlayerIndex(),
            controlScheme,
            Keyboard.current != null ? Keyboard.current.deviceId : InputDevice.InvalidDeviceId,
            true,
            Keyboard.current != null ? Keyboard.current.displayName : "Keyboard"
        );

        entries.Add(entry);
        entries.Sort((left, right) => left.PlayerIndex.CompareTo(right.PlayerIndex));
        return true;
    }

    public static bool TryRegisterGamepad(InputDevice device, out LobbyPlayerSessionEntry entry)
    {
        entry = null;

        if (device == null || !(device is Gamepad))
            return false;

        if (entries.Count >= MaxPlayers || HasGamepadDevice(device.deviceId))
            return false;

        entry = new LobbyPlayerSessionEntry(
            GetNextPlayerIndex(),
            "Gamepad",
            device.deviceId,
            false,
            device.displayName
        );

        entries.Add(entry);
        entries.Sort((left, right) => left.PlayerIndex.CompareTo(right.PlayerIndex));
        return true;
    }

    static int GetNextPlayerIndex()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            bool slotTaken = false;
            for (int j = 0; j < entries.Count; j++)
            {
                if (entries[j].PlayerIndex == i)
                {
                    slotTaken = true;
                    break;
                }
            }

            if (!slotTaken)
                return i;
        }

        return -1;
    }
}

public sealed class LobbyPlayerSessionEntry
{
    public int PlayerIndex { get; }
    public string ControlScheme { get; }
    public int DeviceId { get; }
    public bool IsKeyboard { get; }
    public string DeviceDisplayName { get; }

    public LobbyPlayerSessionEntry(int playerIndex, string controlScheme, int deviceId, bool isKeyboard, string deviceDisplayName)
    {
        PlayerIndex = playerIndex;
        ControlScheme = controlScheme;
        DeviceId = deviceId;
        IsKeyboard = isKeyboard;
        DeviceDisplayName = deviceDisplayName;
    }
}
