/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace Match;

public static class EntityHelper
{
    public static CCSGameRules? GetGameRules()
    {
        return Utilities
            .FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules")
            .FirstOrDefault()
            ?.GameRules;
    }

    public static IEnumerable<CCSPlayerPawn> GetAlivePawnsInTeam(CsTeam team)
    {
        return Utilities
            .FindAllEntitiesByDesignerName<CCSPlayerPawn>("player")
            .Where(pawn => pawn.LifeState == 0 && pawn.TeamNum == (byte)team);
    }
}
