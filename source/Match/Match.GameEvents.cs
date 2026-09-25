/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using Match.Get5.Events;

namespace Match;

public partial class Match
{
    public HookResult OnPlayerChat(EventPlayerChat @event, GameEventInfo _)
    {
        var player = Utilities.GetPlayerFromUserid(@event.Userid);
        if (player == null)
            return HookResult.Continue;
        var message = @event.Text.Trim();
        if (message.Length == 0)
            return HookResult.Continue;
        Rules.SendEvent(
            OnPlayerSayEvent.Create(player, @event.Teamonly ? "say_team" : "say", message)
        );
        return HookResult.Continue;
    }
}
