# Architecture

GameSync is a native Windows desktop application (WinUI 3 / Windows App SDK) that synchronizes video game saves through the user's own private GitHub repository. There is **no proprietary GameSync backend**.

## Solution layout

```
GameSync.sln
src/
  GameSync.App/             WinUI 3 UI, ViewModels, navigation, lifecycle, crash handling
  GameSync.Core/            Domain models, interfaces, workflows, CLI parsing, versioning
  GameSync.Infrastructure/  Git/GitHub/Windows/filesystem/update implementations
tests/
  GameSync.Core.Tests/
  GameSync.Infrastructure.Tests/
installer/                  Inno Setup script + build-release.ps1 + packaging notes
.github/workflows/          CI + Release
docs/
```

## Layering rules

| Project | May depend on | Must not depend on |
|---------|---------------|--------------------|
| Core | .NET BCL, Microsoft.Extensions.* abstractions | WinUI, App, Infrastructure |
| Infrastructure | Core, Windows APIs, NuGet clients | App / WinUI UI types |
| App | Core, Infrastructure, WinUI, CommunityToolkit.Mvvm | — |

Business logic lives in Core services and workflows. Views bind to ViewModels. ViewModels call interfaces. Infrastructure implements those interfaces.

## Shared vs local configuration

**Shared (Git):** `config/games.json` plus `saves/<game-id>/...` inside the user's repository.

**Local (not synced):** `%LOCALAPPDATA%\GameSync\` — `machine.json`, `ui.json`, repository clones, cache, logs, backups.

Executable paths differ per PC and therefore live only in `machine.json`.

**Secrets:** Windows Credential Manager exclusively. Never JSON.

## Packaging & updates

- **Unpackaged** WinUI 3 app installed via **Inno Setup** (`GameSync-Setup-x64.exe`) to Program Files, x64 first.
- Shortcuts target the installed `GameSync.exe` via `Environment.ProcessPath`.
- **Updates:** `IAppUpdateService` → `GitHubReleaseAppUpdateService` checks GitHub Releases for a newer tag and opens the HTTPS Setup.exe download URI. User data outside the install directory is preserved.

## Logging & crashes

- File logs under `%LOCALAPPDATA%\GameSync\logs\` with retention (default 14 days), size rollover, and secret redaction.
- `CrashHandler` registers UI / AppDomain / unobserved-task handlers, logs critically, and shows a sanitized dialog.

## Dependency injection

- `AddGameSyncCore()` registers Core services.
- `AddGameSyncInfrastructure()` registers path resolution, configuration, Git/GitHub, sync, launch, shortcuts, metadata, updates, logging.
- The App builds an `IServiceProvider` at startup (`AppServices.Configure`).

## CLI

`AppCommandParser` in Core parses `--game`, `--sync`, `--status`, `--settings`, `--help`. Dashboard and launcher windows share the same parser and single-instance activation.

## Design decisions

1. **Windows App SDK 2.x** targeting `net10.0-windows`.
2. **Minimum OS:** Windows 10 19041 via `TargetPlatformMinVersion`.
3. **JSON:** `System.Text.Json` with camelCase for `games.json`.
4. **Embedded Git:** LibGit2Sharp (no git.exe requirement).
5. **Credential store:** P/Invoke CredWrite/CredRead.
