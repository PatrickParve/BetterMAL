## Why

This change fixes triage item **B4** from `docs/ISSUE_TRIAGE.md`, in Phase 7 ("Dev-setup & docs hygiene") of its fix order. Following the README's "Option B" (run natively) doesn't work. The backend listens on a port nothing else points at. The OAuth redirect goes to a third port. MAL credentials never reach the backend. The Postgres password can differ from the one the compose Postgres was created with. Once the README is public, the documented dev path should work as written.

The triage entry's fix direction suggested `dotnet user-secrets` for the credentials. Follow-up discussion replaced that with one mechanism: a natively run backend reads the same repo-root `.env` that Docker Compose reads. That one file covers the MAL credentials, the callback port and the Postgres connection string, so there aren't two secret stores to keep in sync.

**Verified against the code** (2026-09-17, at `94828d2`). Paths without a prefix are under `backend/AnimeTracker.Api/`.

- **The backend port is hardcoded in seven places, and they disagree.**
  - `.env.example:13` ships `BACKEND_PORT=5050`. Port 5000 is taken by macOS's AirPlay Receiver.
  - `Properties/launchSettings.json:8`: `applicationUrl` is `http://localhost:5273`.
  - `appsettings.json:15`: `Mal:CallbackPort` is `5000`. `Services/Mal/Auth/MalOAuthService.cs:164` builds `redirect_uri` from it.
  - `Services/Mal/MalOptions.cs:13`: the property default is `5000`. Not in the triage entry.
  - `docker-compose.yml:30,32`: both fallbacks are `${BACKEND_PORT:-5000}`.
  - `frontend/vite.config.ts:8`: the dev proxy falls back to `'5000'` when `.env` has no `BACKEND_PORT`. The triage entry lists this file under Phase 7 but not in its evidence.
  - `README.md`: six references to `5000`, at `:45-46` (MAL redirect URL), `:66` (`.env` block), `:94` (Docker "Access the app"), `:161` and `:170` (native run), and `:191` (env-var table default).
- **Native `dotnet run` never reads `.env`.**
  - Docker gets `Mal__ClientId`, `Mal__ClientSecret`, `Mal__CallbackPort` and `ConnectionStrings__Default` from compose's `environment:` block (`docker-compose.yml:24-30`).
  - `Program.cs` has no equivalent. `appsettings.json:13-14` leaves `Mal:ClientId` and `Mal:ClientSecret` empty.
- **The native Postgres password is hardcoded.**
  - `appsettings.Development.json:9` uses `Password=devlocalpassword`.
  - The compose Postgres volume is created with `.env`'s `POSTGRES_PASSWORD` (`docker-compose.yml:8`), and Postgres never rereads it.
  - `.env.example:19` ships `changeme`, so a fresh clone gets two different passwords.
- **Native run listens on a fixed port, not `BACKEND_PORT`.** Not in the triage entry. Kestrel's port comes only from `launchSettings.json`. So if `.env` sets a port other than the default, the Vite proxy and the OAuth redirect follow `.env` while the backend stays put. Docker has no such gap, because `BACKEND_PORT` drives both the published port (`:32`) and the callback port (`:30`).

## What Changes

- **5050 becomes the one default port.**
  - `launchSettings.json` `applicationUrl` becomes `http://localhost:5050`.
  - `appsettings.json` `Mal:CallbackPort` and the `MalOptions.CallbackPort` default become `5050`.
  - Both `docker-compose.yml` fallbacks become `${BACKEND_PORT:-5050}`, and so does the `frontend/vite.config.ts` fallback.
  - All six README references change to `5050`, including the redirect URL users register at myanimelist.net/apiconfig.
- **In Development only, the backend reads the repo-root `.env`.**
  - Right after `WebApplication.CreateBuilder`, and before anything reads configuration, a small loader resolves `.env` two directories above the content root (the project folder under the documented `cd backend/AnimeTracker.Api && dotnet run`).
  - It parses `.env` by hand, without a NuGet package, and layers the results over the existing configuration:
    - `MAL_CLIENT_ID` becomes `Mal:ClientId`, and `MAL_CLIENT_SECRET` becomes `Mal:ClientSecret`.
    - `BACKEND_PORT` becomes `Mal:CallbackPort`, plus the listen URL `http://localhost:<port>`.
    - `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` and `POSTGRES_PORT` become `ConnectionStrings:Default` against `Host=localhost`.
  - If `.env` is missing, nothing is layered and `appsettings.Development.json` stays the fallback. Nothing throws.
  - Outside Development the loader does nothing. That includes Docker, which runs with `ASPNETCORE_ENVIRONMENT=Production` (`docker-compose.yml:26`).
- **The Postgres password default matches.** `.env.example:19` and the README's copy of it (`:72`) change to `devlocalpassword`, matching `appsettings.Development.json:9`. A fresh clone that copies `.env.example` unmodified then gets the same password on both paths. This password isn't a secret worth generating or rotating. It only guards a Postgres bound to `127.0.0.1` (`docker-compose.yml:12`) in a single-user local app. Anyone local who could use it can already read the plaintext `.env`, or the plaintext MAL tokens in that same database (`Services/Mal/Auth/MalTokenStore.cs`).
- **The README's native-run section describes the loader.** The backend listens on 5050 (or `BACKEND_PORT`). It picks up the MAL credentials and the database connection from the same `.env` used for Docker, with no extra setup step. This replaces the "already points at the postgres container's exposed port" wording at `:160-162`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `deployment`:
  - **Modified:** "Secrets via gitignored .env". The requirement already said secrets load from the gitignored `.env`, but a native run never did that, and nothing in the spec described how. It now says both run paths read the same file. Run natively in Development, the backend reads `.env` itself, the way Docker Compose reads it, and reaches Postgres through its locally exposed port. Outside Development it never reads the file. Adds scenarios for a native run with a `.env`, values with comments or quotes, a native run with no `.env`, a malformed port, and a non-Development run with a `.env` present. The wording also drops "tokens" from the list of `.env` secrets: MAL tokens come from authorization and are stored in Postgres (`mal-api-integration`'s "Tokens persist in Postgres").
  - **Added:** "One backend port, set by BACKEND_PORT". `BACKEND_PORT` sets the port the backend is reached on (under Docker and natively), the port in the OAuth redirect URI, and the dev server's proxy target, and it defaults to 5050. No spec said this before. `mal-api-integration`'s authorization scenario uses `http://localhost:{port}/callback` without saying where `{port}` comes from.

## Impact

- **Backend** (`backend/AnimeTracker.Api/`):
  - `Program.cs`: one call to the loader after `CreateBuilder`
  - new `Services/Infrastructure/DevDotEnvOverlay.cs`: the parser, the key mapping and the Development gate
  - `Properties/launchSettings.json`, `appsettings.json`, `Services/Mal/MalOptions.cs`: port 5050
- **Backend tests**: new `backend/AnimeTracker.Api.Tests/Services/Infrastructure/DevDotEnvOverlayTests.cs`
- **Frontend**: `frontend/vite.config.ts` (fallback port only)
- **Repo root**: `docker-compose.yml` (fallback ports only), `.env.example` (password default), `README.md`
- **No new dependencies.** The connection string is built with `NpgsqlConnectionStringBuilder`, which the project already has through `Npgsql.EntityFrameworkCore.PostgreSQL`.
- **Existing setups.**
  - A developer whose `.env` already sets `BACKEND_PORT` sees no Docker change.
  - A Docker user with no `BACKEND_PORT` in `.env` moves from host port 5000 to 5050, and must update the redirect URL registered with their MAL app.
- **Out of scope.**
  - Registering or updating the redirect URL on myanimelist.net. That's a manual step outside this repo.
  - `dotnet user-secrets`. It was considered and rejected in favour of the loader.
  - Any change to Docker/Production behaviour.
  - SP5 (the other Phase 7 item), S1–S4, and items from other phases.
