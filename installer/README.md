# GameSync packaging

GameSync ships as an **unpackaged** WinUI 3 desktop app installed with **Inno Setup** (`GameSync-Setup-x64.exe`) for Windows 10/11 (x64). Updates are distributed via **GitHub Releases** (download the new Setup.exe).

> Inno Setup produces a classic installer `.exe`, not an `.msi`. MSIX is not used for distribution.

## User data (survives install / update / uninstall)

All application data lives outside the install directory:

| Path | Contents |
|------|----------|
| `%LOCALAPPDATA%\GameSync\machine.json` | Machine ID, repo connection, per-PC executables |
| `%LOCALAPPDATA%\GameSync\ui.json` | Theme / onboarding flags |
| `%LOCALAPPDATA%\GameSync\repositories\` | Local Git clones |
| `%LOCALAPPDATA%\GameSync\backups\` | Local save backups |
| `%LOCALAPPDATA%\GameSync\logs\` | Rolling logs |
| `%LOCALAPPDATA%\GameSync\cache\` | Cache |

**Uninstall** removes Program Files binaries and Start Menu / Desktop shortcuts created by the installer. It does **not** delete `%LOCALAPPDATA%\GameSync\`. Credential Manager secrets (`GameSync/GitHub/...`) also remain until the user signs out or clears them.

Desktop / Start Menu **game** shortcuts (`.lnk` created by GameSync) are user files and are not removed by uninstall.

## Prerequisites (build machine)

- .NET 10 SDK
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`ISCC.exe`)

## Build installer (local)

From the repository root:

```powershell
.\installer\build-release.ps1
# or pin a version:
.\installer\build-release.ps1 -Version 1.0.0
```

This:

1. Publishes unpackaged self-contained + Windows App SDK self-contained `win-x64` output to `artifacts\publish\`
2. Compiles `installer\GameSync.iss` → `artifacts\GameSync-Setup-x64.exe`

Manual steps:

```powershell
dotnet publish src/GameSync.App/GameSync.App.csproj `
  -c Release `
  -p:Platform=x64 `
  -p:RuntimeIdentifier=win-x64 `
  -p:WindowsPackageType=None `
  -p:WindowsAppSDKSelfContained=true `
  -p:SelfContained=true `
  -p:Version=1.0.0 `
  -o artifacts/publish

& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DMyAppVersion=1.0.0 installer\GameSync.iss
```

**Code signing is not used.** Unsigned installers may show Windows SmartScreen; users can continue with More info → Run anyway. Full production steps: [docs/production.md](../docs/production.md).

## Updates

1. Publish `GameSync-Setup-x64.exe` to a GitHub Release (tag `vMAJOR.MINOR.PATCH`).
2. Set environment variables for the running app (or bake into Options):
   - `GAMESYNC_UPDATE_OWNER`
   - `GAMESYNC_UPDATE_REPO`

Settings → Automatic updates uses `IAppUpdateService` to detect a newer release and open the Setup.exe download URL (HTTPS, GitHub hosts only).

## Versioning

Semantic version `MAJOR.MINOR.PATCH` is defined once in `Directory.Build.props` (`Version`). Pass the same value to `build-release.ps1` / `/DMyAppVersion=`. GitHub Release tags should be `vMAJOR.MINOR.PATCH`.
