using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Displays the active role and ability cooldown for each player
public class RoleHUDElement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] TextMeshProUGUI roleNameText;
    [SerializeField] TextMeshProUGUI abilityStatusText;
    [SerializeField] Image           cooldownFill;
    [SerializeField] Image           roleColorIndicator;

    PlayerRole trackedRole;

    public void Initialize(PlayerRole role)
    {
        trackedRole = role;
        if (role.Role == null) return;

        if (roleNameText != null)
            roleNameText.text = role.Role.roleName.ToUpper();

        if (roleColorIndicator != null)
            roleColorIndicator.color = role.Role.roleColor;
    }

    void Update()
    {
        if (trackedRole == null) return;

        // Cooldown fill
        if (cooldownFill != null)
            cooldownFill.fillAmount = trackedRole.AbilityReady
                ? 1f
                : 1f - trackedRole.AbilityCooldownNormalized;

        if (abilityStatusText != null)
            abilityStatusText.text = trackedRole.AbilityReady
                ? "Q — READY"
                : $"COOLDOWN {trackedRole.AbilityCooldownNormalized * trackedRole.Role.abilityCooldown:F0}s";
    }
}
