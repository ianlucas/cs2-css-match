/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;

namespace Match;

public static class Cstv
{
    public static string? Filename { get; set; }

    public static void Initialize()
    {
        Runtime.Plugin.AddCommandListener("changelevel", OnChangelevelPre, HookMode.Pre);
    }

    public static void Shutdown()
    {
        Runtime.Plugin.RemoveCommandListener("changelevel", OnChangelevelPre, HookMode.Pre);
    }

    public static HookResult OnChangelevelPre(CCSPlayerController? _, CommandInfo info)
    {
        if (IsRecording())
        {
            Stop();
            Server.ExecuteCommand(info.GetCommandString);
            return HookResult.Stop;
        }
        return HookResult.Continue;
    }

    public static bool IsRecording() => Filename != null;

    public static void Record(string? filename)
    {
        if (!IsEnabled() || IsRecording() || filename == null)
            return;
        Filename = filename;
        Server.ExecuteCommand($"tv_record \"{filename}\"");
        Runtime.Log($"Demo recording started: {filename}");
    }

    public static void Stop()
    {
        if (IsRecording())
        {
            Filename = null;
            Server.ExecuteCommand("tv_stoprecord");
            Runtime.Log("Demo recording stopped.");
        }
    }

    public static string? GetFilename() => Filename;

    public static bool IsEnabled() => ConVar.Find("tv_enable")?.GetPrimitiveValue<bool>() == true;

    public static void Set(bool value)
    {
        if (value)
            ServerHelper.ExecuteCommand([
                "tv_enable 1",
                "tv_record_immediate 1",
                $"tv_delay {ConVars.TvDelay.Value}",
            ]);
        else
            Server.ExecuteCommand("tv_enable 0");
    }
}
