using System.Collections;
using UnityEngine;

/// <summary>
/// Populates the 4 bottom player slots at runtime.
/// Waits 1 second so GameManager.ApplySelectedRoles() finishes first,
/// then reads PlayerRole directly from each active PlayerMovement.
/// Also re-populates whenever a role is assigned mid-session.
/// </summary>
public class PlayerSlotsUI : MonoBehaviour
{
    [SerializeField] PlayerSlot[] slots;   // 4 slots wired in HUDLayoutBuilder

    [Header("Style")]
    [SerializeField] UIStyleConfig style;

    void OnEnable()
    {
        PlayerRole.OnRoleAssigned          += HandleRoleAssigned;
        PlayerManager.OnPlayerCountChanged += PopulateSlots;
    }

    void OnDisable()
    {
        PlayerRole.OnRoleAssigned          -= HandleRoleAssigned;
        PlayerManager.OnPlayerCountChanged -= PopulateSlots;
    }

    IEnumerator Start()
    {
        // Give GameManager.ApplySelectedRoles() time to finish (it delays 0.5s)
        yield return new WaitForSeconds(1f);
        PopulateSlots();
    }

    void HandleRoleAssigned(PlayerRole _) => PopulateSlots();

    void PopulateSlots()
    {
        // Hide all slots first
        for (int i = 0; i < slots.Length; i++)
            slots[i]?.SetEmpty(style);

        // Find every active player and show their slot
        var players = FindObjectsOfType<PlayerMovement>();
        foreach (var player in players)
        {
            int idx = player.PlayerIndex;
            if (idx < 0 || idx >= slots.Length || slots[idx] == null) continue;

            var roleComp = player.GetComponent<PlayerRole>();
            string roleName = roleComp?.Role?.roleName ?? "CREW";

            slots[idx].SetPlayer(idx, roleName, null, style);
        }
    }

    // ── Public API (called by external systems) ──────────────────

    public void SetPlayerRole(int playerIndex, string roleName, UnityEngine.Sprite icon)
    {
        if (playerIndex < 0 || playerIndex >= slots.Length) return;
        slots[playerIndex]?.SetPlayer(playerIndex, roleName, icon, style);
    }

    public void SetPlayerEmpty(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= slots.Length) return;
        slots[playerIndex]?.SetEmpty(style);
    }
}
