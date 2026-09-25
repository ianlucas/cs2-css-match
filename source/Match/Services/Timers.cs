/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Timers;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace Match;

public static class Timers
{
    private static readonly ConcurrentDictionary<
        string,
        (CancellationTokenSource Cts, Timer[] Timers)
    > _timers = [];
    private const float ChatInterval = 15.0f;

    public static void ClearAll()
    {
        foreach (var name in _timers.Keys)
            Clear(name);
    }

    public static void Clear(string name)
    {
        if (_timers.TryRemove(name, out var entry))
        {
            entry.Cts.Cancel();
            foreach (var timer in entry.Timers)
                timer.Kill();
        }
    }

    public static void Set(string name, float interval, Action callback)
    {
        Clear(name);
        var cts = new CancellationTokenSource();
        _timers[name] = (cts, [Runtime.Plugin.AddTimer(interval, Defer(cts, callback))]);
    }

    public static void SetEveryChatInterval(string name, Action callback)
    {
        SetRepeat(name, ChatInterval, callback);
    }

    public static void SetEverySecond(string name, Action callback)
    {
        SetRepeat(name, 1, callback);
    }

    private static void SetRepeat(string name, float interval, Action callback)
    {
        Clear(name);
        var cts = new CancellationTokenSource();
        var flags = TimerFlags.STOP_ON_MAPCHANGE;
        _timers[name] = (
            cts,
            [
                Runtime.Plugin.AddTimer(0.001f, Defer(cts, callback), flags),
                Runtime.Plugin.AddTimer(interval, Defer(cts, callback), flags | TimerFlags.REPEAT),
            ]
        );
    }

    // Killing a timer from inside another timer's callback can corrupt CounterStrikeSharp's
    // timer list, and our callbacks often clear timers, so they run on the next frame instead.
    private static Action Defer(CancellationTokenSource cts, Action callback) =>
        () =>
            Server.NextFrame(() =>
            {
                if (!cts.IsCancellationRequested)
                    callback();
            });
}
