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

        // Aplicar multiplicador de velocidad
        if (movement != null)
            movement.SetSpeedMultiplier(currentRole.moveSpeedMultiplier);

        // Aplicar multiplicador de reparación
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

    // ── Implementaciones de habilidades ──────────────────────────

    void ExecuteRemoteRepair()
    {
        // Encuentra la estación rota más cercana y avanza su reparación
        var stations = FindObjectsOfType<RepairStation>();
        RepairStation closest = null;
        float minDist = float.MaxValue;

        foreach (var s in stations)
        {
            if (s.State != RepairStation.StationState.Broken) continue;
            float d = Vector3.Distance(transform.position, s.transform.position);
            if (d < minDist) { minDist = d; closest = s; }
        }

        if (closest != null)
        {
            closest.ApplyRemoteRepairBoost(0.3f); // avanza 30% la reparación
            Debug.Log($"[Hacker] Remote repair on {closest.Type}");
        }
    }

    void ExecuteTeamSpeedBoost()
    {
        // Boost de velocidad a todos los jugadores cercanos por 8 segundos
        var players = FindObjectsOfType<PlayerMovement>();
        foreach (var p in players)
        {
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < 15f)
                p.ApplyTemporarySpeedBoost(1.5f, 8f);
        }
        Debug.Log("[Comandante] Team speed boost activated");
    }

    void ExecuteElectricBomb()
    {
        // Desactiva todos los NPCs cercanos por 5 segundos
        // Compatible con sistema de NPCs cuando estén implementados
        Debug.Log("[Saboteador] Electric bomb — NPCs disabled (pending NPC system)");
        // Los NPCs deben escuchar: PlayerRole.OnAbilityUsed
        // y verificar si ability == ElectricBomb
    }

    void ExecuteResetStation()
    {
        // Encuentra estación más cercana y resetea su degradación
        var stations = FindObjectsOfType<RepairStation>();
        RepairStation closest = null;
        float minDist = float.MaxValue;

        foreach (var s in stations)
        {
            float d = Vector3.Distance(transform.position, s.transform.position);
            if (d < minDist) { minDist = d; closest = s; }
        }

        if (closest != null)
        {
            closest.ResetDegradation();
            Debug.Log($"[Mecánico] Station reset: {closest.Type}");
        }
    }

    void ExecutePreviewAttack()
    {
        if (FailureSystem.Instance != null)
        {
            Debug.Log("[Comandante] Preview next attack — " +
                      "próxima implementación con CoreXBrain");
        }
    }
}
