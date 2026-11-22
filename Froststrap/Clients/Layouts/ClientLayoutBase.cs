// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;
using Froststrap.Clients.Model;

namespace Froststrap.Clients.Layouts;

/// <summary>
/// Shared plumbing for layouts. Contains no client identity decisions: subclasses answer
/// every question and keep their differences visible.
/// </summary>
internal abstract class ClientLayoutBase : IClientLayout
{
    public abstract string ClientId { get; }

    public abstract ClientTarget Target { get; }

    public abstract ExecutableShape Shape { get; }

    public abstract string? BinaryType { get; }

    public abstract string? ExecutableName { get; }

    public abstract IReadOnlyList<string> ProcessNames { get; }

    public abstract LogShape LogFormat { get; }

    public virtual string? LogKindToken => null;

    public virtual IReadOnlyList<string> ReadySignals => [];

    public virtual IReadOnlyList<string> LeaveSignals => [];

    public virtual IReadOnlyList<string> ExcludedLogTokens => ["CrashHandler", "Installer"];

    public virtual IReadOnlyList<string> RequiredFiles => [];

    public virtual bool MatchProcessNamePrefix => false;

    public virtual string? CrashHandlerProcessName => "RobloxCrashHandler";

    public virtual string? SigningTeamId => null;

    public virtual CookieFormat CookieFormat => CookieFormat.None;

    public virtual ChannelStore ChannelStore => ChannelStore.None;

    public virtual HostIntegration HostIntegration => HostIntegration.None;

    public virtual (string ContentRoot, string ClientSettingsRoot) ModRoots(string versionRoot) => (versionRoot, versionRoot);

    public abstract ClientInstall Resolve(ClientResolveContext context);

    protected string ResolveVersionedRoot(ClientResolveContext context)
    {
        string folder = context.StaticDirectory && !string.IsNullOrEmpty(BinaryType)
            ? BinaryType
            : context.State.VersionGuid;

        return Path.Combine(context.VersionsRoot, folder);
    }

    /// <summary>
    /// Base install for a client whose files live under <c>Versions/{binary type}</c> or
    /// <c>Versions/{version guid}</c>. Callers refine content and log roots with <c>with</c>.
    /// </summary>
    protected ClientInstall VersionedInstall(ClientResolveContext context, bool isBundle = false)
    {
        string root = ResolveVersionedRoot(context);

        return new ClientInstall
        {
            ClientId = ClientId,
            Target = Target,
            VersionGuid = context.State.VersionGuid,
            InstallRoot = root,
            ContentRoot = root,
            ClientSettingsRoot = root,
            IsBundle = isBundle
        };
    }
}
