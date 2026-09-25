/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using System.Diagnostics;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;

namespace Match;

public static class Runtime
{
    public static BasePlugin Plugin { get; set; } = null!;

    public static void Initialize(BasePlugin plugin)
    {
        Plugin = plugin;
    }

    public static void Log(string message, bool sendToChat = false, bool force = false)
    {
        if (sendToChat)
            Server.PrintToChatAll(message);
        if (!ConVars.IsVerbose.Value && !force)
            return;
        var stackTrace = new StackTrace();
        var frame = stackTrace.GetFrame(1);
        var method = frame?.GetMethod();
        var className = method?.DeclaringType?.Name;
        var methodName = method?.Name;
        var prefix =
            className != null && methodName != null ? $"{className}::{methodName}" : "Match";
        Plugin.Logger.LogInformation("{Prefix} {Message}", prefix, message);
    }
}
