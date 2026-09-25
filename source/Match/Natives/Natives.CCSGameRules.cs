/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

namespace Match;

public static partial class Natives
{
    public static readonly MemoryFunctionWithReturn<
        nint,
        bool
    > CCSGameRules_AreTeamsPlayingSwitchedSides = new(
        GameData.GetSignature("CCSGameRules::AreTeamsPlayingSwitchedSides")
    );

    public static readonly MemoryFunctionWithReturn<nint, nint> CCSGameRules_HandleSwapTeams = new(
        GameData.GetSignature("CCSGameRules::HandleSwapTeams")
    );

    public static readonly MemoryFunctionWithReturn<
        nint,
        bool
    > CCSGameRules_IsLastRoundBeforeHalfTime = new(
        GameData.GetSignature("CCSGameRules::IsLastRoundBeforeHalfTime")
    );
}
