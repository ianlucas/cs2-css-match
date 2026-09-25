/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;

namespace Match;

public partial class Match
{
    public HookResult OnChangeTeamPre(DynamicHook hook)
    {
        var controller = hook.GetParam<CCSPlayerController>(0);
        if (controller.IsValid && !controller.IsBot && Rules.AreTeamsLocked())
        {
            var playerState = controller.GetState();
            var team = hook.GetParam<int>(1);
            var lockedTeam =
                playerState != null ? (int)playerState.Team.CurrentTeam : (int)CsTeam.Spectator;
            if (team != lockedTeam)
            {
                hook.SetParam(1, lockedTeam);
                return HookResult.Changed;
            }
        }
        return HookResult.Continue;
    }

    public HookResult OnMaintainBotQuotaPre(DynamicHook hook)
    {
        hook.SetReturn<byte>(0);
        if (!ConVars.IsBots.Value)
        {
            if (DidKickBots)
                return HookResult.Stop;
            PlayerHelper.KickAllBots("Kicked by match_bots' ConVar.");
            DidKickBots = true;
            return HookResult.Stop;
        }
        var connectedHumans = PlayerHelper.GetActualPlayers();
        if (!connectedHumans.Any() && Rules.State is ReadyupWarmupState)
        {
            PlayerHelper.KickAllBots("Kicked by match_bots' ConVar.");
            return HookResult.Stop;
        }
        var neededPerTeam = ConVars.PlayersNeededPerTeam.Value;
        var teams = new List<(IEnumerable<CCSPlayerController>, string)>()
        {
            (PlayerHelper.GetCT(), "ct"),
            (PlayerHelper.GetT(), "t"),
        };
        foreach (var (players, team) in teams)
        {
            int bots = 0;
            int humans = 0;
            CCSPlayerController? botToKick = null;
            foreach (var player in players)
                if (player.IsBot)
                {
                    bots++;
                    botToKick ??= player;
                }
                else
                    humans++;
            if (bots + humans > neededPerTeam && botToKick != null)
                botToKick.Kick(
                    $"Kicked by match_bots' ConVar.",
                    NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED
                );
            if (bots + humans < neededPerTeam)
                Server.ExecuteCommand($"bot_add_{team}");
        }
        foreach (var player in PlayerHelper.GetSpectators())
            if (player.IsBot)
                player.Kick(
                    "Kicked by match_bots' ConVar.",
                    NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED
                );
        return HookResult.Stop;
    }
}
