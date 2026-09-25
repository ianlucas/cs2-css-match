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
        uint,
        nint
    > CCSPlayerPawnBase_IncrementNumMVPs = new(
        GameData.GetSignature("CCSPlayerPawnBase::IncrementNumMVPs")
    );
}
