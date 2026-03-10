using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    [Header("Config")]
    [SerializeField] string gameplaySceneName = "02_Gameplay";
    [SerializeField] int    minPlayersToStart = 1;

    [Header("Roles disponibles")]
    [SerializeField] List<RoleDefinition> availableRoles = new();

    // Estado de selección por jugador (playerIndex → role)
    Dictionary<int, RoleDefinition> selectedRoles = new();
    Dictionary<int, bool>           readyPlayers  = new();
    int connectedPlayers = 0;

    public static event System.Action<int, RoleDefinition> OnPlayerRoleSelected;
    public static event System.Action<int, bool>           OnPlayerReadyChanged;
    public static event System.Action                      OnAllPlayersReady;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public List<RoleDefinition> GetAvailableRoles() => availableRoles;

    public int GetConnectedPlayerCount() => connectedPlayers;

    public RoleDefinition GetRoleByIndex(int roleIndex)
    {
        if (roleIndex < 0 || roleIndex >= availableRoles.Count) return null;
        return availableRoles[roleIndex];
    }

    public void RegisterPlayer(int playerIndex)
    {
        connectedPlayers++;
        selectedRoles[playerIndex] = availableRoles.Count > 0
            ? availableRoles[0] : null;
        readyPlayers[playerIndex] = false;
        Debug.Log($"[Lobby] Player {playerIndex} registered. Total: {connectedPlayers}");
    }

    public void SelectRole(int playerIndex, RoleDefinition role)
    {
        if (!selectedRoles.ContainsKey(playerIndex)) return;
        selectedRoles[playerIndex] = role;
        readyPlayers[playerIndex]  = false; // reset ready al cambiar rol
        OnPlayerRoleSelected?.Invoke(playerIndex, role);
        Debug.Log($"[Lobby] P{playerIndex} selected: {role.roleName}");
    }

    public void SetPlayerReady(int playerIndex, bool ready)
    {
        if (!readyPlayers.ContainsKey(playerIndex)) return;
        readyPlayers[playerIndex] = ready;
        OnPlayerReadyChanged?.Invoke(playerIndex, ready);

        if (ready) CheckAllReady();
    }

    public void ToggleReady(int playerIndex)
    {
        bool current = readyPlayers.ContainsKey(playerIndex)
            && readyPlayers[playerIndex];
        SetPlayerReady(playerIndex, !current);
    }

    public RoleDefinition GetSelectedRole(int playerIndex)
    {
        return selectedRoles.ContainsKey(playerIndex)
            ? selectedRoles[playerIndex] : null;
    }

    public bool IsPlayerReady(int playerIndex)
    {
        return readyPlayers.ContainsKey(playerIndex)
            && readyPlayers[playerIndex];
    }

    void CheckAllReady()
    {
        if (connectedPlayers < minPlayersToStart) return;

        foreach (var kv in readyPlayers)
            if (!kv.Value) return;

        OnAllPlayersReady?.Invoke();
        Debug.Log("[Lobby] All players ready! Starting game...");
        Invoke(nameof(LoadGameplay), 1.5f);
    }

    void LoadGameplay()
    {
        // Guardar roles seleccionados para que GameManager los aplique
        RoleSelectionData.Save(selectedRoles);
        SceneManager.LoadScene(gameplaySceneName);
    }
}
