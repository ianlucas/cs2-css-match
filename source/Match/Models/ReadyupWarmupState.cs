/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using Match.Get5.Events;

namespace Match;

public class ReadyupWarmupState : WarmupState
{
    public override string Name => "warmup";
    public static readonly List<string> ReadyCmds = ["css_ready", "css_r", "css_pronto"];
    public static readonly List<string> UnreadyCmds = ["css_unready", "css_ur", "css_naopronto"];
    private long _warmupStart = 0;
    private static bool IsBo1 => Rules.GetTotalMapCount() < 2;

    public override void Load()
    {
        Runtime.Log($"Matchmaking mode: {Rules.IsMatchmaking()}");
        Cstv.Stop();
        Cstv.Set(ConVars.IsTvRecord.Value);
        if (Rules.EnsureCorrectMap())
            return /* Map will be changed. */
            ;
        base.Load();
        RegisterListener<Listeners.OnTick>(OnTick);
        HookGameEvent<EventPlayerSpawn>(OnPlayerSpawn);
        HookGameEvent<EventPlayerTeam>(OnPlayerTeam);
        HookGameEvent<EventPlayerDisconnect>(OnPlayerDisconnect);
        HookGameEvent<EventRoundPrestart>(OnRoundPrestart);
        HookGameEvent<EventCsWinPanelMatch>(OnCsWinPanelMatch);
        RegisterCommand(ReadyCmds, "Mark yourself as ready to start the match.", OnReadyCommand);
        RegisterCommand(UnreadyCmds, "Mark yourself as not ready.", OnUnreadyCommand);
        Rules.ResetTeamsForNewMatch();
        if (this is not NoneState && ConVars.IsMatchmaking.Value)
        {
            var nextMap = Rules.GetNextMap();
            ConVar.Find("nextlevel")?.StringValue = nextMap?.MapName ?? "";
            _warmupStart = TimeHelper.NowSeconds();
            if (IsBo1)
            {
                Timers.SetEverySecond("ReadyStatusReminder", SendReadyStatusReminder);
                Timers.Set(
                    "MatchmakingReadyTimeout",
                    ConVars.MatchmakingReadyTimeout.Value,
                    OnMatchCancelled
                );
            }
        }
        Timers.SetEveryChatInterval("WarmupInstructions", SendWarmupInstructions);
        Runtime.Log("Executing warm-up commands...");
        Config.ExecWarmup(
            warmupTime: Rules.IsMatchmaking() && IsBo1 ? ConVars.MatchmakingReadyTimeout.Value : -1,
            isLockTeams: Rules.AreTeamsLocked()
        );
        _matchCancelled = false;
        if (this is not NoneState && ConVars.IsMatchmaking.Value && Rules.IsFirstMap())
            Runtime.Log("The series has begun.", force: true);
    }

    public void OnTick()
    {
        bool didUpdatePlayers = false;
        foreach (var player in PlayerHelper.GetPlayersInTeams())
            if (
                !player.IsBot
                && player.SetPlayerClan(
                    Runtime.Plugin.Localizer[
                        player.GetState()?.IsReady == true ? "match.ready" : "match.not_ready"
                    ]
                )
            )
                didUpdatePlayers = true;
        if (didUpdatePlayers)
            PlayerHelper.UpdateScoreboard();
    }

    public void SendWarmupInstructions()
    {
        var needed = Rules.GetNeededPlayersCount() - Rules.GetReadyPlayersCount();
        foreach (var player in PlayerHelper.GetPlayersInTeams())
        {
            var localize = Runtime.Plugin.Localizer;
            var playerState = player.GetState();
            player.PrintToChat(localize["match.commands", Rules.GetChatPrefix()]);
            if (needed > 0)
                player.PrintToChat(localize["match.commands_needed", needed]);
            if (playerState?.IsReady != true)
                player.PrintToChat(localize["match.commands_ready"]);
            player.PrintToChat(localize["match.commands_gg"]);
        }
    }

    public void SendReadyStatusReminder()
    {
        var timeleft = Math.Max(
            0,
            ConVars.MatchmakingReadyTimeout.Value - (TimeHelper.NowSeconds() - _warmupStart)
        );
        if (timeleft % 30 != 0)
            return;
        var formattedTimeleft = TimeHelper.FormatMmSs(timeleft);
        var unreadyTeams = Rules.GetUnreadyTeams();
        if (timeleft == 0)
            Timers.Clear("ReadyStatusReminder");
        else
            switch (unreadyTeams.Count())
            {
                case 1:
                    var team = unreadyTeams.First();
                    Server.PrintToChatAll(
                        Runtime.Plugin.Localizer[
                            "match.match_waiting_team",
                            Rules.GetChatPrefix(stripColors: true),
                            team.FormattedName,
                            formattedTimeleft
                        ]
                    );
                    break;

                case 2:
                    Server.PrintToChatAll(
                        Runtime.Plugin.Localizer[
                            "match.match_waiting_players",
                            Rules.GetChatPrefix(stripColors: true),
                            formattedTimeleft
                        ]
                    );
                    break;
            }
    }

    public HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo _)
    {
        var player = @event.Userid;
        if (!Rules.IsLoadedFromFile && player != null)
            player.GetState()?.LeaveTeam();
        return HookResult.Continue;
    }

    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo _)
    {
        var player = @event.Userid;
        // This fixes warmup time not matching the actual warmup time when the
        // first player connects.
        if (
            player != null
            && !player.IsBot
            && ConVars.IsMatchmaking.Value
            && IsBo1
            && PlayerHelper.GetActualPlayers().Count() == 1
        )
            Server.ExecuteCommand(
                $"mp_warmuptime {Math.Max(
                1,
                ConVars.MatchmakingReadyTimeout.Value - (TimeHelper.NowSeconds() - _warmupStart)
            )}"
            );
        return HookResult.Continue;
    }

    public void OnReadyCommand(CCSPlayerController? player, CommandInfo _)
    {
        if (player != null && !_matchCancelled)
        {
            Runtime.Log($"{player.PlayerName} sent !ready.");
            var playerState = player.GetState();
            if (playerState == null && !Rules.IsLoadedFromFile)
            {
                var team = Rules.GetTeam(player.Team);
                if (team != null && team.CanAddPlayer())
                {
                    playerState = new(player.SteamID, player.PlayerName, team, player);
                    team.AddPlayer(playerState);
                }
            }
            if (playerState != null)
            {
                playerState.IsReady = true;
                Rules.SendEvent(OnTeamReadyStatusChangedEvent.Create(team: playerState.Team));
                TryStartMatch();
            }
        }
    }

    public void OnUnreadyCommand(CCSPlayerController? player, CommandInfo _)
    {
        Runtime.Log($"{player?.PlayerName} sent !unready.");
        var playerState = player?.GetState();
        if (playerState != null)
        {
            playerState.IsReady = false;
            Rules.SendEvent(OnTeamReadyStatusChangedEvent.Create(team: playerState.Team));
        }
    }

    public void TryStartMatch()
    {
        var players = Rules.GetAllPlayers();
        if (players.Count() == Rules.GetNeededPlayersCount() && Rules.AreAllPlayersReady())
        {
            if (!Rules.IsLoadedFromFile)
                Rules.Setup();
            Rules.SetState(
                ConVars.IsKnifeRoundEnabled.Value ? new KnifeRoundState() : new LiveState()
            );
        }
    }

    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo _)
    {
        var player = @event.Userid;
        if (!Rules.IsLoadedFromFile && player != null)
            player.GetState()?.LeaveTeam();
        return HookResult.Continue;
    }
}
