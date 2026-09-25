/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace Match;

public static class CCSGameRulesExtensions
{
    public static bool AreTeamsPlayingSwitchedSides(this CCSGameRules self)
    {
        return Natives.CCSGameRules_AreTeamsPlayingSwitchedSides.Invoke(self.Handle);
    }

    public static void HandleSwapTeams(this CCSGameRules self)
    {
        Natives.CCSGameRules_HandleSwapTeams.Invoke(self.Handle);
    }

    public static bool IsLastRoundBeforeHalfTime(this CCSGameRules self)
    {
        return Natives.CCSGameRules_IsLastRoundBeforeHalfTime.Invoke(self.Handle);
    }

    public static CsTeam DetermineWinnerBySurvival(this CCSGameRules self)
    {
        var tPlayers = PlayerHelper.GetT();
        var ctPlayers = PlayerHelper.GetCT();
        int tAlive = tPlayers.Count(CountAlive);
        int tHealth = tPlayers.Sum(SumHealth);
        int ctAlive = ctPlayers.Count(CountAlive);
        int ctHealth = ctPlayers.Sum(SumHealth);
        if (ctAlive != tAlive)
        {
            var winner = ctAlive > tAlive ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
            Runtime.Log(
                $"Knife round winner determined by alive count: CT={ctAlive}, T={tAlive}, Winner={winner}"
            );
            return winner;
        }
        if (ctHealth != tHealth)
        {
            var winner = ctHealth > tHealth ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
            Runtime.Log(
                $"Knife round winner determined by health: CT={ctHealth}, T={tHealth}, Winner={winner}"
            );
            return winner;
        }
        var randomWinner = (CsTeam)new Random().Next(2, 4);
        Runtime.Log($"Knife round winner determined randomly: {randomWinner}");
        return randomWinner;
        static bool CountAlive(CCSPlayerController player) => player.GetHealth() > 0;
        static int SumHealth(CCSPlayerController player) => player.GetHealth();
    }
}
