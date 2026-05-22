namespace Tamp.MsDeploy;

/// <summary>
/// msdeploy authentication mode (<c>authType=</c> in the source/dest token).
/// </summary>
public enum MsDeployAuthType
{
    /// <summary>Default — passes Windows credentials of the current process.</summary>
    Default,
    /// <summary>HTTP Basic auth — username + password.</summary>
    Basic,
    /// <summary>NTLM auth.</summary>
    Ntlm,
}

/// <summary>
/// Typed msdeploy source / destination provider. Models the
/// <c>-source:provider=path,computerName=...,userName=...,password=...</c>
/// shape as a record so adopters never hand-concatenate connection strings.
/// </summary>
/// <remarks>
/// msdeploy supports many provider kinds (~80); this record exposes the
/// handful adopters reach for in CI pipelines: <c>contentPath</c>, <c>iisApp</c>,
/// <c>package</c>, <c>archiveDir</c>. Other providers can be modeled via
/// <see cref="Custom"/> until / unless they earn a static factory.
/// </remarks>
public sealed record MsDeployProvider
{
    /// <summary>Provider kind (e.g. <c>"contentPath"</c>, <c>"iisApp"</c>, <c>"package"</c>, <c>"archiveDir"</c>).</summary>
    public required string Kind { get; init; }

    /// <summary>Path / value for the provider (the bit after the <c>=</c>).</summary>
    public required string Value { get; init; }

    /// <summary>Remote computer URL or hostname (<c>computerName=</c>). Null for local providers.</summary>
    public string? ComputerName { get; init; }

    /// <summary>Username for remote agent connection (<c>userName=</c>).</summary>
    public string? UserName { get; init; }

    /// <summary>Password for remote agent connection (<c>password=</c>). Tracked as <see cref="Secret"/> so it's redacted from logs.</summary>
    public Secret? Password { get; init; }

    /// <summary>Auth mode (<c>authType=</c>). Defaults to <see cref="MsDeployAuthType.Default"/> (omitted from the token).</summary>
    public MsDeployAuthType AuthType { get; init; } = MsDeployAuthType.Default;

    /// <summary>Extra provider settings beyond the standard fields — appended verbatim to the token (e.g. <c>"includeAcls=false"</c>).</summary>
    public IReadOnlyList<string> ExtraSettings { get; init; } = Array.Empty<string>();

    // ---- Static factories for the common providers ----

    /// <summary>Local or UNC path (<c>contentPath=&lt;path&gt;</c>).</summary>
    public static MsDeployProvider ContentPath(string path) =>
        new() { Kind = "contentPath", Value = path };

    /// <summary>IIS application path (<c>iisApp=&lt;site/app&gt;</c>). Use with a <c>computerName</c> for remote targets.</summary>
    public static MsDeployProvider IisApp(string siteOrApp) =>
        new() { Kind = "iisApp", Value = siteOrApp };

    /// <summary>Web Deploy zip package (<c>package=&lt;file.zip&gt;</c>).</summary>
    public static MsDeployProvider Package(string packagePath) =>
        new() { Kind = "package", Value = packagePath };

    /// <summary>Web Deploy archive directory (<c>archiveDir=&lt;path&gt;</c>).</summary>
    public static MsDeployProvider ArchiveDir(string archivePath) =>
        new() { Kind = "archiveDir", Value = archivePath };

    /// <summary>Escape-hatch for providers not modeled as first-class factories.</summary>
    public static MsDeployProvider Custom(string kind, string value) =>
        new() { Kind = kind, Value = value };

    /// <summary>Fluent: attach a remote computer URL.</summary>
    public MsDeployProvider WithComputerName(string computerName) => this with { ComputerName = computerName };

    /// <summary>Fluent: attach credentials. Password is captured as <see cref="Secret"/>.</summary>
    public MsDeployProvider WithCredentials(string userName, Secret password, MsDeployAuthType authType = MsDeployAuthType.Basic) =>
        this with { UserName = userName, Password = password, AuthType = authType };

    /// <summary>Fluent: append extra provider settings.</summary>
    public MsDeployProvider WithExtraSetting(string keyEqualsValue) =>
        this with { ExtraSettings = new List<string>(ExtraSettings) { keyEqualsValue } };

    /// <summary>Render the provider as a single msdeploy <c>-source:</c> / <c>-dest:</c> token (without the leading flag).</summary>
    internal string Render()
    {
        var parts = new List<string> { $"{Kind}={Value}" };
        if (!string.IsNullOrEmpty(ComputerName)) parts.Add($"computerName={ComputerName}");
        if (!string.IsNullOrEmpty(UserName)) parts.Add($"userName={UserName}");
        if (Password is not null) parts.Add($"password={Password.Reveal()}");
        if (AuthType != MsDeployAuthType.Default)
            parts.Add($"authType={AuthType switch
            {
                MsDeployAuthType.Basic => "Basic",
                MsDeployAuthType.Ntlm => "NTLM",
                _ => throw new InvalidOperationException($"Unhandled AuthType: {AuthType}"),
            }}");
        foreach (var e in ExtraSettings) parts.Add(e);
        return string.Join(",", parts);
    }
}

/// <summary>
/// Typed skip rule. Models <c>-skip:objectName=section,absolutePath=...</c>
/// so adopters don't hand-build the comma-separated payload.
/// </summary>
public sealed record MsDeploySkipRule
{
    /// <summary>e.g. <c>"filePath"</c>, <c>"dirPath"</c>, <c>"setAcl"</c>.</summary>
    public string? ObjectName { get; init; }
    /// <summary>Regex matched against the absolute path.</summary>
    public string? AbsolutePath { get; init; }
    /// <summary>e.g. <c>"path"</c> — the attribute used to scope the rule.</summary>
    public string? KeyAttribute { get; init; }
    /// <summary>Action to take (rarely needed; default = skip).</summary>
    public string? SkipAction { get; init; }

    public static MsDeploySkipRule FilePath(string regex) =>
        new() { ObjectName = "filePath", AbsolutePath = regex };

    public static MsDeploySkipRule DirPath(string regex) =>
        new() { ObjectName = "dirPath", AbsolutePath = regex };

    internal string Render()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(ObjectName)) parts.Add($"objectName={ObjectName}");
        if (!string.IsNullOrEmpty(AbsolutePath)) parts.Add($"absolutePath={AbsolutePath}");
        if (!string.IsNullOrEmpty(KeyAttribute)) parts.Add($"keyAttribute={KeyAttribute}");
        if (!string.IsNullOrEmpty(SkipAction)) parts.Add($"skipAction={SkipAction}");
        if (parts.Count == 0) throw new InvalidOperationException("MsDeploySkipRule must have at least one field set.");
        return string.Join(",", parts);
    }
}
