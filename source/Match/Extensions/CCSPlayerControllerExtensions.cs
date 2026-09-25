/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.ValveConstants.Protobuf;

namespace Match;

public static class CCSPlayerControllerExtensions
{
    public static bool SetPlayerClan(this CCSPlayerController self, string clan)
    {
        if (self.Clan != clan)
        {
            self.Clan = clan;
            Utilities.SetStateChanged(self, "CCSPlayerController", "m_szClan");
            return true;
        }
        return false;
    }

    public static int GetHealth(this CCSPlayerController self)
    {
        return Math.Max(
            (self.SteamID == 0 ? self.Pawn.Value : self.PlayerPawn.Value)?.Health ?? 0,
            0
        );
    }

    public static PlayerState? GetState(this CCSPlayerController self)
    {
        return Rules.GetPlayerState(self);
    }

    public static void Kick(
        this CCSPlayerController self,
        string reason,
        NetworkDisconnectionReason gameReason
    )
    {
        Runtime.Log($"Kicking {self.PlayerName}: {reason}");
        self.Disconnect(gameReason);
    }
}
