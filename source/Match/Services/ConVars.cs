/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;

namespace Match;

public static class ConVars
{
    public static readonly FakeConVar<string> ChatPrefix = new(
        "match_chat_prefix",
        "Prefix displayed before chat messages.",
        "[{red}Match{default}]"
    );

    public static readonly FakeConVar<string> ServerGraphicUrl = new(
        "match_server_graphic_url",
        "URL of the image displayed when a player dies.",
        ""
    );

    public static readonly FakeConVar<int> ServerGraphicDuration = new(
        "match_server_graphic_duration",
        "Duration in seconds to display the server graphic.",
        5
    );

    public static readonly FakeConVar<string> ServerId = new(
        "get5_server_id",
        "Unique identifier for this server.",
        ""
    );

    public static readonly FakeConVar<bool> IsVerbose = new(
        "match_verbose",
        "Enable verbose debug logging.",
        true
    );

    public static readonly FakeConVar<bool> IsTvRecord = new(
        "match_tv_record",
        "Enable automatic demo recording.",
        true
    );

    public static readonly FakeConVar<int> TvDelay = new(
        "match_tv_delay",
        "SourceTV broadcast delay in seconds.",
        105
    );

    public static readonly FakeConVar<bool> IsRestartFirstMap = new(
        "match_restart_first_map",
        "Whether to restart the first map on load.",
        false
    );

    public static readonly FakeConVar<int> MaxRounds = new(
        "match_max_rounds",
        "Maximum number of rounds per match.",
        24
    );

    public static readonly FakeConVar<int> OtMaxRounds = new(
        "match_ot_max_rounds",
        "Number of overtime rounds to determine the winner.",
        6
    );

    public static readonly FakeConVar<int> PlayersNeeded = new(
        "match_players_needed",
        "Total number of players required to start a match.",
        10
    );

    public static readonly FakeConVar<int> PlayersNeededPerTeam = new(
        "match_players_needed_per_team",
        "Number of players required per team to start a match.",
        5
    );

    public static readonly FakeConVar<bool> IsBots = new(
        "match_bots",
        "Allow bots to join and fill empty player slots.",
        false
    );

    public static readonly FakeConVar<bool> IsMatchmaking = new(
        "match_matchmaking",
        "Enable matchmaking mode.",
        false
    );

    public static readonly FakeConVar<bool> IsMatchmakingKick = new(
        "match_matchmaking_kick",
        "Kick players who are not part of the current match.",
        true
    );

    public static readonly FakeConVar<int> MatchmakingReadyTimeout = new(
        "match_matchmaking_ready_timeout",
        "Time in seconds for players to ready up.",
        300
    );

    public static readonly FakeConVar<bool> IsKnifeRoundEnabled = new(
        "match_knife_round_enabled",
        "Enable knife rounds for side selection.",
        true
    );

    public static readonly FakeConVar<int> KnifeVoteTimeout = new(
        "match_knife_vote_timeout",
        "Time in seconds to decide which side to start on after knife round.",
        60
    );

    public static readonly FakeConVar<bool> IsFriendlyPause = new(
        "match_friendly_pause",
        "Allow teams to pause the match at any time.",
        false
    );

    public static readonly FakeConVar<bool> IsForfeitEnabled = new(
        "match_forfeit_enabled",
        "Automatically forfeit teams with disconnected players.",
        true
    );

    public static readonly FakeConVar<int> ForfeitTimeout = new(
        "match_forfeit_timeout",
        "Time in seconds before forfeiting a team with disconnected players.",
        120
    );

    public static readonly FakeConVar<int> SurrenderTimeout = new(
        "match_surrender_timeout",
        "Time in seconds allowed for surrender voting.",
        30
    );

    public static readonly FakeConVar<bool> IsEventStore = new(
        "match_event_store",
        "Store all emitted events to disk during a match.",
        false
    );

    public static readonly FakeConVar<bool> IsResultStore = new(
        "match_result_store",
        "Store map results to disk after each map.",
        true
    );

    public static readonly FakeConVar<string> RemoteLogUrl = new(
        "get5_remote_log_url",
        "URL endpoint for sending match events.",
        ""
    );

    public static readonly FakeConVar<string> RemoteLogHeaderKey = new(
        "get5_remote_log_header_key",
        "Header key name for remote logging requests.",
        ""
    );

    public static readonly FakeConVar<string> RemoteLogHeaderValue = new(
        "get5_remote_log_header_value",
        "Header value for remote logging requests.",
        ""
    );

    public static void Initialize(BasePlugin plugin)
    {
        plugin.RegisterFakeConVars(typeof(ConVars));
    }
}
