// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Xunit;

namespace Froststrap.Tests;

[CollectionDefinition("PlaytimeDbTests", DisableParallelization = true)]
public sealed class PlaytimeDbTestsCollection : ICollectionFixture<PlaytimeDbTestsFixture>
{
}

public sealed class PlaytimeDbTestsFixture : IDisposable
{
    public void Dispose() { }
}