/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API;

namespace Match;

public static class PathHelper
{
    public static string GetConfigConVarPath(string path = "")
    {
        return $"addons/counterstrikesharp/configs/plugins/Match{path}";
    }

    public static string GetConfigPath(string path = "")
    {
        return GetCSGOPath(GetConfigConVarPath(path));
    }

    public static string GetCSGOPath(string path = "")
    {
        return Path.Combine(Server.GameDirectory, "csgo", path);
    }
}
