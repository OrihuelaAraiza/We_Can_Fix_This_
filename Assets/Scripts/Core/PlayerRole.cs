using UnityEngine;

public class PlayerRole : MonoBehaviour
{
    [Header("Current Role")]
    [SerializeField] RoleDefinition currentRole;

    [Header("Runtime")]
    [SerializeField] float  abilityCooldownTimer = 0f;
    [SerializeField] bool   abilityReady         = true;

    // Referencias
    PlayerMovement   movement;
    PlayerInteract   interact;

    public RoleDefinition Role         => currentRole;
    public bool           AbilityReady => abilityReady;
    public float AbilityCooldownNormalized =>
        currentRole != null ? abilityCooldownTimer / currentRole.abilityCooldown : 0f;

    public static event System.Action<PlayerRole> OnAbilityUsed;
    public static event System.Action<PlayerRole> OnRoleAssigned;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        interact = GetComponent<PlayerInteract>();
    }

    void Update()
    {
        if (!abilityReady)
        {
            abilityCooldownTimer -= Time.deltaTime;
            if (abilityCooldownTimer <= 0f)
            {
                abilityCooldownTimer = 0f;
                abilityReady = true;
                Debug.Log($"[PlayerRole] Ability ready for {currentRole?.roleName}");
            }
        }
    }

    public void AssignRole(RoleDefinition role)
    {
        currentRole = role;
        ApplyRoleStats();
        OnRoleAssigned?.Invoke(this);
        Debug.Log($"[PlayerRole] Role assigned: {role.roleName}");
    }

    void ApplyRoleStats()
    {
        if (currentRole == null) return;

        if (movement != null)
            movement.SetSpeedMultiplier(currentRole.moveSpeedMultiplier);

        if (interact != null)
            interact.SetRepairMultiplier(currentRole.repairSpeedMultiplier);
    }

    public void UseAbility()
    {
        if (!abilityReady || currentRole == null) return;
        if (currentRole.ability == RoleAbility.None) return;

        ExecuteAbility(currentRole.ability);

        abilityReady         = false;
        abilityCooldownTimer = currentRole.abilityCooldown;
        OnAbilityUsed?.Invoke(this);
    }

    void ExecuteAbility(RoleAbility ability)
    {
        switch (ability)
        {
            case RoleAbility.RemoteRepair:
                ExecuteRemoteRepair();
                break;
            case RoleAbility.TeamSpeedBoost:
                ExecuteTeamSpeedBoost();
                break;
            case RoleAbility.ElectricBomb:
                ExecuteElectricBomb();
                break;
            case RoleAbility.ResetStation:
                ExecuteResetStation();
                break;
            case RoleAbility.TurretAnywhere:
                Debug.Log("[PlayerRole] TurretAnywhere — pendiente torreta");
                break;
            case RoleAbility.PreviewNextAttack:
                ExecutePreviewAttack();
                break;
        }
    }

    // ── Ability implementations ──────────────────────────────────

    void ExecuteRemoteRepair()
    {
        // Find the nearest broken station and advance its repair
        RepairStation closest = null;
        float minDist = float.MaxValue;

        foreach (var s in RepairStation.ActiveStations)
        {
            if (s == null) continue;
            if (s.State != RepairStation.StationState.Broken) continue;
            float d = Vector3.Distance(transform.position, s.transform.position);
            if (d < minDist) { minDist = d; closest = s; }
        }

        if (closest != null)
        {
            closest.ApplyRemoteRepairBoost(0.3f);
            Debug.Log($"[Hacker] Remote repair on {closest.Type}");
        }
    }

    void ExecuteTeamSpeedBoost()
    {
        // Speed boost to all nearby players for 8 seconds
        var players = FindObjectsOfType<PlayerMovement>();
        foreach (var p in players)
        {
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < 15f)
                p.ApplyTemporarySpeedBoost(1.5f, 8f);
        }
        Debug.Log("[Commander] Team speed boost activated");
    }

    void ExecuteElectricBomb()
    {
        const float bombRadius   = 15f;
        const float disableSecs  = 5f;

        var npcs = FindObjectsOfType<NPCBehaviourBase>();
        int affected = 0;

        foreach (var npc in npcs)
        {
            float dist = Vector3.Distance(transform.position, npc.transform.position);
            if (dist <= bombRadius)
            {
                npc.Disable(disableSecs);
                affected++;
            }
        }

        Debug.Log($"[Saboteur] Electric bomb — {affected} NPCs disabled for {disableSecs}s");
    }

    void ExecuteResetStation()
    {
        // Find nearest station and reset its degradation
        RepairStation closest = null;
        float minDist = float.MaxValue;

        foreach (var s in RepairStation.ActiveStations)
        {
            if (s == null) continue;
            float d = Vector3.Distance(transform.position, s.transform.position);
            if (d < minDist) { minDist = d; closest = s; }
        }

        if (closest != null)
        {
            closest.ResetDegradation();
            Debug.Log($"[Mechanic] Station reset: {closest.Type}");
        }
    }

    void ExecutePreviewAttack()
    {
        if (FailureSystem.Instance != null)
        {
            Debug.Log("[Commander] Preview next attack — upcoming implementation with CoreXBrain");
        }
    }
}
