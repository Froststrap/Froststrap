// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Clients.Model
{
    internal sealed record InstallRequest
    {
        public required ClientTarget Target { get; init; }

        public InstallReason Reason { get; init; } = InstallReason.Missing;

        public string? Channel { get; init; }

        public bool Force { get; init; }

        public bool Background { get; init; }

        public DistributionState? Existing { get; init; }
    }

    internal sealed record InstallProgress(
        InstallPhase Phase,
        string Message,
        long Current = 0,
        long Total = 0,
        int Step = 0,
        int StepCount = 0,
        int StepPercent = -1)
    {
        public double? Fraction => Total > 0 ? (double)Current / Total : null;
    }

    internal sealed record InstallResult
    {
        public required ClientInstall Install { get; init; }

        public bool Success { get; init; }

        public string? Error { get; init; }
    }

    internal sealed record ClientInspection
    {
        public required ClientInstall Install { get; init; }

        /// <summary>True when the client's state is saved and the resolved install exists.</summary>
        public bool IsInstalled { get; init; }

        /// <summary>True when a distribution state file exists on disk.</summary>
        public bool IsStateSaved { get; init; }

        public InstallReason Reason { get; init; } = InstallReason.Missing;

        public string? Error { get; init; }
    }

    internal sealed record LaunchRequest
    {
        public LaunchMode Mode { get; init; } = LaunchMode.Player;

        public string? CommandLine { get; init; }

        public string? WorkingDirectory { get; init; }

        public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

        public IReadOnlyList<int> AutoclosePids { get; init; } = [];
    }

    internal sealed record LaunchResult
    {
        public bool Started { get; init; }

        public int Pid { get; init; }

        public string? ProcessName { get; init; }

        public string? LogHint { get; init; }

        public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    }

    internal sealed record KillRequest
    {
        public bool Player { get; init; } = true;

        public bool Studio { get; init; }

        /// <summary>
        /// Ignore processes younger than this. Mirrors the rejoin guard that skips a client
        /// started in the last few seconds.
        /// </summary>
        public TimeSpan MinimumAge { get; init; } = TimeSpan.Zero;

        public bool Force { get; init; }
    }
}
