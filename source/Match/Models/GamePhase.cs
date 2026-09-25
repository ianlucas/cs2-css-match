/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Ian Lucas. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace Match;

public enum GamePhase
{
    GAMEPHASE_WARMUP_ROUND = 0,
    GAMEPHASE_PLAYING_STANDARD = 1,
    GAMEPHASE_PLAYING_FIRST_HALF = 2,
    GAMEPHASE_PLAYING_SECOND_HALF = 3,
    GAMEPHASE_HALFTIME = 4,
    GAMEPHASE_MATCH_ENDED = 5,
    GAMEPHASE_MAX = 6,
}
