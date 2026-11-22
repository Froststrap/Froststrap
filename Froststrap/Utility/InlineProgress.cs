// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Utility;

internal sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
{
    public void Report(T value) => handler(value);
}
