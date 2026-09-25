/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;

namespace Match;

public static partial class Natives
{
    public static readonly VirtualFunctionVoid<nint, int> CCSPlayerController_ChangeTeam = new(
        "CCSPlayerController",
        GameData.GetOffset("CCSPlayerController_ChangeTeam")
    );
}
