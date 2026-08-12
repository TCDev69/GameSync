# Configuration

## Shared repository layout

```
<repo>/
  config/
    games.json
  saves/
    <game-id>/
      <save-location-id>/   # directory mappings
      settings.dat          # file mappings (example)
```

Local clones live under:

```
%LOCALAPPDATA%\GameSync\repositories\<owner>__<name>\
```

`RepositoryService` derives that path deterministically from GitHub owner + repository name and will not re-clone when a valid Git repository already exists there.

## games.json

```json
{
  "schemaVersion": 1,
  "games": [
    {
      "id": "cyberpunk_2077",
      "title": "Cyberpunk 2077",
      "coverUrl": "https://...",
      "metadataProviderId": "igdb",
      "metadataExternalId": "1877",
      "saveLocations": [
        {
          "id": "main",
          "type": "directory",
          "remotePath": "saves/cyberpunk_2077/main",
          "localPath": "%USERPROFILE%/Saved Games/CD Projekt Red/Cyberpunk 2077"
        },
        {
          "id": "settings",
          "type": "file",
          "remotePath": "saves/cyberpunk_2077/settings.dat",
          "localPath": "%APPDATA%/Game/settings.dat"
        }
      ]
    }
  ]
}
```

### Rules

- `id` is stable across machines (lowercase, digits, underscores).
- Multiple save locations per game are supported (`file` or `directory`).
- `localPath` may contain Windows environment variables.
- `remotePath` is relative to the repository root.
- Remote paths must **not** be rooted, contain `..`, or escape the repository (enforced by `PathResolver`).

### Mapping examples

Directory:

- local: `C:\Users\...\Saved Games\Game`
- remote: `saves/game/main`

File:

- local: `C:\Users\...\AppData\...\settings.dat`
- remote: `saves/game/settings.dat`

## Local machine.json

Path: `%LOCALAPPDATA%\GameSync\machine.json`

```json
{
  "schemaVersion": 1,
  "machineId": "DESKTOP",
  "repository": {
    "owner": "you",
    "name": "gamesync-saves",
    "defaultBranch": "main",
    "localPath": "C:/Users/you/AppData/Local/GameSync/repositories/you__gamesync-saves",
    "isPrivate": true
  },
  "games": {
    "cyberpunk_2077": {
      "executable": "D:/Games/Cyberpunk 2077/bin/x64/Cyberpunk2077.exe",
      "arguments": "",
      "workingDirectory": ""
    }
  },
  "backup": {
    "enabled": true,
    "maxBackupsPerGame": 10
  }
}
```

Never store access tokens in this file. GitHub tokens are stored in Windows Credential Manager under `GameSync/GitHub/AccessToken` (see `GitService`).

## Local data directories

```
%LOCALAPPDATA%\GameSync\
  machine.json
  ui.json
  repositories\
    <owner>__<name>\
  cache\
  logs\
  backups\
    <game-id>\
      <yyyy-MM-dd_HH-mm-ss>\
```

### Uninstall behavior

Inno Setup uninstall removes Program Files binaries and installer-created Start Menu / Desktop shortcuts. It does **not** delete `%LOCALAPPDATA%\GameSync\` (repositories, backups, logs, machine config) or Credential Manager secrets. Sign out from Settings to remove the GitHub token. Delete the LocalAppData folder manually if you want a full wipe.

## Path variables

Supported (and other Windows environment variables that resolve at runtime):

- `%USERPROFILE%`
- `%APPDATA%`
- `%LOCALAPPDATA%`
- `%PROGRAMFILES%`
- `%PROGRAMFILES(X86)%`
- `%PROGRAMDATA%`, `%TEMP%`, `%TMP%`, `%SYSTEMROOT%`, `%WINDIR%`, `%PUBLIC%`, `%USERNAME%`

`PathResolver` expands variables, normalizes separators via `Path.GetFullPath`, and rejects repository path traversal.
