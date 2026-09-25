/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using Match.Get5.Events;

namespace Match;

public partial class Match
{
    public void OnMapStart(string _)
    {
        PendingInternalPush = true;
    }

    public void OnTick()
    {
        if (PendingInternalPush)
        {
            PendingInternalPush = false;
            OnConfigsExecuted();
        }
    }

    public void OnConfigsExecuted()
    {
        OnIsBotsChanged(null, ConVars.IsBots.Value);
        OnIsMatchmakingChanged(null, ConVars.IsMatchmaking.Value);
        Rules.SetState(Rules.IsSeriesStarted ? new ReadyupWarmupState() : new NoneState());
    }

    public void OnClientAuthorized(int playerSlot, SteamID _)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player != null && !player.IsBot)
        {
            var playerState = player.GetState();
            if (playerState != null)
            {
                if (playerState.Name == "")
                    playerState.Name = player.PlayerName;
                playerState.Handle = player;
            }
            else if (
                !player.IsBot
                && ConVars.IsMatchmaking.Value
                && ConVars.IsMatchmakingKick.Value
                && !AdminManager.PlayerHasPermissions(player, "@css/root")
            )
                player.Kick(
                    "Match is reserved for a lobby.",
                    NetworkDisconnectionReason.NETWORK_DISCONNECT_REJECT_RESERVED_FOR_LOBBY
                );
            Rules.SendEvent(OnPlayerConnectedEvent.Create(player));
        }
    }

    public void OnClientDisconnect(int playerSlot)
    {
        var player = Utilities.GetPlayerFromSlot(playerSlot);
        if (player != null && !player.IsBot)
        {
            player.GetState()?.Handle = null;
            Rules.SendEvent(OnPlayerDisconnectedEvent.Create(player));
        }
    }
}
