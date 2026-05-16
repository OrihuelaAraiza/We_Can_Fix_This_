using UnityEngine;

[CreateAssetMenu(fileName = "RoleDefinition",
                 menuName = "WeCF/Role Definition")]
public class RoleDefinition : ScriptableObject
{
    [Header("Identity")]
    public string roleName;
    public string description;
    public Color  roleColor;
    [TextArea(2,4)] public string perkDescription;
    [TextArea(2,4)] public string penaltyDescription;

    [Header("Movement")]
    [Range(0.5f, 2f)] public float moveSpeedMultiplier   = 1f;

    [Header("Repair")]
    [Range(0.5f, 2f)] public float repairSpeedMultiplier = 1f;
    public bool canRepairWhileMoving = false;
    public bool cannotRepairEnergy   = false;
    public float repairDurabilityBonus = 0f; // 0.5 = +50% durability

    [Header("Special Ability")]
    public RoleAbility ability;
    [Range(10f, 120f)] public float abilityCooldown = 45f;

    [Header("Penalties")]
    [Range(0f, 1f)] public float shipHealthPenaltyOnKnockout = 0f;
    public bool immuneToNPCPush = false;
    public bool cannotRepairManually = false;
}

public enum RoleAbility
{
    None,
    RemoteRepair,       // Hacker — repairs a remote station
    TurretAnywhere,     // Gunner — activates turret from anywhere
    ResetStation,       // Mechanic — resets station cooldown
    TeamSpeedBoost,     // Commander — speed boost to nearby players
    PreviewNextAttack,  // Commander — previews next targeted room
    ElectricBomb        // Saboteur — disables drones
}
