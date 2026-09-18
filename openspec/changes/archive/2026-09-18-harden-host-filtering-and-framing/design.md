## Context

Two findings from `docs/ISSUE_TRIAGE.md` Phase 9, grouped by the doc itself (`:262-268`) because both land in `frontend/nginx.conf` and should be one pass over that file: **S1** (`:61-67`, any `Host` accepted) and **S3** (`:86-91`, no framing headers).

Current state, re-verified at `0cd2d77`:

- `backend/AnimeTracker.Api/appsettings.json:8` — `"AllowedHosts": "*"`. `Program.cs:31` uses `WebApplication.CreateBuilder`, which wires `HostFilteringMiddleware` from this key automatically; there is no `app.UseHostFiltering()` to find, and `"*"` makes the middleware a no-op. `appsettings.Development.json` sets no `AllowedHosts`, so this key is a single point of change for both run paths.
- `frontend/nginx.conf:1-3` — one `server { listen 80; server_name _; }` that proxies `/api/` (`:7-11`) and sets `Cache-Control` in `location /assets/` (`:22-24`) and `location /` (`:26-29`), and no other headers anywhere.

Constraints that shaped the decisions below:

- **No HTTP-level test harness exists.** `AnimeTracker.Api.Tests` references neither `Microsoft.AspNetCore.Mvc.Testing` nor `Microsoft.AspNetCore.TestHost`; its tests construct controllers and services directly. Booting the app under a `WebApplicationFactory` is not cheap here — `Program.cs:234` calls `db.Database.Migrate()` at startup and registers 13 hosted services, so it would need a live Postgres.
- **No nginx test harness exists** either, and nothing in the repo runs nginx outside Docker.
- The app is only ever reached by typing `localhost` or `127.0.0.1`: `launchSettings.json` uses `http://localhost:5050`, and Compose publishes on `127.0.0.1` only.

## Goals / Non-Goals

**Goals:**

- Refuse any `Host` that is not `localhost`/`127.0.0.1`, at both the backend and the nginx frontend, independently of each other.
- Send `X-Frame-Options: DENY` on every frontend response, without disturbing existing `Cache-Control` behaviour.
- Keep both documented run paths working unchanged on whatever port `BACKEND_PORT` selects.
- Leave a regression test behind for the backend half, without inventing a heavy integration harness for a one-value config change.

**Non-Goals:**

- S2 (CSRF header middleware) and S4 (non-root containers) — separate items in the same phase, proposed separately.
- A full CSP. The triage doc offers `frame-ancestors 'none'` as an *alternative* to `X-Frame-Options`, not an addition.
- `docker-compose.yml` port bindings — already `127.0.0.1`-only and not part of either finding.
- `[::1]` Host variants — the Host header text is always `localhost` or `127.0.0.1` regardless of which interface the connection arrives on.
- Serving BetterMAL under a LAN IP or machine hostname. That is out of scope by design and this change deliberately forecloses it (see Risks).

## Decisions

Every behavioural claim below was verified live against the exact proposed config, in `nginx:1.27-alpine` containers, before this document was written.

### D1. Both requirements go in `deployment/spec.md` as ADDED

Nothing under `openspec/specs/` matches `AllowedHosts`, `nginx`, `Host header`, `server_name`, or `X-Frame` — confirmed by grep, so this is new spec territory, not a wording change. `deployment` already owns the Compose stack, the nginx-served frontend, and the `BACKEND_PORT` rule, so both requirements belong there. Both are `ADDED`: no existing requirement changes behaviour, and `MODIFIED` with partial content would lose detail at archive time.

*Alternative considered:* a separate `security` capability. Rejected — it would hold two requirements that are entirely about how the app is exposed on the host, which is what `deployment` already describes.

### D2. Backend host filtering is a config value, not code

Set `"AllowedHosts": "localhost;127.0.0.1"`. ASP.NET Core matches the hostname and ignores the port, so this covers any `BACKEND_PORT` without enumerating ports — which keeps the existing "One backend port, set by BACKEND_PORT" requirement intact.

The default rejection is a bare `400 Bad Request` from `HostFilteringMiddleware`, emitted before routing. Confirmed by driving the real middleware: a disallowed `Host` yields 400 and the next delegate is never invoked, so no controller runs. That response is adequate — an attacker-controlled hostname deserves no detail — so no custom handler is added, per the triage note.

*Alternative considered:* custom middleware in `Program.cs`. Rejected — it would duplicate framework behaviour already in the pipeline.

### D3. nginx uses a `default_server` block returning 444

Split into two `server` blocks on port 80: a catch-all `listen 80 default_server; server_name _; return 444;`, and the real block narrowed to `server_name localhost 127.0.0.1;` with its three locations unchanged. nginx strips the port before matching `server_name`, so `Host: localhost:5173` matches `localhost` — verified.

`444` is nginx's non-standard "close the connection, send nothing", which tells a prober less than a `403` body would. Verified: `Host: evil.example` yields an empty reply on both `/` and `/api/`, and is neither proxied nor served any frontend file.

*Alternatives considered:* `return 403` (leaks that something is listening and generates a response body); an `if ($host !~ ...)` guard inside the single existing block (`if` in a `location` context is famously error-prone, and a second `server` block is the idiomatic nginx answer).

### D4. `X-Frame-Options` is repeated in each location that sets its own `add_header`

This is the one non-obvious part. nginx's `add_header` directives are inherited by a `location` **only if that location declares no `add_header` of its own** — declaring one drops *all* inherited ones. `location /assets/` and `location /` each already set `Cache-Control`, so a server-level `X-Frame-Options` alone silently never reaches either.

Verified both ways against the real config:

| Response | Server-level only | Server-level + repeated in location |
| --- | --- | --- |
| `/` | `Cache-Control: no-cache`, **no XFO** | `Cache-Control: no-cache` + `X-Frame-Options: DENY` |
| `/assets/` | `Cache-Control: …immutable`, **no XFO** | `Cache-Control: …immutable` + `X-Frame-Options: DENY` |
| `/api/` | `X-Frame-Options: DENY` (inherited) | `X-Frame-Options: DENY` (inherited) |

So: `add_header X-Frame-Options DENY always;` at the `server` level **and** repeated inside `location /assets/` and `location /`. `location /api/` declares no `add_header`, so it inherits correctly and is left alone — confirmed with a real upstream behind the proxy, not inferred. The `444` block needs nothing; it never serves a body.

`always` is kept so the header is present on error responses too, not only 2xx/3xx.

### D5. The backend test drives the real middleware fed by the real `appsettings.json`

The test loads the shipped `appsettings.json`, binds its `AllowedHosts` into `HostFilteringOptions` the way the framework does, and invokes a real `HostFilteringMiddleware`, asserting that a foreign `Host` gets 400 with the next delegate never called, and that `localhost`/`127.0.0.1` pass with and without a port.

Two things were confirmed by compiling and running a throwaway version of this test, then deleting it:

- The API project's `appsettings.json` **is** copied to the test project's output directory, so the test reads the real file rather than a restatement of it. It therefore fails today (`AllowedHosts` is `*`) and passes after the change — a genuine regression test, not a tautology.
- `HostFilteringMiddleware`'s third constructor parameter is `IOptionsMonitor<HostFilteringOptions>`, **not** `IOptions<>`. A small test double implementing `IOptionsMonitor` is needed; passing `Options.Create(...)` does not compile.

No new package is required — the ASP.NET Core framework reference flows transitively through the existing `ProjectReference`.

*Alternative considered:* `WebApplicationFactory`. Rejected per the constraints above: a live Postgres and 13 hosted services to test one config value. *Also considered:* asserting the config string equals `"localhost;127.0.0.1"`. Rejected — it restates the change instead of testing the behaviour it buys.

### D6. nginx is verified by a documented manual procedure, not an automated test

There is no nginx harness in the repo, and adding one (a container fixture plus a Docker dependency in CI) is disproportionate to a config change. The verification is scripted in `tasks.md` as explicit commands instead.

One trap worth recording: `nginx -t` **resolves `proxy_pass` upstream hostnames at parse time**, so validating this file standalone fails with `host not found in upstream "backend"` unless the name resolves. Confirmed both ways. Pass `--add-host backend:127.0.0.1` when testing outside Compose.

### D7. `X-Frame-Options`, not a CSP

The triage doc presents `X-Frame-Options: DENY` and `frame-ancestors 'none'` as alternatives. `X-Frame-Options` is one line, universally supported, and sufficient for the stated threat. Nothing found during design argued for adding a CSP, and a partial CSP added only for `frame-ancestors` invites the mistaken assumption that a real CSP is in place.

## Risks / Trade-offs

- **A legitimate caller sends a non-loopback `Host` and starts getting 400/444.** → Audited every path that reaches the backend: nginx proxies with `proxy_set_header Host $host`, and `$host` is port-stripped and therefore `localhost` or `127.0.0.1`; Vite's dev proxy uses `changeOrigin: true` against `localhost:{BACKEND_PORT}`; Compose publishes on `127.0.0.1` only; no Compose service defines a healthcheck that would hit the backend under its service name (only `postgres` has one, and it runs `pg_isready` locally). Task 5 re-checks both run paths end to end.
- **Reaching the app by LAN IP or machine hostname stops working.** → Accepted and intended; this is the finding being fixed. It is also the most likely way a future change trips over this, so both the requirement text and the nginx comment state the rule.
- **`default_server` collides if another config declares one on port 80.** → `frontend/Dockerfile` copies this file to `/etc/nginx/conf.d/default.conf`, replacing the stock image's only conf.d file, so it is the sole port-80 config. A duplicate would fail loudly at startup (`duplicate default server`), not silently; `nginx -t` in task 4 catches it before the image ships.
- **Someone later adds an `add_header` to a location and silently drops `X-Frame-Options`.** → The D4 rule is the easiest thing here to get wrong, so it is recorded as a comment in `nginx.conf` next to the repeated directives, not just in this document.
- **`X-Frame-Options` is obsoleted in favour of CSP `frame-ancestors`.** → `DENY` remains honoured by current browsers. If a CSP is ever introduced for other reasons, `frame-ancestors 'none'` should subsume this and the header can go.
- **The backend test asserts against the shipped `appsettings.json`.** → If that file stops being copied to the test output the test would silently read nothing; the test asserts the file exists before asserting its contents.

## Migration Plan

No data migration, no API change, no new dependency. Deploy is `docker compose up --build` (the frontend image must be rebuilt for `nginx.conf` to take effect; the backend picks up `appsettings.json` on restart). Rollback is reverting the commit and rebuilding — nothing persists state.

## Open Questions

None. The two questions design was asked to settle — where the requirements live (D1) and whether S3 belongs elsewhere (it does not) — are resolved above, and the behavioural uncertainties were settled empirically rather than left open.
