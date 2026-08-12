# Release process

## Version source of truth

| Surface | Source |
|---------|--------|
| Assemblies / NuGet | `Directory.Build.props` → `Version` |
| Inno Setup | `/DMyAppVersion=` (set by `installer/build-release.ps1`) |
| Settings → About | `AppVersion.Semantic` |
| GitHub Release | Tag `vMAJOR.MINOR.PATCH` |

Bump version by editing `Directory.Build.props` (or passing `-Version x.y.z` / `-p:Version=x.y.z` on the release build).

## Packaging

Ship path: **unpackaged** self-contained WinUI 3 app + **Inno Setup** `GameSync-Setup-x64.exe`.

```powershell
.\installer\build-release.ps1 -Version 1.0.0
```

Code signing is **not** used for this product. See [production.md](production.md) for the full publish guide.

## CI / CD

- `.github/workflows/ci.yml` — restore, Debug build, tests, Release build on push/PR.
- `.github/workflows/release.yml` — on tag `v*.*.*`, publish unpackaged x64 + compile Inno Setup, attach `GameSync-Setup-x64.exe` to the GitHub Release.

Optional client environment for in-app update checks:

- `GAMESYNC_UPDATE_OWNER`
- `GAMESYNC_UPDATE_REPO`

## Release checklist

1. Update `Version` in `Directory.Build.props`.
2. Run locally: Debug + Release builds and `dotnet test`.
3. Verify installer build for x64 (see [installer/README.md](../installer/README.md)).
4. Tag `vX.Y.Z` and push (triggers Release workflow).
5. Confirm Release asset: `GameSync-Setup-x64.exe`.
6. Spot-check install → Settings About version → update check.
7. Confirm `%LOCALAPPDATA%\GameSync\` intact after upgrade.

## What was verified in this workspace

Automated verification covers compile, unit/integration tests, and packaging project configuration. End-to-end items that require a signed certificate, GitHub OAuth app, real games, or a second PC are listed as **manual** in the QA matrix in `docs/development.md`.
