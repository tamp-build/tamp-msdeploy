# Changelog

All notable changes to `Tamp.MsDeploy` are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] — Unreleased

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
