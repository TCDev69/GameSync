# Development

## Prerequisites

- Windows 10 (19041+) or Windows 11
- .NET 10 SDK
- Windows App SDK runtime (via NuGet for development)
- Visual Studio 2022+ with WinUI workload **or** `dotnet` CLI
- For GitHub auth: official builds include a default OAuth client id; set `GAMESYNC_GITHUB_CLIENT_ID` only when shipping your own fork

## Build

```powershell
dotnet restore GameSync.sln
dotnet build GameSync.sln -c Debug -p:Platform=x64
dotnet build GameSync.sln -c Release -p:Platform=x64
```

## Test

```powershell
dotnet test GameSync.sln -c Release -p:Platform=x64
```

Unit tests mock `IProcessLauncher` and GitHub HTTP clients. They never launch real games or require a GitHub account.

## Run

```powershell
dotnet run --project src/GameSync.App/GameSync.App.csproj -p:Platform=x64
```

CLI examples:

```powershell
dotnet run --project src/GameSync.App/GameSync.App.csproj -p:Platform=x64 -- --help
dotnet run --project src/GameSync.App/GameSync.App.csproj -p:Platform=x64 -- --settings
dotnet run --project src/GameSync.App/GameSync.App.csproj -p:Platform=x64 -- --status
dotnet run --project src/GameSync.App/GameSync.App.csproj -p:Platform=x64 -- --sync
dotnet run --project src/GameSync.App/GameSync.App.csproj -p:Platform=x64 -- --game cyberpunk_2077
```

## Configuration (dev)

| Variable | Purpose |
|----------|---------|
| `GAMESYNC_GITHUB_CLIENT_ID` | Optional override of baked-in OAuth client id |
| `GAMESYNC_UPDATE_OWNER` / `GAMESYNC_UPDATE_REPO` | Optional override of Releases update feed |

Defaults: Client ID + `TCDev` / `GameSync` in `GameSyncOptions`. Local installer: [Inno Setup 6](https://jrsoftware.org/isinfo.php) + `.\installer\build-release.ps1`.

## Project conventions

- Nullable reference types enabled.
- Async I/O with `CancellationToken` on long-running APIs.
- No UI-thread blocking; no `Thread.Sleep` for workflow control.
- Prefer small, single-purpose services.
- Do not commit secrets.
- Expensive filesystem / Git / HTTP work belongs on background threads; ViewModels use `ConfigureAwait(true)` only when marshalling back to the UI dispatcher is required.

## Manual QA matrix (production)

Automated CI covers build + unit tests. The following require a real machine / GitHub account and are **manual**:

1. Fresh Inno Setup install (`GameSync-Setup-x64.exe`)  
2. GitHub authentication  
3. Repository selection / initialize  
4. Add game + Fetch metadata  
5. Configure save directory + executable  
6. Launch game; pull/push saves  
7. Second PC: pull config, different executable, launch  
8. Desktop + Start Menu shortcuts (`--game <id>` → installed GameSync.exe)  
9. Conflict resolve + history restore  
10. App update via GitHub Releases Setup.exe  
11. Uninstall / reinstall — confirm `%LOCALAPPDATA%\GameSync\` survives  

## Related docs

- [architecture.md](architecture.md)
- [release.md](release.md)
- [security.md](security.md)
- [known-limitations.md](known-limitations.md)
- [../installer/README.md](../installer/README.md)
