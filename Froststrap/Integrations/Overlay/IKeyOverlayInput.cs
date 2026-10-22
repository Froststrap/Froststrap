// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Models.Overlay;

namespace Froststrap.Integrations.Overlay
{
    internal interface IKeyOverlayInput : IDisposable
    {
        KeyOverlaySnapshot GetSnapshot();
    }
}
