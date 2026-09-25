/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace Match.Get5;

public static class Get5EventHelpers
{
    public static string ToSideString(CsTeam team) => team == CsTeam.Terrorist ? "t" : "ct";

    public static string ToTeamString(PlayerTeam? team) =>
        team != null ? $"team{team.Index + 1}" : "spec";

    public static object ToStatsTeam(PlayerTeam team) =>
        new
        {
            id = team.Id,
            name = team.Name,
            series_score = team.SeriesScore,
            score = team.Score,
            score_ct = team.Stats.ScoreCT,
            score_t = team.Stats.ScoreT,
            side = ToSideString(team.CurrentTeam),
            starting_side = ToSideString(team.StartingTeam),
            players = team
                .Players.Select(player => new
                {
                    steamid = player.EventSteamID.ToString(),
                    name = player.Name,
                    stats = player.Stats,
                    is_bot = player.IsBot,
                    ping = player.Handle?.Ping,
                })
                .ToList(),
        };

    public static object? ToWinner(PlayerTeam? team) =>
        team != null
            ? new { side = ToSideString(team.CurrentTeam), team = ToTeamString(team) }
            : null;

    public static object ToPlayer(PlayerState player) =>
        new
        {
            steamid = player.EventSteamID.ToString(),
            name = player.Name,
            user_id = player.Handle?.UserId,
            side = ToSideString(player.Team.CurrentTeam),
            is_bot = player.IsBot,
            ping = player.Handle?.Ping,
        };

    public static object ToPlayer(CCSPlayerController player) =>
        new
        {
            steamid = Rules.GetEventSteamID(player).ToString(),
            name = player.PlayerName,
            user_id = player.UserId,
            side = ToSideString(player.Team),
            is_bot = player.IsBot,
            ping = player.Ping,
        };

    public static object ToWeapon(string weapon)
    {
        var name = weapon.Replace("weapon_", "");
        return new { name, id = ItemHelper.GetItemDefIndex($"weapon_{name}") };
    }

    public static string ToSite(int? site) =>
        site switch
        {
            1 => "a",
            2 => "b",
            _ => "none",
        };
}
