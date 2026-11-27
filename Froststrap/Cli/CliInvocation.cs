// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using Froststrap.Startup;
using NLog;

namespace Froststrap.Cli;

internal sealed class CliInvocation
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly string[] _args;
    private readonly ParseResult _parseResult;
    private readonly FroststrapCliCommand? _command;

    private CliInvocation(string[] args, ParseResult parseResult, FroststrapCliCommand? command)
    {
        _args = args;
        _parseResult = parseResult;
        _command = command;
    }

    public bool ShowConsole => _command?.ShowConsole == true;

    private bool IsHelpOrVersion => !ReferenceEquals(_parseResult.Action, _parseResult.CommandResult.Command.Action);

    public static CliInvocation Parse(string[] args)
    {
        var result = DotMake.CommandLine.Cli.Parse<FroststrapCliCommand>(args);
        var parseResult = result.ParseResult;

        bool canBind = parseResult.Errors.Count == 0
            && ReferenceEquals(parseResult.Action, parseResult.CommandResult.Command.Action);

        return new CliInvocation(args, parseResult, canBind ? result.Bind<FroststrapCliCommand>() : null);
    }

    public bool TryCreateLaunchSettings([NotNullWhen(true)] out LaunchSettings? settings, out int exitCode)
    {
        settings = null;
        exitCode = (int)ErrorCode.ERROR_CANCELLED;

        if (_parseResult.Errors.Count > 0)
        {
            WindowsConsole.AttachToParent();

            foreach (var error in _parseResult.Errors)
            {
                Console.Error.WriteLine(error.Message);
                Logger.Error(error.Message);
            }

            return false;
        }

        if (IsHelpOrVersion)
        {
            WindowsConsole.AttachToParent();
            exitCode = _parseResult.Invoke();
            return false;
        }

        var launchSettings = new LaunchSettings(_command!, _args);

        if (launchSettings.UnknownArgs.Count > 0)
        {
            var unknown = launchSettings.UnknownArgs;
            Logger.Error($"{unknown.Count} unrecognized argument(s): {string.Join(", ", unknown)}");
            return false;
        }

        if (_args.Length != 0)
            Logger.Debug($"Arguments: {string.Join(" ", _args)}");

        settings = launchSettings;
        exitCode = 0;
        return true;
    }
}
