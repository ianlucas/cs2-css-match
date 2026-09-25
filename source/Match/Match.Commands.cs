/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Match.Get5;
using Match.Get5.Events;

namespace Match;

public partial class Match
{
    [ConsoleCommand(
        "match_status",
        "Display the current match status, including state, teams, and players."
    )]
    public void OnMatchStatusCommand(CCSPlayerController? caller, CommandInfo _)
    {
        if (caller != null && !AdminManager.PlayerHasPermissions(caller, "@css/config"))
            return;
        var message = "[MatchPlugin Status]\n\n";
        message += $"State: {Rules.State.GetType().Name}\n";
        message += $"Id: {Rules.Id ?? "(No ID)"}\n";
        message += $"Loaded from file?: {Rules.IsLoadedFromFile}\n";
        message += $"Is matchmaking?: {Rules.IsMatchmaking()}\n";
        message += "\n";
        foreach (var team in Rules.Teams)
        {
            message += $"[Team {team.Index}]\n";
            if (team.Players.Count == 0)
                message += "No players.\n";
            foreach (var player in team.Players)
            {
                message += $"{player.Name}";
                if (team.InGameLeader == player)
                    message += "[L]";
                if (player.Handle != null)
                {
                    var playerTeam = player.Handle.Team switch
                    {
                        CsTeam.Terrorist => "Terrorist",
                        CsTeam.CounterTerrorist => "CT",
                        CsTeam.Spectator => "Spectator",
                        _ => $"Other={player.Handle.Team}",
                    };
                    message += $" ({playerTeam})";
                }
                else
                    message += " (Disconnected)";
                message += "\n";
            }
        }
        caller?.PrintToConsole(message);
        Console.WriteLine(message);
    }

    [ConsoleCommand("css_start", "Force start the match, marking all connected players as ready.")]
    public void OnStartCommand(CCSPlayerController? caller, CommandInfo _)
    {
        if (caller != null && !AdminManager.PlayerHasPermissions(caller, "@css/config"))
            return;
        if (Rules.State is not ReadyupWarmupState)
            return;
        if (!Rules.IsLoadedFromFile)
        {
            foreach (var player in PlayerHelper.GetPlayersInTeams())
            {
                var playerState = player.GetState();
                if (playerState == null)
                {
                    var team = Rules.GetTeam(player.Team);
                    if (team == null)
                        player.ChangeTeam(CsTeam.Spectator);
                    else
                    {
                        playerState = new(player.SteamID, player.PlayerName, team, player);
                        team.AddPlayer(playerState);
                    }
                }
                if (playerState != null)
                    playerState.IsReady = true;
            }
            Rules.Setup();
        }
        else
            foreach (var player in Rules.GetAllPlayers())
                player.IsReady = true;
        Runtime.Log(
            sendToChat: true,
            message: Localizer[
                "match.admin_start",
                Rules.GetChatPrefix(true),
                caller?.PlayerName ?? "Console"
            ]
        );
        Rules.SetState(ConVars.IsKnifeRoundEnabled.Value ? new KnifeRoundState() : new LiveState());
    }

    [ConsoleCommand("css_map", "Change to the specified map (must start with \"de_\").")]
    public void OnMapCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && !AdminManager.PlayerHasPermissions(caller, "@css/config"))
            return;
        if (info.ArgCount != 2)
            return;
        var mapname = info.GetArg(1).ToLower().Trim();
        if (!mapname.StartsWith("de_"))
            return;
        if (Rules.AreTeamsLocked())
            return;
        Runtime.Log(
            sendToChat: true,
            message: Localizer[
                "match.admin_map",
                Rules.GetChatPrefix(true),
                caller?.PlayerName ?? "Console"
            ]
        );
        Server.ExecuteCommand($"changelevel {mapname}");
    }

    [ConsoleCommand("css_restart", "Reset and restart the match plugin.")]
    public void OnRestartCommand(CCSPlayerController? caller, CommandInfo _)
    {
        if (caller != null && !AdminManager.PlayerHasPermissions(caller, "@css/config"))
            return;
        Runtime.Log(
            sendToChat: true,
            message: Localizer[
                "match.admin_restart",
                Rules.GetChatPrefix(true),
                caller?.PlayerName ?? "Console"
            ]
        );
        Rules.Reset();
        Rules.SetState(new NoneState());
    }

    [ConsoleCommand("match_load", "Load a match configuration from a JSON file.")]
    [ConsoleCommand("get5_loadmatch", "Load a match configuration from a JSON file.")]
    public void OnMatchLoadCommand(CCSPlayerController? caller, CommandInfo info)
    {
        if (caller != null && !AdminManager.PlayerHasPermissions(caller, "@css/config"))
            return;
        if (Rules.State is not ReadyupWarmupState)
            return;
        if (info.ArgCount != 2)
            return;
        var name = info.GetArg(1).Trim();
        var file = Get5Match.Read(name);
        if (file.Error != null)
            Rules.SendEvent(OnLoadMatchConfigFailedEvent.Create(reason: file.Error));
        var match = file.Contents;
        if (match == null || file.Path == null)
            return;
        Rules.SendEvent(OnPreLoadMatchConfigEvent.Create(filename: file.Path));
        Rules.Reset();
        Rules.IsLoadedFromFile = true;
        Rules.Id = match.Matchid;
        Rules.IsClinchSeries = match.ClinchSeries ?? true;
        // Maps
        var maplist = match.Maplist.Get();
        if (maplist != null)
            foreach (var mapName in maplist)
                Rules.AddMap(mapName);
        else
        {
            Rules.Reset();
            return;
        }
        // Teams
        Rules.Team1.StartingTeam = CsTeam.Terrorist;
        Rules.Team2.StartingTeam = CsTeam.CounterTerrorist;
        for (var index = 0; index < Rules.Teams.Count; index++)
        {
            var teamSchema = (index == 0 ? match.Team1 : match.Team2)?.Get();
            if (teamSchema == null)
                continue;
            ulong? leaderId = ulong.TryParse(teamSchema.Leaderid, out ulong li) ? li : null;
            Rules.ConfigureTeamFromSchema(index, teamSchema, leaderId);
        }
        if (match.Cvars != null)
            foreach (var (key, value) in match.Cvars)
            {
                var cmd = $"{key} {value}";
                Runtime.Log($"Executing command: {cmd}");
                Server.ExecuteCommand(cmd);
            }
        Server.NextWorldUpdate(() =>
        {
            Rules.Setup();
            Rules.SetState(new ReadyupWarmupState());
        });
    }
}
