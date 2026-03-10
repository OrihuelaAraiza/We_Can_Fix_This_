using System.Collections.Generic;
using UnityEngine;

public static class RoleSelectionData
{
    static Dictionary<int, RoleDefinition> selections = new();

    public static void Save(Dictionary<int, RoleDefinition> data)
    {
        selections = new Dictionary<int, RoleDefinition>(data);
    }

    public static RoleDefinition GetRole(int playerIndex)
    {
        return selections.ContainsKey(playerIndex)
            ? selections[playerIndex] : null;
    }

    public static void Clear() => selections.Clear();
}
