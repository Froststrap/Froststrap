// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Clients.Model;

internal sealed record InstallProgress(
    InstallPhase Phase,
    string Message,
    long Current = 0,
    long Total = 0,
    int Step = 0,
    int StepCount = 0,
    int StepPercent = -1);

internal sealed record InstallResult
{
    public bool Success { get; init; }

    public string? Error { get; init; }
}

internal sealed record LaunchRequest
{
    public LaunchMode Mode { get; init; } = LaunchMode.Player;

    public string? CommandLine { get; init; }

    public GameJoinData? JoinData { get; init; }

    public string? WorkingDirectory { get; init; }

    public bool RunAsAdmin { get; init; }
}

internal sealed record LaunchResult
{
    public bool Started { get; init; }

    public int Pid { get; init; }

    public Func<CancellationToken, Task>? WaitUntilReadyAsync { get; init; }
}

internal sealed record KillRequest
{
    /// <summary>
    /// Ignore processes younger than this. Mirrors the rejoin guard that skips a client
    /// started in the last few seconds.
    /// </summary>
    public TimeSpan MinimumAge { get; init; } = TimeSpan.Zero;

    public bool Force { get; init; }

    public bool WaitForExit { get; init; }
}
