# Changelog

All notable changes to `Tamp.MsDeploy` are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] - 2026-11-20 - .NET 11 in, .NET 8 and .NET 9 out

### Changed

- **Multi-targets `net10.0;net11.0`** instead of `net8.0;net9.0;net10.0` ([ADR 0015](https://github.com/tamp-build/tamp/blob/main/docs/adr/0015-target-framework-strategy.md)). .NET 11 shipped 2026-11-10 and .NET 8 and .NET 9 reached end of support the same day; the rule is to add a TFM the day a release ships and drop one the day Microsoft ends its support. `global.json` pins SDK 11.0.100.

  **If you target `net8.0` or `net9.0`, stay on the previous version** — this one carries no assets your project can resolve.

- **`Tamp.*` pinned to 1.21.0**, which carries a SARIF fix worth knowing about if this project's output is read as evidence: before 1.21.0, a SARIF result whose scanner omitted `level` was read as `none` — the least severe value — instead of the declared `warning`, on every runtime before net11 ([tamp#166](https://github.com/tamp-build/tamp/issues/166)).

- **Publishing moved to nuget.org trusted publishing (OIDC).** The `tamp_nuget_api_key` org secret expired 2026-11-01 along with every other pre-2026-08-17 nuget.org key. The publish job now mints a short-lived key from its own OIDC token, so there is no long-lived credential to rotate or leak.

## [0.1.1] — 2026-09-27

### Added

- Initial release. Typed wrappers for `msdeploy.exe` (Web Deploy) over the two highest-leverage verbs:
  - `Sync` (`-verb:sync`) — payload synchronization from source provider to destination provider.
  - `Dump` (`-verb:dump`) — discovery against a source provider.
- Typed `MsDeployProvider` record with static factories for `contentPath`, `iisApp`, `package`, `archiveDir`, plus a `Custom` escape hatch.
- Fluent provider enrichment: `WithComputerName`, `WithCredentials` (password as `Secret`), `WithExtraSetting`.
- Typed `MsDeploySkipRule` (no raw `-skip:` string passthrough at the public surface).
- Cross-cutting flags: `-allowUntrusted`, `-verbose`, `-whatif`, `-retryAttempts`, `-retryInterval`, `-disableLink:`, `-enableLink:`, `-useCheckSum`.
- Source AND destination passwords tracked independently on `CommandPlan.Secrets`.
- Parallel fluent + object-init authoring surface.
- Multi-target `net8.0;net9.0;net10.0`. Wrapper runs anywhere; `msdeploy.exe` is Windows-only.
- Package now ships XML documentation files (`.xml`) alongside the assembly, so consumers get IntelliSense and API docs. (Mirrors [tamp-build/tamp#3](https://github.com/tamp-build/tamp/pull/50).)

### Scope

- Payload sync only. Site lifecycle (start/stop/recycle, app-pool ops, binding mgmt) is out of scope — handled separately by `Tamp.IisOnPrem`.
