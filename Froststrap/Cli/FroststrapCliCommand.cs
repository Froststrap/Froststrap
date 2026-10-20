// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using DotMake.CommandLine;

namespace Froststrap.Cli;

[CliCommand(
    Name = "froststrap",
    Description = "Froststrap - A Fishstrap fork\n\n" +
        "Examples:\n" +
        "  froststrap --player \"roblox-player:1+launchmode:play+channel:zcontrol\"\n" +
        "  froststrap --studio \"place.rbxl\"\n" +
        "  froststrap --settings --console",
    ShortFormAutoGenerate = CliNameAutoGenerate.None)]
internal class FroststrapCliCommand
{
    [CliOption(Name = "--settings", Description = "Open the settings window", Arity = CliArgumentArity.Zero)]
    public bool Settings { get; set; }

    [CliOption(Name = "--background-updater", Description = "Run as the background updater", Arity = CliArgumentArity.Zero)]
    public bool BackgroundUpdater { get; set; }

    [CliOption(Name = "--no-launch", Description = "Install or update Roblox without launching it", Arity = CliArgumentArity.Zero)]
    public bool NoLaunch { get; set; }

    [CliOption(Name = "--test-mode", Description = "Enable test mode", Arity = CliArgumentArity.Zero)]
    public bool TestMode { get; set; }

    [CliOption(Name = "--upgrade", Description = "Force an upgrade", Arity = CliArgumentArity.Zero)]
    public bool Upgrade { get; set; }

    [CliOption(
        Name = "--player",
        Alias = "p",
        Description = "Launch Roblox Player; pass a roblox-player: URI or launch arguments as a target",
        Required = false,
        Arity = CliArgumentArity.Zero)]
    public bool Player { get; set; }

    [CliOption(
        Name = "--studio",
        Alias = "s",
        Description = "Launch Roblox Studio; pass a roblox-studio: URI or a local place file as a target",
        Required = false,
        Arity = CliArgumentArity.Zero)]
    public bool Studio { get; set; }

    [CliOption(
        Name = "--roblox-version",
        Description = "Pin the Roblox client version to the given version hash",
        Required = false,
        Arity = CliArgumentArity.ExactlyOne)]
    public string? RobloxVersion { get; set; }

    [CliOption(
        Name = "--channel",
        Description = "Use the given Roblox release channel",
        Required = false,
        Arity = CliArgumentArity.ExactlyOne)]
    public string? Channel { get; set; }

    [CliOption(Name = "--force", Description = "Force a reinstallation of Roblox", Arity = CliArgumentArity.Zero)]
    public bool Force { get; set; }

    [CliOption(
        Name = "--game-shortcut",
        Description = "Launch a game from shortcut data (placeId[;jobId[;accessCode]])",
        Required = false,
        Arity = CliArgumentArity.ExactlyOne)]
    public string? GameShortcut { get; set; }

    [CliOption(Name = "--console", Alias = "c", Description = "Allocate a debug console window", Arity = CliArgumentArity.Zero)]
    public bool ShowConsole { get; set; }

    [CliOption(Name = "--no-gpu", Alias = "g", Description = "Use software rendering instead of GPU acceleration", Arity = CliArgumentArity.Zero)]
    public bool NoGpu { get; set; }

    [CliArgument(
        Description = "Roblox launch URI (roblox:, roblox-player:, roblox-studio:), " +
            "a version-<hash> pin, or a Roblox Studio place file")]
    public string[] Targets { get; set; } = [];
}
