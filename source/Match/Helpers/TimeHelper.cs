/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;

namespace Match;

public static class TimeHelper
{
    public static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static long NowSeconds() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public static string FormatMmSs(long seconds)
    {
        return $"{seconds / 60}:{seconds % 60:D2}";
    }

    public static CancellationTokenSource DelayTicks(int ticks, Action callback)
    {
        var cts = new CancellationTokenSource();
        Server.RunOnTick(
            Server.TickCount + ticks,
            () =>
            {
                if (!cts.IsCancellationRequested)
                    callback();
            }
        );
        return cts;
    }
}
