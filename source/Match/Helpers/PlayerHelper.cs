/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;

namespace Match;

public static class PlayerHelper
{
    public static CCSPlayerController? GetPlayerFromSteamID(ulong steamID)
    {
        return GetAllPlayers().FirstOrDefault(p => p.SteamID == steamID);
    }

    public static IEnumerable<CCSPlayerController> GetAllPlayers()
    {
        return Utilities.GetPlayers().Where(p => !p.IsHLTV);
    }

    public static IEnumerable<CCSPlayerController> GetAllValidPlayers()
    {
        return GetAllPlayers().Where(p => p.IsValid);
    }

    public static IEnumerable<CCSPlayerController> GetT()
    {
        return GetAllValidPlayers().Where(p => p.Team == CsTeam.Terrorist);
    }

    public static IEnumerable<CCSPlayerController> GetCT()
    {
        return GetAllValidPlayers().Where(p => p.Team == CsTeam.CounterTerrorist);
    }

    public static IEnumerable<CCSPlayerController> GetSpectators()
    {
        return GetAllValidPlayers().Where(p => p.Team == CsTeam.Spectator);
    }

    public static IEnumerable<CCSPlayerController> GetPlayersInTeams()
    {
        return GetActualPlayers()
            .Where(p =>
                p.Pawn.Value?.TeamNum == (int)CsTeam.CounterTerrorist
                || p.Pawn.Value?.TeamNum == (int)CsTeam.Terrorist
            );
    }

    public static IEnumerable<CCSPlayerController> GetActualPlayers()
    {
        return GetAllValidPlayers().Where(p => !p.IsBot);
    }

    public static void KickAllBots(string reason)
    {
        foreach (var player in GetAllValidPlayers())
            if (player.IsBot)
                player.Kick(reason, NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
    }

    public static void UpdateScoreboard()
    {
        var @event = new EventNextlevelChanged(true);
        @event.FireEvent(false);
    }

    public static void RemovePlayerClans()
    {
        bool didUpdatePlayers = false;
        foreach (var player in GetActualPlayers())
            if (player.SetPlayerClan(""))
                didUpdatePlayers = true;
        if (didUpdatePlayers)
            UpdateScoreboard();
    }

    public static void PrintToChatAllRepeat(string message, int amount = 3)
    {
        for (var n = 0; n < amount; n++)
            Server.PrintToChatAll(message);
    }
}
