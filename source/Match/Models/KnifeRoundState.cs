/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;
using Match.Get5.Events;

namespace Match;

public class KnifeRoundState : ReadyupWarmupState
{
    public override string Name => "knife";

    public override void Load()
    {
        Rules.KnifeRoundWinner = null;
        HookGameEvent<EventRoundStart>(OnRoundStart);
        AddHook(Natives.CCSPlayerPawnBase_IncrementNumMVPs, OnIncrementNumMVPsPre);
        if (OperatingSystem.IsWindows())
            AddHook(VirtualFunctions.TerminateRoundFuncWindows, OnTerminateRoundWindowsPre);
        else
            AddHook(VirtualFunctions.TerminateRoundFuncLinux, OnTerminateRoundLinuxPre);
        Runtime.Log("Executing knife round configuration");
        Config.ExecKnife();
        Cstv.Record(Rules.GetDemoFilename());
        PlayerHelper.RemovePlayerClans();
    }

    private RoundEndReason? TryGetKnifeWinnerReason()
    {
        var winner = EntityHelper.GetGameRules()?.DetermineWinnerBySurvival();
        if (winner == null)
            return null;
        Rules.KnifeRoundWinner = Rules.GetTeam(winner.Value);
        return winner == CsTeam.Terrorist ? RoundEndReason.TerroristsWin : RoundEndReason.CTsWin;
    }

    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo _)
    {
        if (Rules.KnifeRoundWinner != null)
            Server.NextWorldUpdate(() => Rules.SetState(new KnifeVoteWarmupState()));
        else
        {
            PlayerHelper.PrintToChatAllRepeat(
                Runtime.Plugin.Localizer["match.knife", Rules.GetChatPrefix()]
            );
            Rules.SendEvent(OnKnifeRoundStartedEvent.Create());
        }
        return HookResult.Continue;
    }

    public HookResult OnIncrementNumMVPsPre(DynamicHook hook)
    {
        hook.SetReturn<nint>(0);
        return HookResult.Stop;
    }

    public HookResult OnTerminateRoundWindowsPre(DynamicHook hook)
    {
        if (TryGetKnifeWinnerReason() is RoundEndReason reason)
            hook.SetParam(2, (uint)reason);
        return HookResult.Continue;
    }

    public HookResult OnTerminateRoundLinuxPre(DynamicHook hook)
    {
        if (TryGetKnifeWinnerReason() is RoundEndReason reason)
            hook.SetParam(1, (uint)reason);
        return HookResult.Continue;
    }
}
