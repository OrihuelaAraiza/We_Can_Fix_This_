using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Slot de HUD individual por jugador.
/// Se activa cuando el jugador se une y muestra su rol + cooldown.
/// Colocar uno en cada esquina del canvas (4 total).
/// </summary>
public class PlayerHUDSlot : MonoBehaviour
{
    [Header("Referencias UI")]
    [SerializeField] TextMeshProUGUI playerLabel;
    [SerializeField] TextMeshProUGUI roleNameText;
    [SerializeField] TextMeshProUGUI abilityNameText;
    [SerializeField] TextMeshProUGUI abilityCooldownText;
    [SerializeField] Image           abilityFill;
    [SerializeField] Image           roleBorderColor;
    [SerializeField] GameObject      emptySlotIndicator;

    [Header("Config")]
    [SerializeField] int playerIndex = 0;

    PlayerRole trackedRole;

    // ─────────────────────────────────────────────────────────────
    void OnEnable()
    {
        PlayerRole.OnRoleAssigned += OnRoleAssigned;
    }

    void OnDisable()
    {
        PlayerRole.OnRoleAssigned -= OnRoleAssigned;
    }

    void Start()
    {
        SetEmpty();
    }

    void Update()
    {
        if (trackedRole == null) return;
        UpdateCooldown();
    }

    // ─────────────────────────────────────────────────────────────

    void OnRoleAssigned(PlayerRole role)
    {
        var movement = role.GetComponent<PlayerMovement>();
        if (movement == null || movement.PlayerIndex != playerIndex) return;

        trackedRole = role;
        ApplyRoleDisplay(role);
    }

    void ApplyRoleDisplay(PlayerRole role)
    {
        if (role.Role == null) return;

        if (emptySlotIndicator != null) emptySlotIndicator.SetActive(false);

        if (playerLabel != null)
            playerLabel.text = $"P{playerIndex + 1}";

        if (roleNameText != null)
        {
            roleNameText.text  = role.Role.roleName.ToUpper();
            roleNameText.color = role.Role.roleColor;
        }

        if (abilityNameText != null)
        {
            string abilityLabel = role.Role.ability == RoleAbility.None
                ? "Sin habilidad"
                : role.Role.ability.ToString();
            abilityNameText.text = $"[Q] {abilityLabel}";
        }

        if (roleBorderColor != null)
            roleBorderColor.color = role.Role.roleColor;

        if (abilityFill != null)
            abilityFill.fillAmount = 1f;
    }

    void UpdateCooldown()
    {
        if (trackedRole.Role == null) return;

        bool  ready      = trackedRole.AbilityReady;
        float normalized = trackedRole.AbilityCooldownNormalized;

        if (abilityFill != null)
            abilityFill.fillAmount = ready ? 1f : 1f - normalized;

        if (abilityCooldownText != null)
        {
            if (trackedRole.Role.ability == RoleAbility.None)
                abilityCooldownText.text = "";
            else if (ready)
                abilityCooldownText.text = "LISTO";
            else
            {
                float seconds = normalized * trackedRole.Role.abilityCooldown;
                abilityCooldownText.text = $"{seconds:F0}s";
            }
        }
    }

    void SetEmpty()
    {
        if (emptySlotIndicator   != null) emptySlotIndicator.SetActive(true);
        if (roleNameText         != null) roleNameText.text = "";
        if (abilityNameText      != null) abilityNameText.text = "";
        if (abilityCooldownText  != null) abilityCooldownText.text = "";
        if (abilityFill          != null) abilityFill.fillAmount = 0f;
    }
}
