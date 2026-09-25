/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;

namespace Match;

public partial class LiveState
{
    private PlayerTeam? _surrendingTeam;
    private bool _canSurrender = false;

    public void OnSurrenderCommand(CCSPlayerController? player, CommandInfo _)
    {
        var playerState = player?.GetState();
        if (
            playerState != null
            && _canSurrender
            && (_surrendingTeam == null || _surrendingTeam == playerState.Team)
            && !playerState.Team.SurrenderVotes.Contains(playerState.SteamID)
        )
        {
            _surrendingTeam = playerState.Team;
            playerState.Team.SurrenderVotes.Add(playerState.SteamID);
            var neededVotes = playerState.Team.Players.Count(p => p.Handle != null);
            var timerName = $"SurrenderTimeout_Team{playerState.Team.Index}";
            if (playerState.Team.SurrenderVotes.Count >= neededVotes)
            {
                if (!_canSurrender)
                    return;
                Timers.Clear(timerName);
                Server.PrintToChatAll(
                    Runtime.Plugin.Localizer[
                        "match.surrender_success",
                        Rules.GetChatPrefix(),
                        playerState.Team.FormattedName
                    ]
                );
                playerState.Team.IsSurrended = true;
                playerState.Team.Score = 0;
                playerState.Team.Opposition.Score = 1;
                Runtime.Log("Terminating match due to team surrender");
                EntityHelper
                    .GetGameRules()
                    ?.TerminateRound(
                        0,
                        playerState.Team.CurrentTeam == CsTeam.Terrorist
                            ? RoundEndReason.TerroristsSurrender
                            : RoundEndReason.CTsSurrender
                    );
            }
            else if (playerState.Team.SurrenderVotes.Count == 1)
            {
                playerState.Team.SendChat(
                    Runtime.Plugin.Localizer[
                        "match.surrender_start",
                        Rules.GetChatPrefix(),
                        playerState.Name,
                        neededVotes,
                        ConVars.SurrenderTimeout.Value
                    ]
                );
                Timers.Set(
                    timerName,
                    ConVars.SurrenderTimeout.Value,
                    () =>
                    {
                        _surrendingTeam = null;
                        var hadAllSurrenderVotes =
                            playerState.Team.SurrenderVotes.Count == playerState.Team.Players.Count;
                        playerState.Team.SurrenderVotes.Clear();
                        playerState.Team.SendChat(
                            Runtime.Plugin.Localizer[
                                hadAllSurrenderVotes
                                    ? "match.surrender_fail1"
                                    : "match.surrender_fail2",
                                Rules.GetChatPrefix()
                            ]
                        );
                    }
                );
            }
        }
    }
}
