namespace Tamp.MsDeploy;

/// <summary>
/// Common knobs shared by every msdeploy verb. The verb itself (<c>-verb:sync</c> /
/// <c>-verb:dump</c>) and source/destination tokens are emitted by subclasses;
/// this base owns the cross-cutting flags (<c>-allowUntrusted</c>, <c>-whatif</c>,
/// <c>-verbose</c>, <c>-retryAttempts</c>, <c>-retryInterval</c>, <c>-disableLink:</c>).
/// </summary>
/// <remarks>
/// msdeploy uses leading-dash <c>-flag:value</c> arguments where each flag is a single
/// process arg. Source/dest tokens bundle provider + path + connection params as a
/// comma-separated payload — see <see cref="MsDeployProvider"/>.
/// </remarks>
public abstract class MsDeploySettingsBase
{
    /// <summary>Working directory for the spawned <c>msdeploy.exe</c> process.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Per-invocation environment variables.</summary>
    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    /// <summary>Allow connections to remote agents with untrusted TLS certificates (<c>-allowUntrusted</c>).</summary>
    public bool AllowUntrusted { get; set; }

    /// <summary>Verbose output (<c>-verbose</c>).</summary>
    public bool Verbose { get; set; }

    /// <summary>Preview the operation without applying changes (<c>-whatif</c>).</summary>
    public bool WhatIf { get; set; }

    /// <summary>Number of times to retry on transient failure (<c>-retryAttempts:N</c>).</summary>
    public int? RetryAttempts { get; set; }

    /// <summary>Milliseconds between retry attempts (<c>-retryInterval:MS</c>).</summary>
    public int? RetryIntervalMs { get; set; }

    /// <summary>
    /// Link extensions to disable for this run (<c>-disableLink:NAME</c>). Multiple flags supported.
    /// Common values: <c>"AppPoolExtension"</c>, <c>"ContentExtension"</c>, <c>"CertificateExtension"</c>.
    /// </summary>
    public List<string> DisableLinks { get; } = new();

    /// <summary>Link extensions to enable for this run (<c>-enableLink:NAME</c>).</summary>
    public List<string> EnableLinks { get; } = new();

    /// <summary>Subclasses emit the <c>-verb:</c> flag plus verb-specific source/dest tokens.</summary>
    protected abstract IEnumerable<string> BuildVerbArguments();

    /// <summary>Subclasses extending the secret list.</summary>
    protected virtual IEnumerable<Secret> CollectSecrets() => Array.Empty<Secret>();

    /// <summary>Per-verb validation hook.</summary>
    protected virtual void Validate() { }

    internal CommandPlan ToCommandPlan(Tool tool)
    {
        Validate();

        var args = new List<string>();
        args.AddRange(BuildVerbArguments());

        foreach (var link in DisableLinks) args.Add($"-disableLink:{link}");
        foreach (var link in EnableLinks) args.Add($"-enableLink:{link}");
        if (AllowUntrusted) args.Add("-allowUntrusted");
        if (Verbose) args.Add("-verbose");
        if (WhatIf) args.Add("-whatif");
        if (RetryAttempts is int n) args.Add($"-retryAttempts:{n}");
        if (RetryIntervalMs is int ms) args.Add($"-retryInterval:{ms}");

        return new CommandPlan
        {
            Executable = tool.Executable.Value,
            Arguments = args,
            Environment = new Dictionary<string, string>(EnvironmentVariables),
            WorkingDirectory = WorkingDirectory ?? tool.WorkingDirectory,
            Secrets = CollectSecrets().ToList(),
        };
    }
}

/// <summary>Fluent setters for the common knobs.</summary>
public static class MsDeploySettingsBaseExtensions
{
    public static T SetWorkingDirectory<T>(this T s, string? cwd) where T : MsDeploySettingsBase { s.WorkingDirectory = cwd; return s; }
    public static T SetAllowUntrusted<T>(this T s, bool v = true) where T : MsDeploySettingsBase { s.AllowUntrusted = v; return s; }
    public static T SetVerbose<T>(this T s, bool v = true) where T : MsDeploySettingsBase { s.Verbose = v; return s; }
    public static T SetWhatIf<T>(this T s, bool v = true) where T : MsDeploySettingsBase { s.WhatIf = v; return s; }
    public static T SetRetryAttempts<T>(this T s, int? n) where T : MsDeploySettingsBase { s.RetryAttempts = n; return s; }
    public static T SetRetryIntervalMs<T>(this T s, int? ms) where T : MsDeploySettingsBase { s.RetryIntervalMs = ms; return s; }
    public static T AddDisableLink<T>(this T s, string linkName) where T : MsDeploySettingsBase { s.DisableLinks.Add(linkName); return s; }
    public static T AddEnableLink<T>(this T s, string linkName) where T : MsDeploySettingsBase { s.EnableLinks.Add(linkName); return s; }
    public static T SetEnvironmentVariable<T>(this T s, string name, string value) where T : MsDeploySettingsBase { s.EnvironmentVariables[name] = value; return s; }
}

/// <summary>
/// Settings for <c>msdeploy.exe -verb:sync</c>. Requires source AND destination providers.
/// Skip / replace rules are typed (no raw <c>-skip:</c> / <c>-replace:</c> string passthrough at the public surface).
/// </summary>
public sealed class SyncSettings : MsDeploySettingsBase
{
    /// <summary>Source provider — local path, IIS app, package, etc. Required.</summary>
    public MsDeployProvider? Source { get; set; }

    /// <summary>Destination provider. Required.</summary>
    public MsDeployProvider? Destination { get; set; }

    /// <summary>Typed skip rules (<c>-skip:...</c>). Multiple supported.</summary>
    public List<MsDeploySkipRule> SkipRules { get; } = new();

    /// <summary>Use a checksum-based diff instead of timestamp-based (<c>-useCheckSum</c>).</summary>
    public bool UseChecksum { get; set; }

    public SyncSettings SetSource(MsDeployProvider source) { Source = source; return this; }
    public SyncSettings SetDestination(MsDeployProvider destination) { Destination = destination; return this; }
    public SyncSettings AddSkipRule(MsDeploySkipRule rule) { SkipRules.Add(rule); return this; }
    public SyncSettings SetUseChecksum(bool v = true) { UseChecksum = v; return this; }

    protected override void Validate()
    {
        if (Source is null) throw new InvalidOperationException("Source is required for Sync (set via SetSource).");
        if (Destination is null) throw new InvalidOperationException("Destination is required for Sync (set via SetDestination).");
    }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "-verb:sync";
        yield return $"-source:{Source!.Render()}";
        yield return $"-dest:{Destination!.Render()}";
        foreach (var rule in SkipRules) yield return $"-skip:{rule.Render()}";
        if (UseChecksum) yield return "-useCheckSum";
    }

    protected override IEnumerable<Secret> CollectSecrets()
    {
        if (Source?.Password is { } sp) yield return sp;
        if (Destination?.Password is { } dp) yield return dp;
    }
}

/// <summary>
/// Settings for <c>msdeploy.exe -verb:dump</c>. Discovery against a source provider; emits
/// the provider's manifest. No destination.
/// </summary>
public sealed class DumpSettings : MsDeploySettingsBase
{
    /// <summary>Source provider to introspect. Required.</summary>
    public MsDeployProvider? Source { get; set; }

    public DumpSettings SetSource(MsDeployProvider source) { Source = source; return this; }

    protected override void Validate()
    {
        if (Source is null) throw new InvalidOperationException("Source is required for Dump (set via SetSource).");
    }

    protected override IEnumerable<string> BuildVerbArguments()
    {
        yield return "-verb:dump";
        yield return $"-source:{Source!.Render()}";
    }

    protected override IEnumerable<Secret> CollectSecrets()
    {
        if (Source?.Password is { } sp) yield return sp;
    }
}
