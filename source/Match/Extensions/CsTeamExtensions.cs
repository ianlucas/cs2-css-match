/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

using CounterStrikeSharp.API.Modules.Utils;

namespace Match;

public static class CsTeamExtensions
{
    extension(CsTeam self)
    {
        public CsTeam Toggle()
        {
            return self > CsTeam.Spectator
                ? self == CsTeam.Terrorist
                    ? CsTeam.CounterTerrorist
                    : CsTeam.Terrorist
                : self;
        }
    }
}
