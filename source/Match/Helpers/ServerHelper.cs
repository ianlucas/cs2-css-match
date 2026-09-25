/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Utils;

namespace Match;

public static class ServerHelper
{
    public static void ExecuteCommand(List<string> commands)
    {
        foreach (var command in commands)
            Server.ExecuteCommand(command);
    }

    public static void SetTeamName(CsTeam team, string name)
    {
        var index = team == CsTeam.CounterTerrorist ? 1 : 2;
        Server.ExecuteCommand($"mp_teamname_{index} {name}");
    }
}
