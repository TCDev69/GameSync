# Changelog

All notable changes to GameSync are documented here. Version numbers follow [Semantic Versioning](https://semver.org/).

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

[1.0.0]: https://github.com/TCDev/GameSync/releases/tag/v1.0.0
