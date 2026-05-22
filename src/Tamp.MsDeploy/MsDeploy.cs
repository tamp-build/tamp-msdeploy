namespace Tamp.MsDeploy;

/// <summary>
/// Typed wrappers for <c>msdeploy.exe</c> (Web Deploy). v1 covers the two verbs
/// that drive nearly every CI pipeline: <c>Sync</c> (payload deployment) and
/// <c>Dump</c> (discovery against a remote target).
/// </summary>
/// <remarks>
/// <para>
/// Site lifecycle (start/stop/recycle, app-pool ops, binding mgmt) is OUT of
/// scope — handled by <c>Tamp.IisOnPrem</c>. A typical deploy composes both:
/// <c>IisOnPrem.PrepForDeploy</c> → <c>MsDeploy.Sync</c> → <c>IisOnPrem.RestartFromDeploy</c>.
/// </para>
/// <code>
/// [FromPath("msdeploy")] readonly Tool MsDeploy = null!;
/// [Parameter] readonly Secret AgentPassword = null!;
///
/// Target Deploy => _ => _.Executes(() => MsDeploy.Sync(MsDeploy, s => s
///     .SetSource(MsDeployProvider.ContentPath("artifacts/publish"))
///     .SetDestination(MsDeployProvider.IisApp("MySite")
///         .WithComputerName("https://web1.example.com:8172/msdeploy.axd?site=MySite")
///         .WithCredentials("deploy-agent", AgentPassword))));
/// </code>
/// </remarks>
public static class MsDeploy
{
    /// <summary><c>msdeploy.exe -verb:sync</c> — synchronize payload from a source provider to a destination provider.</summary>
    public static CommandPlan Sync(Tool tool, Action<SyncSettings> configure)
        => Run<SyncSettings>(tool, configure);

    /// <summary><c>msdeploy.exe -verb:dump</c> — discover and emit the manifest of a source provider.</summary>
    public static CommandPlan Dump(Tool tool, Action<DumpSettings> configure)
        => Run<DumpSettings>(tool, configure);

    // ---- Object-init overloads ----
    public static CommandPlan Sync(Tool tool, SyncSettings settings) => Plan(tool, settings);
    public static CommandPlan Dump(Tool tool, DumpSettings settings) => Plan(tool, settings);

    private static CommandPlan Run<T>(Tool tool, Action<T>? configure) where T : MsDeploySettingsBase, new()
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        var s = new T();
        configure?.Invoke(s);
        return s.ToCommandPlan(tool);
    }

    private static CommandPlan Plan<T>(Tool tool, T settings) where T : MsDeploySettingsBase
    {
        if (tool is null) throw new ArgumentNullException(nameof(tool));
        if (settings is null) throw new ArgumentNullException(nameof(settings));
        return settings.ToCommandPlan(tool);
    }
}
