# Changelog

All notable changes to GameSync are documented here. Version numbers follow [Semantic Versioning](https://semver.org/).

## [1.0.2] - 2026-08-12

### Added

- Working self-update: the release installer is downloaded, verified and installed unattended, and GameSync restarts itself — no manual download step
- Background update check a few seconds after startup, surfaced as a dismissable banner with an **Install update** action
- Download progress for the update in Settings
- `--check-update` and `--update` CLI commands (`--check-update` exits with `10` when a newer release exists)
- `GAMESYNC_UPDATE_ON_STARTUP=0` disables the startup check
- [docs/updates.md](docs/updates.md) documents the update flow, its verification steps and how to test it

### Security

- Update downloads are verified against the size and `sha256` digest GitHub publishes with the release asset and must carry an executable header; a payload failing any check is deleted instead of run. The installer is not code-signed, so this replaces signature validation.

### Fixed

- Installing an update no longer just opens a browser download; installers are started through the shell so Windows can present the UAC prompt the installer requires

## [1.0.1] - 2026-08-12

### Added

- Import games from an installed Steam library (registry + `libraryfolders.vdf` + `appmanifest_*.acf` discovery) with multi-select and per-game duplicate handling
- Headless CLI: `--help`, `--status`, `--settings`, `--sync`, `--game <id>` run in the console without opening the window
- Launch games through Steam (`steam://run/<appid>`), with an optional monitor process to detect when the session ends
- Drag and drop a `.exe` or `.lnk` shortcut onto the launch field; shortcuts are resolved to their target
- **Browse** button for every save location in Game details (previously only **Remove** was available)
- Git LFS is used automatically for save files larger than 50 MB under `saves/`
- New app logo, regenerated across app tiles, window icons, installer icon and docs (`tools/generate-brand-assets.ps1`)

### Changed

- Steam import only pre-fills save paths that actually exist on disk, and reports how many games still need a save path
- Game registration (`games.json` + `machine.json` + commit/push) moved into a shared `IGameRegistrationService`
- Releases now publish the `CHANGELOG.md` section for the tagged version as the release body

### Fixed

- In-app update checks pointed at the wrong GitHub owner, so no update was ever found

## [1.0.0] - 2026-08-12

### Added

- Native Windows desktop app (WinUI 3) for synchronizing game save files
- GitHub OAuth device flow sign-in (no client secret; token in Windows Credential Manager)
- Private GitHub repository as the sync backend (LibGit2Sharp — no separate Git install)
- Game library: add games, configure save paths and executables per PC
- Sync before launch and after exit; conflict resolution dialog for Git index conflicts
- Sync history with restore and local backups
- Inno Setup installer (`GameSync-Setup-x64.exe`) distributed via GitHub Releases
- In-app update check against GitHub Releases
- Local logging with secret redaction (`%LOCALAPPDATA%\GameSync\logs\`)

### Security

- Atomic file writes for save copies and backups
- Local save path policy blocks sensitive system folders
- HTTPS-only update downloads from GitHub hosts

[1.0.1]: https://github.com/TCDev69/GameSync/releases/tag/v1.0.1
[1.0.0]: https://github.com/TCDev69/GameSync/releases/tag/v1.0.0
