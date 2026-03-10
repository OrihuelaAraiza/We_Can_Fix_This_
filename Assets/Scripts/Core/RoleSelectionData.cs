using System.Collections.Generic;
using UnityEngine;

public static class RoleSelectionData
{
    static Dictionary<int, RoleDefinition> selections = new();

    public static void Save(Dictionary<int, RoleDefinition> data)
    {
        selections = new Dictionary<int, RoleDefinition>(data);
        foreach (var kv in data)
            Debug.Log($"[RoleData] Saved: P{kv.Key} = {kv.Value?.roleName ?? "NULL"}");
    }

    public static RoleDefinition GetRole(int playerIndex)
    {
        var role = selections.ContainsKey(playerIndex) ? selections[playerIndex] : null;
        Debug.Log($"[RoleData] GetRole P{playerIndex} = {role?.roleName ?? "NULL"}");
        return role;
    }

    public static void Clear() => selections.Clear();
}
