/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace Match;

public class WarmupState : ActiveMatchState
{
    public override void Load()
    {
        EntityHelper.GetGameRules()?.RoundsPlayedThisPhase = 0;
        HookGameEvent<EventItemPickup>(OnItemPickup);
    }

    public static HookResult OnItemPickup(EventItemPickup @event, GameEventInfo _)
    {
        var controller = @event.Userid;
        var inGameMoneyServices = controller?.InGameMoneyServices;
        if (controller != null && inGameMoneyServices != null)
        {
            inGameMoneyServices.Account = 16000;
            Utilities.SetStateChanged(controller, "CCSPlayerController", "m_pInGameMoneyServices");
        }
        return HookResult.Continue;
    }
}
