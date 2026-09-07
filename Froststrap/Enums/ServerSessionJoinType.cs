// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Enums
{
    internal enum ServerSessionJoinType
    {
        NewGameNoAvailableSlots = 1,
        NewGameSinglePlayer = 2,
        NewGamePrivateGame = 4,
        Specific = 5,
        SpecificPrivateGame = 6,
        MatchMade = 10,
    }
}