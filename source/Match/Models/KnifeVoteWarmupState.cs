/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Match.Get5.Events;

namespace Match;

public class KnifeVoteWarmupState : WarmupState
{
    public override string Name => "waiting_for_knife_decision";
    public static readonly List<string> StayCmds = ["css_stay", "css_ficar"];
    public static readonly List<string> SwitchCmds = ["css_switch", "css_trocar"];
    public static readonly List<KnifeRoundVote> KnifeRoundVotes =
    [
        KnifeRoundVote.Stay,
        KnifeRoundVote.Switch,
    ];

    public override void Load()
    {
        base.Load();
        HookGameEvent<EventPlayerTeam>(OnPlayerTeamPre, HookMode.Pre);
        RegisterCommand(
            StayCmds,
            "Vote to stay on the current side after winning the knife round.",
            OnStayCommand
        );
        RegisterCommand(
            SwitchCmds,
            "Vote to switch sides after winning the knife round.",
            OnSwitchCommand
        );
        Timers.SetEveryChatInterval("KnifeVoteInstructions", SendKnifeVoteInstructions);
        Timers.Set("KnifeVoteTimeout", ConVars.KnifeVoteTimeout.Value - 1, OnKnifeVoteTimeout);
        Rules.ResetAllKnifeRoundVotes();
        Runtime.Log("Executing knife vote warmup");
        Config.ExecWarmup(warmupTime: ConVars.KnifeVoteTimeout.Value, isLockTeams: true);
    }

    public HookResult OnPlayerTeamPre(EventPlayerTeam @event, GameEventInfo _)
    {
        return HookResult.Stop;
    }

    public void SendKnifeVoteInstructions()
    {
        var team = Rules.KnifeRoundWinner;
        var leader = team?.InGameLeader;
        if (team != null && leader != null)
            Server.PrintToChatAll(
                Runtime.Plugin.Localizer[
                    "match.knife_vote",
                    Rules.GetChatPrefix(),
                    team.FormattedName,
                    leader.Name
                ]
            );
    }

    public void OnStayCommand(CCSPlayerController? player, CommandInfo _)
    {
        var playerState = player?.GetState();
        if (playerState != null)
        {
            Runtime.Log($"{player?.PlayerName} voted !stay.");
            playerState.KnifeRoundVote = KnifeRoundVote.Stay;
            CheckLeaderVote();
        }
    }

    public void OnSwitchCommand(CCSPlayerController? player, CommandInfo _)
    {
        var playerState = player?.GetState();
        if (playerState != null)
        {
            Runtime.Log($"{player?.PlayerName} voted !switch.");
            playerState.KnifeRoundVote = KnifeRoundVote.Switch;
            CheckLeaderVote();
        }
    }

    public void CheckLeaderVote()
    {
        var team = Rules.KnifeRoundWinner;
        if (team != null)
            foreach (var vote in KnifeRoundVotes)
                if (
                    team.Players.Any(p =>
                        p.KnifeRoundVote == vote && p.SteamID == team.InGameLeader?.SteamID
                    )
                )
                {
                    Runtime.Log("Team leader has chosen a side.");
                    ProcessKnifeVote(vote);
                    return;
                }
    }

    public void OnKnifeVoteTimeout()
    {
        Runtime.Log("Knife vote timed out");
        ProcessKnifeVote(KnifeRoundVote.None);
    }

    public void ProcessKnifeVote(KnifeRoundVote decision)
    {
        Runtime.Log($"Processing knife vote decision: {decision}");
        var winnerTeam = Rules.KnifeRoundWinner;
        if (winnerTeam == null)
            return;
        if (decision != KnifeRoundVote.None)
        {
            var localize = Runtime.Plugin.Localizer;
            var decisionLabel = localize[
                decision == KnifeRoundVote.Switch
                    ? "match.knife_decision_switch"
                    : "match.knife_decision_stay"
            ];
            Server.PrintToChatAll(
                localize[
                    "match.knife_decision",
                    Rules.GetChatPrefix(),
                    winnerTeam.FormattedName,
                    decisionLabel
                ]
            );
        }
        if (decision == KnifeRoundVote.Switch)
        {
            Rules.SwapTeamSides();
            EntityHelper.GetGameRules()?.HandleSwapTeams();
        }
        Rules.SendEvent(OnSidePickedEvent.Create(team: winnerTeam));
        Rules.SendEvent(OnKnifeRoundWonEvent.Create(team: winnerTeam, decision));
        Rules.SetState(new LiveState());
    }
}
