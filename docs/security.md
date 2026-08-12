# Security notes

GameSync is a full-trust WinUI desktop app. This document records the threat model and mitigations reviewed for production.

## Secrets

| Secret | Storage |
|--------|---------|
| GitHub access token | Windows Credential Manager (`GameSync/GitHub/...`) |
| OAuth client ID | Public (device flow); configure via env / Options — **never** commit a client secret |

Logs redact `access_token`, `refresh_token`, `Bearer `, `password`, `client_secret`, and common `gh*` token prefixes.

## Path safety

- Remote Git paths are validated with `IPathResolver.IsSafeRemotePath` / `MapRemotePathToRepository` (no `..` traversal outside the clone).
- Local templates expand environment variables then `GetFullPath`.
- Launch refuses executables under `%LOCALAPPDATA%\GameSync\repositories|backups|cache`.
- Repository content is **never** executed automatically.

## Process launching

- Game processes: `UseShellExecute = false`, user-configured path + args from `machine.json`.
- URLs: `IUriLauncher` allows **http/https only**.
- Logs folder: `explorer.exe` with a quoted directory path.

## Untrusted repository content

Cloned repositories may contain arbitrary files. GameSync copies save trees and reads `config/games.json`. Treat the GitHub repo as trusted by the signed-in user; do not open executable attachments from the clone.

## Updates

Updates are delivered as Inno Setup installers (`GameSync-Setup-*.exe`) via HTTPS GitHub Release assets. Only GitHub download hosts are accepted. Because the installer is not code-signed, the download is checked against the size and `sha256` digest GitHub publishes with the asset and must carry an `MZ` executable header; a payload failing any check is deleted instead of executed. Failed updates do not wipe LocalAppData.

## Residual risks (accepted)

- User-configured game executables are inherently arbitrary code execution **by design**.
- Unsigned Setup.exe builds may trigger SmartScreen; users must trust the publisher.
- Desktop `.lnk` game shortcuts point at the installed `GameSync.exe`; recreate them after moving the install directory.
