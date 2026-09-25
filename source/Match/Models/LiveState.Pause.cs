/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Match.Get5.Events;

namespace Match;

public partial class LiveState
{
    private bool _wasPaused = false;
    private string _wasPausedType = "";
    private PlayerTeam? _teamWhichPaused = null;

    public void CheckPauseEvents()
    {
        var gameRules = EntityHelper.GetGameRules();
        if (gameRules == null || !gameRules.FreezePeriod)
            return;
        var isTeamPaused = gameRules.TerroristTimeOutActive || gameRules.CTTimeOutActive;
        var isTechnicalPaused = gameRules.TechnicalTimeOut;
        var isMatchPaused = gameRules.MatchWaitingForResume;
        var isPaused = isTeamPaused || isTechnicalPaused || isMatchPaused;
        var didPauseStateChange = _wasPaused != isPaused;
        if (didPauseStateChange)
        {
            if (isPaused)
            {
                CsTeam? sideWhichPaused =
                    gameRules.TerroristTimeOutActive ? CsTeam.Terrorist
                    : gameRules.CTTimeOutActive ? CsTeam.CounterTerrorist
                    : null;
                var teamWhichPaused =
                    sideWhichPaused != null
                        ? Rules.Team1.CurrentTeam == sideWhichPaused
                            ? Rules.Team1
                            : Rules.Team2.Opposition
                        : null;
                var pauseType =
                    isTeamPaused ? "team"
                    : isTechnicalPaused ? "technical"
                    : "admin";
                if (teamWhichPaused != null)
                    Server.PrintToChatAll(
                        Runtime.Plugin.Localizer[
                            "match.pause_start",
                            Rules.GetChatPrefix(),
                            teamWhichPaused.FormattedName
                        ]
                    );
                Rules.SendEvent(OnMatchPausedEvent.Create(team: teamWhichPaused, pauseType));
                Rules.SendEvent(OnPauseBeganEvent.Create(team: teamWhichPaused, pauseType));
                _teamWhichPaused = teamWhichPaused;
                _wasPausedType = pauseType;
            }
            else
                Rules.SendEvent(
                    OnMatchUnpausedEvent.Create(team: _teamWhichPaused, pauseType: _wasPausedType)
                );
        }
        _wasPaused = isPaused;
    }

    public void OnPauseCommand(CCSPlayerController? player, CommandInfo _)
    {
        var playerState = player?.GetState();
        if (playerState != null)
        {
            if (ConVars.IsFriendlyPause.Value)
            {
                if (EntityHelper.GetGameRules()?.MatchWaitingForResume == true)
                    return;
                Rules.ClearAllTeamUnpauseFlags();
                Server.PrintToChatAll(
                    Runtime.Plugin.Localizer[
                        "match.pause_start",
                        Rules.GetChatPrefix(),
                        playerState.Team.FormattedName
                    ]
                );
                Server.ExecuteCommand("mp_pause_match");
                Rules.SendEvent(
                    OnMatchPausedEvent.Create(team: playerState.Team, pauseType: "tactical")
                );
                return;
            }
            player?.ExecuteClientCommandFromServer("callvote StartTimeOut");
        }
    }

    public void OnUnpauseCommand(CCSPlayerController? player, CommandInfo _)
    {
        var playerState = player?.GetState();
        if (
            playerState != null
            && ConVars.IsFriendlyPause.Value
            && EntityHelper.GetGameRules()?.MatchWaitingForResume == true
        )
        {
            var askedForUnpause = playerState.Team.IsUnpauseMatch;
            playerState.Team.IsUnpauseMatch = true;
            if (!Rules.AreAllTeamsReadyToUnpause())
            {
                if (!askedForUnpause)
                    Timers.SetEveryChatInterval(
                        "FriendlyUnpauseInstructions",
                        () =>
                            Server.PrintToChatAll(
                                Runtime.Plugin.Localizer[
                                    "match.pause_unpause1",
                                    Rules.GetChatPrefix(),
                                    playerState.Team.FormattedName,
                                    playerState.Team.Opposition.FormattedName
                                ]
                            )
                    );
                return;
            }
            else
                Server.PrintToChatAll(
                    Runtime.Plugin.Localizer[
                        "match.pause_unpause2",
                        Rules.GetChatPrefix(),
                        playerState.Team.FormattedName
                    ]
                );
            Timers.Clear("FriendlyUnpauseInstructions");
            Server.ExecuteCommand("mp_unpause_match");
            return;
        }
        if (
            player == null
            || (player != null && AdminManager.PlayerHasPermissions(player, "@css/config"))
        )
        {
            Runtime.Log(
                sendToChat: true,
                message: Runtime.Plugin.Localizer[
                    "match.admin_unpause",
                    Rules.GetChatPrefix(true),
                    player?.PlayerName ?? "Console"
                ]
            );
            Timers.Clear("FriendlyUnpauseInstructions");
            Server.ExecuteCommand("mp_unpause_match");
            return;
        }
    }
}
