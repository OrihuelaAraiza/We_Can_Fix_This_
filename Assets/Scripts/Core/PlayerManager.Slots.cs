using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerManager
{
    bool TryReservePlayerSlot(PlayerInput playerInput, out int slotIndex)
    {
        slotIndex = -1;

        if (playerInput == null)
            return false;

        if (activePlayerSlots.Count + pendingPlayerSlots.Count >= MaxSupportedPlayers)
            return false;

        if (HasConflictingDevice(playerInput))
            return false;

        slotIndex = ResolveSlotIndex(playerInput);
        if (slotIndex < 0)
            return false;

        pendingPlayerSlots[playerInput] = slotIndex;
        return true;
    }

    int ResolveSlotIndex(PlayerInput playerInput)
    {
        LobbyPlayerSessionEntry sessionEntry = FindMatchingSessionEntry(playerInput);
        if (sessionEntry != null && !IsSlotReserved(sessionEntry.PlayerIndex))
            return sessionEntry.PlayerIndex;

        if (playerInput.playerIndex >= 0 && playerInput.playerIndex < MaxSupportedPlayers && !IsSlotReserved(playerInput.playerIndex))
            return playerInput.playerIndex;

        for (int i = 0; i < MaxSupportedPlayers; i++)
        {
            if (!IsSlotReserved(i))
                return i;
        }

        return -1;
    }

    LobbyPlayerSessionEntry FindMatchingSessionEntry(PlayerInput playerInput)
    {
        if (!LobbyPlayerSessionData.HasEntries)
            return null;

        string controlScheme = playerInput.currentControlScheme;
        int primaryDeviceId = GetPrimaryDeviceId(playerInput);

        foreach (LobbyPlayerSessionEntry entry in LobbyPlayerSessionData.Entries)
        {
            if (entry.ControlScheme != controlScheme)
                continue;

            if (entry.IsKeyboard || entry.DeviceId == primaryDeviceId)
                return entry;
        }

        return null;
    }

    bool IsSlotReserved(int slotIndex)
    {
        foreach (int pendingSlot in pendingPlayerSlots.Values)
        {
            if (pendingSlot == slotIndex)
                return true;
        }

        foreach (int activeSlot in activePlayerSlots.Values)
        {
            if (activeSlot == slotIndex)
                return true;
        }

        return false;
    }

    bool HasConflictingDevice(PlayerInput playerInput)
    {
        foreach (PlayerInput pending in pendingPlayerSlots.Keys)
        {
            if (HasDeviceConflict(pending, playerInput))
                return true;
        }

        foreach (PlayerInput active in activePlayerSlots.Keys)
        {
            if (HasDeviceConflict(active, playerInput))
                return true;
        }

        return false;
    }

    bool HasDeviceConflict(PlayerInput existing, PlayerInput incoming)
    {
        if (existing == null || incoming == null)
            return false;

        int existingDeviceId = GetPrimaryDeviceId(existing);
        int incomingDeviceId = GetPrimaryDeviceId(incoming);

        if (existingDeviceId == InputDevice.InvalidDeviceId || incomingDeviceId == InputDevice.InvalidDeviceId)
            return false;

        if (existingDeviceId != incomingDeviceId)
            return false;

        return !CanShareDevice(existing, incoming);
    }

    static bool CanShareDevice(PlayerInput left, PlayerInput right)
    {
        if (left == null || right == null)
            return false;

        return left.currentControlScheme == "KeyboardP1" && right.currentControlScheme == "KeyboardP2"
            || left.currentControlScheme == "KeyboardP2" && right.currentControlScheme == "KeyboardP1";
    }

    static int GetPrimaryDeviceId(PlayerInput playerInput)
    {
        return playerInput != null && playerInput.devices.Count > 0
            ? playerInput.devices[0].deviceId
            : InputDevice.InvalidDeviceId;
    }

    static string GetDeviceDisplayName(PlayerInput playerInput)
    {
        return playerInput != null && playerInput.devices.Count > 0
            ? playerInput.devices[0].displayName
            : "Unknown";
    }

    void RejectJoin(PlayerInput playerInput, string reason)
    {
        Debug.LogWarning($"[PlayerManager] Rejecting player join. {reason}");
        ReleasePlayerReservation(playerInput);

        if (playerInput != null)
            Destroy(playerInput.gameObject);

        UpdateJoinAvailability();
    }

    void ReleasePlayerReservation(PlayerInput playerInput)
    {
        if (playerInput == null)
            return;

        pendingPlayerSlots.Remove(playerInput);
        activePlayerSlots.Remove(playerInput);
    }
}
