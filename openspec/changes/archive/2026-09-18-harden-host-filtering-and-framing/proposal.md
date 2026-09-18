## Why

Every layer that serves BetterMAL accepts any `Host` header, and nothing stops the UI being framed. `appsettings.json:8` sets `"AllowedHosts": "*"`, and `frontend/nginx.conf:3` serves `server_name _;`, so a page on `evil.example` that re-points its own DNS at `127.0.0.1` after loading reaches the API as a *same-origin* caller — CORS never enters into it. There is no auth in front of that API, and it can now delete list entries in a way that propagates to MAL (`Controllers/EntriesController.cs:35` → `Services/Sync/EntryPushService.cs:60-92` → `Services/Mal/MalClient.cs:145`), import a crafted transfer file (`Controllers/TransferController.cs:30`), and export everything (`/api/transfer/export`, `/api/my-list/backup`). Both fixes land in `frontend/nginx.conf`, so the triage doc groups them into one pass over that file (`docs/ISSUE_TRIAGE.md:262-268`).

## What Changes

- **S1 — backend host filtering.** `backend/AnimeTracker.Api/appsettings.json` changes `"AllowedHosts": "*"` to `"localhost;127.0.0.1"`. ASP.NET Core's host-filtering middleware is already in the pipeline via `WebApplication.CreateBuilder` and reads this key itself, so no code changes. `appsettings.Development.json` has no `AllowedHosts` key, so this one edit covers native Development and Docker/Production alike.
- **S1 — nginx host filtering.** `frontend/nginx.conf` splits into two `server` blocks on port 80: a `default_server` catch-all that `return 444;` (closes the connection with no response) for any other hostname, and the real block narrowed to `server_name localhost 127.0.0.1;` keeping its existing `/api/`, `/assets/`, and `/` locations.
- **S3 — framing protection.** `add_header X-Frame-Options DENY always;` on the real `server` block *and* repeated inside `location /assets/` and `location /`. nginx drops inherited `add_header` directives in any location that sets one of its own, and both of those already set `Cache-Control` — verified live: with the header only at server level, neither location sends it. `location /api/` sets no `add_header`, so it inherits correctly.
- No behaviour change for normal use: `localhost` and `127.0.0.1` on any port keep working, which is every way the app is actually reached.

## Capabilities

### New Capabilities

None. Both changes are new requirements on an existing capability.

### Modified Capabilities

- `deployment`: gains two requirements it has no coverage for today — that the backend and the frontend proxy both reject requests whose `Host` is not `localhost`/`127.0.0.1`, and that frontend responses carry `X-Frame-Options: DENY`. Confirmed absent: nothing under `openspec/specs/` matches `AllowedHosts`, `nginx`, `Host header`, `server_name`, or `X-Frame`. `deployment` is where the Compose stack, the nginx-served frontend, and the `BACKEND_PORT` rule already live, so both belong there rather than in a new capability.

## Impact

- `backend/AnimeTracker.Api/appsettings.json` — one value.
- `frontend/nginx.conf` — restructured into two server blocks plus three `add_header` lines.
- `backend/AnimeTracker.Api.Tests/` — a new test binding the real `appsettings.json` into `HostFilteringOptions` and driving the real `HostFilteringMiddleware`. Verified feasible with no new package: the API's `appsettings.json` is copied to the test output, and the middleware is constructible directly (its third argument is `IOptionsMonitor<HostFilteringOptions>`, not `IOptions<>`).
- No API surface, database, or frontend source changes. No new dependencies.
- Risk concentrates in requests whose `Host` is neither `localhost` nor `127.0.0.1`. Checked and unaffected: nginx→backend proxying sends `Host: $host` (port-stripped, so `localhost`/`127.0.0.1`), Vite's dev proxy uses `changeOrigin: true` against `localhost:{BACKEND_PORT}`, Compose publishes only on `127.0.0.1`, and no service defines a healthcheck that would hit the backend under a service name.
- Out of scope: S2 (CSRF) and S4 (non-root containers), both separate items in the same triage phase; `docker-compose.yml`'s already-correct `127.0.0.1` bindings; a full CSP; and `[::1]` Host variants, since the Host header text is always `localhost` or `127.0.0.1` however the connection arrives.
