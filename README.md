# Tamp.MsDeploy

> Typed wrappers for Web Deploy (`msdeploy.exe`) — payload synchronization to IIS targets. Sync and Dump verbs with typed source/destination providers (`contentPath`, `iisApp`, `package`, `archiveDir`) and typed skip rules.

| Package | Status |
|---|---|
| `Tamp.MsDeploy` | 0.1.0 (initial) |

## Install

```bash
dotnet add package Tamp.MsDeploy
```

Multi-targets net8 / net9 / net10. The **wrapper assembly** runs on any supported runtime — but `msdeploy.exe` itself is Windows-only, so build agents that invoke it must be Windows.

## Scope: payload sync only

This package wraps `msdeploy.exe` for **payload synchronization**. Site lifecycle (start/stop/recycle, app-pool management, binding configuration) is a separate concern handled by `Tamp.IisOnPrem`. A typical end-to-end deploy composes both:

```csharp
Target Deploy => _ => _
    .DependsOn(Build, Test, PublishWebDeploy)
    .Executes(() =>
    {
        IisOnPrem.PrepForDeploy("MySite");
        MsDeploy.Sync(MsDeploy, s => s
            .SetSource(MsDeployProvider.ContentPath(PublishDir))
            .SetDestination(MsDeployProvider.IisApp("MySite")));
        IisOnPrem.RestartFromDeploy("MySite");
    });
```

## Quick start

### Local copy

```csharp
using Tamp;
using Tamp.MsDeploy;

class Build : TampBuild
{
    public static int Main(string[] args) => Execute<Build>(args);

    [FromPath("msdeploy")] readonly Tool MsDeploy = null!;

    Target Deploy => _ => _.Executes(() => MsDeploy.Sync(MsDeploy, s => s
        .SetSource(MsDeployProvider.ContentPath("artifacts/publish"))
        .SetDestination(MsDeployProvider.ContentPath(@"C:\inetpub\wwwroot\MySite"))));
}
```

### Remote IIS via Web Management Service

```csharp
[Parameter] readonly Secret AgentPassword = null!;

Target DeployRemote => _ => _.Executes(() => MsDeploy.Sync(MsDeploy, s => s
    .SetSource(MsDeployProvider.ContentPath("artifacts/publish"))
    .SetDestination(MsDeployProvider.IisApp("MySite")
        .WithComputerName("https://web1.example.com:8172/msdeploy.axd?site=MySite")
        .WithCredentials("deploy-bot", AgentPassword))
    .AddSkipRule(MsDeploySkipRule.FilePath(@".*\\web\.config$"))
    .AddSkipRule(MsDeploySkipRule.DirPath(@".*\\App_Data$"))
    .AddDisableLink("AppPoolExtension")
    .SetAllowUntrusted()
    .SetRetryAttempts(3)));
```

## Verb surface (v1)

| Verb | Wraps | Required |
|---|---|---|
| `Sync` | `-verb:sync` | Source + Destination providers |
| `Dump` | `-verb:dump` | Source provider (discovery only) |

## Providers — typed, not stringly

Source and destination are `MsDeployProvider` records. Static factories cover the providers adopters reach for in CI; an escape hatch is provided for the rest:

```csharp
MsDeployProvider.ContentPath("artifacts/publish")
MsDeployProvider.IisApp("MySite")
MsDeployProvider.Package("artifacts/app.zip")
MsDeployProvider.ArchiveDir("artifacts/archive")
MsDeployProvider.Custom("dbDacFx", "artifacts/db.dacpac")  // escape hatch
```

Each provider can be enriched fluently:

```csharp
MsDeployProvider.IisApp("MySite")
    .WithComputerName("https://web1.example.com:8172/msdeploy.axd?site=MySite")
    .WithCredentials("deploy-bot", agentPassword, MsDeployAuthType.Basic)
    .WithExtraSetting("includeAcls=false")
```

Passwords are always `Secret` — they're embedded in the rendered token (msdeploy requires inline credentials) but tracked separately on `CommandPlan.Secrets` so the runner can redact them from logs and plan output.

## Skip rules — typed

```csharp
MsDeploySkipRule.FilePath(@".*\\web\.config$")
MsDeploySkipRule.DirPath(@".*\\App_Data$")
new MsDeploySkipRule { ObjectName = "setAcl" }
```

No raw `-skip:` string passthrough at the public surface — the typed shape catches typos at compile time and renders the comma-separated payload for you.

## Cross-cutting flags

| Flag | Setter | Notes |
|---|---|---|
| `-allowUntrusted` | `SetAllowUntrusted()` | Required for self-signed agent certs on internal hosts. |
| `-verbose` | `SetVerbose()` | |
| `-whatif` | `SetWhatIf()` | Dry-run inside msdeploy itself. |
| `-retryAttempts:N` | `SetRetryAttempts(n)` | |
| `-retryInterval:MS` | `SetRetryIntervalMs(ms)` | |
| `-disableLink:NAME` | `AddDisableLink(name)` | Repeatable. Common: `AppPoolExtension`, `ContentExtension`. |
| `-enableLink:NAME` | `AddEnableLink(name)` | Repeatable. |
| `-useCheckSum` | `SetUseChecksum()` (Sync only) | Checksum diff instead of timestamp. |

## Settings authoring — fluent or object-init

Both styles produce identical `CommandPlan`s. Fluent is canonical in docs / templates.

## License

MIT — see [LICENSE](LICENSE).
