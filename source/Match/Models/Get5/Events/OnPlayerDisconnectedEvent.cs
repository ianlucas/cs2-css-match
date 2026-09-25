/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace Match.Get5.Events;

public sealed class OnPlayerDisconnectedEvent : Get5Event
{
    [JsonPropertyName("event")]
    public override string EventName => "player_disconnect";

    [JsonPropertyName("player")]
    public object Player { get; init; } = null!;

    public static OnPlayerDisconnectedEvent Create(CCSPlayerController player) =>
        new() { MatchId = Rules.Id, Player = Get5EventHelpers.ToPlayer(player) };
}
