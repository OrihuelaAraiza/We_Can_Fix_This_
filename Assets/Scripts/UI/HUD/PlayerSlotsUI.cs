using System.Collections;
using UnityEngine;

public class PlayerSlotsUI : MonoBehaviour
{
    [SerializeField] PlayerSlot[] slots;

    [Header("Style")]
    [SerializeField] UIStyleConfig style;

    IEnumerator Start()
    {
        // Wait for GameManager.ApplySelectedRoles to finish
        yield return new WaitForSeconds(0.8f);
        PopulateSlots();
    }

    void PopulateSlots()
    {
        var players = FindObjectsOfType<PlayerMovement>();

        // Mark all slots empty first
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].SetEmpty(style);
        }

        // Fill slots from found players
        foreach (var player in players)
        {
            int idx = player.PlayerIndex;
            if (idx < 0 || idx >= slots.Length || slots[idx] == null) continue;

            string roleName = "CREW";
            Sprite roleIcon = null;

            // Try to read PlayerRole component if it exists
            var roleComp = player.GetComponent<MonoBehaviour>();
            // Use reflection-free approach: check for PlayerRole by name
            foreach (var comp in player.GetComponents<MonoBehaviour>())
            {
                if (comp.GetType().Name == "PlayerRole")
                {
                    // Try to get role name via property
                    var roleProp = comp.GetType().GetProperty("Role");
                    if (roleProp != null)
                    {
                        var roleObj = roleProp.GetValue(comp);
                        if (roleObj != null)
                        {
                            var nameField = roleObj.GetType().GetField("roleName");
                            if (nameField != null)
                                roleName = (string)nameField.GetValue(roleObj) ?? "CREW";

                            var iconField = roleObj.GetType().GetField("roleIcon");
                            if (iconField != null)
                                roleIcon = iconField.GetValue(roleObj) as Sprite;
                        }
                    }
                    break;
                }
            }

            slots[idx].SetPlayer(idx, roleName, roleIcon, style);
        }
    }

    // ── Public API ──────────────────────────────────────────────

    public void SetPlayerRole(int playerIndex, string roleName, Sprite icon)
    {
        if (playerIndex < 0 || playerIndex >= slots.Length) return;
        if (slots[playerIndex] != null)
            slots[playerIndex].SetPlayer(playerIndex, roleName, icon, style);
    }

    public void SetPlayerEmpty(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= slots.Length) return;
        if (slots[playerIndex] != null)
            slots[playerIndex].SetEmpty(style);
    }
}
