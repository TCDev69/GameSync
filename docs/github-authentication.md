# GitHub authentication

GameSync uses **GitHub as its only cloud backend**. There is no proprietary GameSync server.

## Architecture

```
UI / Settings
    ↓
IGitHubAuthenticationService  (device flow orchestration)
    ↓
IGitHubOAuthClient            (POST /login/device/code, /login/oauth/access_token)
    ↓
ICredentialStore              (Windows Credential Manager)
    ↓
IGitHubService / IGitHubApiClient  (api.github.com with Bearer token)
    ↓
IGitHubRepositoryConnectionService
    ↓
IRepositoryService + IGitService  (clone under LocalAppData)
```

### Components

| Type | Role |
|------|------|
| `GitHubAuthenticationService` | Start device auth, open verification URL, poll, store token, sign out, resolve user |
| `HttpGitHubOAuthClient` | GitHub OAuth device-flow HTTP calls |
| `HttpGitHubApiClient` | GitHub REST API (`/user`, `/user/repos`, `/repos/{owner}/{name}`) |
| `GitHubService` | Authenticated repository listing / lookup / access verification |
| `GitHubRepositoryConnectionService` | Select → verify → clone → validate/initialize `config/games.json` → save `machine.json` |
| `WindowsCredentialStore` | Persist access token outside JSON |

Tokens are **never** returned to ViewModels, written to JSON, logged, placed in CLI args, or committed to the repository.

## Device authorization flow

1. `StartAuthenticationAsync` → `POST https://github.com/login/device/code` with `client_id` + scopes.
2. UI shows `user_code`; GameSync opens `verification_uri` / `verification_uri_complete` (HTTPS + `github.com` only).
3. `CompleteAuthenticationAsync` polls `POST https://github.com/login/oauth/access_token` with `grant_type=urn:ietf:params:oauth:grant-type:device_code`.
4. On success, access token is stored via `ICredentialStore` under key `GitHub/AccessToken` (Credential Manager target `GameSync/GitHub/AccessToken`).
5. `AuthenticateAsync` runs steps 1–4 for convenience.

Poll handling:

- `authorization_pending` / `slow_down` → continue
- `expired_token` / `access_denied` / other errors → `GitHubAuthenticationFailedException`
- Network failures → `GitHubUnavailableException` (local data is left untouched)

## Permissions (exact)

GameSync requests these **classic OAuth App scopes** (space-delimited, configurable via `GameSyncOptions.GitHubScopes`):

| Scope | Why |
|-------|-----|
| `read:user` | Identify the authenticated account (`GET /user`) with minimal profile access |
| `repo` | Read/write the **user-selected** private (or public) repository contents needed for save sync (clone/fetch/push via HTTPS token auth) |

### Why `repo`?

Classic OAuth scopes cannot grant access to a single repository. For private save repositories, `repo` is the minimum classic scope that allows content read/write. GameSync still **only operates on the repository the user explicitly selects**; it does not enumerate or mutate unrelated repos beyond listing candidates for that selection UI.

GameSync does **not** request:

- `workflow`
- `delete_repo`
- `admin:org`
- `gist`
- broad `user` write scopes

### Configuration

- Public OAuth App **Client ID** (not a secret): defaulted in `GameSyncOptions.GitHubClientId`; override with env `GAMESYNC_GITHUB_CLIENT_ID` if needed
- Enable **Device Flow** on the GitHub OAuth App
- **Authorization callback URL:** GitHub’s create-app form requires it, but Device Flow **never redirects** there. Use any valid URL, for example:
  - `http://127.0.0.1/`
  - or the same value as Homepage URL (`https://github.com/YOUR_USER/GameSync`)

Full production steps (OAuth form, installer, publish, no signing, no git.exe for users): [production.md](production.md).

## Repository connection workflow

```
Connect GitHub → List repositories → User selects → Verify access
  → Clone (deterministic LocalAppData path) → Validate GameSync structure
  → Initialize config/games.json + saves/ if missing → Load games.json
  → Persist owner/name/branch/localPath in machine.json (no credentials)
```

Incompatible `games.json` returns `RepositoryIncompatibleException` with a clear message. Offline / auth failures return structured errors and **do not delete** local clones or backups.

## Security model

- Validate owner, repository name, branch, and HTTPS `github.com` clone URLs (`GitHubRepositoryValidator`).
- Remote save paths remain constrained by `PathResolver` (no traversal).
- Repository content is treated as **data only** — never executed.
- Logs include auth start/success/failure, repo selection, clone, and API errors — never tokens, device codes, or Authorization headers.

## Offline behavior

| Condition | Behavior |
|-----------|----------|
| GitHub / network unavailable | `GitHubUnavailableException`; local repo may still be inspected |
| Auth expired / 401 | `GitHubAuthenticationFailedException`; prompt re-auth |
| Repository missing / no access | `RepositoryUnavailableException` |
| Local clone already present | Reused; not deleted because GitHub is down |

## Credential storage

- Interface: `ICredentialStore`
- Production: `WindowsCredentialStore` (CredWrite/CredRead)
- Key: `GitHub/AccessToken` (see `GitHubCredentialKeys`)
- Sign-out deletes the credential entry

## Implementation status

Implemented: device flow auth, secure token storage, repository list/get/verify, connection workflow, validation, offline error mapping, unit tests with mocked GitHub HTTP boundaries.

Not in MVP: creating repositories on GitHub (`CreateRepository` intentionally omitted).
